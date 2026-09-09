using System;
using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>Por que uma acao sobre o grid foi recusada. Usado pela UI para explicar.</summary>
    public enum GridRejection
    {
        None = 0,
        NotAdjacent,
        AlreadyOwned,
        BeyondRaceRadius,
        NotOwned,
        TileOccupied,
        TerrainNotAllowed,
        TileDestroyed,
        NoBuilding
    }

    public readonly struct GridResult
    {
        private GridResult(bool ok, GridRejection rejection)
        {
            Ok = ok;
            Rejection = rejection;
        }

        public bool Ok { get; }

        public GridRejection Rejection { get; }

        public static readonly GridResult Success = new GridResult(true, GridRejection.None);

        public static GridResult Fail(GridRejection rejection) => new GridResult(false, rejection);
    }

    /// <summary>
    /// Curva de custo de expansao. Cresce com o numero de celulas ja possuidas, de
    /// modo que expandir sem economia de suporte se torne inviavel (spec kingdom-grid).
    /// </summary>
    public sealed class TileCostCurve
    {
        public TileCostCurve(int baseCost = 25, double growth = 1.35, int flatStep = 5)
        {
            BaseCost = baseCost;
            Growth = growth;
            FlatStep = flatStep;
        }

        public int BaseCost { get; }

        public double Growth { get; }

        public int FlatStep { get; }

        public int CostFor(int ownedCount)
        {
            int owned = Math.Max(1, ownedCount);
            double geometric = BaseCost * Math.Pow(Growth, owned - 1);
            return (int)Math.Round(geometric + FlatStep * (owned - 1), MidpointRounding.AwayFromZero);
        }
    }

    /// <summary>
    /// Territorio do jogador: dicionario esparso de celulas, sem limite pre-alocado
    /// (design D4). Cuida de posse, terreno, edificios e producao; ouro e assunto
    /// da run, nao do grid.
    /// </summary>
    public sealed class KingdomGrid
    {
        private readonly Dictionary<Coord, Tile> _tiles = new Dictionary<Coord, Tile>();
        private readonly ITerrainGenerator _terrainGenerator;

        public KingdomGrid(ITerrainGenerator terrainGenerator, Coord hallCoord, TileCostCurve costCurve = null)
        {
            _terrainGenerator = terrainGenerator;
            HallCoord = hallCoord;
            CostCurve = costCurve ?? new TileCostCurve();
        }

        public Coord HallCoord { get; }

        public TileCostCurve CostCurve { get; }

        public int OwnedCount { get; private set; }

        /// <summary>
        /// Celula na coordenada, criada sob demanda. Uma celula nunca vista nasce
        /// nao possuida e nao revelada.
        /// </summary>
        public Tile TileAt(Coord coord)
        {
            if (_tiles.TryGetValue(coord, out Tile existing))
            {
                return existing;
            }

            Tile created = new Tile(coord, _terrainGenerator.TerrainAt(coord));
            _tiles[coord] = created;
            return created;
        }

        public bool IsOwned(Coord coord) => _tiles.TryGetValue(coord, out Tile tile) && tile.Owned;

        public IEnumerable<Tile> OwnedTiles()
        {
            foreach (KeyValuePair<Coord, Tile> pair in _tiles)
            {
                if (pair.Value.Owned)
                {
                    yield return pair.Value;
                }
            }
        }

        /// <summary>Celulas possuidas em ordem estavel. Necessario para simulacao determinista.</summary>
        public List<Tile> OwnedTilesOrdered()
        {
            List<Tile> owned = new List<Tile>();
            foreach (Tile tile in OwnedTiles())
            {
                owned.Add(tile);
            }

            owned.Sort(CompareByCoord);
            return owned;
        }

        private static int CompareByCoord(Tile a, Tile b)
        {
            int byY = a.Coord.Y.CompareTo(b.Coord.Y);
            return byY != 0 ? byY : a.Coord.X.CompareTo(b.Coord.X);
        }

        /// <summary>
        /// Cria o Salao do Reino e marca a primeira celula como possuida. O primeiro
        /// anel nasce revelado: a primeira expansao da run precisa ser uma decisao, e
        /// nao um sorteio, ou o jogador novo perde antes de entender a economia.
        /// Do segundo anel em diante o terreno volta a ser incognita.
        /// </summary>
        public Tile PlaceHall(BuildingDefinition hall)
        {
            Tile tile = TileAt(HallCoord);
            tile.Owned = true;
            tile.Revealed = true;
            tile.Building = hall;
            OwnedCount = 1;

            foreach (Coord neighbor in HallCoord.Neighbors())
            {
                TileAt(neighbor).Revealed = true;
            }

            return tile;
        }

        /// <summary>Celulas compraveis: nao possuidas e ortogonalmente vizinhas ao territorio.</summary>
        public List<Coord> PurchasableCoords(RuleModifiers rules = null)
        {
            RuleModifiers mods = rules ?? RuleModifiers.None;
            HashSet<Coord> candidates = new HashSet<Coord>();

            foreach (Tile owned in OwnedTiles())
            {
                foreach (Coord neighbor in owned.Coord.Neighbors())
                {
                    if (IsOwned(neighbor))
                    {
                        continue;
                    }

                    if (CanPurchase(neighbor, mods).Ok)
                    {
                        candidates.Add(neighbor);
                    }
                }
            }

            List<Coord> result = new List<Coord>(candidates);
            result.Sort((a, b) =>
            {
                int byY = a.Y.CompareTo(b.Y);
                return byY != 0 ? byY : a.X.CompareTo(b.X);
            });
            return result;
        }

        public GridResult CanPurchase(Coord coord, RuleModifiers rules = null)
        {
            RuleModifiers mods = rules ?? RuleModifiers.None;

            if (IsOwned(coord))
            {
                return GridResult.Fail(GridRejection.AlreadyOwned);
            }

            bool adjacentToTerritory = false;
            foreach (Coord neighbor in coord.Neighbors())
            {
                if (IsOwned(neighbor))
                {
                    adjacentToTerritory = true;
                    break;
                }
            }

            if (!adjacentToTerritory)
            {
                return GridResult.Fail(GridRejection.NotAdjacent);
            }

            int maxRadius = mods.GetInt(RuleKeys.MaxDistanceFromHall, 0);
            if (maxRadius > 0 && coord.ManhattanDistanceTo(HallCoord) > maxRadius)
            {
                return GridResult.Fail(GridRejection.BeyondRaceRadius);
            }

            return GridResult.Success;
        }

        /// <summary>Custo da proxima celula, ja com o multiplicador da raca aplicado.</summary>
        public int NextTileCost(RuleModifiers rules = null)
        {
            RuleModifiers mods = rules ?? RuleModifiers.None;
            double multiplier = mods.GetDouble(RuleKeys.TileCostMultiplier, 1.0);
            int raw = CostCurve.CostFor(OwnedCount);
            return Math.Max(1, (int)Math.Round(raw * multiplier, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// Toma posse da celula e revela o terreno. Nao mexe em ouro: quem chama
        /// ja debitou, e so chama depois de CanPurchase passar.
        /// </summary>
        public GridResult Purchase(Coord coord, RuleModifiers rules = null)
        {
            GridResult check = CanPurchase(coord, rules);
            if (!check.Ok)
            {
                return check;
            }

            Tile tile = TileAt(coord);
            tile.Owned = true;
            tile.Revealed = true;
            OwnedCount++;
            return GridResult.Success;
        }

        /// <summary>Concede uma celula sem custo. Usado por efeitos de carta e evento.</summary>
        public GridResult Grant(Coord coord, RuleModifiers rules = null)
        {
            return Purchase(coord, rules);
        }

        public void Reveal(Coord coord)
        {
            TileAt(coord).Revealed = true;
        }

        public void Hide(Coord coord)
        {
            Tile tile = TileAt(coord);
            if (!tile.Owned)
            {
                tile.Revealed = false;
            }
        }

        public GridResult CanBuild(Coord coord, BuildingDefinition building)
        {
            if (!_tiles.TryGetValue(coord, out Tile tile) || !tile.Owned)
            {
                return GridResult.Fail(GridRejection.NotOwned);
            }

            if (tile.Destroyed)
            {
                return GridResult.Fail(GridRejection.TileDestroyed);
            }

            if (tile.HasBuilding)
            {
                return GridResult.Fail(GridRejection.TileOccupied);
            }

            if (!building.CanBuildOn(tile.Terrain))
            {
                return GridResult.Fail(GridRejection.TerrainNotAllowed);
            }

            return GridResult.Success;
        }

        public GridResult Build(Coord coord, BuildingDefinition building)
        {
            GridResult check = CanBuild(coord, building);
            if (!check.Ok)
            {
                return check;
            }

            TileAt(coord).Building = building;
            return GridResult.Success;
        }

        public GridResult Demolish(Coord coord)
        {
            if (!_tiles.TryGetValue(coord, out Tile tile) || !tile.Owned)
            {
                return GridResult.Fail(GridRejection.NotOwned);
            }

            if (!tile.HasBuilding)
            {
                return GridResult.Fail(GridRejection.NoBuilding);
            }

            tile.Building = null;
            return GridResult.Success;
        }

        /// <summary>
        /// Arrasa a celula: perde o edificio e para de produzir, mas continua
        /// possuida e reconstruivel (spec kingdom-grid).
        /// </summary>
        public GridResult Destroy(Coord coord)
        {
            if (!_tiles.TryGetValue(coord, out Tile tile) || !tile.Owned)
            {
                return GridResult.Fail(GridRejection.NotOwned);
            }

            tile.Building = null;
            tile.Destroyed = true;
            return GridResult.Success;
        }

        public GridResult Repair(Coord coord)
        {
            if (!_tiles.TryGetValue(coord, out Tile tile) || !tile.Owned)
            {
                return GridResult.Fail(GridRejection.NotOwned);
            }

            tile.Destroyed = false;
            return GridResult.Success;
        }

        public void SetTerrain(Coord coord, TerrainType terrain)
        {
            TileAt(coord).Terrain = terrain;
        }

        /// <summary>
        /// Celulas possuidas na borda do territorio, ou seja, com ao menos um vizinho
        /// nao possuido. As ameacas comem o territorio a partir daqui.
        /// </summary>
        public List<Tile> BorderTiles()
        {
            List<Tile> border = new List<Tile>();
            foreach (Tile tile in OwnedTilesOrdered())
            {
                foreach (Coord neighbor in tile.Coord.Neighbors())
                {
                    if (!IsOwned(neighbor))
                    {
                        border.Add(tile);
                        break;
                    }
                }
            }

            return border;
        }
    }
}

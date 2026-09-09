using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    public readonly struct ProductionLine
    {
        public ProductionLine(string reason, int gold)
        {
            Reason = reason;
            Gold = gold;
        }

        public string Reason { get; }

        public int Gold { get; }

        public override string ToString() => Reason + ": " + (Gold >= 0 ? "+" : string.Empty) + Gold;
    }

    /// <summary>
    /// Producao de uma celula, discriminada. O detalhamento existe porque a spec
    /// exige que o jogador consiga inspecionar de onde vem cada moeda antes de
    /// encerrar o dia; sem isso a sinergia de adjacencia fica invisivel.
    /// </summary>
    public sealed class ProductionBreakdown
    {
        private readonly List<ProductionLine> _lines = new List<ProductionLine>();

        public ProductionBreakdown(Coord coord)
        {
            Coord = coord;
        }

        public Coord Coord { get; }

        public IReadOnlyList<ProductionLine> Lines => _lines;

        public int Total { get; private set; }

        public void Add(string reason, int gold)
        {
            if (gold == 0)
            {
                return;
            }

            _lines.Add(new ProductionLine(reason, gold));
            Total += gold;
        }

        public override string ToString()
        {
            return Coord + " => " + Total;
        }
    }

    public static class ProductionCalculator
    {
        /// <summary>
        /// Producao de uma celula com bonus de adjacencia aplicados. Celula arrasada
        /// nao produz nada, mesmo continuando possuida.
        /// </summary>
        public static ProductionBreakdown ForTile(KingdomGrid grid, Tile tile, RuleModifiers rules = null)
        {
            RuleModifiers mods = rules ?? RuleModifiers.None;
            ProductionBreakdown breakdown = new ProductionBreakdown(tile.Coord);

            if (!tile.Owned || tile.Destroyed)
            {
                return breakdown;
            }

            if (!tile.HasBuilding)
            {
                // Regra dos Elfos: floresta possuida rende mesmo sem edificio.
                if (tile.Terrain == TerrainType.Forest)
                {
                    int passive = mods.GetInt(RuleKeys.ForestPassiveProduction, 0);
                    breakdown.Add("floresta (raca)", passive);
                }

                return breakdown;
            }

            BuildingDefinition building = tile.Building;
            breakdown.Add(building.DisplayName, building.BaseGoldProduction);

            for (int i = 0; i < building.AdjacencyBonuses.Count; i++)
            {
                AdjacencyBonus bonus = building.AdjacencyBonuses[i];
                int matches = CountMatches(grid, tile.Coord, bonus);
                if (matches > 0)
                {
                    breakdown.Add(bonus.Describe() + " x" + matches, bonus.GoldPerMatch * matches);
                }
            }

            return breakdown;
        }

        private static int CountMatches(KingdomGrid grid, Coord coord, AdjacencyBonus bonus)
        {
            int matches = 0;
            foreach (Coord neighbor in coord.Neighbors())
            {
                Tile tile = grid.TileAt(neighbor);

                if (bonus.Match == AdjacencyMatch.Terrain)
                {
                    // Terreno vizinho conta mesmo sem ser possuido: a floresta ao lado
                    // alimenta a serraria independentemente de quem e o dono.
                    if (tile.Terrain == bonus.Terrain)
                    {
                        matches++;
                    }

                    continue;
                }

                if (tile.Owned && !tile.Destroyed && tile.HasBuilding && tile.Building.Id == bonus.BuildingId)
                {
                    matches++;
                }
            }

            return matches;
        }

        /// <summary>Producao total do reino, com o detalhamento de cada celula.</summary>
        public static int TotalProduction(KingdomGrid grid, RuleModifiers rules, List<ProductionBreakdown> breakdowns = null)
        {
            int total = 0;
            foreach (Tile tile in grid.OwnedTilesOrdered())
            {
                ProductionBreakdown breakdown = ForTile(grid, tile, rules);
                breakdowns?.Add(breakdown);
                total += breakdown.Total;
            }

            return total;
        }

        /// <summary>Defesa somada dos edificios em pe.</summary>
        public static int TotalDefense(KingdomGrid grid)
        {
            int defense = 0;
            foreach (Tile tile in grid.OwnedTiles())
            {
                if (!tile.Destroyed && tile.HasBuilding)
                {
                    defense += tile.Building.Defense;
                }
            }

            return defense;
        }
    }
}

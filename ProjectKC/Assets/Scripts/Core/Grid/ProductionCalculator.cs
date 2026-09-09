using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    public readonly struct ProductionLine
    {
        public ProductionLine(string reason, ResourceKind resource, int amount)
        {
            Reason = reason;
            Resource = resource;
            Gold = amount;
        }

        public string Reason { get; }

        public ResourceKind Resource { get; }

        /// <summary>
        /// Quantidade da parcela. O nome vem de quando so existia ouro; mantido para
        /// nao quebrar leitura de codigo antigo, mas o recurso esta em Resource.
        /// </summary>
        public int Gold { get; }

        public int Amount => Gold;

        public override string ToString()
        {
            return Reason + ": " + (Gold >= 0 ? "+" : string.Empty) + Gold + " " +
                   ResourceKinds.DisplayName(Resource);
        }
    }

    /// <summary>
    /// Producao de uma celula, discriminada por parcela e por recurso. O detalhamento
    /// existe porque a spec exige que o jogador consiga inspecionar de onde vem cada
    /// unidade antes de encerrar o dia; sem isso a sinergia de adjacencia fica
    /// invisivel, e com cinco recursos ficaria incompreensivel.
    /// </summary>
    public sealed class ProductionBreakdown
    {
        private readonly List<ProductionLine> _lines = new List<ProductionLine>();
        private readonly ResourceAmounts _totals = new ResourceAmounts();

        public ProductionBreakdown(Coord coord)
        {
            Coord = coord;
        }

        public Coord Coord { get; }

        public IReadOnlyList<ProductionLine> Lines => _lines;

        public ResourceAmounts Totals => _totals;

        /// <summary>Total em ouro. Atalho para leitura antiga e para a UI resumida.</summary>
        public int Total => _totals[ResourceKind.Gold];

        /// <summary>Celula possuida cujo edificio nao tem gente para operar.</summary>
        public bool Idle { get; internal set; }

        public int this[ResourceKind kind] => _totals[kind];

        public void Add(string reason, int amount)
        {
            Add(reason, ResourceKind.Gold, amount);
        }

        public void Add(string reason, ResourceKind resource, int amount)
        {
            if (amount == 0)
            {
                return;
            }

            _lines.Add(new ProductionLine(reason, resource, amount));
            _totals[resource] += amount;
        }

        public bool IsEmpty => _totals.IsEmpty;

        public override string ToString()
        {
            return Coord + " => " + (Idle ? "ociosa" : _totals.ToString());
        }
    }

    public static class ProductionCalculator
    {
        /// <summary>
        /// Producao de uma celula, com bonus de adjacencia aplicados por recurso.
        /// Celula arrasada nao produz nada, mesmo continuando possuida; celula ociosa
        /// por falta de gente tambem nao, e a diferenca entre as duas e informada.
        /// </summary>
        public static ProductionBreakdown ForTile(
            KingdomGrid grid, Tile tile, RuleModifiers rules = null, bool staffed = true)
        {
            RuleModifiers mods = rules ?? RuleModifiers.None;
            ProductionBreakdown breakdown = new ProductionBreakdown(tile.Coord);

            if (!tile.Owned || tile.Destroyed)
            {
                return breakdown;
            }

            if (!tile.HasBuilding)
            {
                // Ponto de extensao: um terreno possuido pode render por si so, sem
                // edificio. E o que permite uma raca viver da terra em vez de a
                // desenvolver, sem abrir este calculo no meio.
                ResourceAmounts byTerrain = mods.TerrainYield(tile.Terrain);
                foreach (ResourceKind kind in byTerrain.NonZero())
                {
                    breakdown.Add(tile.Terrain + " (raca)", kind, byTerrain[kind]);
                }

                // Chave legada, mantida porque conteudo ja autorado depende dela.
                if (tile.Terrain == TerrainType.Forest)
                {
                    int passive = mods.GetInt(RuleKeys.ForestPassiveProduction, 0);
                    breakdown.Add("floresta (raca)", ResourceKind.Wood, passive);
                }

                return breakdown;
            }

            if (!staffed)
            {
                breakdown.Idle = true;
                return breakdown;
            }

            BuildingDefinition building = tile.Building;

            foreach (ResourceKind kind in building.Production.NonZero())
            {
                breakdown.Add(building.DisplayName, kind, building.Production[kind]);
            }

            for (int i = 0; i < building.AdjacencyBonuses.Count; i++)
            {
                AdjacencyBonus bonus = building.AdjacencyBonuses[i];
                int matches = CountMatches(grid, tile.Coord, bonus);
                if (matches > 0)
                {
                    breakdown.Add(
                        bonus.Describe() + " x" + matches,
                        bonus.Resource,
                        bonus.GoldPerMatch * matches);
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

        /// <summary>Producao total do reino em ouro. Atalho para leitura antiga.</summary>
        public static int TotalProduction(
            KingdomGrid grid, RuleModifiers rules, List<ProductionBreakdown> breakdowns = null)
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

        /// <summary>Defesa somada dos edificios em pe e guarnecidos.</summary>
        public static int TotalDefense(KingdomGrid grid, WorkerAllocation allocation = null)
        {
            int defense = 0;
            foreach (Tile tile in grid.OwnedTiles())
            {
                if (tile.Destroyed || !tile.HasBuilding)
                {
                    continue;
                }

                // Torre sem gente nao defende (spec resources). Sem alocacao, todo
                // edificio conta: e o comportamento do conteudo que nao exige gente.
                if (allocation != null && !allocation.IsStaffed(tile.Coord))
                {
                    continue;
                }

                defense += tile.Building.Defense;
            }

            return defense;
        }

        /// <summary>Defesa guarnecida, com o multiplicador de guarnicao da raca.</summary>
        public static int TotalDefense(
            KingdomGrid grid, WorkerAllocation allocation, RuleModifiers rules)
        {
            int raw = TotalDefense(grid, allocation);
            double multiplier = (rules ?? RuleModifiers.None)
                .GetDouble(RuleKeys.GarrisonDefenseMultiplier, 1.0);

            return (int)System.Math.Round(raw * multiplier, System.MidpointRounding.AwayFromZero);
        }

        /// <summary>Teto de populacao somado pelos edificios em pe.</summary>
        public static int PopulationCapacity(KingdomGrid grid)
        {
            int capacity = 0;
            foreach (Tile tile in grid.OwnedTiles())
            {
                if (!tile.Destroyed && tile.HasBuilding)
                {
                    capacity += tile.Building.PopulationCapacity;
                }
            }

            return capacity;
        }
    }
}

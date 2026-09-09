using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// De onde vem um bonus de adjacencia: de um terreno vizinho ou de um edificio vizinho.
    /// </summary>
    public enum AdjacencyMatch
    {
        Terrain = 0,
        Building = 1
    }

    public sealed class AdjacencyBonus
    {
        public AdjacencyBonus(
            AdjacencyMatch match,
            TerrainType terrain,
            string buildingId,
            int amountPerMatch,
            ResourceKind resource = ResourceKind.Gold)
        {
            Match = match;
            Terrain = terrain;
            BuildingId = buildingId;
            GoldPerMatch = amountPerMatch;
            Resource = resource;
        }

        /// <summary>
        /// Qual recurso o bonus soma. A adjacencia age no recurso do edificio, e nao
        /// num total generico: floresta vizinha rende madeira, nao "producao".
        /// </summary>
        public ResourceKind Resource { get; }

        public static AdjacencyBonus ForTerrain(
            TerrainType terrain, int amountPerMatch, ResourceKind resource = ResourceKind.Gold)
        {
            return new AdjacencyBonus(AdjacencyMatch.Terrain, terrain, null, amountPerMatch, resource);
        }

        public static AdjacencyBonus ForBuilding(
            string buildingId, int amountPerMatch, ResourceKind resource = ResourceKind.Gold)
        {
            return new AdjacencyBonus(
                AdjacencyMatch.Building, TerrainType.Plain, buildingId, amountPerMatch, resource);
        }

        public AdjacencyMatch Match { get; }

        public TerrainType Terrain { get; }

        public string BuildingId { get; }

        public int GoldPerMatch { get; }

        public string Describe()
        {
            string what = Match == AdjacencyMatch.Terrain ? Terrain.ToString() : BuildingId;
            return "adjacencia " + what;
        }
    }

    /// <summary>
    /// Definicao pura de um edificio. O ScriptableObject da camada Unity converte
    /// para este tipo, de modo que o Core continue sem dependencia de engine.
    /// </summary>
    public sealed class BuildingDefinition
    {
        public BuildingDefinition(
            string id,
            string displayName,
            IReadOnlyList<TerrainType> allowedTerrains,
            int goldCost,
            int baseGoldProduction,
            int defense,
            IReadOnlyList<AdjacencyBonus> adjacencyBonuses = null,
            ResourceAmounts cost = null,
            ResourceAmounts production = null,
            int workersRequired = 0,
            int populationCapacity = 0)
        {
            Id = id;
            DisplayName = displayName;
            AllowedTerrains = allowedTerrains ?? new List<TerrainType>();
            GoldCost = goldCost;
            BaseGoldProduction = baseGoldProduction;
            Defense = defense;
            AdjacencyBonuses = adjacencyBonuses ?? new List<AdjacencyBonus>();
            WorkersRequired = workersRequired;
            PopulationCapacity = populationCapacity;

            // Custo e producao em ouro continuam sendo declarados a parte por
            // conveniencia de autoria; sao dobrados nos conjuntos, que sao a fonte
            // de verdade a partir daqui.
            Cost = new ResourceAmounts(cost);
            if (Cost[ResourceKind.Gold] == 0)
            {
                Cost[ResourceKind.Gold] = goldCost;
            }

            Production = new ResourceAmounts(production);
            if (Production[ResourceKind.Gold] == 0)
            {
                Production[ResourceKind.Gold] = baseGoldProduction;
            }
        }

        /// <summary>Custo de construir, em todos os recursos.</summary>
        public ResourceAmounts Cost { get; }

        /// <summary>Producao base por dia, em todos os recursos.</summary>
        public ResourceAmounts Production { get; }

        /// <summary>
        /// Trabalhadores necessarios para operar. Sem eles o edificio fica ocioso:
        /// nao produz, nao guarnece, e continua de pe (spec resources).
        /// </summary>
        public int WorkersRequired { get; }

        /// <summary>Quanto este edificio soma ao teto de populacao do reino.</summary>
        public int PopulationCapacity { get; }

        /// <summary>Edificio que existe para abrigar gente, e nao para produzir.</summary>
        public bool IsHousing => PopulationCapacity > 0;

        /// <summary>Edificio cujo papel principal e defender.</summary>
        public bool IsDefensive => Defense > 0;

        public string Id { get; }

        public string DisplayName { get; }

        /// <summary>Vazio significa que o edificio aceita qualquer terreno.</summary>
        public IReadOnlyList<TerrainType> AllowedTerrains { get; }

        public int GoldCost { get; }

        public int BaseGoldProduction { get; }

        public int Defense { get; }

        public IReadOnlyList<AdjacencyBonus> AdjacencyBonuses { get; }

        public bool CanBuildOn(TerrainType terrain)
        {
            if (AllowedTerrains.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < AllowedTerrains.Count; i++)
            {
                if (AllowedTerrains[i] == terrain)
                {
                    return true;
                }
            }

            return false;
        }

        public override string ToString() => Id;
    }
}

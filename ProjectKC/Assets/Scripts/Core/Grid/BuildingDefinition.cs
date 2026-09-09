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
        public AdjacencyBonus(AdjacencyMatch match, TerrainType terrain, string buildingId, int goldPerMatch)
        {
            Match = match;
            Terrain = terrain;
            BuildingId = buildingId;
            GoldPerMatch = goldPerMatch;
        }

        public static AdjacencyBonus ForTerrain(TerrainType terrain, int goldPerMatch)
        {
            return new AdjacencyBonus(AdjacencyMatch.Terrain, terrain, null, goldPerMatch);
        }

        public static AdjacencyBonus ForBuilding(string buildingId, int goldPerMatch)
        {
            return new AdjacencyBonus(AdjacencyMatch.Building, TerrainType.Plain, buildingId, goldPerMatch);
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
            IReadOnlyList<AdjacencyBonus> adjacencyBonuses = null)
        {
            Id = id;
            DisplayName = displayName;
            AllowedTerrains = allowedTerrains ?? new List<TerrainType>();
            GoldCost = goldCost;
            BaseGoldProduction = baseGoldProduction;
            Defense = defense;
            AdjacencyBonuses = adjacencyBonuses ?? new List<AdjacencyBonus>();
        }

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

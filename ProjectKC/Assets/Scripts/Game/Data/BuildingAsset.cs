using System;
using System.Collections.Generic;
using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
    [CreateAssetMenu(menuName = "Kingdom Collapse/Edificio", fileName = "Building_")]
    public sealed class BuildingAsset : ContentAsset
    {
        [Serializable]
        public sealed class Adjacency
        {
            [Tooltip("Contar terreno vizinho ou edificio vizinho.")]
            public AdjacencyMatch Match = AdjacencyMatch.Terrain;

            public TerrainType Terrain = TerrainType.Forest;

            [Tooltip("Id do edificio vizinho, quando Match for Building.")]
            public string BuildingId;

            public int GoldPerMatch = 1;

            [Tooltip("Qual recurso o bonus soma. Adjacencia age no recurso do edificio.")]
            public ResourceKind Resource = ResourceKind.Gold;
        }

        [SerializeField] private string _displayName = "Edificio";

        [Tooltip("Opcional: sem prefab atribuido, GridView desenha o marcador de cubo atual.")]
        [SerializeField] private GameObject _prefab;

        public GameObject Prefab => _prefab;

        [Tooltip("Terrenos onde pode ser construido. Vazio aceita qualquer terreno.")]
        [SerializeField] private List<TerrainType> _allowedTerrains = new List<TerrainType>();

        [SerializeField] private int _goldCost = 20;
        [SerializeField] private int _baseGoldProduction = 2;
        [SerializeField] private int _defense;
        [SerializeField] private List<Adjacency> _adjacencyBonuses = new List<Adjacency>();

        [Header("Recursos")]
        [Tooltip("Custo de construir, alem do ouro acima.")]
        [SerializeField] private List<ResourceEntry> _cost = new List<ResourceEntry>();

        [Tooltip("Producao por dia, alem do ouro acima.")]
        [SerializeField] private List<ResourceEntry> _production = new List<ResourceEntry>();

        [Tooltip("Trabalhadores para operar. Sem eles o edificio fica ocioso, e nao e destruido.")]
        [SerializeField] private int _workersRequired;

        [Tooltip("Quanto este edificio soma ao teto de populacao do reino.")]
        [SerializeField] private int _populationCapacity;

        [Serializable]
        public sealed class ResourceEntry
        {
            public ResourceKind Resource = ResourceKind.Wood;
            public int Amount = 1;
        }

        private static ResourceAmounts ToAmounts(List<ResourceEntry> entries)
        {
            ResourceAmounts amounts = new ResourceAmounts();
            for (int i = 0; i < entries.Count; i++)
            {
                amounts[entries[i].Resource] += entries[i].Amount;
            }

            return amounts;
        }

        public BuildingDefinition ToDefinition()
        {
            List<AdjacencyBonus> bonuses = new List<AdjacencyBonus>();
            for (int i = 0; i < _adjacencyBonuses.Count; i++)
            {
                Adjacency entry = _adjacencyBonuses[i];
                bonuses.Add(entry.Match == AdjacencyMatch.Terrain
                    ? AdjacencyBonus.ForTerrain(entry.Terrain, entry.GoldPerMatch, entry.Resource)
                    : AdjacencyBonus.ForBuilding(entry.BuildingId, entry.GoldPerMatch, entry.Resource));
            }

            return new BuildingDefinition(
                Id, _displayName, _allowedTerrains, _goldCost, _baseGoldProduction, _defense, bonuses,
                ToAmounts(_cost), ToAmounts(_production), _workersRequired, _populationCapacity);
        }

#if UNITY_EDITOR
        /// <summary>Preenchimento programatico, usado pelo ContentSeeder (task 9.1).
        /// Mesmo padrao de CardAsset: reafirma os campos a cada geracao, entao rodar
        /// o semeador de novo reseta estes numeros para os valores declarados aqui —
        /// ajuste fino depois disso e trabalho de Inspector.</summary>
        public void EditorConfigure(
            string displayName,
            List<TerrainType> allowedTerrains,
            int goldCost,
            int baseGoldProduction,
            int defense,
            List<Adjacency> adjacencyBonuses,
            List<ResourceEntry> cost,
            List<ResourceEntry> production,
            int workersRequired,
            int populationCapacity)
        {
            _displayName = displayName;
            _allowedTerrains = allowedTerrains ?? new List<TerrainType>();
            _goldCost = goldCost;
            _baseGoldProduction = baseGoldProduction;
            _defense = defense;
            _adjacencyBonuses = adjacencyBonuses ?? new List<Adjacency>();
            _cost = cost ?? new List<ResourceEntry>();
            _production = production ?? new List<ResourceEntry>();
            _workersRequired = workersRequired;
            _populationCapacity = populationCapacity;
        }
#endif
    }
}

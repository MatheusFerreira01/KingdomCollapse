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
        }

        [SerializeField] private string _displayName = "Edificio";

        [Tooltip("Terrenos onde pode ser construido. Vazio aceita qualquer terreno.")]
        [SerializeField] private List<TerrainType> _allowedTerrains = new List<TerrainType>();

        [SerializeField] private int _goldCost = 20;
        [SerializeField] private int _baseGoldProduction = 2;
        [SerializeField] private int _defense;
        [SerializeField] private List<Adjacency> _adjacencyBonuses = new List<Adjacency>();

        public BuildingDefinition ToDefinition()
        {
            List<AdjacencyBonus> bonuses = new List<AdjacencyBonus>();
            for (int i = 0; i < _adjacencyBonuses.Count; i++)
            {
                Adjacency entry = _adjacencyBonuses[i];
                bonuses.Add(entry.Match == AdjacencyMatch.Terrain
                    ? AdjacencyBonus.ForTerrain(entry.Terrain, entry.GoldPerMatch)
                    : AdjacencyBonus.ForBuilding(entry.BuildingId, entry.GoldPerMatch));
            }

            return new BuildingDefinition(
                Id, _displayName, _allowedTerrains, _goldCost, _baseGoldProduction, _defense, bonuses);
        }
    }
}

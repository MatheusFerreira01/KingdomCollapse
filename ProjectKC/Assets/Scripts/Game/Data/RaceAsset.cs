using System;
using System.Collections.Generic;
using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
    [CreateAssetMenu(menuName = "Kingdom Collapse/Raca", fileName = "Race_")]
    public sealed class RaceAsset : ContentAsset
    {
        [Serializable]
        public sealed class RuleEntry
        {
            [Tooltip("Use as constantes de RuleKeys. Chave desconhecida e ignorada, nunca quebra.")]
            public string Key;

            public float Value;
        }

        [SerializeField] private string _displayName = "Raca";

        [TextArea]
        [SerializeField] private string _description;

        [Serializable]
        public sealed class ResourceEntry
        {
            public ResourceKind Resource = ResourceKind.Food;
            public int Amount = 10;
        }

        [Tooltip("Recursos com que a run comeca, alem do ouro abaixo.")]
        [SerializeField] private List<ResourceEntry> _startingResources = new List<ResourceEntry>();

        [SerializeField] private int _startingGold = 50;
        [SerializeField] private int _startingIntegrity = 20;
        [SerializeField] private int _handSize = 5;
        [SerializeField] private int _energyPerDay = 3;

        [SerializeField] private List<CardAsset> _startingCards = new List<CardAsset>();

        [Tooltip("Modificadores de regra. Ausencia significa comportamento padrao (design D8).")]
        [SerializeField] private List<RuleEntry> _rules = new List<RuleEntry>();

        [SerializeField] private List<string> _forbiddenCardIds = new List<string>();
        [SerializeField] private List<string> _forbiddenBuildingIds = new List<string>();
        [SerializeField] private List<string> _forbiddenEventIds = new List<string>();

        public string DisplayName => _displayName;

        public string Description => _description;

        public RaceDefinition ToDefinition()
        {
            List<CardDefinition> startingCards = new List<CardDefinition>();
            for (int i = 0; i < _startingCards.Count; i++)
            {
                if (_startingCards[i] != null)
                {
                    startingCards.Add(_startingCards[i].ToDefinition());
                }
            }

            Dictionary<string, double> rules = new Dictionary<string, double>();
            for (int i = 0; i < _rules.Count; i++)
            {
                if (!string.IsNullOrEmpty(_rules[i].Key))
                {
                    rules[_rules[i].Key] = _rules[i].Value;
                }
            }

            ResourceAmounts starting = new ResourceAmounts();
            for (int i = 0; i < _startingResources.Count; i++)
            {
                starting[_startingResources[i].Resource] += _startingResources[i].Amount;
            }

            return new RaceDefinition(
                Id,
                _displayName,
                _startingGold,
                _startingIntegrity,
                _handSize,
                _energyPerDay,
                startingCards,
                new RuleModifiers(rules),
                _forbiddenCardIds,
                _forbiddenBuildingIds,
                _forbiddenEventIds,
                starting);
        }
    }
}

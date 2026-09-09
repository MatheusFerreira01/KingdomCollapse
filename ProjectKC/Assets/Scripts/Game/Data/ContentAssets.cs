using System;
using System.Collections.Generic;
using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
    /// <summary>
    /// Base dos assets de conteudo. Cada asset e um espelho editavel de um tipo puro
    /// do Core: o Inspector edita isto, o jogo roda aquilo. E o que permite balancear
    /// sem recompilar (design D3) sem que o Core conheca o Unity (design D1).
    /// </summary>
    public abstract class ContentAsset : ScriptableObject
    {
        [Tooltip("Id estavel. Nao mudar depois que o conteudo estiver em uso: saves e nos de meta apontam para ele.")]
        [SerializeField] private string _id;

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;

#if UNITY_EDITOR
        /// <summary>Preenche o id com o nome do arquivo quando estiver vazio.</summary>
        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(_id))
            {
                _id = name;
            }
        }
#endif
    }

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

    [CreateAssetMenu(menuName = "Kingdom Collapse/Carta", fileName = "Card_")]
    public sealed class CardAsset : ContentAsset
    {
        [SerializeField] private string _displayName = "Carta";

        [TextArea]
        [SerializeField] private string _rulesText;

        [SerializeField] private int _energyCost = 1;
        [SerializeField] private TargetRequirement _target = TargetRequirement.None;

        [Tooltip("Carta retida nao vai para o descarte no Fim do Dia.")]
        [SerializeField] private bool _retained;

        [Tooltip("Vazio libera para todas as racas.")]
        [SerializeField] private List<string> _allowedRaceIds = new List<string>();

        [SerializeField] private List<EffectEntry> _effects = new List<EffectEntry>();

        public string DisplayName => _displayName;

        public string RulesText => _rulesText;

        public CardDefinition ToDefinition()
        {
            return new CardDefinition(
                Id,
                _displayName,
                _energyCost,
                _target,
                EffectEntry.ToEffects(_effects),
                _retained,
                _allowedRaceIds);
        }
    }

    [CreateAssetMenu(menuName = "Kingdom Collapse/Evento", fileName = "Event_")]
    public sealed class EventAsset : ContentAsset
    {
        [Serializable]
        public sealed class Condition
        {
            public enum ConditionKind
            {
                MinimumDay = 0,
                OwnsTerrain = 1,
                OwnsBuilding = 2,
                MinimumTiles = 3
            }

            public ConditionKind Kind = ConditionKind.MinimumDay;
            public int Value = 1;
            public TerrainType Terrain = TerrainType.Mine;
            public string BuildingId;

            public EventCondition ToCondition()
            {
                switch (Kind)
                {
                    case ConditionKind.OwnsTerrain:
                        return new OwnsTerrainCondition(Terrain);
                    case ConditionKind.OwnsBuilding:
                        return new OwnsBuildingCondition(BuildingId);
                    case ConditionKind.MinimumTiles:
                        return new MinimumTilesCondition(Value);
                    default:
                        return new MinimumDayCondition(Value);
                }
            }
        }

        [Serializable]
        public sealed class Option
        {
            public string Label = "Continuar";
            public List<EffectEntry> Effects = new List<EffectEntry>();
        }

        [SerializeField] private string _displayName = "Evento";

        [TextArea]
        [SerializeField] private string _flavorText;

        [Tooltip("Classe mostrada ao jogador antes de confirmar. Define o orcamento de severidade.")]
        [SerializeField] private EventClass _class = EventClass.Neutral;

        [Tooltip("Peso relativo dentro da classe.")]
        [SerializeField] private float _weight = 1f;

        [Tooltip("Dias em que o evento fica indisponivel depois de sair.")]
        [SerializeField] private int _cooldownDays = 5;

        [SerializeField] private List<Condition> _conditions = new List<Condition>();

        [Tooltip("Uma opcao para evento simples; duas ou mais para evento com escolha.")]
        [SerializeField] private List<Option> _options = new List<Option> { new Option() };

        public EventClass Class => _class;

        public EventDefinition ToDefinition()
        {
            List<EventOption> options = new List<EventOption>();
            for (int i = 0; i < _options.Count; i++)
            {
                options.Add(new EventOption(_options[i].Label, EffectEntry.ToEffects(_options[i].Effects)));
            }

            List<EventCondition> conditions = new List<EventCondition>();
            for (int i = 0; i < _conditions.Count; i++)
            {
                conditions.Add(_conditions[i].ToCondition());
            }

            return new EventDefinition(
                Id, _displayName, _class, options, conditions, _weight, _cooldownDays, _flavorText);
        }
    }

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
                _forbiddenEventIds);
        }
    }

    [CreateAssetMenu(menuName = "Kingdom Collapse/No de Meta", fileName = "Meta_")]
    public sealed class MetaNodeAsset : ContentAsset
    {
        [Serializable]
        public sealed class UnlockEntry
        {
            public UnlockKind Kind = UnlockKind.Card;
            public string ContentId;
        }

        [SerializeField] private string _displayName = "No";

        [TextArea]
        [SerializeField] private string _description;

        [SerializeField] private int _cost = 5;
        [SerializeField] private List<MetaNodeAsset> _prerequisites = new List<MetaNodeAsset>();

        [Tooltip("Vazio significa tronco geral. Preenchido restringe o desbloqueio a esta raca.")]
        [SerializeField] private string _raceBranchId;

        [Tooltip("Meta desbloqueia conteudo. NumericBonus e reprovado pela validacao de catalogo.")]
        [SerializeField] private List<UnlockEntry> _unlocks = new List<UnlockEntry>();

        public string DisplayName => _displayName;

        public MetaNodeDefinition ToDefinition()
        {
            List<Unlock> unlocks = new List<Unlock>();
            for (int i = 0; i < _unlocks.Count; i++)
            {
                unlocks.Add(new Unlock(_unlocks[i].Kind, _unlocks[i].ContentId));
            }

            List<string> prerequisites = new List<string>();
            for (int i = 0; i < _prerequisites.Count; i++)
            {
                if (_prerequisites[i] != null)
                {
                    prerequisites.Add(_prerequisites[i].Id);
                }
            }

            return new MetaNodeDefinition(
                Id, _displayName, _cost, unlocks, prerequisites, _raceBranchId, _description);
        }
    }
}

using System;
using System.Collections.Generic;
using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
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

            [Tooltip("Opcao paga: o custo em ouro e preco, nao dano, e fica isento do teto da classe. " +
                     "So vale se o evento tiver outra opcao sem custo, para dar como recusar.")]
            public bool IsOffer;

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
                options.Add(new EventOption(
                    _options[i].Label,
                    EffectEntry.ToEffects(_options[i].Effects),
                    _options[i].IsOffer));
            }

            List<EventCondition> conditions = new List<EventCondition>();
            for (int i = 0; i < _conditions.Count; i++)
            {
                conditions.Add(_conditions[i].ToCondition());
            }

            return new EventDefinition(
                Id, _displayName, _class, options, conditions, _weight, _cooldownDays, _flavorText);
        }

#if UNITY_EDITOR
        /// <summary>Preenchimento programatico, usado pelo gerador de conteudo inicial.</summary>
        public void EditorConfigure(
            string displayName,
            string flavorText,
            EventClass eventClass,
            List<Option> options,
            List<Condition> conditions = null,
            float weight = 1f,
            int cooldownDays = 5)
        {
            _displayName = displayName;
            _flavorText = flavorText;
            _class = eventClass;
            _options = options ?? new List<Option>();
            _conditions = conditions ?? new List<Condition>();
            _weight = weight;
            _cooldownDays = cooldownDays;
        }
#endif
    }
}

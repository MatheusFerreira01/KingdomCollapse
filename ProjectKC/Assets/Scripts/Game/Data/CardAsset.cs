using System;
using System.Collections.Generic;
using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
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

#if UNITY_EDITOR
        /// <summary>Preenchimento programatico, usado pelo gerador de conteudo inicial.</summary>
        public void EditorConfigure(
            string displayName,
            string rulesText,
            int energyCost,
            TargetRequirement target,
            List<EffectEntry> effects,
            bool retained = false)
        {
            _displayName = displayName;
            _rulesText = rulesText;
            _energyCost = energyCost;
            _target = target;
            _effects = effects ?? new List<EffectEntry>();
            _retained = retained;
        }
#endif
    }
}

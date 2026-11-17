using System;
using System.Collections.Generic;
using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
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

        [Tooltip("Conteudo que o no libera. NumericBonus e reprovado: poder permanente vai em Bonuses.")]
        [SerializeField] private List<UnlockEntry> _unlocks = new List<UnlockEntry>();

        [Serializable]
        public sealed class BonusEntry
        {
            public MetaBonusKind Kind = MetaBonusKind.StartingResource;

            [Tooltip("So vale para StartingResource.")]
            public ResourceKind Resource = ResourceKind.Food;

            public int Amount = 5;
        }

        [Tooltip("Poder permanente no estado inicial da run. Pesa no teto de poder do nivel.")]
        [SerializeField] private List<BonusEntry> _bonuses = new List<BonusEntry>();

        [Tooltip("Nivel de dificuldade que precisa estar liberado para comprar este no.")]
        [Min(1)]
        [SerializeField] private int _requiredLevel = 1;

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

            List<MetaBonus> bonuses = new List<MetaBonus>();
            for (int i = 0; i < _bonuses.Count; i++)
            {
                bonuses.Add(_bonuses[i].Kind == MetaBonusKind.MaxIntegrity
                    ? MetaBonus.Integrity(_bonuses[i].Amount)
                    : MetaBonus.Starting(_bonuses[i].Resource, _bonuses[i].Amount));
            }

            return new MetaNodeDefinition(
                Id, _displayName, _cost, unlocks, prerequisites, _raceBranchId, _description,
                bonuses, _requiredLevel);
        }
    }
}

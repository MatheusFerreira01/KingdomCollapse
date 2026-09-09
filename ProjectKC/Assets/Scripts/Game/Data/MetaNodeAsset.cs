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

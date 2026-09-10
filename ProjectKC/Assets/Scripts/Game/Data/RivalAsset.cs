using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
    /// <summary>Um reino rival autorado no Inspector (design D3).</summary>
    [CreateAssetMenu(menuName = "Kingdom Collapse/Rival", fileName = "Rival_")]
    public sealed class RivalAsset : ContentAsset
    {
        [SerializeField] private string _displayName = "Rival";

        [SerializeField] private RivalIdentityAsset _identity;

        [Header("Campanha")]
        [Tooltip("Ataques que o jogador precisa repelir para derrotar este rival.")]
        [Min(1)]
        [SerializeField] private int _attacksToDefeat = 3;

        [Tooltip("Forca do primeiro ataque.")]
        [Min(1)]
        [SerializeField] private int _firstForce = 8;

        [Tooltip("Forca somada a cada ataque seguinte. Precisa crescer, ou a validacao reprova.")]
        [SerializeField] private float _growthPerAttack = 3f;

        [Tooltip("1 = cresce em linha reta. Acima de 1, acelera.")]
        [SerializeField] private float _exponent = 1f;

        public string DisplayName => _displayName;

        public RivalDefinition ToDefinition()
        {
            return new RivalDefinition(
                Id,
                _displayName,
                _identity != null ? _identity.ToDefinition() : null,
                _attacksToDefeat,
                new RivalCampaignCurve(_firstForce, _growthPerAttack, _exponent));
        }
    }
}

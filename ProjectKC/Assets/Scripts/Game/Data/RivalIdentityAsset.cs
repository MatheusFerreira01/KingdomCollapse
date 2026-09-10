using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
    /// <summary>
    /// Tipo de pressao de um rival, autorado no Inspector. E asset proprio, e nao
    /// campo do rival, para que dois rivais possam compartilhar a mesma identidade e
    /// a validacao consiga dizer que eles se repetem.
    /// </summary>
    [CreateAssetMenu(menuName = "Kingdom Collapse/Identidade de Rival", fileName = "RivalIdentity_")]
    public sealed class RivalIdentityAsset : ContentAsset
    {
        [SerializeField] private string _displayName = "Identidade";

        [Tooltip("O que o jogador deve esperar. Aparece quando o rival declara guerra.")]
        [TextArea]
        [SerializeField] private string _description;

        [Header("Pressao")]
        [Tooltip("Onde cai a perda. Undeclared reprova o catalogo de proposito.")]
        [SerializeField] private PressureAxis _axis = PressureAxis.Undeclared;

        [Tooltip("So vale com o eixo Resource. Populacao tem eixo proprio.")]
        [SerializeField] private ResourceKind _pressuredResource = ResourceKind.Food;

        [Header("Saque (so rival economico)")]
        [Tooltip("Fracao da forca levada mesmo com o ataque repelido. 0,5 = metade.")]
        [Range(0f, 1f)]
        [SerializeField] private float _raidFraction = (float)RivalIdentity.DefaultRaidFraction;

        [Tooltip("Recurso levado por ponto de forca. Ajusta a escala entre forca e recurso.")]
        [SerializeField] private float _lossPerForce = 1f;

        public RivalIdentity ToDefinition()
        {
            return new RivalIdentity(
                Id,
                _displayName,
                _description,
                _axis,
                _axis == PressureAxis.Resource ? _pressuredResource : (ResourceKind?)null,
                _raidFraction,
                _lossPerForce);
        }
    }
}

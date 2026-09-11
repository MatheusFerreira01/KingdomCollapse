using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
    /// <summary>
    /// Espelho editavel de um RivalDefinition: identidade (eixo pressionado) mais a
    /// curva de ataque da campanha (task 9.4, [Editor]). Autorado um por um no
    /// Inspector — nao passa pelo ContentSeeder porque cada rival precisa de ajuste
    /// fino individual, nao de um valor generico repetido.
    /// </summary>
    [CreateAssetMenu(menuName = "Kingdom Collapse/Rival", fileName = "Rival_")]
    public sealed class RivalAsset : ContentAsset
    {
        [SerializeField] private string _displayName = "Rival";

        [Tooltip("Eixo que este rival pressiona a cada ataque.")]
        [SerializeField] private RivalAxis _axis = RivalAxis.Territory;

        [Tooltip("So importa quando Eixo for Resource: qual dos cinco recursos o pedagio cobra.")]
        [SerializeField] private ResourceKind _resource = ResourceKind.Gold;

        [Tooltip("Quantos ataques repelidos derrotam este rival.")]
        [SerializeField] private int _attackCount = 3;

        [Header("Curva de forca da campanha")]
        [SerializeField] private double _baseForce = 3;
        [SerializeField] private double _perDay = 1.0;
        [SerializeField] private double _dayExponent = 1.25;

        public RivalDefinition ToDefinition()
        {
            RivalIdentity identity = new RivalIdentity(Id, _displayName, _axis, _resource);
            ThreatCurve curve = new ThreatCurve(_baseForce, _perDay, _dayExponent);
            return new RivalDefinition(Id, _displayName, identity, _attackCount, curve);
        }
    }
}

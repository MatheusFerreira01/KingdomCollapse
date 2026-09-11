using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
    /// <summary>
    /// Icones do HUD (recursos, energia, defesa, ameaca). Campo opcional: quando um
    /// icone nao esta atribuido, o painel cai no texto atual, entao nada quebra
    /// enquanto os assets gratuitos nao chegam (design "Arte de terreno e construcao
    /// via campo de asset no ScriptableObject").
    /// </summary>
    [CreateAssetMenu(menuName = "Kingdom Collapse/HUD/Conjunto de Icones", fileName = "HudIconSet")]
    public sealed class HudIconSet : ScriptableObject
    {
        [Header("Recursos")]
        [SerializeField] private Texture2D _gold;
        [SerializeField] private Texture2D _wood;
        [SerializeField] private Texture2D _stone;
        [SerializeField] private Texture2D _food;
        [SerializeField] private Texture2D _population;

        [Header("Status")]
        [SerializeField] private Texture2D _energy;
        [SerializeField] private Texture2D _defense;
        [SerializeField] private Texture2D _threat;

        public Texture2D For(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Gold:
                    return _gold;
                case ResourceKind.Wood:
                    return _wood;
                case ResourceKind.Stone:
                    return _stone;
                case ResourceKind.Food:
                    return _food;
                case ResourceKind.Population:
                    return _population;
                default:
                    return null;
            }
        }

        public Texture2D Energy => _energy;

        public Texture2D Defense => _defense;

        public Texture2D Threat => _threat;
    }
}

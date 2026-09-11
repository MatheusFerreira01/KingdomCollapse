using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
    /// <summary>
    /// Textura por tipo de terreno. Campo opcional: sem textura atribuida, o bloco
    /// continua na cor chapada de `TerrainVisuals` (spec terrain-art, design "Arte de
    /// terreno e construcao via campo de asset no ScriptableObject").
    /// </summary>
    [CreateAssetMenu(menuName = "Kingdom Collapse/HUD/Conjunto de Terreno", fileName = "TerrainVisualSet")]
    public sealed class TerrainVisualSet : ScriptableObject
    {
        [Header("Textura (usada se nao houver prefab)")]
        [SerializeField] private Texture2D _plain;
        [SerializeField] private Texture2D _forest;
        [SerializeField] private Texture2D _mine;
        [SerializeField] private Texture2D _river;
        [SerializeField] private Texture2D _ruin;

        [Header("Prop 3D (opcional, tem prioridade sobre a textura)")]
        [Tooltip("Instanciado em cima da celula, como o marcador de construcao. Sem collider proprio: o clique continua indo para o bloco da celula.")]
        [SerializeField] private GameObject _plainProp;
        [SerializeField] private GameObject _forestProp;
        [SerializeField] private GameObject _mineProp;
        [SerializeField] private GameObject _riverProp;
        [SerializeField] private GameObject _ruinProp;

        [Header("Conectores entre celulas vizinhas (opcional)")]
        [Tooltip("Sem prefab, o conector vira uma faixa procedural simples.")]
        [SerializeField] private GameObject _roadProp;

        [Tooltip("Usado quando a conexao cruza rio. Sem prefab, vira faixa procedural.")]
        [SerializeField] private GameObject _bridgeProp;

        public GameObject RoadProp => _roadProp;

        public GameObject BridgeProp => _bridgeProp;

        public Texture2D TextureFor(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Plain:
                    return _plain;
                case TerrainType.Forest:
                    return _forest;
                case TerrainType.Mine:
                    return _mine;
                case TerrainType.River:
                    return _river;
                case TerrainType.Ruin:
                    return _ruin;
                default:
                    return null;
            }
        }

        public GameObject PropFor(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Plain:
                    return _plainProp;
                case TerrainType.Forest:
                    return _forestProp;
                case TerrainType.Mine:
                    return _mineProp;
                case TerrainType.River:
                    return _riverProp;
                case TerrainType.Ruin:
                    return _ruinProp;
                default:
                    return null;
            }
        }
    }
}

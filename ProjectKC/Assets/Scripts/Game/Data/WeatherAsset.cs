using System;
using System.Collections.Generic;
using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
    /// <summary>
    /// Um clima autorado no Inspector. Fica em arquivo proprio, e nao junto dos
    /// outros assets de conteudo, porque o clima nasceu depois: assets ja criados
    /// apontam para o arquivo onde sua classe estava, e mover classe entre arquivos
    /// quebraria essas referencias.
    /// </summary>
    [CreateAssetMenu(menuName = "Kingdom Collapse/Clima", fileName = "Weather_")]
    public sealed class WeatherAsset : ContentAsset
    {
        [Serializable]
        public sealed class TerrainBonus
        {
            public TerrainType Terrain = TerrainType.Plain;

            [Tooltip("Ouro somado por celula produtiva deste terreno. Pode ser negativo.")]
            public int Gold = 1;
        }

        [SerializeField] private string _displayName = "Clima";

        [TextArea]
        [SerializeField] private string _flavorText;

        [Header("Producao")]
        [SerializeField] private List<TerrainBonus> _terrainProduction = new List<TerrainBonus>();

        [Tooltip("Somado a 1 no multiplicador do dia. 0,2 = +20%. Teto de 0,35 para cada lado.")]
        [SerializeField] private float _productionMultiplier;

        [Header("Custos e acoes")]
        [Tooltip("Multiplica o custo de construir hoje. 1 = normal.")]
        [SerializeField] private float _buildCostMultiplier = 1f;

        [SerializeField] private int _energyDelta;
        [SerializeField] private bool _blocksPurchase;
        [SerializeField] private bool _blocksReveal;

        [Header("Dano")]
        [Tooltip("Dano a base. Sempre limitado e nunca letal.")]
        [SerializeField] private int _baseDamage;

        [Header("Efeito sobre a ameaca")]
        [Tooltip("Somado a forca das ameacas que chegam neste dia. Negativo enfraquece.")]
        [SerializeField] private int _threatForceDelta;

        [Tooltip("Adia as ameacas deste dia. Nunca antecipa: encurtar o aviso e proibido.")]
        [SerializeField] private int _threatDelayDays;

        [Header("Sorteio")]
        [SerializeField] private float _weight = 1f;

        [Tooltip("Unico clima autorizado a nao ter vantagem e desvantagem juntas.")]
        [SerializeField] private bool _isCalmDay;

        public string DisplayName => _displayName;

        public WeatherDefinition ToDefinition()
        {
            Dictionary<TerrainType, int> terrain = new Dictionary<TerrainType, int>();
            for (int i = 0; i < _terrainProduction.Count; i++)
            {
                terrain[_terrainProduction[i].Terrain] = _terrainProduction[i].Gold;
            }

            return new WeatherDefinition(
                Id,
                _displayName,
                terrain,
                _productionMultiplier,
                _buildCostMultiplier,
                _energyDelta,
                _baseDamage,
                _blocksPurchase,
                _blocksReveal,
                _threatForceDelta,
                _threatDelayDays,
                _weight,
                _flavorText,
                _isCalmDay);
        }

#if UNITY_EDITOR
        /// <summary>Preenchimento programatico, usado pelo gerador de conteudo inicial.</summary>
        public void EditorConfigure(
            string displayName,
            string flavorText,
            Dictionary<TerrainType, int> terrainProduction,
            float productionMultiplier,
            float buildCostMultiplier,
            int energyDelta,
            int baseDamage,
            bool blocksPurchase,
            bool blocksReveal,
            int threatForceDelta,
            int threatDelayDays,
            float weight,
            bool isCalmDay)
        {
            _displayName = displayName;
            _flavorText = flavorText;
            _productionMultiplier = productionMultiplier;
            _buildCostMultiplier = buildCostMultiplier;
            _energyDelta = energyDelta;
            _baseDamage = baseDamage;
            _blocksPurchase = blocksPurchase;
            _blocksReveal = blocksReveal;
            _threatForceDelta = threatForceDelta;
            _threatDelayDays = threatDelayDays;
            _weight = weight;
            _isCalmDay = isCalmDay;

            _terrainProduction = new List<TerrainBonus>();
            if (terrainProduction == null)
            {
                return;
            }

            foreach (KeyValuePair<TerrainType, int> pair in terrainProduction)
            {
                _terrainProduction.Add(new TerrainBonus { Terrain = pair.Key, Gold = pair.Value });
            }
        }
#endif
    }
}

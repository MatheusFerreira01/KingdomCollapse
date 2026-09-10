using System.Collections.Generic;
using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
    /// <summary>
    /// Um degrau da escada de dificuldade, autorado no Inspector. A posicao na lista
    /// de niveis do GameDatabase e o que define o numero do nivel.
    /// </summary>
    [CreateAssetMenu(menuName = "Kingdom Collapse/Nivel de Dificuldade", fileName = "Level_")]
    public sealed class DifficultyLevelAsset : ContentAsset
    {
        [SerializeField] private string _displayName = "Nivel";

        [Tooltip("O que este nivel endurece em relacao ao anterior. Aparece na selecao de nivel. " +
                 "Obrigatorio a partir do nivel 2.")]
        [TextArea(2, 5)]
        [SerializeField] private string _hardening;

        [Header("Rivais")]
        [Tooltip("Na ordem em que entram em guerra. Cada um precisa exigir mais que o anterior, " +
                 "e nenhuma identidade pode se repetir.")]
        [SerializeField] private List<RivalAsset> _rivals = new List<RivalAsset>();

        [Tooltip("Dia de entrada de cada rival, na mesma ordem. O primeiro sempre entra no dia 1. " +
                 "Vazio usa um rival a cada 8 dias.")]
        [SerializeField] private List<int> _entryDays = new List<int>();

        [Header("Endurecimentos")]
        [Tooltip("Multiplica o saldo inicial de todo recurso menos populacao. 1 = normal.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float _startingResourceFactor = 1f;

        [Tooltip("Poder acumulado que a arvore de meta pode conceder ate este nivel.")]
        [Min(0)]
        [SerializeField] private int _metaPowerCap;

        public string DisplayName => _displayName;

        public DifficultyLevel ToDefinition(int rank)
        {
            List<RivalDefinition> rivals = new List<RivalDefinition>();
            for (int i = 0; i < _rivals.Count; i++)
            {
                if (_rivals[i] != null)
                {
                    rivals.Add(_rivals[i].ToDefinition());
                }
            }

            return new DifficultyLevel(
                Id,
                rank,
                _displayName,
                _hardening,
                rivals,
                _entryDays.Count > 0 ? _entryDays : null,
                _startingResourceFactor,
                _metaPowerCap);
        }
    }
}

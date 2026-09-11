using System.Collections.Generic;
using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
    /// <summary>
    /// Espelho editavel de um degrau da escada de dificuldade (task 9.5, [Editor]).
    /// Autorado um por um: o roster de rivais e as frases de endurecimento sao
    /// escolha de design, nao um padrao repetivel pelo ContentSeeder.
    /// </summary>
    [CreateAssetMenu(menuName = "Kingdom Collapse/Nivel de Dificuldade", fileName = "Difficulty_")]
    public sealed class DifficultyLevelAsset : ContentAsset
    {
        [SerializeField] private string _displayName = "Nivel";

        [SerializeField] private List<RivalAsset> _rivals = new List<RivalAsset>();

        [Tooltip("Frases descrevendo o que este nivel endurece frente ao anterior. A UI le daqui.")]
        [SerializeField] private List<string> _hardenings = new List<string>();

        [SerializeField] private int _metaPowerCap;

        [SerializeField] private int _pressure;

        public string DisplayName => _displayName;

        public DifficultyLevel ToDefinition()
        {
            List<RivalDefinition> rivals = new List<RivalDefinition>();
            for (int i = 0; i < _rivals.Count; i++)
            {
                if (_rivals[i] != null)
                {
                    rivals.Add(_rivals[i].ToDefinition());
                }
            }

            return new DifficultyLevel(Id, _displayName, rivals, _hardenings, _metaPowerCap, _pressure);
        }
    }
}

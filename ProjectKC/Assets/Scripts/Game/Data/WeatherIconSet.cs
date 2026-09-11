using System;
using System.Collections.Generic;
using UnityEngine;

namespace KingdomCollapse.Game
{
    /// <summary>
    /// Icone por clima, procurado pelo Id do <c>WeatherAsset</c> (ex. "weather_sun").
    /// Campo opcional: sem icone atribuido para o clima do dia, o HUD cai no nome do
    /// clima em texto (spec weather-scenery).
    /// </summary>
    [CreateAssetMenu(menuName = "Kingdom Collapse/HUD/Conjunto de Icones de Clima", fileName = "WeatherIconSet")]
    public sealed class WeatherIconSet : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string WeatherId;
            public Texture2D Icon;
        }

        [SerializeField] private List<Entry> _icons = new List<Entry>();

        public Texture2D IconFor(string weatherId)
        {
            for (int i = 0; i < _icons.Count; i++)
            {
                if (_icons[i] != null && _icons[i].WeatherId == weatherId)
                {
                    return _icons[i].Icon;
                }
            }

            return null;
        }
    }
}

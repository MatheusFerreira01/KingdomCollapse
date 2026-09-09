using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Como o clima mexeu numa ameaca ja anunciada. Guardado para a UI conseguir
    /// destacar a mudanca: um numero que muda no relogio sem explicacao seria pior
    /// que um numero fixo (design D13).
    /// </summary>
    public sealed class ThreatWeatherEffect
    {
        public ThreatWeatherEffect(ScheduledThreat threat, string weatherId, int forceDelta, int delayDays)
        {
            Threat = threat;
            WeatherId = weatherId;
            ForceDelta = forceDelta;
            DelayDays = delayDays;
        }

        public ScheduledThreat Threat { get; }

        public string WeatherId { get; }

        public int ForceDelta { get; }

        public int DelayDays { get; }
    }

    /// <summary>
    /// Clima de hoje e previsao de amanha. A previsao nao e enfeite: e o que permite
    /// o jogador decidir hoje em funcao de amanha, no mesmo principio do relogio de
    /// ameaca. Uma vez prevista, a condicao nao muda.
    /// </summary>
    public sealed class WeatherSystem
    {
        private readonly List<WeatherDefinition> _catalog = new List<WeatherDefinition>();

        public WeatherSystem(IEnumerable<WeatherDefinition> catalog = null)
        {
            if (catalog == null)
            {
                return;
            }

            foreach (WeatherDefinition weather in catalog)
            {
                if (weather != null)
                {
                    _catalog.Add(weather);
                }
            }
        }

        public IReadOnlyList<WeatherDefinition> Catalog => _catalog;

        /// <summary>Clima vigente. Nunca nulo enquanto houver catalogo.</summary>
        public WeatherDefinition Today { get; private set; }

        /// <summary>Clima do dia seguinte, ja conhecido durante o Planejamento.</summary>
        public WeatherDefinition Tomorrow { get; private set; }

        public bool HasWeather => _catalog.Count > 0;

        /// <summary>
        /// Prepara o primeiro dia: sorteia o clima de hoje e ja preve o de amanha.
        /// Retorna o que a previsao fez com as ameacas agendadas.
        /// </summary>
        public List<ThreatWeatherEffect> Begin(RunState run, IRandomSource random)
        {
            List<ThreatWeatherEffect> effects = new List<ThreatWeatherEffect>();

            if (!HasWeather)
            {
                return effects;
            }

            Today = Draw(random);
            ApplyToThreats(run, Today, run.Day, effects);

            Tomorrow = Draw(random);
            ApplyToThreats(run, Tomorrow, run.Day + 1, effects);

            return effects;
        }

        /// <summary>
        /// Avanca um dia: a previsao de ontem vira o clima de hoje, e uma previsao
        /// nova e feita para amanha. O clima previsto nunca e re-sorteado, ou a
        /// previsao seria mentira.
        /// </summary>
        public List<ThreatWeatherEffect> Advance(RunState run, IRandomSource random)
        {
            List<ThreatWeatherEffect> effects = new List<ThreatWeatherEffect>();

            if (!HasWeather)
            {
                return effects;
            }

            Today = Tomorrow ?? Draw(random);
            Tomorrow = Draw(random);
            ApplyToThreats(run, Tomorrow, run.Day + 1, effects);

            return effects;
        }

        private WeatherDefinition Draw(IRandomSource random)
        {
            double[] weights = new double[_catalog.Count];
            for (int i = 0; i < _catalog.Count; i++)
            {
                weights[i] = _catalog[i].Weight;
            }

            int index = RunRandom.PickWeighted(random, weights);
            return index < 0 ? _catalog[0] : _catalog[index];
        }

        /// <summary>
        /// Aplica o efeito do clima sobre as ameacas que chegam no dia previsto. Roda
        /// no momento da previsao para que o relogio ja exiba o numero corrigido: a
        /// spec promete que a forca prevista e a forca que chega.
        /// </summary>
        private static void ApplyToThreats(
            RunState run, WeatherDefinition weather, int day, List<ThreatWeatherEffect> effects)
        {
            if (weather == null || !weather.TouchesThreats)
            {
                return;
            }

            List<ScheduledThreat> due = run.Threats.DueOn(day);

            for (int i = 0; i < due.Count; i++)
            {
                ScheduledThreat threat = due[i];
                int before = threat.Force;

                if (weather.ThreatForceDelta != 0)
                {
                    run.Threats.ModifyForce(threat, weather.ThreatForceDelta);
                }

                int delayed = 0;
                if (weather.ThreatDelayDays > 0)
                {
                    delayed = run.Threats.Delay(threat, weather.ThreatDelayDays);
                }

                effects.Add(new ThreatWeatherEffect(
                    threat, weather.Id, threat.Force - before, delayed));
            }
        }
    }
}

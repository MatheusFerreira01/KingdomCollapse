using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Sorteio ponderado de eventos, com cooldown e mistura de classes. Os pesos de
    /// classe existem para que uma sequencia de azar nao vire a experiencia inteira:
    /// positivos e neutros somados dominam a distribuicao (spec day-events).
    /// </summary>
    public sealed class EventPool
    {
        private readonly List<EventDefinition> _catalog = new List<EventDefinition>();
        private readonly Dictionary<string, int> _lastSeenDay = new Dictionary<string, int>();

        public EventPool(IEnumerable<EventDefinition> catalog = null, EventClassWeights weights = null)
        {
            Weights = weights ?? EventClassWeights.Default;

            if (catalog == null)
            {
                return;
            }

            foreach (EventDefinition definition in catalog)
            {
                if (definition != null)
                {
                    _catalog.Add(definition);
                }
            }
        }

        public EventClassWeights Weights { get; }

        public IReadOnlyList<EventDefinition> Catalog => _catalog;

        public void Add(EventDefinition definition)
        {
            if (definition != null)
            {
                _catalog.Add(definition);
            }
        }

        /// <summary>Eventos que passam em elegibilidade e cooldown neste dia.</summary>
        public List<EventDefinition> EligibleFor(RunState run)
        {
            List<EventDefinition> eligible = new List<EventDefinition>();
            for (int i = 0; i < _catalog.Count; i++)
            {
                EventDefinition definition = _catalog[i];

                if (!definition.IsEligible(run))
                {
                    continue;
                }

                if (IsOnCooldown(definition, run.Day))
                {
                    continue;
                }

                eligible.Add(definition);
            }

            return eligible;
        }

        public bool IsOnCooldown(EventDefinition definition, int currentDay)
        {
            if (!_lastSeenDay.TryGetValue(definition.Id, out int lastDay))
            {
                return false;
            }

            return currentDay - lastDay <= definition.CooldownDays;
        }

        /// <summary>
        /// Sorteia no maximo um evento. O peso final combina o peso do proprio
        /// evento com o peso da sua classe, entao o balanceamento de "quanta coisa
        /// ruim acontece" fica num lugar so.
        /// </summary>
        public EventDefinition Draw(RunState run, IRandomSource random)
        {
            List<EventDefinition> eligible = EligibleFor(run);
            if (eligible.Count == 0)
            {
                return null;
            }

            double[] weights = new double[eligible.Count];
            for (int i = 0; i < eligible.Count; i++)
            {
                weights[i] = eligible[i].Weight * Weights.For(eligible[i].Class);
            }

            int index = RunRandom.PickWeighted(random, weights);
            if (index < 0)
            {
                return null;
            }

            EventDefinition chosen = eligible[index];
            _lastSeenDay[chosen.Id] = run.Day;
            return chosen;
        }

        public void ResetCooldowns() => _lastSeenDay.Clear();
    }

    /// <summary>
    /// Peso de cada classe no sorteio. O padrao deixa negativos em menos de um terco
    /// da massa: eventos existem para diversificar a run, nao para dificulta-la.
    /// </summary>
    public sealed class EventClassWeights
    {
        public EventClassWeights(double positive, double neutral, double negative)
        {
            Positive = positive;
            Neutral = neutral;
            Negative = negative;
        }

        public double Positive { get; }

        public double Neutral { get; }

        public double Negative { get; }

        public static readonly EventClassWeights Default = new EventClassWeights(0.40, 0.32, 0.28);

        public double For(EventClass eventClass)
        {
            switch (eventClass)
            {
                case EventClass.Positive:
                    return Positive;
                case EventClass.Neutral:
                    return Neutral;
                case EventClass.Negative:
                    return Negative;
                default:
                    return 0;
            }
        }
    }

    /// <summary>Resultado da fase de Evento, para log e UI.</summary>
    public sealed class EventOutcome
    {
        public EventOutcome(EventDefinition definition, EventOption option, List<EffectResult> results)
        {
            Definition = definition;
            Option = option;
            Results = results ?? new List<EffectResult>();
        }

        public EventDefinition Definition { get; }

        public EventOption Option { get; }

        public List<EffectResult> Results { get; }

        public bool AnyClamped
        {
            get
            {
                for (int i = 0; i < Results.Count; i++)
                {
                    if (Results[i].Clamped)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }

    public static class EventResolver
    {
        /// <summary>
        /// Aplica a opcao escolhida sob o orcamento de severidade da classe do evento.
        /// Nenhum efeito e aplicado antes da escolha: quem chama ja decidiu.
        /// </summary>
        public static EventOutcome Resolve(RunState run, EventDefinition definition, int optionIndex = 0)
        {
            if (definition == null || definition.Options.Count == 0)
            {
                return null;
            }

            int index = optionIndex;
            if (index < 0 || index >= definition.Options.Count)
            {
                index = 0;
            }

            EventOption option = definition.Options[index];
            SeverityBudget budget = SeverityBudget.ForClass(definition.Class);
            List<EffectResult> results = EffectRunner.ApplyAll(
                option.Effects, run, EffectSource.Event, null, budget);

            run.Stats.EventsSeen++;
            return new EventOutcome(definition, option, results);
        }
    }
}

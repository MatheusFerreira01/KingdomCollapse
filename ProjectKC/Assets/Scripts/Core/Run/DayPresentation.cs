using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Um passo da encenação da resolução do dia. Existe no Core, e não na camada
    /// Unity, por um motivo específico: a ordem apresentada tem que ser a ordem
    /// resolvida (spec game-feel), e a única forma de garantir isso é derivar os
    /// passos dos mesmos eventos de domínio que o motor emitiu.
    ///
    /// A encenação não decide nada. Ela é uma leitura do que já aconteceu.
    /// </summary>
    public enum DayStepKind
    {
        Production = 0,
        Food = 1,
        Weather = 2,
        Threat = 3,
        Attack = 4,
        Event = 5,
        Plunder = 6,
        Population = 7,
        Milestone = 8,
        RivalDefeated = 9,
        Outcome = 10
    }

    public sealed class DayStep
    {
        public DayStep(DayStepKind kind, RunEvent source, string headline)
        {
            Kind = kind;
            Source = source;
            Headline = headline;
        }

        public DayStepKind Kind { get; }

        /// <summary>O evento de domínio que originou o passo.</summary>
        public RunEvent Source { get; }

        /// <summary>Uma linha curta que resume o passo, para leitura rápida.</summary>
        public string Headline { get; }

        /// <summary>
        /// Passos que merecem destaque próprio: o clímax do dia e os momentos de
        /// progresso. A UI pausa mais neles.
        /// </summary>
        public bool IsHighlight =>
            Kind == DayStepKind.Attack ||
            Kind == DayStepKind.RivalDefeated ||
            Kind == DayStepKind.Outcome;

        public override string ToString() => Kind + ": " + Headline;
    }

    /// <summary>
    /// Traduz os eventos de domínio de um dia numa sequência apresentável, na ordem
    /// em que o núcleo os resolveu.
    /// </summary>
    public static class DayPresentation
    {
        public static List<DayStep> Build(IReadOnlyList<RunEvent> events)
        {
            List<DayStep> steps = new List<DayStep>();

            if (events == null)
            {
                return steps;
            }

            for (int i = 0; i < events.Count; i++)
            {
                DayStep step = Translate(events[i]);
                if (step != null)
                {
                    steps.Add(step);
                }
            }

            return steps;
        }

        private static DayStep Translate(RunEvent runEvent)
        {
            switch (runEvent)
            {
                case ProductionCollectedEvent production:
                    return production.Produced.IsEmpty
                        ? null
                        : new DayStep(DayStepKind.Production, production,
                            "Produção: " + production.Produced);

                case FoodResolvedEvent food:
                    return new DayStep(DayStepKind.Food, food,
                        food.Famine
                            ? "Fome: " + food.Starved + " de população perdida"
                            : "Consumo: -" + food.Upkeep + " de comida");

                case PopulationGrewEvent growth:
                    return new DayStep(DayStepKind.Population, growth,
                        "População cresce para " + growth.Total + " de " + growth.Capacity);

                case WeatherChangedEvent weather:
                    return weather.Today == null
                        ? null
                        : new DayStep(DayStepKind.Weather, weather,
                            weather.Today.DisplayName +
                            (weather.Tomorrow == null
                                ? string.Empty
                                : " — amanhã: " + weather.Tomorrow.DisplayName));

                case ThreatAnnouncedEvent announced:
                    return new DayStep(DayStepKind.Threat, announced,
                        announced.Threat.Kind + " em " + announced.Threat.DaysUntil(0) * -1 +
                        " dias, força " + announced.Threat.Force);

                case ThreatWeatheredEvent weathered:
                    return new DayStep(DayStepKind.Threat, weathered,
                        "O clima muda a ameaça: " +
                        (weathered.Effect.ForceDelta != 0
                            ? "força " + weathered.Effect.ForceDelta
                            : "adiada em " + weathered.Effect.DelayDays + " dia(s)"));

                case AttackResolvedEvent attack:
                    return new DayStep(DayStepKind.Attack, attack,
                        attack.Report.Repelled
                            ? "Ataque repelido"
                            : "As defesas cedem");

                case PlunderCollectedEvent plunder:
                    return new DayStep(DayStepKind.Plunder, plunder, "Saque: " + plunder.Plunder);

                case DayEventResolvedEvent dayEvent:
                    return new DayStep(DayStepKind.Event, dayEvent,
                        dayEvent.Outcome.Definition.DisplayName);

                case MilestoneReachedEvent milestone:
                    return new DayStep(DayStepKind.Milestone, milestone, milestone.Label);

                case RunCollapsedEvent collapsed:
                    return new DayStep(DayStepKind.Outcome, collapsed, "Colapso");

                default:
                    return null;
            }
        }
    }
}

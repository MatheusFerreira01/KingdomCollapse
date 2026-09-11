using System;
using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Uma presença deixada por um encontro resolvido: age sozinha em intervalos
    /// declarados, e o jogador sempre sabe o preço de encerrá-la antes de tentar
    /// (spec encounters — "Requirement: Presença persistente" e "Presença pode ser
    /// resolvida"). Não referencia o relógio de ameaça em lugar nenhum — é o que
    /// garante que uma presença nunca vira ataque de rival (task 8.5).
    /// </summary>
    public sealed class EncounterPresence
    {
        public EncounterPresence(
            string id,
            string sourceEncounterId,
            string displayName,
            int actionIntervalDays,
            IReadOnlyList<IEffect> periodicEffects,
            int startDay,
            ResourceAmounts resolutionCost)
        {
            Id = id;
            SourceEncounterId = sourceEncounterId;
            DisplayName = displayName;
            ActionIntervalDays = Math.Max(1, actionIntervalDays);
            PeriodicEffects = periodicEffects ?? new List<IEffect>();
            ResolutionCost = resolutionCost ?? new ResourceAmounts();
            NextActionDay = startDay + ActionIntervalDays;
        }

        public string Id { get; }

        public string SourceEncounterId { get; }

        public string DisplayName { get; }

        public int ActionIntervalDays { get; }

        public IReadOnlyList<IEffect> PeriodicEffects { get; }

        /// <summary>Custo conhecido de antemão para encerrar esta presença.</summary>
        public ResourceAmounts ResolutionCost { get; }

        /// <summary>Próximo dia em que a ação periódica dispara. Anunciado — a UI
        /// consulta isto, não é surpresa.</summary>
        public int NextActionDay { get; internal set; }
    }

    /// <summary>
    /// Uma continuação de encontro já agendada para um dia futuro, carregando a
    /// referência à escolha que a originou (spec — "Requirement: Encontros
    /// encadeiam").
    /// </summary>
    public sealed class ScheduledEncounter
    {
        public ScheduledEncounter(EncounterDefinition definition, int day, string previousChoiceContext)
        {
            Definition = definition;
            Day = day;
            PreviousChoiceContext = previousChoiceContext ?? string.Empty;
        }

        public EncounterDefinition Definition { get; }

        public int Day { get; }

        /// <summary>Rótulo da opção anterior que originou esta continuação.</summary>
        public string PreviousChoiceContext { get; }
    }

    /// <summary>
    /// Estado de encontros de uma run: presenças ativas e continuações agendadas.
    /// Vive separado do relógio de ameaça de propósito — os dois nunca se tocam
    /// (spec encounters — "Requirement: Encontros não substituem a campanha").
    /// </summary>
    public sealed class EncounterState
    {
        private readonly List<EncounterPresence> _presences = new List<EncounterPresence>();
        private readonly List<ScheduledEncounter> _scheduledChains = new List<ScheduledEncounter>();

        public IReadOnlyList<EncounterPresence> Presences => _presences;

        public IReadOnlyList<ScheduledEncounter> ScheduledChains => _scheduledChains;

        public void AddPresence(EncounterPresence presence)
        {
            if (presence != null)
            {
                _presences.Add(presence);
            }
        }

        /// <summary>Encerra uma presença pagando o custo declarado. Sem saldo, recusa
        /// sem alterar nada — o custo é conhecido, não é uma aposta.</summary>
        public bool ResolvePresence(RunState run, string presenceId)
        {
            EncounterPresence presence = Find(presenceId);
            if (presence == null || run == null)
            {
                return false;
            }

            if (!run.TrySpend(presence.ResolutionCost, out ResourceShortage _))
            {
                return false;
            }

            _presences.Remove(presence);
            return true;
        }

        private EncounterPresence Find(string id)
        {
            for (int i = 0; i < _presences.Count; i++)
            {
                if (_presences[i].Id == id)
                {
                    return _presences[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Aplica a ação periódica de toda presença vencida hoje, sob o orçamento de
        /// severidade da categoria (task 8.4). Cada presença tem intervalo próprio,
        /// então isto roda por presença, não por dia fixo.
        /// </summary>
        public List<EffectResult> TickPresences(RunState run, int day, SeverityBudget budget)
        {
            List<EffectResult> results = new List<EffectResult>();

            // Copia: um efeito periodico poderia, em tese, mexer na lista (nao mexe
            // hoje, mas iterar a copia evita a armadilha por construcao).
            List<EncounterPresence> snapshot = new List<EncounterPresence>(_presences);

            for (int i = 0; i < snapshot.Count; i++)
            {
                EncounterPresence presence = snapshot[i];
                if (presence.NextActionDay > day)
                {
                    continue;
                }

                results.AddRange(EffectRunner.ApplyAll(presence.PeriodicEffects, run, EffectSource.Event, null, budget));
                presence.NextActionDay = day + presence.ActionIntervalDays;
            }

            return results;
        }

        public void ScheduleChain(EncounterDefinition next, int day, string previousChoiceContext)
        {
            if (next != null)
            {
                _scheduledChains.Add(new ScheduledEncounter(next, day, previousChoiceContext));
            }
        }

        /// <summary>
        /// Continuações que vencem hoje, removidas da fila. Outros encontros
        /// acontecendo no intervalo não afetam isto — a fila é independente
        /// (spec — "Cadeia não se perde").
        /// </summary>
        public List<ScheduledEncounter> DrainDueChains(int day)
        {
            List<ScheduledEncounter> due = new List<ScheduledEncounter>();

            for (int i = _scheduledChains.Count - 1; i >= 0; i--)
            {
                if (_scheduledChains[i].Day <= day)
                {
                    due.Add(_scheduledChains[i]);
                    _scheduledChains.RemoveAt(i);
                }
            }

            return due;
        }
    }

    /// <summary>Resultado de resolver a opção escolhida de um encontro.</summary>
    public sealed class EncounterOutcome
    {
        public EncounterOutcome(EncounterDefinition definition, EncounterOption option, List<EffectResult> results)
        {
            Definition = definition;
            Option = option;
            Results = results ?? new List<EffectResult>();
        }

        public EncounterDefinition Definition { get; }

        public EncounterOption Option { get; }

        public List<EffectResult> Results { get; }
    }

    public static class EncounterResolver
    {
        /// <summary>
        /// Aplica a opção escolhida sob o orçamento de severidade da categoria do
        /// encontro — mesma regra de EventResolver (task 8.4). Nunca toca
        /// ThreatClock nem RivalCampaignState (task 8.5): a assinatura nem os
        /// recebe.
        /// </summary>
        public static EncounterOutcome Resolve(RunState run, EncounterDefinition definition, int optionIndex = 0)
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

            EncounterOption option = definition.Options[index];
            SeverityBudget budget = SeverityBudget.ForClass(definition.SeverityClass);

            if (option.IsOffer && definition.HasDeclineOption)
            {
                budget = budget.AsOffer();
            }

            List<EffectResult> results = EffectRunner.ApplyAll(option.Effects, run, EffectSource.Event, null, budget);
            return new EncounterOutcome(definition, option, results);
        }
    }

    /// <summary>
    /// Sorteio ponderado de encontros, com cooldown — mesma mecânica de EventPool,
    /// categoria própria para não competir pelo mesmo slot dos eventos de fim de
    /// dia (design D10: eventos temperam, encontros marcam a run).
    /// </summary>
    public sealed class EncounterPool
    {
        private readonly List<EncounterDefinition> _catalog = new List<EncounterDefinition>();
        private readonly Dictionary<string, int> _lastSeenDay = new Dictionary<string, int>();

        public EncounterPool(IEnumerable<EncounterDefinition> catalog = null)
        {
            if (catalog == null)
            {
                return;
            }

            foreach (EncounterDefinition definition in catalog)
            {
                if (definition != null)
                {
                    _catalog.Add(definition);
                }
            }
        }

        public IReadOnlyList<EncounterDefinition> Catalog => _catalog;

        public void Add(EncounterDefinition definition)
        {
            if (definition != null)
            {
                _catalog.Add(definition);
            }
        }

        public List<EncounterDefinition> EligibleFor(RunState run)
        {
            List<EncounterDefinition> eligible = new List<EncounterDefinition>();
            for (int i = 0; i < _catalog.Count; i++)
            {
                EncounterDefinition definition = _catalog[i];
                if (!definition.IsEligible(run) || IsOnCooldown(definition, run.Day))
                {
                    continue;
                }

                eligible.Add(definition);
            }

            return eligible;
        }

        public bool IsOnCooldown(EncounterDefinition definition, int currentDay)
        {
            if (!_lastSeenDay.TryGetValue(definition.Id, out int lastDay))
            {
                return false;
            }

            return currentDay - lastDay <= definition.CooldownDays;
        }

        public EncounterDefinition Draw(RunState run, IRandomSource random)
        {
            List<EncounterDefinition> eligible = EligibleFor(run);
            if (eligible.Count == 0)
            {
                return null;
            }

            double[] weights = new double[eligible.Count];
            for (int i = 0; i < eligible.Count; i++)
            {
                weights[i] = eligible[i].Weight;
            }

            int index = RunRandom.PickWeighted(random, weights);
            if (index < 0)
            {
                return null;
            }

            EncounterDefinition chosen = eligible[index];
            _lastSeenDay[chosen.Id] = run.Day;
            return chosen;
        }
    }
}

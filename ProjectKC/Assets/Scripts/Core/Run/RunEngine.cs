using System;
using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    public enum CommandRejection
    {
        None = 0,
        WrongPhase,
        RunOver,
        NotEnoughGold,
        NotEnoughEnergy,
        CardNotInHand,
        InvalidTarget,
        TargetRequired,
        GridRefused
    }

    public readonly struct CommandResult
    {
        private CommandResult(bool ok, CommandRejection rejection, GridRejection gridRejection)
        {
            Ok = ok;
            Rejection = rejection;
            GridRejection = gridRejection;
        }

        public bool Ok { get; }

        public CommandRejection Rejection { get; }

        public GridRejection GridRejection { get; }

        public static readonly CommandResult Success =
            new CommandResult(true, CommandRejection.None, Core.GridRejection.None);

        public static CommandResult Fail(CommandRejection rejection)
        {
            return new CommandResult(false, rejection, Core.GridRejection.None);
        }

        public static CommandResult FailGrid(GridRejection gridRejection)
        {
            return new CommandResult(false, CommandRejection.GridRefused, gridRejection);
        }

        public override string ToString() => Ok ? "ok" : Rejection.ToString();
    }

    /// <summary>Obra em andamento, para racas que atrasam a construcao.</summary>
    public sealed class PendingBuild
    {
        public PendingBuild(Coord coord, BuildingDefinition building, int readyOnDay)
        {
            Coord = coord;
            Building = building;
            ReadyOnDay = readyOnDay;
        }

        public Coord Coord { get; }

        public BuildingDefinition Building { get; }

        public int ReadyOnDay { get; }
    }

    /// <summary>
    /// Maquina de estados do dia e unica porta de entrada para comandos do jogador
    /// (design D2). Comandos so sao aceitos no Planejamento; a resolucao do dia e um
    /// passo atomico, para que a ordem producao -> ameaca -> evento nao dependa de
    /// quem chamou o que primeiro.
    /// </summary>
    public sealed class RunEngine
    {
        private readonly List<RunEvent> _pendingEvents = new List<RunEvent>();
        private readonly List<PendingBuild> _pendingBuilds = new List<PendingBuild>();
        private readonly List<Milestone> _milestones;
        private readonly HashSet<string> _reachedMilestones = new HashSet<string>();

        public RunEngine(RunState run, EventPool eventPool = null, List<Milestone> milestones = null)
        {
            Run = run;
            EventPool = eventPool ?? new EventPool();
            _milestones = milestones ?? Milestone.Default();
        }

        public RunState Run { get; }

        public EventPool EventPool { get; }

        public RunScore FinalScore { get; private set; }

        /// <summary>Ultimo evento de fim de dia sorteado e ainda nao resolvido.</summary>
        public EventDefinition PendingDayEvent { get; private set; }

        public IReadOnlyList<PendingBuild> PendingBuilds => _pendingBuilds;

        /// <summary>Retira os eventos de dominio acumulados. A view consome e desenha.</summary>
        public List<RunEvent> DrainEvents()
        {
            List<RunEvent> drained = new List<RunEvent>(_pendingEvents);
            _pendingEvents.Clear();
            return drained;
        }

        private void Emit(RunEvent runEvent) => _pendingEvents.Add(runEvent);

        private void SetPhase(DayPhase phase)
        {
            Run.Phase = phase;
            Emit(new PhaseChangedEvent(phase));
        }

        /// <summary>Abre a run: resolve o primeiro Inicio do Dia e para no Planejamento.</summary>
        public void StartRun()
        {
            BeginDay();
        }

        // --- Inicio do Dia ---

        private void BeginDay()
        {
            SetPhase(DayPhase.DayStart);

            Run.ActedAggressivelyToday = false;
            CompletePendingBuilds();

            // O relogio anuncia antes do Planejamento: o jogador nunca planeja um dia
            // sem enxergar a fila inteira de ameacas (spec threat-clock).
            List<ScheduledThreat> announced = Run.Threats.AnnounceDue(
                Run.Day, Run.Grid.OwnedCount, Run.Random.Channel(RandomChannel.Threats));
            for (int i = 0; i < announced.Count; i++)
            {
                Emit(new ThreatAnnouncedEvent(announced[i]));
            }

            int energyPenalty = (int)Run.SumModifier(ModifierKeys.EnergyPenalty);
            Run.Energy = Math.Max(0, Run.EnergyPerDay - energyPenalty);

            int extraDraw = (int)Run.SumModifier(ModifierKeys.ExtraDraw);
            int drawn = Run.Deck.DrawUpTo(
                Run.HandSize + extraDraw, Run.Random.Channel(RandomChannel.Cards));

            Emit(new DayStartedEvent(Run.Day, Run.Energy, drawn));
            CheckMilestones();

            SetPhase(DayPhase.Planning);
        }

        private void CompletePendingBuilds()
        {
            for (int i = _pendingBuilds.Count - 1; i >= 0; i--)
            {
                PendingBuild pending = _pendingBuilds[i];
                if (pending.ReadyOnDay > Run.Day)
                {
                    continue;
                }

                GridResult result = Run.Grid.Build(pending.Coord, pending.Building);
                _pendingBuilds.RemoveAt(i);

                if (result.Ok)
                {
                    Run.Stats.BuildingsBuilt++;
                    Emit(new BuildingPlacedEvent(pending.Coord, pending.Building.Id, false, Run.Day));
                }
            }
        }

        // --- Comandos do jogador ---

        private CommandResult EnsurePlanning()
        {
            if (Run.IsOver)
            {
                return CommandResult.Fail(CommandRejection.RunOver);
            }

            return Run.Phase != DayPhase.Planning
                ? CommandResult.Fail(CommandRejection.WrongPhase)
                : CommandResult.Success;
        }

        public CommandResult BuyTile(Coord coord)
        {
            CommandResult phase = EnsurePlanning();
            if (!phase.Ok)
            {
                return phase;
            }

            GridResult check = Run.Grid.CanPurchase(coord, Run.Rules);
            if (!check.Ok)
            {
                return CommandResult.FailGrid(check.Rejection);
            }

            int cost = Run.Grid.NextTileCost(Run.Rules);
            if (!Run.CanAfford(cost))
            {
                return CommandResult.Fail(CommandRejection.NotEnoughGold);
            }

            Run.RemoveGold(cost);
            Run.Grid.Purchase(coord, Run.Rules);
            Run.Stats.PeakTilesOwned = Math.Max(Run.Stats.PeakTilesOwned, Run.Grid.OwnedCount);

            Emit(new TilePurchasedEvent(coord, Run.Grid.TileAt(coord).Terrain, cost));
            CheckMilestones();
            return CommandResult.Success;
        }

        public CommandResult BuildOn(Coord coord, BuildingDefinition building)
        {
            CommandResult phase = EnsurePlanning();
            if (!phase.Ok)
            {
                return phase;
            }

            if (!Run.Race.AllowsBuilding(building))
            {
                return CommandResult.Fail(CommandRejection.InvalidTarget);
            }

            GridResult check = Run.Grid.CanBuild(coord, building);
            if (!check.Ok)
            {
                return CommandResult.FailGrid(check.Rejection);
            }

            if (!Run.CanAfford(building.GoldCost))
            {
                return CommandResult.Fail(CommandRejection.NotEnoughGold);
            }

            Run.RemoveGold(building.GoldCost);

            int delay = Run.Rules.GetInt(RuleKeys.BuildDelayDays, 0);
            if (delay > 0)
            {
                // Regra dos Elfos: a obra e paga agora e fica pronta depois.
                _pendingBuilds.Add(new PendingBuild(coord, building, Run.Day + delay));
                Emit(new BuildingPlacedEvent(coord, building.Id, true, Run.Day + delay));
                return CommandResult.Success;
            }

            Run.Grid.Build(coord, building);
            Run.Stats.BuildingsBuilt++;
            Emit(new BuildingPlacedEvent(coord, building.Id, false, Run.Day));
            CheckMilestones();
            return CommandResult.Success;
        }

        /// <summary>
        /// Repara um quadrado arrasado por ouro. Existe como comando, e nao apenas
        /// como efeito de carta, porque a spec garante que a celula e reconstruivel:
        /// depender do sorteio de uma carta tornaria a destruicao permanente sempre
        /// que a carta nao viesse.
        /// </summary>
        public CommandResult RepairTile(Coord coord)
        {
            CommandResult phase = EnsurePlanning();
            if (!phase.Ok)
            {
                return phase;
            }

            Tile tile = Run.Grid.TileAt(coord);
            if (!tile.Owned)
            {
                return CommandResult.FailGrid(GridRejection.NotOwned);
            }

            if (!tile.Destroyed)
            {
                return CommandResult.Fail(CommandRejection.InvalidTarget);
            }

            int cost = Run.Grid.CostCurve.RepairCost;
            if (!Run.CanAfford(cost))
            {
                return CommandResult.Fail(CommandRejection.NotEnoughGold);
            }

            Run.RemoveGold(cost);
            Run.Grid.Repair(coord);
            Emit(new TileRepairedEvent(coord, cost));
            return CommandResult.Success;
        }

        public CommandResult Demolish(Coord coord)
        {
            CommandResult phase = EnsurePlanning();
            if (!phase.Ok)
            {
                return phase;
            }

            GridResult result = Run.Grid.Demolish(coord);
            return result.Ok ? CommandResult.Success : CommandResult.FailGrid(result.Rejection);
        }

        /// <summary>
        /// Joga uma carta da mao. Valida fase, posse, energia e alvo antes de aplicar
        /// qualquer efeito: alvo invalido devolve a carta intacta e nao gasta energia.
        /// </summary>
        public CommandResult PlayCard(CardDefinition card, Coord? target = null)
        {
            CommandResult phase = EnsurePlanning();
            if (!phase.Ok)
            {
                return phase;
            }

            if (card == null || !Run.Deck.HandContains(card))
            {
                return CommandResult.Fail(CommandRejection.CardNotInHand);
            }

            if (Run.Energy < card.EnergyCost)
            {
                return CommandResult.Fail(CommandRejection.NotEnoughEnergy);
            }

            if (card.NeedsTarget)
            {
                if (!target.HasValue)
                {
                    return CommandResult.Fail(CommandRejection.TargetRequired);
                }

                if (!TargetRules.IsValidTarget(Run.Grid, card.Target, target.Value, Run.Rules))
                {
                    return CommandResult.Fail(CommandRejection.InvalidTarget);
                }
            }

            Run.Energy -= card.EnergyCost;
            Run.Deck.DiscardFromHand(card);

            List<EffectResult> results = EffectRunner.ApplyAll(
                card.Effects, Run, EffectSource.Card, target, SeverityBudget.Unlimited);

            Emit(new CardPlayedEvent(card.Id, target, results));
            CheckMilestones();
            return CommandResult.Success;
        }

        // --- Fim do Dia ---

        /// <summary>
        /// Resolve o dia inteiro: producao, ameacas, evento e limpeza. Para de imediato
        /// se o Colapso acontecer no meio.
        /// </summary>
        public CommandResult EndDay(int eventOptionIndex = 0)
        {
            CommandResult phase = EnsurePlanning();
            if (!phase.Ok)
            {
                return phase;
            }

            SetPhase(DayPhase.Resolution);
            ResolveProduction();
            ResolveThreats();

            if (Run.CheckCollapse())
            {
                FinishRun();
                return CommandResult.Success;
            }

            SetPhase(DayPhase.Event);
            ResolveDayEvent(eventOptionIndex);

            if (Run.CheckCollapse())
            {
                FinishRun();
                return CommandResult.Success;
            }

            SetPhase(DayPhase.DayEnd);
            ResolveEndOfDay();

            if (Run.CheckCollapse())
            {
                FinishRun();
                return CommandResult.Success;
            }

            Run.Day++;
            BeginDay();
            return CommandResult.Success;
        }

        private void ResolveProduction()
        {
            // A producao vem antes de qualquer perda do dia: a spec exige que o ouro
            // esteja creditado antes de o ataque resolver.
            List<ProductionBreakdown> breakdowns = new List<ProductionBreakdown>();
            int gold = Run.CollectDailyProduction(breakdowns);
            Run.AddGold(gold);
            Emit(new ProductionCollectedEvent(gold, breakdowns));
        }

        private void ResolveThreats()
        {
            List<ScheduledThreat> due = Run.Threats.DueOn(Run.Day);
            for (int i = 0; i < due.Count; i++)
            {
                AttackReport report = CombatResolver.Resolve(Run, due[i]);
                Run.Threats.MarkResolved(due[i]);
                Run.ActedAggressivelyToday = true;

                if (report.Repelled)
                {
                    Run.Stats.AttacksSurvived++;
                }

                Emit(new AttackResolvedEvent(report));

                if (Run.CheckCollapse())
                {
                    return;
                }
            }
        }

        private void ResolveDayEvent(int optionIndex)
        {
            EventDefinition definition = EventPool.Draw(Run, Run.Random.Channel(RandomChannel.Events));
            PendingDayEvent = definition;

            if (definition == null)
            {
                return;
            }

            Emit(new DayEventDrawnEvent(definition));
            EventOutcome outcome = EventResolver.Resolve(Run, definition, optionIndex);
            PendingDayEvent = null;

            if (outcome != null)
            {
                Emit(new DayEventResolvedEvent(outcome));
            }
        }

        private void ResolveEndOfDay()
        {
            // Regra dos Orcs: um dia sem combate nem saque custa ouro.
            double stagnation = Run.Rules.GetDouble(RuleKeys.StagnationGoldLossFraction, 0);
            if (stagnation > 0 && !Run.ActedAggressivelyToday)
            {
                int loss = (int)Math.Ceiling(Run.Gold * stagnation);
                Run.RemoveGold(loss);
            }

            int handBefore = Run.Deck.Hand.Count;
            int discarded = Run.Deck.DiscardHand();
            Emit(new HandDiscardedEvent(discarded, handBefore - discarded));

            // Energia nao acumula: o dia seguinte recomeca cheio, nunca somado.
            Run.Energy = 0;
            Run.Stats.DaysSurvived = Run.Day;
            Run.Stats.PeakTilesOwned = Math.Max(Run.Stats.PeakTilesOwned, Run.Grid.OwnedCount);
            Run.TickModifiers();
        }

        private void CheckMilestones()
        {
            for (int i = 0; i < _milestones.Count; i++)
            {
                Milestone milestone = _milestones[i];
                if (_reachedMilestones.Contains(milestone.Id))
                {
                    continue;
                }

                if (!milestone.Predicate(Run))
                {
                    continue;
                }

                _reachedMilestones.Add(milestone.Id);
                Run.Stats.MilestonesReached++;
                Emit(new MilestoneReachedEvent(milestone.Id, milestone.Label));
            }
        }

        private void FinishRun()
        {
            Run.Stats.DaysSurvived = Run.Day;
            FinalScore = ScoreCalculator.Calculate(Run);
            Emit(new RunCollapsedEvent(Run.Collapse, FinalScore));
        }
    }
}

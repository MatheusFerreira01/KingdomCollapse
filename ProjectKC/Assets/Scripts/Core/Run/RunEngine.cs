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
        GridRefused,
        WeatherBlocked,
        NotEnoughResources
    }

    public readonly struct CommandResult
    {
        private CommandResult(
            bool ok, CommandRejection rejection, GridRejection gridRejection, ResourceShortage shortage)
        {
            Ok = ok;
            Rejection = rejection;
            GridRejection = gridRejection;
            Shortage = shortage;
        }

        /// <summary>
        /// Qual recurso faltou, quando a recusa foi por recurso. "Ouro insuficiente"
        /// e inutil quando o que falta e pedra.
        /// </summary>
        public ResourceShortage Shortage { get; }

        public bool Ok { get; }

        public CommandRejection Rejection { get; }

        public GridRejection GridRejection { get; }

        public static readonly CommandResult Success =
            new CommandResult(true, CommandRejection.None, Core.GridRejection.None, ResourceShortage.None);

        public static CommandResult Fail(CommandRejection rejection)
        {
            return new CommandResult(
                false, rejection, Core.GridRejection.None, ResourceShortage.None);
        }

        public static CommandResult FailGrid(GridRejection gridRejection)
        {
            return new CommandResult(
                false, CommandRejection.GridRefused, gridRejection, ResourceShortage.None);
        }

        public static CommandResult FailResource(ResourceShortage shortage)
        {
            return new CommandResult(
                false, CommandRejection.NotEnoughResources, Core.GridRejection.None, shortage);
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

            AdvanceWeather();

            // O relogio anuncia antes do Planejamento: o jogador nunca planeja um dia
            // sem enxergar a fila inteira de ameacas (spec threat-clock).
            AnnounceThreats();

            int energyPenalty = (int)Run.SumModifier(ModifierKeys.EnergyPenalty);
            int weatherEnergy = Run.TodayWeather?.EnergyDelta ?? 0;
            Run.Energy = Math.Max(0, Run.EnergyPerDay - energyPenalty + weatherEnergy);

            int extraDraw = (int)Run.SumModifier(ModifierKeys.ExtraDraw);
            int drawn = Run.Deck.DrawUpTo(
                Run.HandSize + extraDraw, Run.Random.Channel(RandomChannel.Cards));

            Emit(new DayStartedEvent(Run.Day, Run.Energy, drawn));
            WarnAboutFamine();
            CheckMilestones();

            SetPhase(DayPhase.Planning);
        }

        /// <summary>
        /// Preenche o relogio de ameaca. Com campanha de rival, a origem e a
        /// campanha do rival vigente (design D3); sem uma, cai na curva anonima
        /// antiga — o que mantem toda run de teste sem rival funcionando igual.
        /// </summary>
        private void AnnounceThreats()
        {
            if (Run.Campaign != null && Run.Campaign.HasRoster)
            {
                ScheduledThreat scheduled = Run.Campaign.AnnounceIfDue(Run.Threats, Run.Day);

                if (Run.Campaign.JustDeclared != null)
                {
                    Emit(new RivalDeclaredEvent(Run.Campaign.JustDeclared));
                }

                if (scheduled != null)
                {
                    Emit(new ThreatAnnouncedEvent(scheduled));
                }

                return;
            }

            List<ScheduledThreat> announced = Run.Threats.AnnounceDue(
                Run.Day, Run.Grid.OwnedCount, Run.Random.Channel(RandomChannel.Threats));
            for (int i = 0; i < announced.Count; i++)
            {
                Emit(new ThreatAnnouncedEvent(announced[i]));
            }
        }

        /// <summary>
        /// Vira o dia do clima e aplica o que a nova previsao faz com as ameacas.
        /// Roda antes do Planejamento para que o relogio ja mostre o numero corrigido
        /// quando o jogador for decidir (design D13).
        /// </summary>
        private void AdvanceWeather()
        {
            if (!Run.Weather.HasWeather)
            {
                return;
            }

            IRandomSource random = Run.Random.Channel(RandomChannel.Events);

            List<ThreatWeatherEffect> effects = Run.Weather.Today == null
                ? Run.Weather.Begin(Run, random)
                : Run.Weather.Advance(Run, random);

            Emit(new WeatherChangedEvent(Run.Weather.Today, Run.Weather.Tomorrow));

            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].ForceDelta != 0 || effects[i].DelayDays != 0)
                {
                    Emit(new ThreatWeatheredEvent(effects[i]));
                }
            }

            // Dano de clima e pequeno e nunca letal: o clima da textura ao dia, nao
            // encerra a run (spec weather).
            int damage = Run.TodayWeather?.BaseDamage ?? 0;
            if (damage > 0)
            {
                int allowed = Math.Min(damage, Math.Max(0, Run.Integrity - 1));
                if (allowed > 0)
                {
                    Run.Damage(allowed);
                }
            }
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

            if (Run.TodayWeather != null && Run.TodayWeather.BlocksPurchase)
            {
                return CommandResult.Fail(CommandRejection.WeatherBlocked);
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

            ResourceAmounts buildCost = BuildCostOf(building);
            if (!Run.TrySpend(buildCost, out ResourceShortage shortage))
            {
                return CommandResult.FailResource(shortage);
            }

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

        /// <summary>Custo de obra do dia, em todos os recursos, ja com o efeito do clima.</summary>
        public ResourceAmounts BuildCostOf(BuildingDefinition building)
        {
            double multiplier = Run.TodayWeather?.BuildCostMultiplier ?? 1.0;
            ResourceAmounts cost = new ResourceAmounts();

            foreach (ResourceKind kind in building.Cost.NonZero())
            {
                // Populacao nao encarece com o clima: chuva atrasa a obra, nao muda
                // quantas pessoas ela exige.
                double scale = kind == ResourceKind.Population ? 1.0 : multiplier;
                cost[kind] = Math.Max(0,
                    (int)Math.Round(building.Cost[kind] * scale, MidpointRounding.AwayFromZero));
            }

            return cost;
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
            // A producao vem antes de qualquer perda do dia: a spec exige que os
            // recursos estejam creditados antes de o ataque resolver.
            List<ProductionBreakdown> breakdowns = new List<ProductionBreakdown>();
            ResourceAmounts produced = Run.CollectDailyProductionByResource(breakdowns);

            Run.Add(produced);
            Emit(new ProductionCollectedEvent(produced, breakdowns));

            ResolveFood(produced[ResourceKind.Food]);
        }

        /// <summary>
        /// A populacao come depois de a producao entrar. A ordem importa: cobrar
        /// antes de creditar faria a colheita do dia nao alimentar ninguem.
        /// </summary>
        private void ResolveFood(int producedFood)
        {
            int upkeep = Run.DailyFoodUpkeep();
            if (upkeep <= 0)
            {
                return;
            }

            int paid = Run.Remove(ResourceKind.Food, upkeep);
            int missing = upkeep - paid;
            int starved = 0;

            if (missing > 0)
            {
                // Fome nao encerra a run: ela derruba a populacao, e o reino fica
                // incapaz de operar ate ela voltar a crescer (spec resources).
                double severity = Run.Rules.GetDouble(RuleKeys.StarvationSeverity, 1.0);
                starved = (int)Math.Ceiling(missing * severity);
                starved = Run.Remove(ResourceKind.Population, starved);
            }

            Emit(new FoodResolvedEvent(producedFood, upkeep, starved));
        }

        /// <summary>
        /// Cresce a populacao com o excedente de comida, ate o teto dos edificios.
        /// Roda no Fim do Dia, depois de o consumo ja ter acontecido.
        /// </summary>
        private void ResolvePopulationGrowth()
        {
            int capacity = Run.PopulationCapacity();
            int current = Run[ResourceKind.Population];

            if (current >= capacity)
            {
                return;
            }

            double perGrowth = Math.Max(1, Run.Rules.GetDouble(RuleKeys.FoodPerGrowth, 5.0));
            int growth = (int)(Run[ResourceKind.Food] / perGrowth);

            if (growth <= 0)
            {
                return;
            }

            growth = Math.Min(growth, capacity - current);
            if (growth <= 0)
            {
                return;
            }

            Run.Remove(ResourceKind.Food, (int)Math.Round(growth * perGrowth, MidpointRounding.AwayFromZero));
            Run.Add(ResourceKind.Population, growth);
            Emit(new PopulationGrewEvent(growth, Run[ResourceKind.Population], capacity));
        }

        /// <summary>
        /// Avisa se a comida prevista nao cobre o consumo de amanha. O aviso sai no
        /// Planejamento de proposito: sem antecedencia, a fome vira punicao sem aviso.
        /// </summary>
        private void WarnAboutFamine()
        {
            // Usa o consumo previsto, e nao o de hoje: se amanha vem um clima que
            // faz comer mais, o aviso precisa refletir isso enquanto ainda da tempo.
            int upkeep = Run.PredictedFoodUpkeep();
            if (upkeep <= 0)
            {
                return;
            }

            int predicted = Run[ResourceKind.Food] +
                            Run.CollectDailyProductionByResource()[ResourceKind.Food];

            if (predicted < upkeep)
            {
                Emit(new FamineWarningEvent(predicted, upkeep));
            }
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

                    // Ponto de extensao: repelir pode render recursos. E o que
                    // permite uma raca tirar sustento da guerra, invertendo o papel
                    // da campanha inimiga, sem abrir este calculo no meio.
                    ResourceAmounts plunder = Run.Rules.PlunderOnRepel();
                    if (!plunder.IsEmpty)
                    {
                        Run.Add(plunder);
                        Emit(new PlunderCollectedEvent(plunder, report.Kind));
                    }
                }

                Emit(new AttackResolvedEvent(report));
                RegisterCampaignResolution(due[i], report);

                if (Run.CheckCollapse())
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Avisa a campanha do resultado do ataque. Ao derrotar o rival vigente,
        /// emite o marco e declara Vitoria se era o ultimo do roster — o proximo
        /// CheckCollapse (chamado logo em seguida) pega o Outcome ja decidido,
        /// porque IsOver responde true assim que a Vitoria e declarada.
        /// </summary>
        private void RegisterCampaignResolution(ScheduledThreat threat, AttackReport report)
        {
            if (Run.Campaign == null)
            {
                return;
            }

            RivalDefinition candidate = Run.Campaign.Current;
            bool defeated = Run.Campaign.RegisterResolution(Run.Threats, threat, report.Repelled, Run.Day);

            if (!defeated)
            {
                return;
            }

            Emit(new RivalDefeatedEvent(candidate));

            if (Run.Campaign.AllDefeated)
            {
                Run.DeclareVictory();
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

            ResolvePopulationGrowth();
            Run.UpdateChronicShortageStreaks();

            int handBefore = Run.Deck.Hand.Count;
            int discarded = Run.Deck.DiscardHand();
            Emit(new HandDiscardedEvent(discarded, handBefore - discarded));

            // Energia nao acumula: o dia seguinte recomeca cheio, nunca somado.
            Run.Energy = 0;

            // Defesa temporaria tambem nao acumula. Se durasse ate o proximo ataque,
            // os dias sem combate viravam estoque: com ataque a cada quatro dias o
            // jogador empilhava quatro dias de cartas e a defesa passava a crescer
            // mais rapido que qualquer curva de ameaca.
            Run.ConsumePendingDefense();
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

            if (Run.Outcome == RunOutcome.Victory)
            {
                Emit(new RunVictoryEvent(FinalScore));
            }
            else
            {
                Emit(new RunCollapsedEvent(Run.Collapse, FinalScore));
            }
        }
    }
}

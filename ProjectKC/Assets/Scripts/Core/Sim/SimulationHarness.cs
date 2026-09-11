using System;
using System.Collections.Generic;
using System.Text;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Politica de jogo automatica. Existe para que o balanceamento seja medido em
    /// milhares de runs em vez de adivinhado (design D1 e D5). Nao pretende jogar
    /// bem: pretende jogar de forma consistente, para que mudanca de curva apareca
    /// no numero e nao no humor do jogador de teste.
    /// </summary>
    public interface IRunPolicy
    {
        /// <summary>Age durante o Planejamento de um dia.</summary>
        void PlanDay(RunEngine engine, RunBundle bundle);

        /// <summary>Escolhe a opcao de um evento com escolha.</summary>
        int ChooseEventOption(RunState run, EventDefinition definition);
    }

    /// <summary>
    /// Heuristica simples, mas nao ingenua: joga as cartas que cabem na energia,
    /// constroi, e expande quando sobra ouro.
    ///
    /// A parte que importa e olhar o relogio de ameaca: quando o proximo ataque
    /// supera a defesa atual, o bot passa a priorizar defesa em vez de producao.
    /// Um bot que ignora a ameaca anunciada mede o jogo errado, porque atribui a
    /// curva uma letalidade que na verdade e burrice do proprio bot.
    /// </summary>
    public sealed class GreedyPolicy : IRunPolicy
    {
        public GreedyPolicy(double expansionReserveRatio = 1.15)
        {
            ExpansionReserveRatio = expansionReserveRatio;
        }

        /// <summary>Quantas vezes o custo da celula o bot quer ter antes de expandir.</summary>
        public double ExpansionReserveRatio { get; }

        public void PlanDay(RunEngine engine, RunBundle bundle)
        {
            RunState run = engine.Run;

            RepairWhatWasLost(engine, run);

            ScheduledThreat imminent = ImminentThreat(run);
            bool underThreat = imminent != null && imminent.Force > run.TotalDefense();

            // Rival de eixo Population/Resource nao e anulado por defesa (design D4):
            // erguer torre contra ele e gasto jogado fora. O bot reage guardando o
            // que o rival pressiona em vez de guarnecer.
            bool economicThreat = underThreat && imminent.Identity != null && !imminent.Identity.ResolvedByDefense;
            bool wantsDefense = underThreat && !economicThreat;

            // Postura segue a mesma leitura: guarnece antes do ataque que a defesa
            // atual nao segura, devolve gente a producao assim que nao precisa mais
            // (task 7.1). Ameaca economica nao pede guarnicao — guarnecer nao ajuda.
            run.Stance = wantsDefense ? WorkerStance.DefenseFirst : WorkerStance.ProductionFirst;

            bool foodAtRisk = IsFoodAtRisk(run);

            PlayAffordableCards(engine, run, wantsDefense);
            BuildWhereItFits(engine, bundle, run, wantsDefense, foodAtRisk, economicThreat ? imminent.Identity : null);
            ExpandIfComfortable(engine, run, underThreat);
            BuildWhereItFits(engine, bundle, run, wantsDefense, foodAtRisk, economicThreat ? imminent.Identity : null);
        }

        /// <summary>Dias de antecedencia em que o bot entra em postura defensiva.</summary>
        public const int ImminenceWindow = 2;

        /// <summary>Proxima ameaca dentro da janela de imminencia, ou nula. Olhar so
        /// "a forca supera a defesa algum dia" deixaria o bot em panico permanente,
        /// porque isso quase sempre e verdade em algum ponto do futuro.</summary>
        private static ScheduledThreat ImminentThreat(RunState run)
        {
            ScheduledThreat next = run.Threats.NextThreat(run.Day);
            return next != null && next.DaysUntil(run.Day) <= ImminenceWindow ? next : null;
        }

        /// <summary>
        /// Mesma formula do aviso de fome do motor (RunEngine.WarnAboutFamine): usa a
        /// comida prevista, nao a de hoje, pra reagir antes de passar fome de
        /// verdade (task 7.2).
        /// </summary>
        private static bool IsFoodAtRisk(RunState run)
        {
            int upkeep = run.PredictedFoodUpkeep();
            if (upkeep <= 0)
            {
                return false;
            }

            int predicted = run[ResourceKind.Food] + run.CollectDailyProductionByResource()[ResourceKind.Food];
            return predicted < upkeep;
        }

        private static void PlayAffordableCards(RunEngine engine, RunState run, bool wantsDefense)
        {
            // Copia a mao: jogar carta altera a colecao original.
            List<CardDefinition> hand = new List<CardDefinition>(run.Deck.Hand);

            for (int i = 0; i < hand.Count; i++)
            {
                CardDefinition card = hand[i];
                if (card.EnergyCost > run.Energy)
                {
                    continue;
                }

                // Defesa vale so hoje: gasta-la num dia calmo (ou contra ameaca que
                // defesa nao ajuda) e jogar fora.
                if (!wantsDefense && GivesDefense(card))
                {
                    continue;
                }

                if (!card.NeedsTarget)
                {
                    engine.PlayCard(card);
                    continue;
                }

                List<Coord> targets = TargetRules.ValidTargets(run.Grid, card.Target, run.Rules);
                if (targets.Count > 0)
                {
                    engine.PlayCard(card, targets[0]);
                }
            }
        }

        /// <summary>
        /// Repara celulas arrasadas antes de qualquer outra coisa. Celula arrasada nao
        /// produz nem aceita construcao, entao deixa-la assim trava a run inteira: e o
        /// gasto de ouro com melhor retorno que existe.
        /// </summary>
        private static void RepairWhatWasLost(RunEngine engine, RunState run)
        {
            int cost = run.Grid.CostCurve.RepairCost;

            foreach (Tile tile in run.Grid.OwnedTilesOrdered())
            {
                if (!tile.Destroyed || !run.CanAfford(cost))
                {
                    continue;
                }

                engine.RepairTile(tile.Coord);
            }
        }

        private static void BuildWhereItFits(
            RunEngine engine, RunBundle bundle, RunState run, bool wantsDefense, bool foodAtRisk,
            RivalIdentity pressuringIdentity)
        {
            List<BuildingDefinition> buildings = bundle.AvailableBuildings();
            if (buildings.Count == 0)
            {
                return;
            }

            foreach (Tile tile in run.Grid.OwnedTilesOrdered())
            {
                if (!tile.IsBuildable)
                {
                    continue;
                }

                BuildingDefinition best = null;
                for (int i = 0; i < buildings.Count; i++)
                {
                    BuildingDefinition candidate = buildings[i];
                    if (candidate.Id == RunBuilder.HallBuildingId)
                    {
                        continue;
                    }

                    if (!candidate.CanBuildOn(tile.Terrain) || candidate.GoldCost > run.Gold)
                    {
                        continue;
                    }

                    if (best == null
                        || Value(candidate, wantsDefense, foodAtRisk, pressuringIdentity)
                           > Value(best, wantsDefense, foodAtRisk, pressuringIdentity))
                    {
                        best = candidate;
                    }
                }

                if (best != null)
                {
                    engine.BuildOn(tile.Coord, best);
                }
            }
        }

        /// <summary>Se a carta concede defesa temporaria, que so vale no dia.</summary>
        private static bool GivesDefense(CardDefinition card)
        {
            for (int i = 0; i < card.Effects.Count; i++)
            {
                if (card.Effects[i].Kind == EffectKinds.AddDefense)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Prioridade de construção do bot. Fome iminente vence tudo — população
        /// perdida por descuido é o pior desfecho, e é reversível cedo (task 7.2).
        /// Rival que defesa não anula pesa o recurso pressionado em vez de torre
        /// (task 7.3, design D4). Caso contrário, cai na leitura antiga: defesa sob
        /// ameaça, produção em dia calmo.
        /// </summary>
        private static int Value(
            BuildingDefinition building, bool wantsDefense, bool foodAtRisk, RivalIdentity pressuringIdentity)
        {
            if (foodAtRisk)
            {
                return building.Production[ResourceKind.Food] * 6 + building.BaseGoldProduction + building.Defense;
            }

            if (pressuringIdentity != null)
            {
                ResourceKind pressured = pressuringIdentity.Axis == RivalAxis.Population
                    ? ResourceKind.Food // estocar comida sustenta a populacao pressionada
                    : pressuringIdentity.Resource;

                int stockpile = building.Production[pressured] +
                                (pressuringIdentity.Axis == RivalAxis.Population ? building.PopulationCapacity : 0);

                return stockpile * 5 + building.BaseGoldProduction;
            }

            return wantsDefense
                ? building.Defense * 4 + building.BaseGoldProduction
                : building.BaseGoldProduction * 2 + building.Defense;
        }

        /// <summary>Teto por dia, para uma configuracao degenerada nao girar sem fim.</summary>
        private const int MaxPurchasesPerDay = 12;

        private void ExpandIfComfortable(RunEngine engine, RunState run, bool underThreat)
        {
            double reserve = ExpansionReserveRatio;

            // Sob ameaca, expandir e duplamente ruim: gasta o ouro que compraria
            // defesa e ainda aumenta a forca do proximo ataque (design D6).
            if (underThreat)
            {
                reserve *= 1.6;
            }

            // Compra enquanto sobrar folga, e nao uma celula por dia: o teto de uma
            // compra diaria fazia o ouro empilhar sem que a decisao de expandir
            // aparecesse na medicao.
            for (int guard = 0; guard < MaxPurchasesPerDay; guard++)
            {
                List<Coord> purchasable = run.Grid.PurchasableCoords(run.Rules);
                if (purchasable.Count == 0)
                {
                    return;
                }

                int cost = run.Grid.NextTileCost(run.Rules);
                if (run.Gold < cost * reserve)
                {
                    return;
                }

                if (!engine.BuyTile(purchasable[0]).Ok)
                {
                    return;
                }
            }
        }

        public int ChooseEventOption(RunState run, EventDefinition definition)
        {
            return 0;
        }
    }

    public sealed class SimulationResult
    {
        public SimulationResult(
            int seed,
            int collapseDay,
            CollapseReason reason,
            RunOutcome outcome,
            RunScore score,
            int tilesOwned,
            int peakDefense,
            int buildingsBuilt,
            int goldAtEnd,
            int cardsPlayed,
            int woodAtEnd,
            int stoneAtEnd,
            int peakIdleBuildings,
            int peakUnassignedPopulation,
            bool foodEverNearLimit)
        {
            Seed = seed;
            CollapseDay = collapseDay;
            Reason = reason;
            Outcome = outcome;
            Score = score;
            TilesOwned = tilesOwned;
            PeakDefense = peakDefense;
            BuildingsBuilt = buildingsBuilt;
            GoldAtEnd = goldAtEnd;
            CardsPlayed = cardsPlayed;
            WoodAtEnd = woodAtEnd;
            StoneAtEnd = stoneAtEnd;
            PeakIdleBuildings = peakIdleBuildings;
            PeakUnassignedPopulation = peakUnassignedPopulation;
            FoodEverNearLimit = foodEverNearLimit;
        }

        public int WoodAtEnd { get; }

        public int StoneAtEnd { get; }

        /// <summary>Maior contagem de edificio sem gente que a run chegou a ter.</summary>
        public int PeakIdleBuildings { get; }

        /// <summary>Maior populacao sem posto de trabalho que a run chegou a ter.</summary>
        public int PeakUnassignedPopulation { get; }

        /// <summary>A comida mal cobriu o consumo do dia em algum momento da run.</summary>
        public bool FoodEverNearLimit { get; }

        public int Seed { get; }

        public int CollapseDay { get; }

        public CollapseReason Reason { get; }

        /// <summary>Vitoria ou Colapso — a metrica alvo do relatorio olha isto, nao
        /// so o dia (design D7).</summary>
        public RunOutcome Outcome { get; }

        public RunScore Score { get; }

        public int TilesOwned { get; }

        /// <summary>Maior defesa que a run chegou a ter. Constante em todas as runs
        /// significa que o jogador nao tem como responder a ameaca.</summary>
        public int PeakDefense { get; }

        public int BuildingsBuilt { get; }

        /// <summary>Ouro parado no fim. Muito ouro sobrando indica que nao havia no
        /// que gastar, e nao que o jogador foi economico.</summary>
        public int GoldAtEnd { get; }

        public int CardsPlayed { get; }
    }

    /// <summary>
    /// Distribuicao do dia de Colapso. E o relatorio que a tarefa de balanceamento
    /// le para decidir se a curva esta no alvo de 25 a 35 dias.
    /// </summary>
    public sealed class SimulationReport
    {
        public SimulationReport(List<SimulationResult> results)
        {
            Results = results ?? new List<SimulationResult>();

            List<int> days = new List<int>(Results.Count);
            for (int i = 0; i < Results.Count; i++)
            {
                days.Add(Results[i].CollapseDay);
            }

            days.Sort();
            SortedDays = days;
        }

        public List<SimulationResult> Results { get; }

        public List<int> SortedDays { get; }

        public int Count => SortedDays.Count;

        public int Min => Count == 0 ? 0 : SortedDays[0];

        public int Max => Count == 0 ? 0 : SortedDays[Count - 1];

        public double Mean
        {
            get
            {
                if (Count == 0)
                {
                    return 0;
                }

                long sum = 0;
                for (int i = 0; i < SortedDays.Count; i++)
                {
                    sum += SortedDays[i];
                }

                return sum / (double)Count;
            }
        }

        public int Median => Percentile(0.50);

        public int Percentile(double fraction)
        {
            if (Count == 0)
            {
                return 0;
            }

            int index = (int)Math.Floor(fraction * (Count - 1));
            return SortedDays[Math.Max(0, Math.Min(Count - 1, index))];
        }

        /// <summary>
        /// Fracao de runs que venceram a campanha — a metrica alvo depois desta
        /// mudanca (design D7). "Dia mediano de Colapso" media a coisa errada num
        /// jogo com Vitoria: dois dias-26 podem ser um triunfo e uma derrota.
        /// </summary>
        public double VictoryFraction
        {
            get
            {
                if (Count == 0)
                {
                    return 0;
                }

                int victories = 0;
                for (int i = 0; i < Results.Count; i++)
                {
                    if (Results[i].Outcome == RunOutcome.Victory)
                    {
                        victories++;
                    }
                }

                return victories / (double)Count;
            }
        }

        /// <summary>Mediana de dias entre as runs vencidas. Zero quando nenhuma venceu.</summary>
        public int MedianVictoryDuration()
        {
            List<int> days = new List<int>();
            for (int i = 0; i < Results.Count; i++)
            {
                if (Results[i].Outcome == RunOutcome.Victory)
                {
                    days.Add(Results[i].CollapseDay);
                }
            }

            if (days.Count == 0)
            {
                return 0;
            }

            days.Sort();
            return days[days.Count / 2];
        }

        public int CountByReason(CollapseReason reason)
        {
            int count = 0;
            for (int i = 0; i < Results.Count; i++)
            {
                if (Results[i].Reason == reason)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Quantas runs terminaram dentro da janela alvo da spec.</summary>
        public double FractionWithin(int minDay, int maxDay)
        {
            if (Count == 0)
            {
                return 0;
            }

            int inside = 0;
            for (int i = 0; i < SortedDays.Count; i++)
            {
                if (SortedDays[i] >= minDay && SortedDays[i] <= maxDay)
                {
                    inside++;
                }
            }

            return inside / (double)Count;
        }

        /// <summary>
        /// Sinais de que o problema nao e de curva, e sim de conteudo faltando.
        /// Ajustar numero num jogo sem alavanca so produz um jogo sem alavanca com
        /// numeros diferentes, entao estes avisos vem antes do veredito de duracao.
        /// </summary>
        public List<string> Diagnose()
        {
            List<string> warnings = new List<string>();

            if (Count == 0)
            {
                return warnings;
            }

            int minDefense = int.MaxValue;
            int maxDefense = 0;
            int minTiles = int.MaxValue;
            int maxTiles = 0;
            int totalBuildings = 0;
            int totalCards = 0;
            long totalGoldLeft = 0;
            int minWood = int.MaxValue;
            int maxWood = 0;
            int minStone = int.MaxValue;
            int maxStone = 0;
            long totalIdleBuildings = 0;
            long totalUnassignedPopulation = 0;
            int foodNearLimitRuns = 0;

            for (int i = 0; i < Results.Count; i++)
            {
                SimulationResult result = Results[i];
                minDefense = Math.Min(minDefense, result.PeakDefense);
                maxDefense = Math.Max(maxDefense, result.PeakDefense);
                minTiles = Math.Min(minTiles, result.TilesOwned);
                maxTiles = Math.Max(maxTiles, result.TilesOwned);
                totalBuildings += result.BuildingsBuilt;
                totalCards += result.CardsPlayed;
                totalGoldLeft += result.GoldAtEnd;
                minWood = Math.Min(minWood, result.WoodAtEnd);
                maxWood = Math.Max(maxWood, result.WoodAtEnd);
                minStone = Math.Min(minStone, result.StoneAtEnd);
                maxStone = Math.Max(maxStone, result.StoneAtEnd);
                totalIdleBuildings += result.PeakIdleBuildings;
                totalUnassignedPopulation += result.PeakUnassignedPopulation;
                if (result.FoodEverNearLimit)
                {
                    foodNearLimitRuns++;
                }
            }

            if (minDefense == maxDefense)
            {
                warnings.Add(
                    "DEFESA CONSTANTE (" + maxDefense + " em todas as runs): o jogador nao tem " +
                    "como responder a ameaca. Falta edificio ou carta que some defesa. " +
                    "Enquanto isso, o dia do Colapso e aritmetica, nao decisao.");
            }

            if (minTiles == maxTiles)
            {
                warnings.Add(
                    "TERRITORIO CONSTANTE (" + maxTiles + " celulas): a expansao nao esta " +
                    "acontecendo. Ou o custo cresce rapido demais para a renda, ou nao ha " +
                    "o que construir nas celulas compradas.");
            }

            if (Min == Max)
            {
                warnings.Add(
                    "ZERO VARIACAO entre " + Count + " sementes: nada no jogo depende de " +
                    "sorteio nem de escolha. Calibrar curva aqui nao muda a natureza do " +
                    "problema.");
            }

            double buildingsPerRun = totalBuildings / (double)Count;
            if (buildingsPerRun < 1.0)
            {
                warnings.Add(
                    "POUCA CONSTRUCAO (" + buildingsPerRun.ToString("0.0") + " por run): as " +
                    "celulas compradas nao aceitam nenhum edificio disponivel. Confira se " +
                    "todos os terrenos tem ao menos um edificio.");
            }

            if (totalCards == 0)
            {
                warnings.Add(
                    "NENHUMA CARTA JOGADA: sem cartas no pool, o jogador so tem comprar e " +
                    "construir. A economia de acoes do dia esta inerte.");
            }

            double goldPerRun = totalGoldLeft / (double)Count;
            if (goldPerRun > 30)
            {
                warnings.Add(
                    "OURO PARADO (" + goldPerRun.ToString("0") + " em media ao morrer): sobra " +
                    "dinheiro sem ter no que gastar. Falta destino de ouro, nao ouro.");
            }

            if (minWood == maxWood)
            {
                warnings.Add(
                    "MADEIRA PARADA (" + maxWood + " em todas as runs): nenhum edificio ou carta " +
                    "disponivel produz ou consome madeira. O recurso existe no dado, mas nao no jogo.");
            }

            if (minStone == maxStone)
            {
                warnings.Add(
                    "PEDRA PARADA (" + maxStone + " em todas as runs): nenhum edificio ou carta " +
                    "disponivel produz ou consome pedra. O recurso existe no dado, mas nao no jogo.");
            }

            double idleBuildingsPerRun = totalIdleBuildings / (double)Count;
            if (idleBuildingsPerRun > 0.5)
            {
                warnings.Add(
                    "EDIFICIO SEM GENTE (" + idleBuildingsPerRun.ToString("0.0") + " por run em " +
                    "media, no pico): populacao nao cobre os postos que o bot construiu. Falta " +
                    "crescimento de populacao, ou sobra edificio.");
            }

            double unassignedPerRun = totalUnassignedPopulation / (double)Count;
            if (unassignedPerRun > 1.0)
            {
                warnings.Add(
                    "POPULACAO OCIOSA (" + unassignedPerRun.ToString("0.0") + " sem posto em " +
                    "media, no pico): tem gente parada sem trabalho nem guarnicao disponivel.");
            }

            double foodNearLimitFraction = foodNearLimitRuns / (double)Count;
            if (foodNearLimitFraction > 0.5)
            {
                warnings.Add(
                    "COMIDA NO LIMITE (" + (foodNearLimitFraction * 100).ToString("0") + "% das " +
                    "runs chegaram a ter estoque mal cobrindo o consumo do dia): a populacao " +
                    "arrisca cair por pouco, mesmo sem fome declarada.");
            }

            return warnings;
        }

        public string Describe()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Runs simuladas: " + Count);
            builder.AppendLine("Vitorias: " + (VictoryFraction * 100).ToString("0.0") + "%" +
                               (VictoryFraction > 0 ? "  (mediana " + MedianVictoryDuration() + " dias)" : string.Empty));
            builder.AppendLine("Dia de Colapso  min=" + Min + "  p25=" + Percentile(0.25) +
                               "  mediana=" + Median + "  p75=" + Percentile(0.75) + "  max=" + Max);
            builder.AppendLine("Media: " + Mean.ToString("0.0"));
            builder.AppendLine("Dentro do alvo 25-35: " + (FractionWithin(25, 35) * 100).ToString("0.0") + "%");
            builder.AppendLine("Por integridade: " + CountByReason(CollapseReason.IntegrityLost) +
                               "  por territorio: " + CountByReason(CollapseReason.TerritoryLost));

            List<string> warnings = Diagnose();
            if (warnings.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("DIAGNOSTICO ESTRUTURAL:");
                for (int i = 0; i < warnings.Count; i++)
                {
                    builder.AppendLine("  * " + warnings[i]);
                }
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Roda muitas runs sem abrir o Editor. E a razao pratica do Core ser puro:
    /// balancear um roguelite a mao nao escala.
    /// </summary>
    public static class SimulationHarness
    {
        /// <summary>Teto de dias, para que uma configuracao quebrada nao rode para sempre.</summary>
        public const int MaxDays = 500;

        public static SimulationResult RunOnce(
            RunSetup setup,
            ContentCatalog catalog,
            IRunPolicy policy = null,
            MetaTree tree = null,
            MetaProfile profile = null)
        {
            IRunPolicy chosen = policy ?? new GreedyPolicy();
            RunBundle bundle = RunBuilder.Build(setup, catalog, tree, profile);
            RunEngine engine = bundle.Engine;
            engine.StartRun();

            int peakDefense = bundle.Run.TotalDefense();
            int cardsPlayed = 0;
            int peakIdleBuildings = 0;
            int peakUnassignedPopulation = 0;
            bool foodEverNearLimit = false;

            while (!bundle.Run.IsOver && bundle.Run.Day <= MaxDays)
            {
                int handBefore = bundle.Run.Deck.Hand.Count;
                chosen.PlanDay(engine, bundle);
                cardsPlayed += Math.Max(0, handBefore - bundle.Run.Deck.Hand.Count);

                int defense = bundle.Run.TotalDefense();
                if (defense > peakDefense)
                {
                    peakDefense = defense;
                }

                WorkerAllocation allocation = bundle.Run.Allocation();
                peakIdleBuildings = Math.Max(peakIdleBuildings, allocation.IdleBuildings);
                peakUnassignedPopulation = Math.Max(peakUnassignedPopulation, allocation.Unassigned);

                int upkeep = bundle.Run.DailyFoodUpkeep();
                if (upkeep > 0 && bundle.Run[ResourceKind.Food] <= upkeep)
                {
                    foodEverNearLimit = true;
                }

                // Descarta os eventos de dominio: a simulacao nao desenha nada, e
                // acumular a lista por 500 dias so gasta memoria.
                engine.DrainEvents();

                EventDefinition pending = engine.PendingDayEvent;
                int option = pending == null ? 0 : chosen.ChooseEventOption(bundle.Run, pending);
                engine.EndDay(option);
            }

            RunScore score = engine.FinalScore ?? ScoreCalculator.Calculate(bundle.Run);
            return new SimulationResult(
                setup.Seed,
                bundle.Run.Stats.DaysSurvived,
                bundle.Run.Collapse,
                bundle.Run.Outcome,
                score,
                bundle.Run.Grid.OwnedCount,
                peakDefense,
                bundle.Run.Stats.BuildingsBuilt,
                bundle.Run.Gold,
                cardsPlayed,
                bundle.Run[ResourceKind.Wood],
                bundle.Run[ResourceKind.Stone],
                peakIdleBuildings,
                peakUnassignedPopulation,
                foodEverNearLimit);
        }

        public static SimulationReport RunBatch(
            int runs,
            Func<int, RunSetup> setupFactory,
            ContentCatalog catalog,
            IRunPolicy policy = null,
            MetaTree tree = null,
            MetaProfile profile = null)
        {
            List<SimulationResult> results = new List<SimulationResult>(runs);

            for (int seed = 0; seed < runs; seed++)
            {
                results.Add(RunOnce(setupFactory(seed), catalog, policy, tree, profile));
            }

            return new SimulationReport(results);
        }
    }
}

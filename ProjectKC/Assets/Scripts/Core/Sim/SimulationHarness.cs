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
    /// Heuristica simples: guarda uma reserva de ouro, constroi onde o terreno
    /// permite, expande quando sobra, e joga as cartas que couberem na energia.
    /// </summary>
    public sealed class GreedyPolicy : IRunPolicy
    {
        public GreedyPolicy(double expansionReserveRatio = 1.5)
        {
            ExpansionReserveRatio = expansionReserveRatio;
        }

        /// <summary>Quantas vezes o custo da celula o bot quer ter antes de expandir.</summary>
        public double ExpansionReserveRatio { get; }

        public void PlanDay(RunEngine engine, RunBundle bundle)
        {
            RunState run = engine.Run;

            PlayAffordableCards(engine, run);
            BuildWhereItFits(engine, bundle, run);
            ExpandIfComfortable(engine, run);
            BuildWhereItFits(engine, bundle, run);
        }

        private static void PlayAffordableCards(RunEngine engine, RunState run)
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

        private static void BuildWhereItFits(RunEngine engine, RunBundle bundle, RunState run)
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

                    if (best == null || Value(candidate) > Value(best))
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

        /// <summary>Producao mais defesa, para o bot nao ignorar torres.</summary>
        private static int Value(BuildingDefinition building)
        {
            return building.BaseGoldProduction * 2 + building.Defense;
        }

        private void ExpandIfComfortable(RunEngine engine, RunState run)
        {
            List<Coord> purchasable = run.Grid.PurchasableCoords(run.Rules);
            if (purchasable.Count == 0)
            {
                return;
            }

            int cost = run.Grid.NextTileCost(run.Rules);
            if (run.Gold < cost * ExpansionReserveRatio)
            {
                return;
            }

            engine.BuyTile(purchasable[0]);
        }

        public int ChooseEventOption(RunState run, EventDefinition definition)
        {
            return 0;
        }
    }

    public sealed class SimulationResult
    {
        public SimulationResult(int seed, int collapseDay, CollapseReason reason, RunScore score, int tilesOwned)
        {
            Seed = seed;
            CollapseDay = collapseDay;
            Reason = reason;
            Score = score;
            TilesOwned = tilesOwned;
        }

        public int Seed { get; }

        public int CollapseDay { get; }

        public CollapseReason Reason { get; }

        public RunScore Score { get; }

        public int TilesOwned { get; }
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

        public string Describe()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Runs simuladas: " + Count);
            builder.AppendLine("Dia de Colapso  min=" + Min + "  p25=" + Percentile(0.25) +
                               "  mediana=" + Median + "  p75=" + Percentile(0.75) + "  max=" + Max);
            builder.AppendLine("Media: " + Mean.ToString("0.0"));
            builder.AppendLine("Dentro do alvo 25-35: " + (FractionWithin(25, 35) * 100).ToString("0.0") + "%");
            builder.AppendLine("Por integridade: " + CountByReason(CollapseReason.IntegrityLost) +
                               "  por territorio: " + CountByReason(CollapseReason.TerritoryLost));
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

            while (!bundle.Run.IsOver && bundle.Run.Day <= MaxDays)
            {
                chosen.PlanDay(engine, bundle);

                // Descarta os eventos de dominio: a simulacao nao desenha nada, e
                // acumular a lista por 500 dias so gasta memoria.
                engine.DrainEvents();

                EventDefinition pending = engine.PendingDayEvent;
                int option = pending == null ? 0 : chosen.ChooseEventOption(bundle.Run, pending);
                engine.EndDay(option);
            }

            RunScore score = engine.FinalScore ?? ScoreCalculator.Calculate(bundle.Run);
            return new SimulationResult(
                setup.Seed, bundle.Run.Stats.DaysSurvived, bundle.Run.Collapse, score, bundle.Run.Grid.OwnedCount);
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

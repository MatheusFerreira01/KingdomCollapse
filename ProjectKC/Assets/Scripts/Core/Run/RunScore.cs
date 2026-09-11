using System;
using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    public readonly struct ScoreLine
    {
        public ScoreLine(string label, int quantity, int pointsEach, int points)
        {
            Label = label;
            Quantity = quantity;
            PointsEach = pointsEach;
            Points = points;
        }

        public string Label { get; }

        public int Quantity { get; }

        public int PointsEach { get; }

        public int Points { get; }

        public override string ToString() => Label + " x" + Quantity + " = " + Points;
    }

    /// <summary>
    /// Placar de Colapso. Discriminado por exigencia da spec: o jogador precisa ver
    /// de onde veio cada ponto, ou o fim de run vira uma tela de derrota com um
    /// numero solto.
    /// </summary>
    public sealed class RunScore
    {
        private readonly List<ScoreLine> _lines = new List<ScoreLine>();

        public IReadOnlyList<ScoreLine> Lines => _lines;

        public int TotalPoints { get; private set; }

        public int MetaCurrency { get; internal set; }

        public CollapseReason Reason { get; internal set; }

        public RunOutcome Outcome { get; internal set; }

        internal void Add(string label, int quantity, int pointsEach)
        {
            if (quantity <= 0 || pointsEach == 0)
            {
                return;
            }

            int points = quantity * pointsEach;
            _lines.Add(new ScoreLine(label, quantity, pointsEach, points));
            TotalPoints += points;
        }

        public override string ToString() => TotalPoints + " pts / " + MetaCurrency + " moedas";
    }

    public static class ScoreCalculator
    {
        public const int PointsPerDay = 10;
        public const int PointsPerTile = 6;
        public const int PointsPerBuilding = 8;
        public const int PointsPerAttackSurvived = 15;
        public const int PointsPerMilestone = 25;

        /// <summary>
        /// Bonus de vencer a campanha. Grande o bastante pra qualquer Vitoria render
        /// mais moeda de meta que um Colapso com o mesmo reino construido (spec
        /// rival-kingdoms — "Vitoria rende mais que Colapso").
        /// </summary>
        public const int PointsPerVictory = 150;

        /// <summary>Quantos pontos valem uma moeda de meta.</summary>
        public const int PointsPerMetaCoin = 20;

        /// <summary>
        /// Piso de moeda. Existe porque a spec exige que uma run curta ainda renda
        /// algo: sem isso, morrer cedo nao progride e o jogador so perde tempo.
        /// </summary>
        public const int MinimumMetaCoins = 3;

        public static RunScore Calculate(RunState run)
        {
            RunScore score = new RunScore { Reason = run.Collapse, Outcome = run.Outcome };

            int standingTiles = run.StandingTileCount();
            int buildings = CountBuildings(run.Grid);

            score.Add("Dias sobrevividos", run.Stats.DaysSurvived, PointsPerDay);
            score.Add("Celulas de pe", standingTiles, PointsPerTile);
            score.Add("Edificios erguidos", buildings, PointsPerBuilding);
            score.Add("Ataques repelidos", run.Stats.AttacksSurvived, PointsPerAttackSurvived);
            score.Add("Marcos alcancados", run.Stats.MilestonesReached, PointsPerMilestone);

            if (run.Outcome == RunOutcome.Victory)
            {
                score.Add("Vitoria da campanha", 1, PointsPerVictory);
            }

            score.MetaCurrency = Math.Max(
                MinimumMetaCoins,
                (int)Math.Floor(score.TotalPoints / (double)PointsPerMetaCoin));

            return score;
        }

        private static int CountBuildings(KingdomGrid grid)
        {
            int count = 0;
            foreach (Tile tile in grid.OwnedTiles())
            {
                if (!tile.Destroyed && tile.HasBuilding)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>Marco alcancavel durante a run. Da retorno de progresso antes do Colapso.</summary>
    public sealed class Milestone
    {
        public Milestone(string id, string label, Func<RunState, bool> predicate)
        {
            Id = id;
            Label = label;
            Predicate = predicate;
        }

        public string Id { get; }

        public string Label { get; }

        public Func<RunState, bool> Predicate { get; }

        public static List<Milestone> Default()
        {
            return new List<Milestone>
            {
                new Milestone("day_10", "Sobreviveu ao dia 10", run => run.Day >= 10),
                new Milestone("day_20", "Sobreviveu ao dia 20", run => run.Day >= 20),
                new Milestone("day_30", "Sobreviveu ao dia 30", run => run.Day >= 30),
                new Milestone("tiles_8", "Territorio de 8 celulas", run => run.Grid.OwnedCount >= 8),
                new Milestone("tiles_16", "Territorio de 16 celulas", run => run.Grid.OwnedCount >= 16),
                new Milestone("first_repel", "Repeliu um ataque", run => run.Stats.AttacksSurvived >= 1)
            };
        }
    }
}

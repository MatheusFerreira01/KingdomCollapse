using System.Collections.Generic;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    [TestFixture]
    public class DayLoopTests
    {
        private static List<string> PhaseSequence(List<RunEvent> events)
        {
            List<string> phases = new List<string>();
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i] is PhaseChangedEvent phase)
                {
                    phases.Add(phase.Phase.ToString());
                }
            }

            return phases;
        }

        [Test]
        public void RunComeca_NoDia1ComUmaCelulaEValoresDaRaca()
        {
            RaceDefinition race = TestContent.Humans(gold: 77, integrity: 13, handSize: 4, energy: 2);
            RunBundle bundle = TestContent.Run(race);

            Assert.That(bundle.Run.Day, Is.EqualTo(1));
            Assert.That(bundle.Run.Grid.OwnedCount, Is.EqualTo(1));
            Assert.That(bundle.Run.Gold, Is.EqualTo(77));
            Assert.That(bundle.Run.Integrity, Is.EqualTo(13));
            Assert.That(bundle.Run.HandSize, Is.EqualTo(4));
            Assert.That(bundle.Run.EnergyPerDay, Is.EqualTo(2));
        }

        [Test]
        public void FasesAvancamNaOrdemEspecificada()
        {
            RunBundle bundle = TestContent.Run();
            bundle.Engine.DrainEvents();

            bundle.Engine.EndDay();
            List<string> phases = PhaseSequence(bundle.Engine.DrainEvents());

            Assert.That(phases, Is.EqualTo(new List<string>
            {
                "Resolution", "Event", "DayEnd", "DayStart", "Planning"
            }));
        }

        [Test]
        public void JogadorSoAgeNoPlanejamento()
        {
            RunBundle bundle = TestContent.Run();
            Assert.That(bundle.Run.Phase, Is.EqualTo(DayPhase.Planning));

            bundle.Run.Phase = DayPhase.Resolution;
            int goldBefore = bundle.Run.Gold;
            int tilesBefore = bundle.Run.Grid.OwnedCount;

            CommandResult buy = bundle.Engine.BuyTile(new Coord(1, 0));
            CommandResult build = bundle.Engine.BuildOn(Coord.Zero, TestContent.Farm());
            CommandResult end = bundle.Engine.EndDay();

            Assert.That(buy.Rejection, Is.EqualTo(CommandRejection.WrongPhase));
            Assert.That(build.Rejection, Is.EqualTo(CommandRejection.WrongPhase));
            Assert.That(end.Rejection, Is.EqualTo(CommandRejection.WrongPhase));
            Assert.That(bundle.Run.Gold, Is.EqualTo(goldBefore));
            Assert.That(bundle.Run.Grid.OwnedCount, Is.EqualTo(tilesBefore));
        }

        [Test]
        public void CompraSemOuro_ERejeitadaSemAlterarEstado()
        {
            RaceDefinition poor = TestContent.Humans(gold: 0);
            RunBundle bundle = TestContent.Run(poor);

            CommandResult result = bundle.Engine.BuyTile(new Coord(1, 0));

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.NotEnoughGold));
            Assert.That(bundle.Run.Gold, Is.Zero);
            Assert.That(bundle.Run.Grid.OwnedCount, Is.EqualTo(1));
        }

        [Test]
        public void CompraValida_DebitaOuroEAumentaTerritorio()
        {
            RunBundle bundle = TestContent.Run();
            int cost = bundle.Run.Grid.NextTileCost(bundle.Run.Rules);
            int goldBefore = bundle.Run.Gold;

            CommandResult result = bundle.Engine.BuyTile(new Coord(1, 0));

            Assert.That(result.Ok, Is.True);
            Assert.That(bundle.Run.Gold, Is.EqualTo(goldBefore - cost));
            Assert.That(bundle.Run.Grid.OwnedCount, Is.EqualTo(2));
        }

        [Test]
        public void ProducaoEhCreditadaAntesDoAtaque()
        {
            // O Salao produz e a horda chega no mesmo dia: o ouro tem que entrar antes.
            RunBundle bundle = TestContent.Run(firstThreatDay: 3);
            bundle.Engine.DrainEvents();

            int productionIndex = -1;
            int attackIndex = -1;

            for (int day = 0; day < 5 && !bundle.Run.IsOver; day++)
            {
                bundle.Engine.EndDay();
                List<RunEvent> events = bundle.Engine.DrainEvents();

                for (int i = 0; i < events.Count; i++)
                {
                    if (events[i] is ProductionCollectedEvent && productionIndex < 0)
                    {
                        productionIndex = i;
                    }

                    if (events[i] is AttackResolvedEvent && attackIndex < 0)
                    {
                        attackIndex = i;
                        break;
                    }
                }

                if (attackIndex >= 0)
                {
                    break;
                }

                productionIndex = -1;
            }

            Assert.That(attackIndex, Is.GreaterThanOrEqualTo(0), "nenhum ataque aconteceu na janela testada");
            Assert.That(productionIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(productionIndex, Is.LessThan(attackIndex), "producao deve preceder a ameaca");
        }

        [Test]
        public void IntegridadeZerada_EncerraARunImediatamente()
        {
            RunBundle bundle = TestContent.Run();
            bundle.Run.Damage(bundle.Run.Integrity);

            bool collapsed = bundle.Run.CheckCollapse();

            Assert.That(collapsed, Is.True);
            Assert.That(bundle.Run.IsOver, Is.True);
            Assert.That(bundle.Run.Collapse, Is.EqualTo(CollapseReason.IntegrityLost));
        }

        [Test]
        public void PerdaDaUltimaCelulaDePe_EncerraARun()
        {
            RunBundle bundle = TestContent.Run();
            bundle.Run.Grid.Destroy(Coord.Zero);

            bool collapsed = bundle.Run.CheckCollapse();

            Assert.That(collapsed, Is.True);
            Assert.That(bundle.Run.Collapse, Is.EqualTo(CollapseReason.TerritoryLost));
        }

        [Test]
        public void ComandosSaoRecusadosDepoisDoColapso()
        {
            RunBundle bundle = TestContent.Run();
            bundle.Run.Damage(bundle.Run.Integrity);
            bundle.Run.CheckCollapse();

            CommandResult result = bundle.Engine.BuyTile(new Coord(1, 0));

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.RunOver));
        }

        [Test]
        public void ColapsoEmiteEventoDeDominioComPlacar()
        {
            // Raca fragil e ameacas desde cedo: a run precisa colapsar dentro do teste.
            RaceDefinition fragile = TestContent.Humans(gold: 0, integrity: 3);
            RunBundle bundle = TestContent.Run(fragile, firstThreatDay: 3);
            bundle.Engine.DrainEvents();

            for (int i = 0; i < 60 && !bundle.Run.IsOver; i++)
            {
                bundle.Engine.EndDay();
            }

            Assert.That(bundle.Run.IsOver, Is.True, "a run deveria ter colapsado dentro de 60 dias");
            Assert.That(bundle.Engine.FinalScore, Is.Not.Null);
            Assert.That(bundle.Engine.FinalScore.Lines.Count, Is.GreaterThan(0));
        }

        [Test]
        public void PlacarDiscriminaCadaComponente()
        {
            RunBundle bundle = TestContent.Run();
            bundle.Run.Stats.DaysSurvived = 12;
            bundle.Run.Stats.AttacksSurvived = 2;
            bundle.Run.Stats.MilestonesReached = 1;

            RunScore score = ScoreCalculator.Calculate(bundle.Run);

            int sum = 0;
            for (int i = 0; i < score.Lines.Count; i++)
            {
                sum += score.Lines[i].Points;
            }

            Assert.That(sum, Is.EqualTo(score.TotalPoints));
            Assert.That(score.Lines.Count, Is.GreaterThanOrEqualTo(4));
        }

        [Test]
        public void RunMaisLonga_PontuaMais()
        {
            RunBundle shortRun = TestContent.Run();
            RunBundle longRun = TestContent.Run();
            shortRun.Run.Stats.DaysSurvived = 8;
            longRun.Run.Stats.DaysSurvived = 24;

            RunScore shortScore = ScoreCalculator.Calculate(shortRun.Run);
            RunScore longScore = ScoreCalculator.Calculate(longRun.Run);

            Assert.That(longScore.TotalPoints, Is.GreaterThan(shortScore.TotalPoints));
            Assert.That(longScore.MetaCurrency, Is.GreaterThan(shortScore.MetaCurrency));
        }

        [Test]
        public void RunCurta_AindaRendeMoedaDeMeta()
        {
            RunBundle bundle = TestContent.Run();
            bundle.Run.Stats.DaysSurvived = 3;

            RunScore score = ScoreCalculator.Calculate(bundle.Run);

            Assert.That(score.MetaCurrency, Is.GreaterThan(0));
        }

        [Test]
        public void DiaCompleto_EmiteASequenciaEsperadaDeEventos()
        {
            RunBundle bundle = TestContent.Run();
            bundle.Engine.DrainEvents();

            bundle.Engine.EndDay();
            List<RunEvent> events = bundle.Engine.DrainEvents();

            List<string> kinds = new List<string>();
            for (int i = 0; i < events.Count; i++)
            {
                kinds.Add(events[i].Kind);
            }

            Assert.That(kinds, Contains.Item("production_collected"));
            Assert.That(kinds, Contains.Item("hand_discarded"));
            Assert.That(kinds, Contains.Item("day_started"));
            Assert.That(kinds.IndexOf("production_collected"), Is.LessThan(kinds.IndexOf("hand_discarded")));
        }

        [Test]
        public void MarcoAlcancado_EmiteEventoUmaVezSo()
        {
            RunBundle bundle = TestContent.Run();

            for (int i = 0; i < 12 && !bundle.Run.IsOver; i++)
            {
                bundle.Engine.EndDay();
            }

            List<RunEvent> all = bundle.Engine.DrainEvents();
            int dayTenMilestones = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] is MilestoneReachedEvent milestone && milestone.MilestoneId == "day_10")
                {
                    dayTenMilestones++;
                }
            }

            Assert.That(bundle.Run.Stats.MilestonesReached, Is.GreaterThan(0));
            Assert.That(dayTenMilestones, Is.LessThanOrEqualTo(1));
        }

        /// <summary>
        /// Falencia cronica colapsa a run (task 11.2/11.3): sem gastar nem ganhar
        /// ouro nenhum, dias seguidos com ouro zerado precisam terminar a partida —
        /// mesma logica da fome cronica, so que para a economia geral.
        /// </summary>
        [Test]
        public void FalenciaCronicaEncerraARunPorColapso()
        {
            // Salao sem producao nenhuma: com o Hall padrao de TestContent (2 de
            // ouro/dia) o ouro nunca ficaria zerado tempo suficiente pra testar isto.
            RaceDefinition race = TestContent.Humans(gold: 0);
            ContentCatalog catalog = new ContentCatalog();
            catalog.AddBuilding(
                new BuildingDefinition("hall", "Salao do Reino", new List<TerrainType>(), 0, 0, 1), true);
            catalog.AddRace(race, true);

            RunBundle bundle = TestContent.Run(race, catalog);

            for (int i = 0; i < 20 && !bundle.Run.IsOver; i++)
            {
                bundle.Engine.EndDay();
            }

            Assert.That(bundle.Run.IsOver, Is.True, "falencia cronica precisa encerrar a run");
            Assert.That(bundle.Run.Collapse, Is.EqualTo(CollapseReason.Bankruptcy));
        }

        [Test]
        public void ConstruirDebitaOuroEContaNasEstatisticas()
        {
            RunBundle bundle = TestContent.Run();
            bundle.Engine.BuyTile(new Coord(1, 0));
            bundle.Run.Grid.SetTerrain(new Coord(1, 0), TerrainType.Plain);
            int goldBefore = bundle.Run.Gold;

            CommandResult result = bundle.Engine.BuildOn(new Coord(1, 0), TestContent.Farm());

            Assert.That(result.Ok, Is.True);
            Assert.That(bundle.Run.Gold, Is.EqualTo(goldBefore - TestContent.Farm().GoldCost));
            Assert.That(bundle.Run.Stats.BuildingsBuilt, Is.EqualTo(1));
        }
    }
}

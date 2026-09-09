using System.Collections.Generic;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    /// <summary>
    /// A encenação é uma leitura do que já aconteceu, nunca uma decisão. Estes testes
    /// travam as duas garantias que fazem isso valer: a ordem apresentada é a ordem
    /// resolvida, e encenar não muda o resultado da run (spec game-feel).
    /// </summary>
    [TestFixture]
    public class DayPresentationTests
    {
        private static RunBundle Run(int seed = 99, int firstThreatDay = 3)
        {
            RaceDefinition race = new RaceDefinition(
                "humans", "Humanos", 60, 20, 5, 3,
                new List<CardDefinition>(), RuleModifiers.None, null, null, null,
                ResourceAmounts.Of((ResourceKind.Population, 4), (ResourceKind.Food, 30)));

            ContentCatalog catalog = TestContent.Catalog(race);
            catalog.AddWeather(new WeatherDefinition("calm", "Dia limpo", null, isCalmDay: true));

            RunBundle bundle = RunBuilder.Build(
                new RunSetup(race.Id, seed) { FirstThreatDay = firstThreatDay }, catalog);
            bundle.Engine.StartRun();
            return bundle;
        }

        private static List<DayStep> ResolveOneDay(RunBundle bundle)
        {
            bundle.Engine.DrainEvents();
            bundle.Engine.EndDay();
            return DayPresentation.Build(bundle.Engine.DrainEvents());
        }

        [Test]
        public void AOrdemApresentadaEAOrdemResolvida()
        {
            RunBundle bundle = Run();
            List<DayStep> steps = ResolveOneDay(bundle);

            int production = steps.FindIndex(s => s.Kind == DayStepKind.Production);
            int food = steps.FindIndex(s => s.Kind == DayStepKind.Food);

            Assert.That(production, Is.GreaterThanOrEqualTo(0));
            Assert.That(food, Is.GreaterThan(production),
                "o consumo precisa aparecer depois da producao, que e a ordem em que o nucleo resolve");
        }

        [Test]
        public void NenhumResultadoAparecAntesDaSuaCausa()
        {
            RunBundle bundle = Run(seed: 7, firstThreatDay: 3);

            for (int day = 0; day < 6 && !bundle.Run.IsOver; day++)
            {
                List<DayStep> steps = ResolveOneDay(bundle);

                int attack = steps.FindIndex(s => s.Kind == DayStepKind.Attack);
                int plunder = steps.FindIndex(s => s.Kind == DayStepKind.Plunder);

                if (attack >= 0 && plunder >= 0)
                {
                    Assert.That(plunder, Is.GreaterThan(attack), "saque vem do ataque, nao antes dele");
                }
            }
        }

        [Test]
        public void EncenarNaoAlteraOResultadoDaRun()
        {
            // Mesma semente, um caminho lendo os passos e outro descartando os eventos.
            RunBundle staged = Run(seed: 4242);
            RunBundle plain = Run(seed: 4242);

            for (int day = 0; day < 12; day++)
            {
                if (!staged.Run.IsOver)
                {
                    staged.Engine.EndDay();
                    DayPresentation.Build(staged.Engine.DrainEvents());
                }

                if (!plain.Run.IsOver)
                {
                    plain.Engine.EndDay();
                    plain.Engine.DrainEvents();
                }
            }

            Assert.That(plain.Run.Day, Is.EqualTo(staged.Run.Day));
            Assert.That(plain.Run.Integrity, Is.EqualTo(staged.Run.Integrity));
            Assert.That(plain.Run.Gold, Is.EqualTo(staged.Run.Gold));
            Assert.That(plain.Run[ResourceKind.Population], Is.EqualTo(staged.Run[ResourceKind.Population]));
            Assert.That(plain.Run.Grid.OwnedCount, Is.EqualTo(staged.Run.Grid.OwnedCount));
            Assert.That(plain.Run.Collapse, Is.EqualTo(staged.Run.Collapse));
        }

        [Test]
        public void OAtaqueEDestaque()
        {
            RunBundle bundle = Run(firstThreatDay: 3);

            for (int day = 0; day < 8 && !bundle.Run.IsOver; day++)
            {
                List<DayStep> steps = ResolveOneDay(bundle);
                DayStep attack = steps.Find(s => s.Kind == DayStepKind.Attack);

                if (attack != null)
                {
                    Assert.That(attack.IsHighlight, Is.True, "o climax do dia precisa de destaque proprio");
                    return;
                }
            }

            Assert.Fail("nenhum ataque aconteceu na janela testada");
        }

        [Test]
        public void RepelirEQuebrarSaoDistinguiveis()
        {
            AttackReport repelled = new AttackReport(ThreatKind.Horde, 4, 5, 10);
            AttackReport broken = new AttackReport(ThreatKind.Horde, 4, 20, 5) { Overflow = 15 };

            List<DayStep> repelledSteps = DayPresentation.Build(
                new List<RunEvent> { new AttackResolvedEvent(repelled) });
            List<DayStep> brokenSteps = DayPresentation.Build(
                new List<RunEvent> { new AttackResolvedEvent(broken) });

            Assert.That(repelledSteps[0].Headline, Is.Not.EqualTo(brokenSteps[0].Headline));
            Assert.That(repelledSteps[0].Headline, Does.Contain("repelido"));
        }

        [Test]
        public void PassosSemConteudo_NaoViramRuido()
        {
            // Um dia sem produção nenhuma não deve gerar um passo vazio para o
            // jogador assistir.
            List<DayStep> steps = DayPresentation.Build(new List<RunEvent>
            {
                new ProductionCollectedEvent(new ResourceAmounts(), new List<ProductionBreakdown>())
            });

            Assert.That(steps, Is.Empty);
        }

        [Test]
        public void EventosDesconhecidos_SaoIgnorados()
        {
            List<DayStep> steps = DayPresentation.Build(new List<RunEvent>
            {
                new PhaseChangedEvent(DayPhase.Planning),
                new HandDiscardedEvent(3, 0)
            });

            Assert.That(steps, Is.Empty, "fase e descarte nao sao cena");
        }

        [Test]
        public void ListaVazia_NaoQuebra()
        {
            Assert.That(DayPresentation.Build(null), Is.Empty);
            Assert.That(DayPresentation.Build(new List<RunEvent>()), Is.Empty);
        }

        [Test]
        public void ProducaoEDescritaPorRecurso()
        {
            ResourceAmounts produced = ResourceAmounts.Of(
                (ResourceKind.Wood, 4), (ResourceKind.Food, 2));

            List<DayStep> steps = DayPresentation.Build(new List<RunEvent>
            {
                new ProductionCollectedEvent(produced, new List<ProductionBreakdown>())
            });

            Assert.That(steps[0].Headline, Does.Contain("madeira"));
            Assert.That(steps[0].Headline, Does.Contain("comida"));
        }

        [Test]
        public void FomeEDistintaDeConsumoNormal()
        {
            List<DayStep> normal = DayPresentation.Build(new List<RunEvent>
            {
                new FoodResolvedEvent(5, 4, 0)
            });

            List<DayStep> famine = DayPresentation.Build(new List<RunEvent>
            {
                new FoodResolvedEvent(0, 4, 2)
            });

            Assert.That(normal[0].Headline, Does.Not.Contain("Fome"));
            Assert.That(famine[0].Headline, Does.Contain("Fome"));
        }
    }
}

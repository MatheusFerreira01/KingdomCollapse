using System.Collections.Generic;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    /// <summary>
    /// O fim do dia em duas etapas: o jogo para no evento com escolha, e o resultado
    /// tem de ser identico ao de quem ja sabia a opcao.
    /// </summary>
    public sealed class EndOfDayFlowTests
    {
        private static EventDefinition Offer()
        {
            return new EventDefinition(
                "oferta", "Oferta", EventClass.Neutral,
                new List<EventOption>
                {
                    new EventOption("Recusar", new List<IEffect>()),
                    new EventOption("Aceitar", new List<IEffect> { new GainResourceEffect(ResourceKind.Gold, 25) }, true)
                },
                cooldownDays: 0);
        }

        private static EventDefinition Plain()
        {
            return EventDefinition.Simple(
                "simples", "Simples", EventClass.Positive,
                new List<IEffect> { new GainResourceEffect(ResourceKind.Gold, 7) }, cooldownDays: 0);
        }

        private static RunBundle Run(EventDefinition definition)
        {
            return TestContent.Run(catalog: TestContent.Catalog(null, definition));
        }

        [Test]
        public void EventoComEscolha_ParaODiaAteAResposta()
        {
            RunBundle bundle = Run(Offer());

            bundle.Engine.BeginEndDay();

            Assert.That(bundle.Engine.IsAwaitingEventChoice, Is.True);
            Assert.That(bundle.Engine.PendingDayEvent.Id, Is.EqualTo("oferta"));
            Assert.That(bundle.Run.Day, Is.EqualTo(1), "o dia nao vira antes da escolha");
            Assert.That(bundle.Engine.BuyTile(new Coord(1, 0)).Ok, Is.False, "nada mais e aceito enquanto o evento espera");
        }

        [Test]
        public void RespostaAplicaAOpcaoETerminaODia()
        {
            RunBundle bundle = Run(Offer());
            bundle.Engine.BeginEndDay();
            int before = bundle.Run.Gold;

            Assert.That(bundle.Engine.ResolvePendingEvent(1).Ok, Is.True);

            Assert.That(bundle.Run.Gold, Is.EqualTo(before + 25));
            Assert.That(bundle.Run.Day, Is.EqualTo(2));
            Assert.That(bundle.Run.Phase, Is.EqualTo(DayPhase.Planning));
            Assert.That(bundle.Engine.IsAwaitingEventChoice, Is.False);
        }

        [Test]
        public void EventoSemEscolha_ResolveDeUmaVez()
        {
            RunBundle bundle = Run(Plain());

            bundle.Engine.BeginEndDay();

            Assert.That(bundle.Engine.IsAwaitingEventChoice, Is.False);
            Assert.That(bundle.Run.Day, Is.EqualTo(2));
        }

        [Test]
        public void ResponderSemEventoPendente_ERecusado()
        {
            RunBundle bundle = Run(Plain());

            Assert.That(bundle.Engine.ResolvePendingEvent(0).Rejection, Is.EqualTo(CommandRejection.WrongPhase));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void DuasEtapas_ProduzemOMesmoEstadoQueEncerrarDireto(int option)
        {
            RunBundle direct = Run(Offer());
            RunBundle staged = Run(Offer());

            for (int day = 0; day < 6; day++)
            {
                direct.Engine.EndDay(option);

                staged.Engine.BeginEndDay();
                if (staged.Engine.IsAwaitingEventChoice)
                {
                    staged.Engine.ResolvePendingEvent(option);
                }
            }

            Assert.That(staged.Run.Day, Is.EqualTo(direct.Run.Day));
            Assert.That(staged.Run.Pool.ToString(), Is.EqualTo(direct.Run.Pool.ToString()));
            Assert.That(staged.Run.Integrity, Is.EqualTo(direct.Run.Integrity));
        }

        [Test]
        public void ChanceDeEvento_ZeroNuncaSorteia_UmSempreSorteia()
        {
            EventPool pool = new EventPool(new[] { Plain() }) { EventChance = 0 };
            RunBundle bundle = TestContent.Run();

            for (int i = 0; i < 20; i++)
            {
                Assert.That(pool.Draw(bundle.Run, bundle.Run.Random.Channel(RandomChannel.Events)), Is.Null);
            }

            pool.EventChance = 1;
            Assert.That(pool.Draw(bundle.Run, bundle.Run.Random.Channel(RandomChannel.Events)), Is.Not.Null);
        }

        // --- Crescimento com reserva ---

        private static RunBundle Settlement(int population, int food)
        {
            RaceDefinition race = new RaceDefinition(
                "humans", "Humanos", 50, 20, 5, 3,
                startingResources: ResourceAmounts.Of(
                    (ResourceKind.Population, population), (ResourceKind.Food, food)));
            RunBundle bundle = TestContent.Run(race);
            bundle.Run.Grid.Grant(new Coord(1, 0));
            bundle.Run.Grid.Build(new Coord(1, 0), new BuildingDefinition(
                "casa", "Casa", new List<TerrainType>(), 0, 0, 0, populationCapacity: 40));
            return bundle;
        }

        [Test]
        public void Crescimento_RespeitaOTetoPorDia()
        {
            RunBundle bundle = Settlement(1, 900);

            bundle.Engine.EndDay();

            Assert.That(bundle.Run[ResourceKind.Population], Is.EqualTo(1 + RunEngine.MaxGrowthPerDay));
        }

        [Test]
        public void Crescimento_ParaQuandoSoSobraAComidaDeAmanha()
        {
            // 3 de gente, 9 de comida: come 3, sobram 6. Crescer um custa 5 e deixaria
            // 1 para quatro bocas: nao cresce.
            RunBundle bundle = Settlement(3, 9);

            bundle.Engine.EndDay();

            Assert.That(bundle.Run[ResourceKind.Population], Is.EqualTo(3));
            Assert.That(bundle.Run[ResourceKind.Food], Is.EqualTo(6));
        }
    }
}

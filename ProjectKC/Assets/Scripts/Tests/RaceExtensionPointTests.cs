using System.Collections.Generic;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    /// <summary>
    /// Guarda os pontos de extensão que mantêm as raças viáveis como **verbo**, e não
    /// como multiplicador (design D11).
    ///
    /// As raças próprias foram adiadas para a mudança seguinte. O risco de adiar é
    /// projetar a economia sem espaço para elas e descobrir tarde demais que ficaram
    /// impossíveis. Estes testes falham se essa porta for fechada: são o custo de
    /// adiar sem empurrar uma reescrita para frente.
    /// </summary>
    [TestFixture]
    public class RaceExtensionPointTests
    {
        private static RaceDefinition RaceWith(
            Dictionary<string, double> rules, ResourceAmounts starting = null)
        {
            return new RaceDefinition(
                "test", "Teste", 50, 20, 5, 3,
                new List<CardDefinition>(),
                new RuleModifiers(rules),
                null, null, null,
                starting);
        }

        private static RunBundle Run(RaceDefinition race, int firstThreatDay = 999)
        {
            return TestContent.Run(race, TestContent.Catalog(race), firstThreatDay: firstThreatDay);
        }

        // --- Orcs: comida vem da guerra ---

        [Test]
        public void RacaQueSaqueia_TiraRecursoDeAtaqueRepelido()
        {
            RaceDefinition raiders = RaceWith(new Dictionary<string, double>
            {
                { RuleKeys.Plunder(ResourceKind.Food), 6 },
                { RuleKeys.Plunder(ResourceKind.Gold), 3 }
            });

            RunBundle bundle = Run(raiders);
            bundle.Run.AddPendingDefense(1000);
            int foodBefore = bundle.Run[ResourceKind.Food];
            int goldBefore = bundle.Run.Gold;

            ScheduledThreat threat = new ScheduledThreat(ThreatKind.Horde, bundle.Run.Day, 5, 1);
            AttackReport report = CombatResolver.Resolve(bundle.Run, threat);
            ResourceAmounts plunder = bundle.Run.Rules.PlunderOnRepel();
            bundle.Run.Add(plunder);

            Assert.That(report.Repelled, Is.True);
            Assert.That(bundle.Run[ResourceKind.Food], Is.EqualTo(foodBefore + 6));
            Assert.That(bundle.Run.Gold, Is.EqualTo(goldBefore + 3));
        }

        [Test]
        public void SaqueAcontecePeloMotorAoRepelir()
        {
            RaceDefinition raiders = RaceWith(new Dictionary<string, double>
            {
                { RuleKeys.Plunder(ResourceKind.Food), 5 }
            });

            // Ameaça fraca desde cedo, e defesa grande: o ataque será repelido.
            RunBundle bundle = Run(raiders, firstThreatDay: 3);
            bundle.Engine.DrainEvents();

            bool sawPlunder = false;
            for (int day = 0; day < 12 && !bundle.Run.IsOver && !sawPlunder; day++)
            {
                bundle.Run.AddPendingDefense(1000);
                bundle.Engine.EndDay();

                List<RunEvent> events = bundle.Engine.DrainEvents();
                for (int i = 0; i < events.Count; i++)
                {
                    if (events[i] is PlunderCollectedEvent plunder)
                    {
                        sawPlunder = true;
                        Assert.That(plunder.Plunder[ResourceKind.Food], Is.EqualTo(5));
                    }
                }
            }

            Assert.That(sawPlunder, Is.True, "repelir precisa render saque sem tocar no calculo de ataque");
        }

        [Test]
        public void RacaQueNaoSaqueia_NaoGanhaNadaAoRepelir()
        {
            RunBundle bundle = Run(TestContent.Humans());

            Assert.That(bundle.Run.Rules.PlunderOnRepel().IsEmpty, Is.True);
        }

        // --- Elfos: viver da terra em vez de a desenvolver ---

        [Test]
        public void RacaQueVivedaTerra_ColheSemEdificio()
        {
            RaceDefinition forestFolk = RaceWith(new Dictionary<string, double>
            {
                { RuleKeys.TerrainYield(TerrainType.Forest, ResourceKind.Wood), 3 },
                { RuleKeys.TerrainYield(TerrainType.Forest, ResourceKind.Food), 1 }
            });

            RunBundle bundle = Run(forestFolk);
            Coord forest = new Coord(1, 0);
            bundle.Run.Grid.Grant(forest);
            bundle.Run.Grid.SetTerrain(forest, TerrainType.Forest);

            ProductionBreakdown breakdown = ProductionCalculator.ForTile(
                bundle.Run.Grid, bundle.Run.Grid.TileAt(forest), bundle.Run.Rules);

            Assert.That(breakdown[ResourceKind.Wood], Is.EqualTo(3));
            Assert.That(breakdown[ResourceKind.Food], Is.EqualTo(1));
        }

        [Test]
        public void RendimentoDeTerreno_NaoVazaParaOutroTerreno()
        {
            RaceDefinition forestFolk = RaceWith(new Dictionary<string, double>
            {
                { RuleKeys.TerrainYield(TerrainType.Forest, ResourceKind.Wood), 3 }
            });

            RunBundle bundle = Run(forestFolk);
            Coord mine = new Coord(1, 0);
            bundle.Run.Grid.Grant(mine);
            bundle.Run.Grid.SetTerrain(mine, TerrainType.Mine);

            ProductionBreakdown breakdown = ProductionCalculator.ForTile(
                bundle.Run.Grid, bundle.Run.Grid.TileAt(mine), bundle.Run.Rules);

            Assert.That(breakdown.IsEmpty, Is.True);
        }

        // --- Anões: guarnição vale mais ---

        [Test]
        public void RacaComGuarnicaoMelhor_DefendeMais()
        {
            BuildingDefinition tower = new BuildingDefinition(
                "tower", "Torre", new List<TerrainType>(), 0, 0, 10, null, workersRequired: 1);

            RaceDefinition baseline = RaceWith(
                new Dictionary<string, double>(),
                ResourceAmounts.Of(ResourceKind.Population, 5));

            RaceDefinition fortified = RaceWith(
                new Dictionary<string, double> { { RuleKeys.GarrisonDefenseMultiplier, 1.5 } },
                ResourceAmounts.Of(ResourceKind.Population, 5));

            int plain = Defense(baseline, tower);
            int strong = Defense(fortified, tower);

            Assert.That(strong, Is.GreaterThan(plain));
        }

        private static int Defense(RaceDefinition race, BuildingDefinition tower)
        {
            RunBundle bundle = Run(race);
            Coord coord = new Coord(1, 0);
            bundle.Run.Grid.Grant(coord);
            bundle.Run.Grid.Build(coord, tower);
            return bundle.Run.TotalDefense();
        }

        // --- Sem declaração, nada muda ---

        [Test]
        public void RacaSemNenhumaChave_JogaComoBaseline()
        {
            RaceDefinition bare = RaceWith(new Dictionary<string, double>());
            RunBundle bundle = Run(bare);

            Assert.That(bundle.Run.Rules.PlunderOnRepel().IsEmpty, Is.True);
            Assert.That(bundle.Run.Rules.TerrainYield(TerrainType.Forest).IsEmpty, Is.True);
            Assert.That(bundle.Run.Rules.GetDouble(RuleKeys.GarrisonDefenseMultiplier, 1.0), Is.EqualTo(1.0));
        }

        [Test]
        public void ChaveDeExtensaoTemNomeEstavel()
        {
            // Os nomes viram dado autorado no Inspector: mudá-los quebra conteúdo
            // em silêncio, porque chave desconhecida é ignorada por definição.
            Assert.That(RuleKeys.Plunder(ResourceKind.Food), Is.EqualTo("plunder_food"));
            Assert.That(
                RuleKeys.TerrainYield(TerrainType.Forest, ResourceKind.Wood),
                Is.EqualTo("terrain_forest_wood"));
        }
    }
}

using System.Collections.Generic;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    [TestFixture]
    public class GreedyPolicyTests
    {
        // --- 7.1 Postura de trabalho segue a ameaca ---

        [Test]
        public void BotEntraEmPosturaDefensiva_QuandoAtaqueIminenteSuperaADefesa()
        {
            RunBundle bundle = TestContent.Run(firstThreatDay: 999);
            bundle.Run.Threats.Schedule(bundle.Run.Day + 2, 50, bundle.Run.Day, null, null);

            new GreedyPolicy().PlanDay(bundle.Engine, bundle);

            Assert.That(bundle.Run.Stance, Is.EqualTo(WorkerStance.DefenseFirst));
        }

        [Test]
        public void BotVoltaAPosturaDeProducao_SemAmeacaIminente()
        {
            RunBundle bundle = TestContent.Run(firstThreatDay: 999);

            new GreedyPolicy().PlanDay(bundle.Engine, bundle);

            Assert.That(bundle.Run.Stance, Is.EqualTo(WorkerStance.ProductionFirst));
        }

        // --- 7.2 Comida antes de fome por descuido ---

        [Test]
        public void BotPriorizaProducaoDeComida_QuandoOEstoquePericlita()
        {
            BuildingDefinition farm = new BuildingDefinition(
                "farm_test", "Fazenda", new List<TerrainType> { TerrainType.Plain }, 5, 0, 0,
                production: ResourceAmounts.Of(ResourceKind.Food, 5));
            BuildingDefinition mine = new BuildingDefinition(
                "mine_test", "Mina", new List<TerrainType> { TerrainType.Plain }, 5, 0, 0,
                production: ResourceAmounts.Of(ResourceKind.Stone, 5));

            ContentCatalog catalog = new ContentCatalog();
            catalog.AddBuilding(TestContent.Hall(), true);
            catalog.AddBuilding(farm, true);
            catalog.AddBuilding(mine, true);
            catalog.AddRace(TestContent.Humans(), true);

            RunSetup setup = new RunSetup("humans", 1) { FirstThreatDay = 999 };
            RunBundle bundle = RunBuilder.Build(setup, catalog);
            bundle.Engine.StartRun();

            Coord coord = new Coord(1, 0);
            bundle.Run.Grid.Grant(coord);
            bundle.Run.Grid.SetTerrain(coord, TerrainType.Plain);
            bundle.Run.Add(ResourceKind.Population, 20); // upkeep alto, comida zerada: risco
            bundle.Run.Add(ResourceKind.Gold, 100);

            new GreedyPolicy().PlanDay(bundle.Engine, bundle);

            Tile tile = bundle.Run.Grid.TileAt(coord);
            Assert.That(tile.HasBuilding, Is.True);
            Assert.That(tile.Building.Id, Is.EqualTo("farm_test"),
                "com estoque de comida periclitando, o bot devia priorizar a fazenda, nao a mina");
        }

        // --- 7.3 Reacao a identidade do rival ---

        [Test]
        public void BotContraRivalEconomico_EstocaOQueEPressionadoEmVezDeErguerTorre()
        {
            BuildingDefinition tower = new BuildingDefinition(
                "tower_test", "Torre", new List<TerrainType>(), 5, 0, 20);
            BuildingDefinition farm = new BuildingDefinition(
                "farm_test", "Fazenda", new List<TerrainType>(), 5, 0, 0,
                production: ResourceAmounts.Of(ResourceKind.Food, 5));

            ContentCatalog catalog = new ContentCatalog();
            catalog.AddBuilding(TestContent.Hall(), true);
            catalog.AddBuilding(tower, true);
            catalog.AddBuilding(farm, true);
            catalog.AddRace(TestContent.Humans(), true);

            RivalDefinition rival = TestContent.Rival(
                "econ", TestContent.ResourceIdentity(resource: ResourceKind.Food),
                attackCount: 5, curve: TestContent.FlatCampaignCurve(999));

            RunSetup setup = new RunSetup("humans", 1)
            {
                FirstThreatDay = 999,
                Rivals = new List<RivalDefinition> { rival }
            };
            RunBundle bundle = RunBuilder.Build(setup, catalog);
            bundle.Engine.StartRun();

            Coord coord = new Coord(1, 0);
            bundle.Run.Grid.Grant(coord);
            bundle.Run.Grid.SetTerrain(coord, TerrainType.Plain);
            bundle.Run.Add(ResourceKind.Gold, 100);

            // Dia 1: primeiro ataque agendado (dia 4), fora da janela de imminencia
            // (2 dias). Avanca sem agir pra entrar na janela.
            bundle.Engine.EndDay();

            new GreedyPolicy().PlanDay(bundle.Engine, bundle);

            Tile tile = bundle.Run.Grid.TileAt(coord);
            Assert.That(tile.HasBuilding, Is.True);
            Assert.That(tile.Building.Id, Is.EqualTo("farm_test"),
                "defesa nao anula rival economico (design D4); o bot devia estocar, nao erguer torre");
        }
    }
}

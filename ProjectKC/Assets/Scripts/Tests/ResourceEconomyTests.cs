using System.Collections.Generic;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    /// <summary>
    /// A economia vista de dentro da run: o que a raça declara chega ao saldo, e o
    /// saldo é debitado e creditado por recurso, sem um recurso vazar no outro.
    /// </summary>
    [TestFixture]
    public class ResourceEconomyTests
    {
        private static RaceDefinition RaceWith(ResourceAmounts starting)
        {
            return new RaceDefinition(
                "humans", "Humanos", 50, 20, 5, 3,
                new List<CardDefinition>(),
                RuleModifiers.None,
                null, null, null,
                starting);
        }

        [Test]
        public void RunComecaComOsRecursosDeclaradosPelaRaca()
        {
            RaceDefinition race = RaceWith(ResourceAmounts.Of(
                (ResourceKind.Gold, 30),
                (ResourceKind.Wood, 12),
                (ResourceKind.Stone, 6),
                (ResourceKind.Food, 20),
                (ResourceKind.Population, 4)));

            RunBundle bundle = TestContent.Run(race, TestContent.Catalog(race));

            Assert.That(bundle.Run[ResourceKind.Gold], Is.EqualTo(30));
            Assert.That(bundle.Run[ResourceKind.Wood], Is.EqualTo(12));
            Assert.That(bundle.Run[ResourceKind.Stone], Is.EqualTo(6));
            Assert.That(bundle.Run[ResourceKind.Food], Is.EqualTo(20));
            Assert.That(bundle.Run[ResourceKind.Population], Is.EqualTo(4));
        }

        [Test]
        public void RacaSemRecursosDeclarados_CaiNoOuroInicial()
        {
            // Conteúdo antigo declara só ouro; ele continua funcionando.
            RunBundle bundle = TestContent.Run(TestContent.Humans(gold: 77));

            Assert.That(bundle.Run.Gold, Is.EqualTo(77));
            Assert.That(bundle.Run[ResourceKind.Gold], Is.EqualTo(77));
            Assert.That(bundle.Run[ResourceKind.Wood], Is.Zero);
        }

        [Test]
        public void CreditarUmRecursoNaoTocaOsOutros()
        {
            RunBundle bundle = TestContent.Run();
            int goldBefore = bundle.Run.Gold;

            bundle.Run.Add(ResourceKind.Wood, 9);

            Assert.That(bundle.Run[ResourceKind.Wood], Is.EqualTo(9));
            Assert.That(bundle.Run.Gold, Is.EqualTo(goldBefore));
        }

        [Test]
        public void PagarCustoMisto_DebitaTudoOuNada()
        {
            RaceDefinition race = RaceWith(ResourceAmounts.Of(
                (ResourceKind.Wood, 10), (ResourceKind.Stone, 2)));
            RunBundle bundle = TestContent.Run(race, TestContent.Catalog(race));

            ResourceAmounts tooMuch = ResourceAmounts.Of(
                (ResourceKind.Wood, 4), (ResourceKind.Stone, 9));

            bool paid = bundle.Run.TrySpend(tooMuch, out ResourceShortage shortage);

            Assert.That(paid, Is.False);
            Assert.That(shortage.Kind, Is.EqualTo(ResourceKind.Stone));
            Assert.That(bundle.Run[ResourceKind.Wood], Is.EqualTo(10), "recusa não pode debitar pela metade");
        }

        [Test]
        public void RecusaDizQualRecursoFalta()
        {
            RunBundle bundle = TestContent.Run();

            bundle.Run.CanAfford(ResourceAmounts.Of(ResourceKind.Stone, 5), out ResourceShortage shortage);

            Assert.That(shortage.Any, Is.True);
            Assert.That(shortage.Kind, Is.EqualTo(ResourceKind.Stone));
            Assert.That(shortage.ToString(), Does.Contain("pedra"));
        }

        // --- Construcao paga em recursos (task 1.3) ---

        private static BuildingDefinition Sawmill()
        {
            return new BuildingDefinition(
                "sawmill", "Serraria",
                new List<TerrainType> { TerrainType.Forest },
                0, 0, 0,
                new List<AdjacencyBonus>
                {
                    AdjacencyBonus.ForTerrain(TerrainType.Forest, 2, ResourceKind.Wood)
                },
                cost: ResourceAmounts.Of((ResourceKind.Wood, 5), (ResourceKind.Stone, 3)),
                production: ResourceAmounts.Of(ResourceKind.Wood, 3),
                workersRequired: 2);
        }

        private static RunBundle RunWithMaterials(int wood, int stone)
        {
            RaceDefinition race = RaceWith(ResourceAmounts.Of(
                (ResourceKind.Gold, 100), (ResourceKind.Wood, wood), (ResourceKind.Stone, stone)));
            return TestContent.Run(race, TestContent.Catalog(race));
        }

        [Test]
        public void ConstruirDebitaTodosOsMateriais()
        {
            RunBundle bundle = RunWithMaterials(wood: 10, stone: 10);
            Coord forest = new Coord(1, 0);
            bundle.Run.Grid.Grant(forest);
            bundle.Run.Grid.SetTerrain(forest, TerrainType.Forest);

            CommandResult result = bundle.Engine.BuildOn(forest, Sawmill());

            Assert.That(result.Ok, Is.True);
            Assert.That(bundle.Run[ResourceKind.Wood], Is.EqualTo(5));
            Assert.That(bundle.Run[ResourceKind.Stone], Is.EqualTo(7));
        }

        [Test]
        public void ConstruirSemUmDosMateriais_ERecusadoSemDebitarNada()
        {
            RunBundle bundle = RunWithMaterials(wood: 10, stone: 1);
            Coord forest = new Coord(1, 0);
            bundle.Run.Grid.Grant(forest);
            bundle.Run.Grid.SetTerrain(forest, TerrainType.Forest);

            CommandResult result = bundle.Engine.BuildOn(forest, Sawmill());

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.NotEnoughResources));
            Assert.That(bundle.Run[ResourceKind.Wood], Is.EqualTo(10), "recusa nao pode debitar madeira");
            Assert.That(bundle.Run[ResourceKind.Stone], Is.EqualTo(1));
            Assert.That(bundle.Run.Grid.TileAt(forest).HasBuilding, Is.False);
        }

        [Test]
        public void RecusaDeConstrucaoDizQualMaterialFalta()
        {
            RunBundle bundle = RunWithMaterials(wood: 10, stone: 1);
            Coord forest = new Coord(1, 0);
            bundle.Run.Grid.Grant(forest);
            bundle.Run.Grid.SetTerrain(forest, TerrainType.Forest);

            CommandResult result = bundle.Engine.BuildOn(forest, Sawmill());

            Assert.That(result.Shortage.Any, Is.True);
            Assert.That(result.Shortage.Kind, Is.EqualTo(ResourceKind.Stone));
            Assert.That(result.Shortage.Missing, Is.EqualTo(2));
        }

        [Test]
        public void EdificioDeclaraTrabalhadores()
        {
            Assert.That(Sawmill().WorkersRequired, Is.EqualTo(2));
            Assert.That(TestContent.Farm().WorkersRequired, Is.Zero, "conteudo antigo nao exige gente");
        }

        [Test]
        public void CustoEmOuroContinuaFuncionandoParaConteudoAntigo()
        {
            RunBundle bundle = RunWithMaterials(wood: 0, stone: 0);
            Coord plain = new Coord(1, 0);
            bundle.Run.Grid.Grant(plain);
            bundle.Run.Grid.SetTerrain(plain, TerrainType.Plain);
            int goldBefore = bundle.Run.Gold;

            CommandResult result = bundle.Engine.BuildOn(plain, TestContent.Farm());

            Assert.That(result.Ok, Is.True);
            Assert.That(bundle.Run.Gold, Is.EqualTo(goldBefore - TestContent.Farm().GoldCost));
        }

        [Test]
        public void OuroContinuaSendoContadoNasEstatisticas()
        {
            RunBundle bundle = TestContent.Run();
            int earnedBefore = bundle.Run.Stats.GoldEarned;

            bundle.Run.Add(ResourceKind.Gold, 25);
            bundle.Run.Add(ResourceKind.Wood, 25);

            Assert.That(bundle.Run.Stats.GoldEarned, Is.EqualTo(earnedBefore + 25),
                "madeira não pode entrar na estatística de ouro");
        }
    }
}

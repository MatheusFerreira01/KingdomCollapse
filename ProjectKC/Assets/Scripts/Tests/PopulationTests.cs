using System.Collections.Generic;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    /// <summary>
    /// A cadeia que sustenta a economia: comida alimenta população, população opera
    /// edifícios, e a mesma gente não guarnece a torre e trabalha no campo.
    /// </summary>
    [TestFixture]
    public class PopulationTests
    {
        private static BuildingDefinition Farm(int workers = 1)
        {
            return new BuildingDefinition(
                "farm", "Fazenda", new List<TerrainType> { TerrainType.Plain }, 0, 0, 0, null,
                production: ResourceAmounts.Of(ResourceKind.Food, 4),
                workersRequired: workers);
        }

        private static BuildingDefinition Mine(int workers = 2)
        {
            return new BuildingDefinition(
                "mine", "Mina", new List<TerrainType> { TerrainType.Mine }, 0, 0, 0, null,
                production: ResourceAmounts.Of(ResourceKind.Stone, 3),
                workersRequired: workers);
        }

        private static BuildingDefinition Tower(int workers = 2, int defense = 6)
        {
            return new BuildingDefinition(
                "watchtower", "Torre", new List<TerrainType>(), 0, 0, defense, null,
                workersRequired: workers);
        }

        private static BuildingDefinition House(int capacity = 5)
        {
            return new BuildingDefinition(
                "house", "Alojamento", new List<TerrainType>(), 0, 0, 0, null,
                populationCapacity: capacity);
        }

        private static RaceDefinition RaceWith(ResourceAmounts starting, RuleModifiers rules = null)
        {
            return new RaceDefinition(
                "humans", "Humanos", 0, 20, 5, 3,
                new List<CardDefinition>(), rules ?? RuleModifiers.None,
                null, null, null, starting);
        }

        private static RunBundle Run(ResourceAmounts starting, RuleModifiers rules = null)
        {
            RaceDefinition race = RaceWith(starting, rules);
            return TestContent.Run(race, TestContent.Catalog(race));
        }

        private static Coord Place(RunBundle bundle, Coord coord, TerrainType terrain, BuildingDefinition building)
        {
            bundle.Run.Grid.Grant(coord);
            bundle.Run.Grid.SetTerrain(coord, terrain);
            bundle.Run.Grid.Build(coord, building);
            return coord;
        }

        // --- Produção por terreno (1.4) ---

        [Test]
        public void TerrenosRendemRecursosDiferentes()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(ResourceKind.Population, 10));
            Place(bundle, new Coord(1, 0), TerrainType.Plain, Farm());
            Place(bundle, new Coord(0, 1), TerrainType.Mine, Mine());

            ResourceAmounts produced = bundle.Run.CollectDailyProductionByResource();

            Assert.That(produced[ResourceKind.Food], Is.EqualTo(4));
            Assert.That(produced[ResourceKind.Stone], Is.EqualTo(3));
        }

        [Test]
        public void MinaNaoRendeComidaNemFazendaRendePedra()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(ResourceKind.Population, 10));
            Place(bundle, new Coord(1, 0), TerrainType.Mine, Mine());

            ResourceAmounts produced = bundle.Run.CollectDailyProductionByResource();

            Assert.That(produced[ResourceKind.Stone], Is.EqualTo(3));
            Assert.That(produced[ResourceKind.Food], Is.Zero);
        }

        // --- Consumo diário (1.5) ---

        [Test]
        public void PopulacaoComeTodoDia()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(
                (ResourceKind.Population, 3), (ResourceKind.Food, 20)));

            bundle.Engine.EndDay();

            Assert.That(bundle.Run[ResourceKind.Food], Is.EqualTo(17));
        }

        [Test]
        public void DobrarAPopulacaoDobraOConsumo()
        {
            RunBundle small = Run(ResourceAmounts.Of(
                (ResourceKind.Population, 3), (ResourceKind.Food, 50)));
            RunBundle big = Run(ResourceAmounts.Of(
                (ResourceKind.Population, 6), (ResourceKind.Food, 50)));

            Assert.That(big.Run.DailyFoodUpkeep(), Is.EqualTo(small.Run.DailyFoodUpkeep() * 2));
        }

        [Test]
        public void ConsumoVemDepoisDaProducao()
        {
            // Sem comida em caixa, mas com fazenda: a colheita do dia precisa
            // alimentar quem colheu.
            RunBundle bundle = Run(ResourceAmounts.Of(ResourceKind.Population, 2));
            Place(bundle, new Coord(1, 0), TerrainType.Plain, Farm());
            bundle.Engine.DrainEvents();

            bundle.Engine.EndDay();

            List<RunEvent> events = bundle.Engine.DrainEvents();
            FoodResolvedEvent food = null;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i] is FoodResolvedEvent resolved)
                {
                    food = resolved;
                }
            }

            Assert.That(food, Is.Not.Null);
            Assert.That(food.Produced, Is.EqualTo(4));
            Assert.That(food.Starved, Is.Zero, "a colheita do dia deve alimentar a populacao");
        }

        // --- Escassez (1.6) ---

        [Test]
        public void FomeReduzAPopulacao()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(
                (ResourceKind.Population, 5), (ResourceKind.Food, 2)));

            bundle.Engine.EndDay();

            Assert.That(bundle.Run[ResourceKind.Food], Is.Zero);
            Assert.That(bundle.Run[ResourceKind.Population], Is.LessThan(5));
        }

        [Test]
        public void FomePassageiraNaoEncerraARun()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(ResourceKind.Population, 4));

            bundle.Engine.EndDay();

            Assert.That(bundle.Run[ResourceKind.Population], Is.LessThan(4));
            Assert.That(bundle.Run.IsOver, Is.False, "um dia de fome derruba populacao, nao a run");
        }

        /// <summary>
        /// Fome que persiste por dias demais colapsa a run (task 11.3 revisao):
        /// sem isto, um rival de eixo Recurso/Populacao (design D4, "nao resolvido
        /// por defesa") nunca conseguia de fato terminar a run — so incomodava pra
        /// sempre enquanto a defesa segurasse o ataque em si.
        /// </summary>
        [Test]
        public void FomeCronicaEncerraARunPorColapso()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(ResourceKind.Population, 4));

            for (int i = 0; i < 10 && !bundle.Run.IsOver; i++)
            {
                bundle.Engine.EndDay();
            }

            Assert.That(bundle.Run.IsOver, Is.True, "fome cronica precisa encerrar a run");
            Assert.That(bundle.Run.Collapse, Is.EqualTo(CollapseReason.Starvation));
        }

        [Test]
        public void FomeEAnunciadaComAntecedencia()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(
                (ResourceKind.Population, 5), (ResourceKind.Food, 1)));

            List<RunEvent> events = bundle.Engine.DrainEvents();
            FamineWarningEvent warning = null;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i] is FamineWarningEvent found)
                {
                    warning = found;
                }
            }

            Assert.That(bundle.Run.Phase, Is.EqualTo(DayPhase.Planning));
            Assert.That(warning, Is.Not.Null, "o aviso precisa sair antes de o jogador decidir");
            Assert.That(warning.Deficit, Is.GreaterThan(0));
        }

        [Test]
        public void SemFomePrevista_NaoHaAviso()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(
                (ResourceKind.Population, 2), (ResourceKind.Food, 100)));

            List<RunEvent> events = bundle.Engine.DrainEvents();
            for (int i = 0; i < events.Count; i++)
            {
                Assert.That(events[i], Is.Not.InstanceOf<FamineWarningEvent>());
            }
        }

        // --- Crescimento (1.7) ---

        [Test]
        public void ExcedenteDeComidaViraGente()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(
                (ResourceKind.Population, 1), (ResourceKind.Food, 60)));
            Place(bundle, new Coord(1, 0), TerrainType.Plain, House(10));
            int before = bundle.Run[ResourceKind.Population];

            bundle.Engine.EndDay();

            Assert.That(bundle.Run[ResourceKind.Population], Is.GreaterThan(before));
        }

        [Test]
        public void CapacidadeLimitaOCrescimento()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(
                (ResourceKind.Population, 2), (ResourceKind.Food, 500)));
            Place(bundle, new Coord(1, 0), TerrainType.Plain, House(3));

            for (int i = 0; i < 5 && !bundle.Run.IsOver; i++)
            {
                bundle.Engine.EndDay();
            }

            Assert.That(bundle.Run.PopulationCapacity(), Is.EqualTo(3));
            Assert.That(bundle.Run[ResourceKind.Population], Is.LessThanOrEqualTo(3),
                "nao cresce alem do teto mesmo com comida sobrando");
        }

        [Test]
        public void SemAlojamento_NaoCresce()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(
                (ResourceKind.Population, 1), (ResourceKind.Food, 500)));

            bundle.Engine.EndDay();

            Assert.That(bundle.Run[ResourceKind.Population], Is.EqualTo(1));
        }

        // --- Trabalhadores (1.8) ---

        [Test]
        public void EdificioSemGente_NaoProduzENaoEDestruido()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(ResourceKind.Population, 0));
            Coord plain = Place(bundle, new Coord(1, 0), TerrainType.Plain, Farm(workers: 2));

            ResourceAmounts produced = bundle.Run.CollectDailyProductionByResource();

            Assert.That(produced[ResourceKind.Food], Is.Zero);
            Assert.That(bundle.Run.Grid.TileAt(plain).HasBuilding, Is.True, "ocioso nao e destruido");
        }

        [Test]
        public void OciosidadeEInformada()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(ResourceKind.Population, 0));
            Coord plain = Place(bundle, new Coord(1, 0), TerrainType.Plain, Farm(workers: 2));

            List<ProductionBreakdown> lines = new List<ProductionBreakdown>();
            bundle.Run.CollectDailyProductionByResource(lines);

            ProductionBreakdown tile = lines.Find(b => b.Coord == plain);
            Assert.That(tile, Is.Not.Null);
            Assert.That(tile.Idle, Is.True);
        }

        [Test]
        public void PerderPopulacao_DeixaEdificiosOciososSemDestruir()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(ResourceKind.Population, 2));
            Coord plain = Place(bundle, new Coord(1, 0), TerrainType.Plain, Farm(workers: 2));

            Assert.That(bundle.Run.Allocation().IsStaffed(plain), Is.True);

            bundle.Run.Remove(ResourceKind.Population, 2);

            Assert.That(bundle.Run.Allocation().IsStaffed(plain), Is.False);
            Assert.That(bundle.Run.Allocation().IdleBuildings, Is.EqualTo(1));
            Assert.That(bundle.Run.Grid.TileAt(plain).HasBuilding, Is.True);
        }

        [Test]
        public void EdificioQueNaoExigeGente_OperaSempre()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(ResourceKind.Population, 0));
            Coord plain = Place(bundle, new Coord(1, 0), TerrainType.Plain, Farm(workers: 0));

            Assert.That(bundle.Run.Allocation().IsStaffed(plain), Is.True);
            Assert.That(bundle.Run.CollectDailyProductionByResource()[ResourceKind.Food], Is.EqualTo(4));
        }

        // --- Guarnição (1.9) ---

        [Test]
        public void TorreSemGuarnicao_NaoDefende()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(ResourceKind.Population, 0));
            // O Salao nao exige gente, entao a defesa dele entra na linha de base.
            int baseline = bundle.Run.TotalDefense();

            Place(bundle, new Coord(1, 0), TerrainType.Plain, Tower());

            Assert.That(bundle.Run.TotalDefense(), Is.EqualTo(baseline),
                "torre sem gente nao pode somar defesa alguma");
        }

        [Test]
        public void TorreGuarnecida_Defende()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(ResourceKind.Population, 2));
            int baseline = bundle.Run.TotalDefense();

            Place(bundle, new Coord(1, 0), TerrainType.Plain, Tower(workers: 2, defense: 6));

            Assert.That(bundle.Run.TotalDefense(), Is.EqualTo(baseline + 6));
        }

        [Test]
        public void AMesmaGenteNaoFazDuasCoisas()
        {
            // Duas pessoas, dois postos de dois: só um deles pode operar.
            RunBundle bundle = Run(ResourceAmounts.Of(ResourceKind.Population, 2));
            Place(bundle, new Coord(1, 0), TerrainType.Plain, Farm(workers: 2));
            Place(bundle, new Coord(0, 1), TerrainType.Plain, Tower(workers: 2, defense: 6));

            bundle.Run.Stance = WorkerStance.ProductionFirst;
            int foodWhenWorking = bundle.Run.CollectDailyProductionByResource()[ResourceKind.Food];
            int defenseWhenWorking = bundle.Run.TotalDefense();

            bundle.Run.Stance = WorkerStance.DefenseFirst;
            int foodWhenGuarding = bundle.Run.CollectDailyProductionByResource()[ResourceKind.Food];
            int defenseWhenGuarding = bundle.Run.TotalDefense();

            Assert.That(foodWhenWorking, Is.GreaterThan(foodWhenGuarding), "guarnecer custa producao");
            Assert.That(defenseWhenGuarding, Is.GreaterThan(defenseWhenWorking), "produzir custa defesa");
        }

        [Test]
        public void AlocacaoEDeterministica()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(ResourceKind.Population, 3));
            Place(bundle, new Coord(1, 0), TerrainType.Plain, Farm(workers: 2));
            Place(bundle, new Coord(0, 1), TerrainType.Mine, Mine(workers: 2));

            WorkerAllocation first = bundle.Run.Allocation();
            WorkerAllocation second = bundle.Run.Allocation();

            Assert.That(second.IsStaffed(new Coord(1, 0)), Is.EqualTo(first.IsStaffed(new Coord(1, 0))));
            Assert.That(second.IsStaffed(new Coord(0, 1)), Is.EqualTo(first.IsStaffed(new Coord(0, 1))));
            Assert.That(second.IdleBuildings, Is.EqualTo(first.IdleBuildings));
        }

        [Test]
        public void AlocacaoRelataOQueFalta()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(ResourceKind.Population, 1));
            Place(bundle, new Coord(1, 0), TerrainType.Plain, Farm(workers: 2));

            WorkerAllocation allocation = bundle.Run.Allocation();

            Assert.That(allocation.Required, Is.EqualTo(2));
            Assert.That(allocation.Assigned, Is.Zero);
            Assert.That(allocation.Unassigned, Is.EqualTo(1));
            Assert.That(allocation.IsFullyStaffed, Is.False);
        }
    }
}

using System.Collections.Generic;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    /// <summary>
    /// Efeitos que operam sobre os cinco recursos, e os tetos que impedem um evento
    /// de esvaziar o reino pela porta dos fundos.
    /// </summary>
    [TestFixture]
    public class ResourceEffectTests
    {
        private static RunBundle Run(ResourceAmounts starting)
        {
            RaceDefinition race = new RaceDefinition(
                "humans", "Humanos", 0, 20, 5, 3,
                new List<CardDefinition>(), RuleModifiers.None,
                null, null, null, starting);

            return TestContent.Run(race, TestContent.Catalog(race));
        }

        private static EffectContext Ctx(RunState run, SeverityBudget budget = null)
        {
            return new EffectContext(run, EffectSource.Card, null, budget);
        }

        // --- Ganho e perda por recurso (2.1) ---

        [Test]
        public void GanharUmRecurso_NaoTocaOsOutros()
        {
            RunBundle bundle = Run(ResourceAmounts.Of((ResourceKind.Gold, 10), (ResourceKind.Wood, 10)));

            new GainResourceEffect(ResourceKind.Wood, 5).Apply(Ctx(bundle.Run));

            Assert.That(bundle.Run[ResourceKind.Wood], Is.EqualTo(15));
            Assert.That(bundle.Run[ResourceKind.Gold], Is.EqualTo(10));
        }

        [Test]
        public void PerderUmRecurso_NaoTocaOsOutros()
        {
            RunBundle bundle = Run(ResourceAmounts.Of((ResourceKind.Stone, 10), (ResourceKind.Food, 10)));

            new LoseResourceEffect(ResourceKind.Stone, 4, GoldScaling.Flat).Apply(Ctx(bundle.Run));

            Assert.That(bundle.Run[ResourceKind.Stone], Is.EqualTo(6));
            Assert.That(bundle.Run[ResourceKind.Food], Is.EqualTo(10));
        }

        [Test]
        public void GanhoPorCelula_EscalaComOTerritorio()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(ResourceKind.Wood, 0));
            TestContent.GrantTiles(bundle.Run.Grid, new Coord(1, 0), new Coord(0, 1));

            new GainResourceEffect(ResourceKind.Wood, 2, GoldScaling.PerOwnedTile).Apply(Ctx(bundle.Run));

            Assert.That(bundle.Run[ResourceKind.Wood], Is.EqualTo(6));
        }

        [Test]
        public void PerderSemTerORecurso_NaoFazNada()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(ResourceKind.Gold, 10));

            EffectResult result = new LoseResourceEffect(ResourceKind.Stone, 0.5).Apply(Ctx(bundle.Run));

            Assert.That(result.Applied, Is.False);
            Assert.That(result.Description, Does.Contain("pedra"));
        }

        // --- População (2.2) ---

        [Test]
        public void RecrutarRespeitaOTetoDePopulacao()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(ResourceKind.Population, 1));
            Coord coord = new Coord(1, 0);
            bundle.Run.Grid.Grant(coord);
            bundle.Run.Grid.Build(coord, new BuildingDefinition(
                "house", "Alojamento", new List<TerrainType>(), 0, 0, 0, null, populationCapacity: 3));

            new RecruitEffect(10).Apply(Ctx(bundle.Run));

            Assert.That(bundle.Run[ResourceKind.Population], Is.EqualTo(3),
                "recrutar alem do teto criaria gente sem onde morar");
        }

        [Test]
        public void RecrutarSemEspaco_NaoFazNada()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(ResourceKind.Population, 2));

            EffectResult result = new RecruitEffect(5).Apply(Ctx(bundle.Run));

            Assert.That(result.Applied, Is.False);
            Assert.That(bundle.Run[ResourceKind.Population], Is.EqualTo(2));
        }

        [Test]
        public void RealocarSobeDefesaEDerrubaProducao()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(ResourceKind.Population, 2));

            Coord farm = new Coord(1, 0);
            bundle.Run.Grid.Grant(farm);
            bundle.Run.Grid.SetTerrain(farm, TerrainType.Plain);
            bundle.Run.Grid.Build(farm, new BuildingDefinition(
                "farm", "Fazenda", new List<TerrainType> { TerrainType.Plain }, 0, 0, 0, null,
                production: ResourceAmounts.Of(ResourceKind.Food, 5), workersRequired: 2));

            Coord tower = new Coord(0, 1);
            bundle.Run.Grid.Grant(tower);
            bundle.Run.Grid.Build(tower, new BuildingDefinition(
                "tower", "Torre", new List<TerrainType>(), 0, 0, 8, null, workersRequired: 2));

            new SetStanceEffect(WorkerStance.ProductionFirst).Apply(Ctx(bundle.Run));
            int foodWorking = bundle.Run.CollectDailyProductionByResource()[ResourceKind.Food];
            int defenseWorking = bundle.Run.TotalDefense();

            new SetStanceEffect(WorkerStance.DefenseFirst).Apply(Ctx(bundle.Run));
            int foodGuarding = bundle.Run.CollectDailyProductionByResource()[ResourceKind.Food];
            int defenseGuarding = bundle.Run.TotalDefense();

            Assert.That(defenseGuarding, Is.GreaterThan(defenseWorking));
            Assert.That(foodGuarding, Is.LessThan(foodWorking));
        }

        [Test]
        public void MudarParaAPosturaAtual_NaoFazNada()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(ResourceKind.Population, 2));
            bundle.Run.Stance = WorkerStance.DefenseFirst;

            EffectResult result = new SetStanceEffect(WorkerStance.DefenseFirst).Apply(Ctx(bundle.Run));

            Assert.That(result.Applied, Is.False);
        }

        // --- Teto de severidade por recurso (2.3) ---

        [Test]
        public void EventoNegativo_NaoZeraAPopulacao()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(ResourceKind.Population, 6));

            new LoseResourceEffect(ResourceKind.Population, 1.0)
                .Apply(Ctx(bundle.Run, SeverityBudget.NegativeEvent));

            Assert.That(bundle.Run[ResourceKind.Population], Is.GreaterThan(0),
                "um reino sem gente nao opera nada; zerar seria encerrar a run pela porta dos fundos");
        }

        [Test]
        public void PerdaDePopulacao_RespeitaOTetoDaClasse()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(ResourceKind.Population, 10));

            new LoseResourceEffect(ResourceKind.Population, 0.8)
                .Apply(Ctx(bundle.Run, SeverityBudget.NegativeEvent));

            // Teto de evento negativo: 20% da populacao.
            Assert.That(bundle.Run[ResourceKind.Population], Is.GreaterThanOrEqualTo(8));
        }

        [Test]
        public void CartaNaoTemTetoDePopulacao()
        {
            RunBundle bundle = Run(ResourceAmounts.Of(ResourceKind.Population, 10));

            new LoseResourceEffect(ResourceKind.Population, 0.5)
                .Apply(Ctx(bundle.Run, SeverityBudget.Unlimited));

            Assert.That(bundle.Run[ResourceKind.Population], Is.EqualTo(5),
                "o jogador escolheu jogar a carta; nao ha surpresa a limitar");
        }

        [Test]
        public void CatalogoComPerdaDePopulacaoAlemDoTeto_EReprovado()
        {
            EventDefinition greedy = EventDefinition.Simple(
                "peste", "Peste", EventClass.Negative,
                new List<IEffect> { new LoseResourceEffect(ResourceKind.Population, 0.6) });

            List<CatalogViolation> violations = EventCatalogValidator.Validate(
                new List<EventDefinition> { greedy });

            bool found = false;
            for (int i = 0; i < violations.Count; i++)
            {
                if (violations[i].Rule == "populacao")
                {
                    found = true;
                }
            }

            Assert.That(found, Is.True, string.Join(" | ", violations));
        }

        [Test]
        public void OfertaNaoIsentaPerdaDePopulacao()
        {
            // Preco aceito e isento; gente nao e moeda.
            EventDefinition pact = new EventDefinition(
                "pacto", "Pacto", EventClass.Neutral,
                new List<EventOption>
                {
                    new EventOption("Recusar", new List<IEffect>()),
                    new EventOption(
                        "Aceitar",
                        new List<IEffect> { new LoseResourceEffect(ResourceKind.Population, 0.9) },
                        isOffer: true)
                });

            List<CatalogViolation> violations = EventCatalogValidator.Validate(
                new List<EventDefinition> { pact });

            bool found = false;
            for (int i = 0; i < violations.Count; i++)
            {
                if (violations[i].Rule == "populacao")
                {
                    found = true;
                }
            }

            Assert.That(found, Is.True, "oferta que cobra gente sem teto seria dano com outro nome");
        }

        // --- Clima e comida (2.4) ---

        [Test]
        public void ClimaPodeAumentarOConsumoDeComida()
        {
            RaceDefinition race = new RaceDefinition(
                "humans", "Humanos", 0, 20, 5, 3,
                new List<CardDefinition>(), RuleModifiers.None, null, null, null,
                ResourceAmounts.Of((ResourceKind.Population, 10), (ResourceKind.Food, 100)));

            ContentCatalog catalog = TestContent.Catalog(race);
            catalog.AddWeather(new WeatherDefinition(
                "cold", "Frio", null, productionMultiplier: 0.1, foodUpkeepMultiplier: 1.4));

            RunBundle bundle = RunBuilder.Build(
                new RunSetup(race.Id, 3) { FirstThreatDay = 999 }, catalog);
            bundle.Engine.StartRun();

            Assert.That(bundle.Run.DailyFoodUpkeep(), Is.EqualTo(14));
        }

        [Test]
        public void AvisoDeFomeUsaOClimaPrevisto()
        {
            RaceDefinition race = new RaceDefinition(
                "humans", "Humanos", 0, 20, 5, 3,
                new List<CardDefinition>(), RuleModifiers.None, null, null, null,
                ResourceAmounts.Of((ResourceKind.Population, 10), (ResourceKind.Food, 12)));

            ContentCatalog catalog = TestContent.Catalog(race);
            catalog.AddWeather(new WeatherDefinition(
                "cold", "Frio", null, productionMultiplier: 0.1, foodUpkeepMultiplier: 1.4));

            RunBundle bundle = RunBuilder.Build(
                new RunSetup(race.Id, 3) { FirstThreatDay = 999 }, catalog);
            bundle.Engine.StartRun();

            // 12 de comida cobre o consumo normal de 10, mas nao os 14 do frio.
            List<RunEvent> events = bundle.Engine.DrainEvents();
            FamineWarningEvent warning = null;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i] is FamineWarningEvent found)
                {
                    warning = found;
                }
            }

            Assert.That(warning, Is.Not.Null, "o aviso precisa considerar o clima previsto");
            Assert.That(warning.PredictedUpkeep, Is.EqualTo(14));
        }

        [Test]
        public void ClimaComConsumoAlemDoTeto_EReprovado()
        {
            WeatherDefinition famine = new WeatherDefinition(
                "fome", "Fome", null, productionMultiplier: 0.1, foodUpkeepMultiplier: 2.5);

            List<CatalogViolation> violations = WeatherCatalogValidator.Validate(
                new List<WeatherDefinition> { famine });

            bool found = false;
            for (int i = 0; i < violations.Count; i++)
            {
                if (violations[i].Rule == "consumo de comida")
                {
                    found = true;
                }
            }

            Assert.That(found, Is.True, string.Join(" | ", violations));
        }

        [Test]
        public void ClimaQueBarateiaOConsumo_ContaComoVantagem()
        {
            WeatherDefinition mild = new WeatherDefinition(
                "ameno", "Ameno", null, productionMultiplier: -0.1, foodUpkeepMultiplier: 0.8);

            Assert.That(mild.HasUpside(), Is.True);
            Assert.That(mild.HasDownside(), Is.True);
        }
    }
}

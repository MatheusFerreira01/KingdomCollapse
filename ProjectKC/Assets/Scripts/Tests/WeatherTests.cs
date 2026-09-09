using System.Collections.Generic;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    [TestFixture]
    public class WeatherTests
    {
        private static WeatherDefinition Rain()
        {
            return new WeatherDefinition(
                "rain", "Chuva",
                new Dictionary<TerrainType, int> { { TerrainType.River, 2 }, { TerrainType.Forest, 1 } },
                buildCostMultiplier: 1.25,
                threatDelayDays: 1);
        }

        private static WeatherDefinition Drought()
        {
            return new WeatherDefinition(
                "drought", "Seca",
                new Dictionary<TerrainType, int> { { TerrainType.Mine, 2 }, { TerrainType.Plain, -2 } });
        }

        private static WeatherDefinition Storm()
        {
            return new WeatherDefinition(
                "storm", "Tempestade",
                productionMultiplier: -0.25,
                baseDamage: 1,
                threatForceDelta: -4);
        }

        private static WeatherDefinition Fog()
        {
            return new WeatherDefinition(
                "fog", "Neblina",
                energyDelta: 1,
                blocksPurchase: true,
                blocksReveal: true);
        }

        private static WeatherDefinition CalmDay()
        {
            return new WeatherDefinition("calm", "Dia limpo", isCalmDay: true);
        }

        private static RunBundle RunWith(params WeatherDefinition[] weather)
        {
            RaceDefinition race = TestContent.Humans();
            ContentCatalog catalog = TestContent.Catalog(race);
            for (int i = 0; i < weather.Length; i++)
            {
                catalog.AddWeather(weather[i]);
            }

            RunSetup setup = new RunSetup(race.Id, 4242) { FirstThreatDay = 999 };
            RunBundle bundle = RunBuilder.Build(setup, catalog);
            bundle.Engine.StartRun();
            return bundle;
        }

        [Test]
        public void TodoDiaTemClima()
        {
            RunBundle bundle = RunWith(Rain(), Drought(), CalmDay());

            for (int i = 0; i < 6 && !bundle.Run.IsOver; i++)
            {
                Assert.That(bundle.Run.TodayWeather, Is.Not.Null, "dia " + bundle.Run.Day + " sem clima");
                bundle.Engine.EndDay();
            }
        }

        [Test]
        public void PrevisaoEHonrada()
        {
            RunBundle bundle = RunWith(Rain(), Drought(), Storm(), CalmDay());

            for (int i = 0; i < 8 && !bundle.Run.IsOver; i++)
            {
                WeatherDefinition forecast = bundle.Run.Weather.Tomorrow;
                Assert.That(forecast, Is.Not.Null, "sem previsao no dia " + bundle.Run.Day);

                bundle.Engine.EndDay();

                Assert.That(bundle.Run.TodayWeather, Is.SameAs(forecast),
                    "o clima do dia divergiu da previsao");
            }
        }

        [Test]
        public void PrevisaoDisponivelNoPlanejamento()
        {
            RunBundle bundle = RunWith(Rain(), CalmDay());

            Assert.That(bundle.Run.Phase, Is.EqualTo(DayPhase.Planning));
            Assert.That(bundle.Run.Weather.Tomorrow, Is.Not.Null);
        }

        [Test]
        public void ClimaNaoOcupaOLugarDoEvento()
        {
            EventDefinition gift = EventDefinition.Simple(
                "presente", "Presente", EventClass.Positive,
                new List<IEffect> { new GainGoldEffect(5) }, cooldownDays: 0);

            RaceDefinition race = TestContent.Humans();
            ContentCatalog catalog = TestContent.Catalog(race, gift);
            catalog.AddWeather(CalmDay());

            RunBundle bundle = RunBuilder.Build(new RunSetup(race.Id, 7) { FirstThreatDay = 999 }, catalog);
            bundle.Engine.StartRun();
            bundle.Engine.DrainEvents();
            bundle.Engine.EndDay();

            List<RunEvent> events = bundle.Engine.DrainEvents();
            bool sawWeather = false;
            bool sawEvent = false;

            for (int i = 0; i < events.Count; i++)
            {
                if (events[i] is WeatherChangedEvent)
                {
                    sawWeather = true;
                }

                if (events[i] is DayEventResolvedEvent)
                {
                    sawEvent = true;
                }
            }

            Assert.That(sawWeather, Is.True);
            Assert.That(sawEvent, Is.True, "clima e evento devem coexistir no mesmo dia");
        }

        [Test]
        public void ChuvaFavoreceRioEEncareceObra()
        {
            RunBundle bundle = RunWith(Rain());
            Coord river = new Coord(1, 0);
            bundle.Run.Grid.Grant(river);
            bundle.Run.Grid.SetTerrain(river, TerrainType.River);
            bundle.Run.Grid.Build(river, new BuildingDefinition(
                "docks", "Ancoradouro", new List<TerrainType> { TerrainType.River }, 22, 3, 0));

            ResourceAmounts withRain = bundle.Run.CollectDailyProductionByResource();
            int costWithRain = bundle.Engine.BuildCostOf(TestContent.Farm())[ResourceKind.Gold];

            Assert.That(bundle.Run.TodayWeather.Id, Is.EqualTo("rain"));

            // O clima age no recurso do terreno: rio rende comida, nao ouro.
            Assert.That(withRain[ResourceKind.Food], Is.GreaterThan(0), "rio deve render comida na chuva");
            Assert.That(costWithRain, Is.GreaterThan(TestContent.Farm().GoldCost));
        }

        [Test]
        public void SecaInverteOFavorecimento()
        {
            RunBundle bundle = RunWith(Drought());
            Coord plain = new Coord(1, 0);
            bundle.Run.Grid.Grant(plain);
            bundle.Run.Grid.SetTerrain(plain, TerrainType.Plain);
            bundle.Run.Grid.Build(plain, TestContent.Farm());

            List<ProductionBreakdown> lines = new List<ProductionBreakdown>();
            bundle.Run.CollectDailyProduction(lines);

            ProductionBreakdown farmTile = lines.Find(b => b.Coord == plain);
            Assert.That(farmTile, Is.Not.Null);

            // Seca castiga a planicie na comida, que e o recurso daquele terreno.
            Assert.That(farmTile[ResourceKind.Food], Is.LessThan(0));
        }

        [Test]
        public void TempestadeEnfraqueceAHordaAntesDoDia()
        {
            RaceDefinition race = TestContent.Humans();
            ContentCatalog catalog = TestContent.Catalog(race);
            catalog.AddWeather(Storm());

            RunBundle bundle = RunBuilder.Build(
                new RunSetup(race.Id, 11) { FirstThreatDay = 4, ThreatLeadDays = 3 }, catalog);
            bundle.Engine.StartRun();

            ScheduledThreat next = bundle.Run.Threats.NextThreat(bundle.Run.Day);
            Assert.That(next, Is.Not.Null);

            int announcedForce = next.Force;
            int arrivalDay = next.ArrivalDay;

            // Avanca ate o dia da chegada, guardando a ultima forca exibida.
            while (!bundle.Run.IsOver && bundle.Run.Day < arrivalDay)
            {
                ScheduledThreat pending = bundle.Run.Threats.NextThreat(bundle.Run.Day);
                if (pending != null && pending.ArrivalDay == arrivalDay)
                {
                    announcedForce = pending.Force;
                }

                bundle.Engine.DrainEvents();
                bundle.Engine.EndDay();
            }

            List<RunEvent> events = bundle.Engine.DrainEvents();
            AttackReport report = null;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i] is AttackResolvedEvent attack)
                {
                    report = attack.Report;
                }
            }

            if (report != null)
            {
                Assert.That(report.Force, Is.EqualTo(announcedForce),
                    "a forca resolvida deve ser a ultima forca exibida no relogio");
            }
        }

        [Test]
        public void ChuvaAdiaAChegada()
        {
            ThreatClock clock = new ThreatClock(firstThreatDay: 4);
            clock.AnnounceDue(1, 3, new XorShiftRandom(5));
            ScheduledThreat threat = clock.NextThreat(1);
            int before = threat.ArrivalDay;

            int applied = clock.Delay(threat, 2);

            Assert.That(applied, Is.EqualTo(2));
            Assert.That(clock.NextThreat(1).ArrivalDay, Is.EqualTo(before + 2));
        }

        [Test]
        public void AdiamentoNuncaAntecipa()
        {
            ThreatClock clock = new ThreatClock(firstThreatDay: 4);
            clock.AnnounceDue(1, 3, new XorShiftRandom(5));
            ScheduledThreat threat = clock.NextThreat(1);
            int before = threat.ArrivalDay;

            Assert.That(clock.Delay(threat, -3), Is.Zero);
            Assert.That(threat.ArrivalDay, Is.EqualTo(before));
        }

        [Test]
        public void NeblinaImpedeExpandir()
        {
            RunBundle bundle = RunWith(Fog());

            CommandResult result = bundle.Engine.BuyTile(new Coord(1, 0));

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.WeatherBlocked));
            Assert.That(bundle.Run.Grid.OwnedCount, Is.EqualTo(1));
        }

        [Test]
        public void NeblinaImpedeRevelar()
        {
            RunBundle bundle = RunWith(Fog());
            Coord far = new Coord(0, 4);

            EffectResult result = new RevealTileEffect()
                .Apply(new EffectContext(bundle.Run, EffectSource.Card, far));

            Assert.That(result.Applied, Is.False);
            Assert.That(bundle.Run.Grid.TileAt(far).Revealed, Is.False);
        }

        [Test]
        public void ClimaNuncaEncerraARun()
        {
            RunBundle bundle = RunWith(Storm());
            bundle.Run.Damage(bundle.Run.Integrity - 1);

            for (int i = 0; i < 10 && !bundle.Run.IsOver; i++)
            {
                bundle.Engine.EndDay();
            }

            Assert.That(bundle.Run.Integrity, Is.GreaterThanOrEqualTo(1));
            Assert.That(bundle.Run.Collapse, Is.Not.EqualTo(CollapseReason.IntegrityLost));
        }

        [Test]
        public void ClimaNuncaArrasaTerritorio()
        {
            RunBundle bundle = RunWith(Storm(), Rain(), Fog(), Drought());
            TestContent.GrantTiles(bundle.Run.Grid, new Coord(1, 0), new Coord(0, 1));

            for (int i = 0; i < 10 && !bundle.Run.IsOver; i++)
            {
                bundle.Engine.EndDay();
            }

            foreach (Tile tile in bundle.Run.Grid.OwnedTiles())
            {
                Assert.That(tile.Destroyed, Is.False, tile.Coord + " foi arrasada pelo clima");
            }
        }

        [Test]
        public void RunSemCatalogoDeClima_Funciona()
        {
            RunBundle bundle = TestContent.Run();

            Assert.That(bundle.Run.Weather.HasWeather, Is.False);
            Assert.That(bundle.Run.TodayWeather, Is.Null);
            Assert.That(bundle.Engine.EndDay().Ok, Is.True);
        }

        // --- Validacao de catalogo ---

        [Test]
        public void CatalogoDeClimaValido_NaoTemViolacao()
        {
            List<CatalogViolation> violations = WeatherCatalogValidator.Validate(
                new List<WeatherDefinition> { Rain(), Drought(), Storm(), Fog(), CalmDay() });

            Assert.That(violations, Is.Empty, string.Join(" | ", violations));
        }

        [Test]
        public void ClimaSoBom_EReprovado()
        {
            WeatherDefinition tooKind = new WeatherDefinition(
                "bonanca", "Bonanca", productionMultiplier: 0.3);

            List<CatalogViolation> violations = WeatherCatalogValidator.Validate(
                new List<WeatherDefinition> { tooKind });

            Assert.That(violations.Count, Is.EqualTo(1));
            Assert.That(violations[0].Rule, Is.EqualTo("dois lados"));
        }

        [Test]
        public void ClimaSoRuim_EReprovado()
        {
            WeatherDefinition tooCruel = new WeatherDefinition(
                "praga", "Praga", productionMultiplier: -0.3, baseDamage: 2);

            List<CatalogViolation> violations = WeatherCatalogValidator.Validate(
                new List<WeatherDefinition> { tooCruel });

            Assert.That(violations.Count, Is.EqualTo(1));
            Assert.That(violations[0].Rule, Is.EqualTo("dois lados"));
        }

        [Test]
        public void DiaLimpo_EIsentoDeTerOsDoisLados()
        {
            List<CatalogViolation> violations = WeatherCatalogValidator.Validate(
                new List<WeatherDefinition> { CalmDay() });

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void ClimaAcimaDoTetoDeProducao_EReprovado()
        {
            WeatherDefinition extreme = new WeatherDefinition(
                "apocalipse", "Apocalipse", productionMultiplier: -0.9, energyDelta: 1);

            List<CatalogViolation> violations = WeatherCatalogValidator.Validate(
                new List<WeatherDefinition> { extreme });

            bool found = false;
            for (int i = 0; i < violations.Count; i++)
            {
                if (violations[i].Rule == "producao")
                {
                    found = true;
                }
            }

            Assert.That(found, Is.True, string.Join(" | ", violations));
        }

        [Test]
        public void ClimaQueAntecipaAtaque_EReprovado()
        {
            WeatherDefinition rushing = new WeatherDefinition(
                "pressa", "Pressa", productionMultiplier: 0.1, threatDelayDays: -2);

            List<CatalogViolation> violations = WeatherCatalogValidator.Validate(
                new List<WeatherDefinition> { rushing });

            bool found = false;
            for (int i = 0; i < violations.Count; i++)
            {
                if (violations[i].Rule == "adiamento")
                {
                    found = true;
                }
            }

            Assert.That(found, Is.True, "clima nao pode encurtar o aviso de uma ameaca");
        }
    }
}

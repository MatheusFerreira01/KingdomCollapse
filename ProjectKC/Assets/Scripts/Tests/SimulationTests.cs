using System.Collections.Generic;
using System.Diagnostics;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    [TestFixture]
    public class SimulationTests
    {
        private static ContentCatalog SimCatalog()
        {
            RaceDefinition humans = TestContent.Humans();
            ContentCatalog catalog = TestContent.Catalog(humans);

            catalog.AddCard(TestContent.GoldCard("moeda", 1, 12), unlockedFromStart: true);
            catalog.AddEvent(
                EventDefinition.Simple(
                    "caravana", "Caravana", EventClass.Positive,
                    new List<IEffect> { new GainGoldEffect(10) }, cooldownDays: 2),
                unlockedFromStart: true);
            catalog.AddEvent(
                EventDefinition.Simple(
                    "imposto", "Imposto", EventClass.Negative,
                    new List<IEffect> { new LoseGoldEffect(0.15) }, cooldownDays: 2),
                unlockedFromStart: true);

            return catalog;
        }

        private static RunSetup Setup(int seed)
        {
            return new RunSetup("humans", seed);
        }

        [Test]
        public void UmaRunSimulada_TerminaEmColapso()
        {
            SimulationResult result = SimulationHarness.RunOnce(Setup(1), SimCatalog());

            Assert.That(result.Reason, Is.Not.EqualTo(CollapseReason.None));
            Assert.That(result.CollapseDay, Is.GreaterThan(0));
            Assert.That(result.CollapseDay, Is.LessThan(SimulationHarness.MaxDays));
            Assert.That(result.Score.MetaCurrency, Is.GreaterThan(0));
        }

        [Test]
        public void MesmaSemente_ProduzResultadoIdentico()
        {
            SimulationResult first = SimulationHarness.RunOnce(Setup(4242), SimCatalog());
            SimulationResult second = SimulationHarness.RunOnce(Setup(4242), SimCatalog());

            Assert.That(second.CollapseDay, Is.EqualTo(first.CollapseDay));
            Assert.That(second.Reason, Is.EqualTo(first.Reason));
            Assert.That(second.Score.TotalPoints, Is.EqualTo(first.Score.TotalPoints));
        }

        [Test]
        public void SementesDiferentes_ProduzemRunsDiferentes()
        {
            SimulationReport report = SimulationHarness.RunBatch(30, Setup, SimCatalog());

            Assert.That(report.Min, Is.LessThan(report.Max), "todas as runs deram exatamente o mesmo dia");
        }

        [Test]
        public void MilRuns_RodamEmSegundosSemAbrirCena()
        {
            ContentCatalog catalog = SimCatalog();
            Stopwatch stopwatch = Stopwatch.StartNew();

            SimulationReport report = SimulationHarness.RunBatch(1000, Setup, catalog);

            stopwatch.Stop();

            Assert.That(report.Count, Is.EqualTo(1000));
            Assert.That(stopwatch.Elapsed.TotalSeconds, Is.LessThan(20),
                "1000 runs levaram " + stopwatch.Elapsed.TotalSeconds.ToString("0.0") + "s");
            TestContext.WriteLine(report.Describe());
            TestContext.WriteLine("Tempo: " + stopwatch.Elapsed.TotalSeconds.ToString("0.00") + "s");
        }

        [Test]
        public void RelatorioDescreveADistribuicao()
        {
            SimulationReport report = SimulationHarness.RunBatch(50, Setup, SimCatalog());

            Assert.That(report.Median, Is.GreaterThanOrEqualTo(report.Min));
            Assert.That(report.Median, Is.LessThanOrEqualTo(report.Max));
            Assert.That(report.Percentile(0.25), Is.LessThanOrEqualTo(report.Median));
            Assert.That(report.Percentile(0.75), Is.GreaterThanOrEqualTo(report.Median));
            Assert.That(report.FractionWithin(0, 999), Is.EqualTo(1.0).Within(0.001));
            Assert.That(report.Describe(), Does.Contain("mediana"));
        }

        [Test]
        public void CurvaMaisAgressiva_EncurtaAsRuns()
        {
            // Verifica que o harness reage a mudanca de curva: sem isso ele nao serve
            // para balancear.
            ContentCatalog catalog = SimCatalog();

            SimulationReport gentle = SimulationHarness.RunBatch(
                60, seed => new RunSetup("humans", seed) { ThreatCurve = new ThreatCurve(2, 0.8, 1.1, 0.7) }, catalog);

            SimulationReport brutal = SimulationHarness.RunBatch(
                60, seed => new RunSetup("humans", seed) { ThreatCurve = new ThreatCurve(10, 4.0, 1.5, 3.0) }, catalog);

            Assert.That(brutal.Median, Is.LessThan(gentle.Median),
                "gentil=" + gentle.Median + " brutal=" + brutal.Median);
        }

        [Test]
        public void PoliticaMaisConservadora_MudaOResultado()
        {
            ContentCatalog catalog = SimCatalog();

            SimulationReport eager = SimulationHarness.RunBatch(
                40, Setup, catalog, new GreedyPolicy(1.0));
            SimulationReport cautious = SimulationHarness.RunBatch(
                40, Setup, catalog, new GreedyPolicy(6.0));

            Assert.That(cautious.Mean, Is.Not.EqualTo(eager.Mean),
                "as duas politicas deveriam produzir distribuicoes distintas");
        }
    }
}

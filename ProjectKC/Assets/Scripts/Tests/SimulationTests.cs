using System.Collections.Generic;
using System.Diagnostics;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    [TestFixture]
    public class SimulationTests
    {
        /// <summary>
        /// Catalogo do simulador (task 11.1): espelha o conteudo real do jogo
        /// (BalanceContent, que reflete o ContentSeeder) em vez do minimo de regra
        /// que TestContent.Catalog oferece — sem isso o diagnostico mede um jogo que
        /// ninguem joga e acusa "madeira parada" mesmo com serraria no catalogo real.
        /// </summary>
        private static ContentCatalog SimCatalog()
        {
            return BalanceContent.Catalog();
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
                60, seed => new RunSetup("humans", seed) { ThreatCurve = new ThreatCurve(2, 0.8, 1.1) }, catalog);

            SimulationReport brutal = SimulationHarness.RunBatch(
                60, seed => new RunSetup("humans", seed) { ThreatCurve = new ThreatCurve(10, 4.0, 1.5) }, catalog);

            Assert.That(brutal.Median, Is.LessThan(gentle.Median),
                "gentil=" + gentle.Median + " brutal=" + brutal.Median);
        }

        // --- 11.2/11.3: nivel 1, os 3 rivais reais ---

        private static RunSetup Level1Setup(int seed)
        {
            return new RunSetup("humans", seed)
            {
                FirstThreatDay = 999, // desliga a curva anonima: so a campanha dos rivais ameaca
                Rivals = BalanceContent.Level1Rivals(),
                CampaignIntervalDays = 4,
                CampaignRestDays = 3,
                ThreatLeadDays = 3
            };
        }

        [Test]
        public void Nivel1_TaxaDeVitoria()
        {
            SimulationReport report = SimulationHarness.RunBatch(1000, Level1Setup, SimCatalog());

            TestContext.WriteLine(report.Describe());
            TestContext.WriteLine("Taxa de vitoria nivel 1: " + (report.VictoryFraction * 100).ToString("0.0") + "%");
        }

        /// <summary>
        /// Task 11.3: um bot que so ergue defesa (torre/posto, sem fazenda/mina/
        /// serraria/ancoradouro) precisa perder mais para o rival economico do que um
        /// bot com a economia completa — prova que os tres rivais exigem respostas
        /// diferentes, e nao so "mais defesa resolve tudo".
        /// </summary>
        [Test]
        public void RivalEconomico_SoDefesaPerdeMaisQueEconomiaCompleta()
        {
            List<RivalDefinition> saboteurOnly = new List<RivalDefinition>
            {
                new RivalDefinition(
                    "Rival_Saboteur", "Sabotadores",
                    new RivalIdentity("Rival_Saboteur", "Sabotadores", RivalAxis.Resource, ResourceKind.Food),
                    5, new ThreatCurve(baseForce: 3, perDay: 1.0, dayExponent: 1.2))
            };

            RunSetup Setup(int seed) => new RunSetup("humans", seed)
            {
                FirstThreatDay = 999,
                Rivals = saboteurOnly,
                CampaignIntervalDays = 4,
                CampaignRestDays = 3,
                ThreatLeadDays = 3
            };

            SimulationReport fullEconomy = SimulationHarness.RunBatch(300, Setup, BalanceContent.Catalog());
            SimulationReport towersOnly = SimulationHarness.RunBatch(300, Setup, BalanceContent.TowersOnlyCatalog());

            TestContext.WriteLine("Economia completa: " + fullEconomy.Describe());
            TestContext.WriteLine("So torres: " + towersOnly.Describe());

            // Nenhum dos dois necessariamente vence a campanha (ela nao e o alvo
            // deste teste) — o que prova a resposta diferente e quanto tempo cada um
            // aguenta: so defesa fica sem comida e colapsa cedo, sempre, enquanto a
            // economia completa aguenta muito mais.
            Assert.That(towersOnly.Median, Is.LessThan(fullEconomy.Median),
                "so defesa deveria sobreviver bem menos tempo contra o rival economico do que " +
                "a economia completa (cheio=dia " + fullEconomy.Median + " torres=dia " + towersOnly.Median + ")");
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

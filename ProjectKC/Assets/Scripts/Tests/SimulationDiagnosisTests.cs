using System.Collections.Generic;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    /// <summary>
    /// O simulador precisa distinguir "curva mal ajustada" de "conteudo faltando".
    /// Estes testes fixam esse contrato: sem eles a ferramenta manda afrouxar a
    /// economia quando o problema real e nao existir defesa no jogo.
    /// </summary>
    [TestFixture]
    public class SimulationDiagnosisTests
    {
        private static RaceDefinition Race() => TestContent.Humans();

        /// <summary>Catalogo sem nenhuma fonte de defesa alem do Salao.</summary>
        private static ContentCatalog CatalogWithoutDefense()
        {
            RaceDefinition race = Race();
            ContentCatalog catalog = new ContentCatalog();
            catalog.AddBuilding(TestContent.Hall(), true);
            catalog.AddBuilding(TestContent.Farm(), true);
            catalog.AddRace(race, true);
            return catalog;
        }

        private static ContentCatalog CatalogWithDefense()
        {
            ContentCatalog catalog = CatalogWithoutDefense();
            catalog.AddBuilding(TestContent.Watchtower(), true);
            catalog.AddCard(TestContent.GoldCard("moeda", 1, 15), true);
            return catalog;
        }

        private static RunSetup Setup(int seed) => new RunSetup("humans", seed);

        [Test]
        public void SemFonteDeDefesa_AcusaDefesaConstante()
        {
            SimulationReport report = SimulationHarness.RunBatch(40, Setup, CatalogWithoutDefense());

            List<string> warnings = report.Diagnose();

            Assert.That(Joined(warnings), Does.Contain("DEFESA CONSTANTE"));
        }

        [Test]
        public void SemCartas_AcusaEconomiaDeAcoesInerte()
        {
            SimulationReport report = SimulationHarness.RunBatch(40, Setup, CatalogWithoutDefense());

            Assert.That(Joined(report.Diagnose()), Does.Contain("NENHUMA CARTA JOGADA"));
        }

        [Test]
        public void ComTorreDisponivel_NaoAcusaDefesaConstante()
        {
            SimulationReport report = SimulationHarness.RunBatch(40, Setup, CatalogWithDefense());

            Assert.That(Joined(report.Diagnose()), Does.Not.Contain("DEFESA CONSTANTE"));
        }

        [Test]
        public void DistribuicaoSemVariacao_EAcusada()
        {
            SimulationReport report = SimulationHarness.RunBatch(40, Setup, CatalogWithoutDefense());

            if (report.Min == report.Max)
            {
                Assert.That(Joined(report.Diagnose()), Does.Contain("ZERO VARIACAO"));
            }
            else
            {
                Assert.That(Joined(report.Diagnose()), Does.Not.Contain("ZERO VARIACAO"));
            }
        }

        [Test]
        public void DiagnosticoAparaceNoRelatorioDeTexto()
        {
            SimulationReport report = SimulationHarness.RunBatch(20, Setup, CatalogWithoutDefense());

            Assert.That(report.Describe(), Does.Contain("DIAGNOSTICO ESTRUTURAL"));
        }

        [Test]
        public void RelatorioVazio_NaoDiagnosticaNada()
        {
            SimulationReport report = new SimulationReport(new List<SimulationResult>());

            Assert.That(report.Diagnose(), Is.Empty);
        }

        [Test]
        public void ResultadoRegistraDefesaEConstrucoes()
        {
            SimulationResult result = SimulationHarness.RunOnce(Setup(7), CatalogWithDefense());

            Assert.That(result.PeakDefense, Is.GreaterThan(0));
            Assert.That(result.BuildingsBuilt, Is.GreaterThanOrEqualTo(0));
            Assert.That(result.GoldAtEnd, Is.GreaterThanOrEqualTo(0));
        }

        private static string Joined(List<string> warnings)
        {
            return string.Join(" | ", warnings);
        }
    }
}

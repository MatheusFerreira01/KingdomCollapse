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

        // --- 7.4 Diagnostico dos recursos novos ---

        /// <summary>Constroi um SimulationResult direto, sem rodar simulacao — pra
        /// testar o texto do diagnostico com valores exatos em vez de depender do
        /// bot chegar la sozinho.</summary>
        private static SimulationResult Result(
            int woodAtEnd = 0, int stoneAtEnd = 0, int peakIdleBuildings = 0,
            int peakUnassignedPopulation = 0, bool foodEverNearLimit = false)
        {
            return new SimulationResult(
                seed: 1, collapseDay: 10, reason: CollapseReason.IntegrityLost, outcome: RunOutcome.Collapse,
                score: new RunScore(), tilesOwned: 4, peakDefense: 5, buildingsBuilt: 2, goldAtEnd: 0,
                cardsPlayed: 3, woodAtEnd: woodAtEnd, stoneAtEnd: stoneAtEnd,
                peakIdleBuildings: peakIdleBuildings, peakUnassignedPopulation: peakUnassignedPopulation,
                foodEverNearLimit: foodEverNearLimit);
        }

        [Test]
        public void MadeiraParada_ApareceQuandoNenhumaRunProduzMadeira()
        {
            SimulationReport report = new SimulationReport(new List<SimulationResult> { Result(), Result(), Result() });

            Assert.That(Joined(report.Diagnose()), Does.Contain("MADEIRA PARADA"));
        }

        [Test]
        public void MadeiraParada_SomeQuandoAlgumaRunTemMadeiraDiferente()
        {
            SimulationReport report = new SimulationReport(new List<SimulationResult>
            {
                Result(woodAtEnd: 0), Result(woodAtEnd: 12), Result(woodAtEnd: 3)
            });

            Assert.That(Joined(report.Diagnose()), Does.Not.Contain("MADEIRA PARADA"));
        }

        [Test]
        public void PedraParada_ApareceQuandoNenhumaRunProduzPedra()
        {
            SimulationReport report = new SimulationReport(new List<SimulationResult> { Result(), Result() });

            Assert.That(Joined(report.Diagnose()), Does.Contain("PEDRA PARADA"));
        }

        [Test]
        public void PedraParada_SomeQuandoAlgumaRunTemPedraDiferente()
        {
            SimulationReport report = new SimulationReport(new List<SimulationResult>
            {
                Result(stoneAtEnd: 0), Result(stoneAtEnd: 7)
            });

            Assert.That(Joined(report.Diagnose()), Does.Not.Contain("PEDRA PARADA"));
        }

        [Test]
        public void EdificioSemGente_ApareceQuandoOciosidadeEPersistente()
        {
            SimulationReport report = new SimulationReport(new List<SimulationResult>
            {
                Result(peakIdleBuildings: 3), Result(peakIdleBuildings: 2)
            });

            Assert.That(Joined(report.Diagnose()), Does.Contain("EDIFICIO SEM GENTE"));
        }

        [Test]
        public void EdificioSemGente_SomeQuandoNaoHaOciosidade()
        {
            SimulationReport report = new SimulationReport(new List<SimulationResult>
            {
                Result(peakIdleBuildings: 0), Result(peakIdleBuildings: 0)
            });

            Assert.That(Joined(report.Diagnose()), Does.Not.Contain("EDIFICIO SEM GENTE"));
        }

        [Test]
        public void PopulacaoOciosa_ApareceQuandoSobraGenteSemPosto()
        {
            SimulationReport report = new SimulationReport(new List<SimulationResult>
            {
                Result(peakUnassignedPopulation: 5), Result(peakUnassignedPopulation: 4)
            });

            Assert.That(Joined(report.Diagnose()), Does.Contain("POPULACAO OCIOSA"));
        }

        [Test]
        public void PopulacaoOciosa_SomeQuandoTodaGenteTemPosto()
        {
            SimulationReport report = new SimulationReport(new List<SimulationResult>
            {
                Result(peakUnassignedPopulation: 0), Result(peakUnassignedPopulation: 0)
            });

            Assert.That(Joined(report.Diagnose()), Does.Not.Contain("POPULACAO OCIOSA"));
        }

        [Test]
        public void ComidaNoLimite_ApareceQuandoAMaioriaDasRunsChegaPerto()
        {
            SimulationReport report = new SimulationReport(new List<SimulationResult>
            {
                Result(foodEverNearLimit: true), Result(foodEverNearLimit: true), Result(foodEverNearLimit: false)
            });

            Assert.That(Joined(report.Diagnose()), Does.Contain("COMIDA NO LIMITE"));
        }

        [Test]
        public void ComidaNoLimite_SomeQuandoOEstoqueFolga()
        {
            SimulationReport report = new SimulationReport(new List<SimulationResult>
            {
                Result(foodEverNearLimit: false), Result(foodEverNearLimit: false)
            });

            Assert.That(Joined(report.Diagnose()), Does.Not.Contain("COMIDA NO LIMITE"));
        }

        // --- 7.5 Metrica alvo: fracao de vitorias ---

        [Test]
        public void VictoryFraction_ContaSoRunsVencidas()
        {
            RunScore score = new RunScore();
            SimulationResult victory = new SimulationResult(
                1, 20, CollapseReason.None, RunOutcome.Victory, score, 4, 5, 2, 0, 3, 0, 0, 0, 0, false);
            SimulationResult collapse = new SimulationResult(
                2, 15, CollapseReason.IntegrityLost, RunOutcome.Collapse, score, 4, 5, 2, 0, 3, 0, 0, 0, 0, false);

            SimulationReport report = new SimulationReport(new List<SimulationResult> { victory, collapse });

            Assert.That(report.VictoryFraction, Is.EqualTo(0.5));
            Assert.That(report.MedianVictoryDuration(), Is.EqualTo(20));
            Assert.That(report.Describe(), Does.Contain("Vitorias:"));
        }
    }
}

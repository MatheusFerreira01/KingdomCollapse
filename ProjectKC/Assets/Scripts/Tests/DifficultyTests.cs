using System.Collections.Generic;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    [TestFixture]
    public class DifficultyTests
    {
        private static RivalIdentity Arson() =>
            new RivalIdentity("arson", "Incendio", "Queima a base.", PressureAxis.Integrity);

        private static DifficultyLevel Level(
            int rank,
            int rivals = 2,
            int firstForce = 6,
            IReadOnlyList<int> entryDays = null,
            double scarcity = 1.0,
            string hardening = "Mais duro.")
        {
            RivalIdentity[] identities = { TestContent.AssaultIdentity(), TestContent.FoodRaidIdentity(), Arson() };
            List<RivalDefinition> list = new List<RivalDefinition>();
            for (int i = 0; i < rivals; i++)
            {
                list.Add(TestContent.Rival("r" + rank + "_" + i, identities[i % identities.Length],
                    attacks: 3, firstForce: firstForce + i * 10, growth: 3));
            }

            return new DifficultyLevel("nivel" + rank, rank, "Nivel " + rank, rank == 1 ? "" : hardening,
                list, entryDays, scarcity);
        }

        private static DifficultyLadder Ladder(params DifficultyLevel[] levels) => new DifficultyLadder(levels);

        // --- Nivel e selecao (5.1) ---

        [Test]
        public void PerfilNovoSoTemOPrimeiroNivel()
        {
            DifficultyLadder ladder = Ladder(Level(1), Level(2, firstForce: 9), Level(3, firstForce: 12));

            List<DifficultyLevel> available = ladder.Available(MetaProfile.NewProfile());

            Assert.That(available.Count, Is.EqualTo(1));
            Assert.That(available[0].Rank, Is.EqualTo(1));
            Assert.That(ladder.CanStart(MetaProfile.NewProfile(), 2), Is.False);
        }

        [Test]
        public void QualquerNivelJaLiberadoPodeSerEscolhido()
        {
            DifficultyLadder ladder = Ladder(Level(1), Level(2, firstForce: 9), Level(3, firstForce: 12));
            MetaProfile profile = MetaProfile.NewProfile();
            profile.HighestUnlockedLevel = 3;

            Assert.That(ladder.Available(profile).Count, Is.EqualTo(3));
            Assert.That(ladder.CanStart(profile, 1), Is.True);
            Assert.That(ladder.CanStart(profile, 4), Is.False, "nivel que nao existe nao abre");
        }

        [Test]
        public void NivelDecideOsRivaisDaRun()
        {
            DifficultyLevel level = Level(1, rivals: 3, entryDays: new[] { 1, 6, 11 });
            RunSetup setup = new RunSetup("humans", 5) { Level = level, FirstThreatDay = 4 };

            RunBundle bundle = RunBuilder.Build(setup, TestContent.Catalog());

            Assert.That(bundle.Run.Level, Is.SameAs(level));
            Assert.That(bundle.Run.Threats.Rivals, Is.EqualTo(level.Rivals));
            Assert.That(bundle.Run.Threats.EntryDayOf(1), Is.EqualTo(6));
            Assert.That(bundle.Run.Threats.EntryDayOf(2), Is.EqualTo(11));
        }

        [Test]
        public void EscassezCortaRecursosMasNaoPopulacao()
        {
            RaceDefinition race = new RaceDefinition(
                "humans", "Humanos", 100, 20, 5, 3,
                startingResources: new ResourceAmounts { [ResourceKind.Wood] = 50, [ResourceKind.Population] = 10 });
            RunSetup normal = new RunSetup("humans", 5) { Level = Level(1) };
            RunSetup scarce = new RunSetup("humans", 5) { Level = Level(2, scarcity: 0.5) };

            RunState a = RunBuilder.Build(normal, TestContent.Catalog(race)).Run;
            RunState b = RunBuilder.Build(scarce, TestContent.Catalog(race)).Run;

            Assert.That(b[ResourceKind.Gold], Is.EqualTo(a[ResourceKind.Gold] / 2));
            Assert.That(b[ResourceKind.Wood], Is.EqualTo(a[ResourceKind.Wood] / 2));
            Assert.That(b[ResourceKind.Population], Is.EqualTo(a[ResourceKind.Population]));
        }

        [Test]
        public void DificuldadeMaiorValeMais()
        {
            RunState easy = RunBuilder.Build(new RunSetup("humans", 5) { Level = Level(1) }, TestContent.Catalog()).Run;
            RunState hard = RunBuilder.Build(new RunSetup("humans", 5) { Level = Level(3) }, TestContent.Catalog()).Run;
            easy.Stats.DaysSurvived = hard.Stats.DaysSurvived = 20;
            easy.Stats.RivalsDefeated = hard.Stats.RivalsDefeated = 1;

            RunScore easyScore = ScoreCalculator.Calculate(easy);
            RunScore hardScore = ScoreCalculator.Calculate(hard);

            Assert.That(hardScore.TotalPoints, Is.GreaterThan(easyScore.TotalPoints));
            Assert.That(hardScore.LevelRank, Is.EqualTo(3));
        }

        // --- Desbloqueio por Vitoria (5.2) ---

        private static RunScore Finished(RunOutcome outcome, int levelRank, int points = 500)
        {
            RunState run = RunBuilder.Build(
                new RunSetup("humans", 5) { Level = Level(levelRank) }, TestContent.Catalog()).Run;
            run.Stats.DaysSurvived = points / ScoreCalculator.PointsPerDay;

            if (outcome == RunOutcome.Victory)
            {
                run.DeclareVictory();
            }
            else
            {
                run.Damage(run.Integrity);
                run.CheckCollapse();
            }

            return ScoreCalculator.Calculate(run);
        }

        [Test]
        public void VencerAbreODegrauSeguinte()
        {
            MetaProfile profile = MetaProfile.NewProfile();
            profile.HighestUnlockedLevel = 2;

            profile.RecordRun(Finished(RunOutcome.Victory, 2), 20);

            Assert.That(profile.HighestUnlockedLevel, Is.EqualTo(3));
        }

        [Test]
        public void ColapsarNaoAbreNadaMesmoComPlacarAlto()
        {
            MetaProfile profile = MetaProfile.NewProfile();

            profile.RecordRun(Finished(RunOutcome.Collapse, 1, points: 100000), 20);

            Assert.That(profile.HighestUnlockedLevel, Is.EqualTo(1));
        }

        [Test]
        public void RegistroDizQualNivelAcabouDeAbrir()
        {
            // A tela de fim precisa nomear o desbloqueio (spec game-feel), e so quem
            // registra a run sabe se ela abriu algo novo.
            MetaProfile profile = MetaProfile.NewProfile();

            Assert.That(profile.RecordRun(Finished(RunOutcome.Collapse, 1), 20), Is.EqualTo(0));
            Assert.That(profile.RecordRun(Finished(RunOutcome.Victory, 1), 20), Is.EqualTo(2));
            Assert.That(profile.RecordRun(Finished(RunOutcome.Victory, 1), 20), Is.EqualTo(0),
                "vencer de novo o mesmo nivel nao abre nada novo");
        }

        [Test]
        public void VencerNivelBaixoNaoRebaixaODesbloqueio()
        {
            MetaProfile profile = MetaProfile.NewProfile();
            profile.HighestUnlockedLevel = 4;

            profile.RecordRun(Finished(RunOutcome.Victory, 1), 20);

            Assert.That(profile.HighestUnlockedLevel, Is.EqualTo(4));
        }

        [Test]
        public void DesbloqueioSobreviveASerializar()
        {
            MetaProfile profile = MetaProfile.NewProfile();
            profile.RecordRun(Finished(RunOutcome.Victory, 1), 20);

            MetaProfile restored = ProfileCodec.Deserialize(ProfileCodec.Serialize(profile));

            Assert.That(restored.HighestUnlockedLevel, Is.EqualTo(2));
            Assert.That(restored.Version, Is.EqualTo(MetaProfile.CurrentVersion));
        }

        [Test]
        public void SaveDaVersao1AbreNoPrimeiroNivelSemPerderNada()
        {
            const string v1 = "{\"version\":1,\"currency\":42,\"runsPlayed\":3,\"bestDay\":12,\"bestScore\":300,\"nodes\":[\"a\"]}";

            MetaProfile profile = ProfileCodec.Deserialize(v1);

            Assert.That(profile.HighestUnlockedLevel, Is.EqualTo(1));
            Assert.That(profile.Currency, Is.EqualTo(42));
            Assert.That(profile.HasNode("a"), Is.True);
        }

        // --- Escada monotona (5.3) ---

        [Test]
        public void EscadaBemFormadaPassa()
        {
            DifficultyLadder ladder = Ladder(
                Level(1, rivals: 2),
                Level(2, rivals: 3),
                Level(3, rivals: 3, entryDays: new[] { 1, 5, 10 }, scarcity: 0.8));

            List<CatalogViolation> violations = DifficultyLadderValidator.Validate(ladder);

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void DegrauQueNaoEndureceNadaReprova()
        {
            List<CatalogViolation> violations = DifficultyLadderValidator.Validate(Ladder(Level(1), Level(2)));

            Assert.That(violations.Exists(v => v.SubjectId == "nivel2" && v.Detail.Contains("nao endurece")), Is.True);
        }

        [Test]
        public void DegrauMaisFacilEmAlgumEixoReprovaMesmoEndurecendoOutro()
        {
            // Mais forte nas campanhas, mas com um rival a menos.
            List<CatalogViolation> violations = DifficultyLadderValidator.Validate(
                Ladder(Level(1, rivals: 3), Level(2, rivals: 2, firstForce: 40)));

            Assert.That(violations.Exists(v => v.Detail.Contains("mais facil") && v.Detail.Contains("numero de rivais")), Is.True);
        }

        [Test]
        public void EntradaMaisTardiaEDegrauMaisFacil()
        {
            List<CatalogViolation> violations = DifficultyLadderValidator.Validate(Ladder(
                Level(1, rivals: 2, entryDays: new[] { 1, 6 }),
                Level(2, rivals: 2, firstForce: 9, entryDays: new[] { 1, 12 })));

            Assert.That(violations.Exists(v => v.Detail.Contains("ritmo de entrada")), Is.True);
        }

        [Test]
        public void MaisEscassezEDegrauMaisDuro()
        {
            List<CatalogViolation> violations = DifficultyLadderValidator.Validate(
                Ladder(Level(1), Level(2, scarcity: 0.7)));

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void NivelSoDeAssaltoReprovaMesmoComCatalogoEconomico()
        {
            DifficultyLevel assaultOnly = new DifficultyLevel("so-assalto", 1, "So assalto", "", new[]
            {
                TestContent.Rival("a", TestContent.AssaultIdentity(), firstForce: 6),
                TestContent.Rival("b", Arson(), firstForce: 16)
            });

            List<CatalogViolation> violations = DifficultyLadderValidator.Validate(Ladder(assaultOnly));

            Assert.That(violations.Exists(v => v.SubjectId == "so-assalto" && v.Rule == "contra-jogo"), Is.True);
        }

        [Test]
        public void NivelRepeteIdentidadeReprova()
        {
            DifficultyLevel repeated = new DifficultyLevel("repetido", 1, "Repetido", "", new[]
            {
                TestContent.Rival("a", TestContent.FoodRaidIdentity(), firstForce: 6),
                TestContent.Rival("b", TestContent.FoodRaidIdentity(), firstForce: 16)
            });

            List<CatalogViolation> violations = DifficultyLadderValidator.Validate(Ladder(repeated));

            Assert.That(violations.Exists(v => v.Detail.Contains("repete a identidade")), Is.True);
        }

        // --- Texto de selecao vem do dado (5.4) ---

        [Test]
        public void SelecaoMostraOTextoAutoradoDoNivel()
        {
            const string authored = "Os Saqueadores entram no dia 4, e o celeiro comeca pela metade.";
            DifficultyLadder ladder = Ladder(Level(1), Level(2, scarcity: 0.5, hardening: authored));
            MetaProfile profile = MetaProfile.NewProfile();

            List<LevelChoice> choices = ladder.Choices(profile);

            Assert.That(choices.Count, Is.EqualTo(2));
            Assert.That(choices[1].Hardening, Is.EqualTo(authored));
            Assert.That(choices[0].Unlocked, Is.True);
            Assert.That(choices[1].Unlocked, Is.False, "o bloqueado aparece, para o jogador ver o que vem");
        }

        [Test]
        public void NivelAcimaDoPrimeiroSemTextoReprova()
        {
            List<CatalogViolation> violations = DifficultyLadderValidator.Validate(
                Ladder(Level(1), Level(2, scarcity: 0.5, hardening: "")));

            Assert.That(violations.Exists(v => v.Rule == "endurecimento"), Is.True);
        }
    }
}

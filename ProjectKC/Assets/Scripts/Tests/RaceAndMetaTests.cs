using System.Collections.Generic;
using System.IO;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    [TestFixture]
    public class RaceTests
    {
        [Test]
        public void RacaSemModificador_JogaComoBaseline()
        {
            // Design D8: modificador ausente e comportamento padrao, nunca erro.
            RaceDefinition bare = TestContent.WithRules("nua", new Dictionary<string, double>());
            RaceDefinition declared = TestContent.Humans();

            RunBundle bareRun = TestContent.Run(bare, TestContent.Catalog(bare), seed: 99);
            RunBundle declaredRun = TestContent.Run(declared, TestContent.Catalog(declared), seed: 99);

            Assert.That(bareRun.Run.Rules.Has(RuleKeys.TileCostMultiplier), Is.False);
            Assert.That(
                bareRun.Run.Grid.NextTileCost(bareRun.Run.Rules),
                Is.EqualTo(declaredRun.Run.Grid.NextTileCost(declaredRun.Run.Rules)));
            Assert.That(bareRun.Run.HandSize, Is.EqualTo(5));
            Assert.That(bareRun.Run.EnergyPerDay, Is.EqualTo(3));
        }

        [Test]
        public void ChaveDesconhecida_DevolveOPadrao()
        {
            RuleModifiers rules = RuleModifiers.None;

            Assert.That(rules.GetDouble("chave_que_nao_existe", 4.2), Is.EqualTo(4.2));
            Assert.That(rules.GetInt("outra", 7), Is.EqualTo(7));
            Assert.That(rules.GetBool("mais_uma", true), Is.True);
        }

        [Test]
        public void EstadoInicialVemDaRaca()
        {
            RaceDefinition race = TestContent.Humans(gold: 123, integrity: 17, handSize: 6, energy: 4);

            RunBundle bundle = TestContent.Run(race);

            Assert.That(bundle.Run.Gold, Is.EqualTo(123));
            Assert.That(bundle.Run.MaxIntegrity, Is.EqualTo(17));
            Assert.That(bundle.Run.HandSize, Is.EqualTo(6));
            Assert.That(bundle.Run.EnergyPerDay, Is.EqualTo(4));
        }

        [Test]
        public void Humanos_CompramUmaCartaAMais()
        {
            RaceDefinition humans = TestContent.Humans(
                rules: new RuleModifiers(new Dictionary<string, double>
                {
                    { RuleKeys.ExtraCardsPerDay, 1 }
                }));

            RunBundle bundle = TestContent.Run(humans, TestContent.Catalog(humans));

            Assert.That(bundle.Run.HandSize, Is.EqualTo(6), "5 de base + 1 da raca");
        }

        [Test]
        public void Elfos_FlorestaProduzSemEdificio()
        {
            RaceDefinition elves = TestContent.WithRules("elves", new Dictionary<string, double>
            {
                { RuleKeys.ForestPassiveProduction, 2 }
            });
            RunBundle bundle = TestContent.Run(elves, TestContent.Catalog(elves));
            Coord forest = new Coord(1, 0);
            bundle.Run.Grid.Grant(forest);
            bundle.Run.Grid.SetTerrain(forest, TerrainType.Forest);

            ProductionBreakdown breakdown = ProductionCalculator.ForTile(
                bundle.Run.Grid, bundle.Run.Grid.TileAt(forest), bundle.Run.Rules);

            Assert.That(breakdown.Total, Is.EqualTo(2));
            Assert.That(breakdown.Lines[0].Reason, Does.Contain("raca"));
        }

        [Test]
        public void Elfos_ConstroemComUmDiaDeAtraso()
        {
            RaceDefinition elves = TestContent.WithRules("elves", new Dictionary<string, double>
            {
                { RuleKeys.BuildDelayDays, 1 }
            });
            RunBundle bundle = TestContent.Run(elves, TestContent.Catalog(elves));
            Coord target = new Coord(1, 0);
            bundle.Run.Grid.Grant(target);
            bundle.Run.Grid.SetTerrain(target, TerrainType.Plain);

            CommandResult result = bundle.Engine.BuildOn(target, TestContent.Farm());

            Assert.That(result.Ok, Is.True);
            Assert.That(bundle.Run.Grid.TileAt(target).HasBuilding, Is.False, "a obra ainda nao ficou pronta");
            Assert.That(bundle.Engine.PendingBuilds.Count, Is.EqualTo(1));

            bundle.Engine.EndDay();

            Assert.That(bundle.Run.Grid.TileAt(target).HasBuilding, Is.True, "no dia seguinte a obra fica de pe");
        }

        [Test]
        public void Orcs_PerdemOuroEmDiaSemCombate()
        {
            RaceDefinition orcs = TestContent.WithRules("orcs", new Dictionary<string, double>
            {
                { RuleKeys.StagnationGoldLossFraction, 0.20 }
            });
            RunBundle bundle = TestContent.Run(orcs, TestContent.Catalog(orcs));
            int goldBefore = bundle.Run.Gold;

            bundle.Engine.EndDay();

            Assert.That(bundle.Run.Gold, Is.LessThan(goldBefore + 10),
                "um dia parado deve custar ouro aos Orcs");
        }

        [Test]
        public void RacaBaseline_NaoPerdeOuroPorEstagnacao()
        {
            RunBundle bundle = TestContent.Run();
            int goldBefore = bundle.Run.Gold;

            bundle.Engine.EndDay();

            Assert.That(bundle.Run.Gold, Is.GreaterThanOrEqualTo(goldBefore));
        }

        [Test]
        public void RacaNaoMudaNoMeioDaRun()
        {
            RunBundle bundle = TestContent.Run();
            string raceId = bundle.Run.Race.Id;

            bundle.Engine.EndDay();
            bundle.Engine.EndDay();

            Assert.That(bundle.Run.Race.Id, Is.EqualTo(raceId));
        }

        [Test]
        public void CartaProibida_NaoEntraNoDeckNemEmRecompensa()
        {
            CardDefinition banned = TestContent.GoldCard("proibida");
            RaceDefinition race = new RaceDefinition(
                "restrita", "Restrita", 50, 20, 5, 3,
                new List<CardDefinition>(),
                RuleModifiers.None,
                new List<string> { "proibida" });

            ContentCatalog catalog = TestContent.Catalog(race);
            catalog.AddCard(banned, unlockedFromStart: true);

            RunSetup setup = new RunSetup("restrita", 3) { FirstThreatDay = 999 };
            RunBundle bundle = RunBuilder.Build(setup, catalog);
            bundle.Engine.StartRun();

            Assert.That(bundle.Run.Deck.AllCards(), Has.No.Member(banned));

            // A recompensa e um caminho de entrada tanto quanto o deck inicial, entao
            // a proibicao precisa valer aqui tambem.
            EffectResult reward = new AddCardToDeckEffect(banned)
                .Apply(new EffectContext(bundle.Run, EffectSource.Event));

            Assert.That(reward.Applied, Is.False);
            Assert.That(bundle.Run.Deck.AllCards(), Has.No.Member(banned));
        }

        [Test]
        public void RacaBloqueada_NaoPodeSerJogada()
        {
            RaceDefinition locked = TestContent.WithRules("trancada", new Dictionary<string, double>());
            ContentCatalog catalog = new ContentCatalog();
            catalog.AddBuilding(TestContent.Hall(), true);
            catalog.AddRace(locked, unlockedFromStart: false);

            RunSetup setup = new RunSetup("trancada", 1);

            Assert.Throws<System.InvalidOperationException>(() => RunBuilder.Build(setup, catalog));
        }

        [Test]
        public void ConteudoBloqueado_NaoApareceEmNenhumPool()
        {
            CardDefinition locked = TestContent.GoldCard("carta_trancada");
            EventDefinition lockedEvent = EventDefinition.Simple(
                "evento_trancado", "Trancado", EventClass.Positive,
                new List<IEffect> { new GainGoldEffect(1) });

            ContentCatalog catalog = TestContent.Catalog();
            catalog.AddCard(locked, unlockedFromStart: false);
            catalog.AddEvent(lockedEvent, unlockedFromStart: false);

            RunSetup setup = new RunSetup("humans", 8) { FirstThreatDay = 999 };
            RunBundle bundle = RunBuilder.Build(setup, catalog);
            bundle.Engine.StartRun();

            Assert.That(bundle.Run.Deck.AllCards(), Has.No.Member(locked));
            Assert.That(bundle.Engine.EventPool.Catalog, Has.No.Member(lockedEvent));
        }
    }

    [TestFixture]
    public class MetaProgressionTests
    {
        private string _tempDirectory;

        [SetUp]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "kc_tests_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }

        private string SavePath => Path.Combine(_tempDirectory, "profile.json");

        private static MetaNodeDefinition Node(
            string id, int cost, string[] prerequisites = null, string branch = null, UnlockKind kind = UnlockKind.Card)
        {
            return new MetaNodeDefinition(
                id, id, cost,
                new List<Unlock> { new Unlock(kind, id + "_content") },
                prerequisites == null ? null : new List<string>(prerequisites),
                branch);
        }

        [Test]
        public void SerializarEDesserializar_PreservaOPerfil()
        {
            MetaProfile profile = MetaProfile.NewProfile();
            profile.AddCurrency(42);
            profile.RunsPlayed = 7;
            profile.BestDayReached = 23;
            profile.BestScore = 815;
            profile.MarkPurchased("no_a");
            profile.MarkPurchased("no_b");

            MetaProfile round = ProfileCodec.Deserialize(ProfileCodec.Serialize(profile));

            Assert.That(round.Version, Is.EqualTo(profile.Version));
            Assert.That(round.Currency, Is.EqualTo(42));
            Assert.That(round.RunsPlayed, Is.EqualTo(7));
            Assert.That(round.BestDayReached, Is.EqualTo(23));
            Assert.That(round.BestScore, Is.EqualTo(815));
            Assert.That(round.HasNode("no_a"), Is.True);
            Assert.That(round.HasNode("no_b"), Is.True);
        }

        [Test]
        public void SaldoSobreviveAoFechamentoDoJogo()
        {
            FileProfileStore store = new FileProfileStore(SavePath);
            MetaProfile profile = MetaProfile.NewProfile();
            profile.AddCurrency(31);
            profile.MarkPurchased("tronco_1");
            store.Save(profile);

            ProfileLoadResult reloaded = new FileProfileStore(SavePath).Load();

            Assert.That(reloaded.Status, Is.EqualTo(ProfileLoadStatus.Loaded));
            Assert.That(reloaded.Profile.Currency, Is.EqualTo(31));
            Assert.That(reloaded.Profile.HasNode("tronco_1"), Is.True);
        }

        [Test]
        public void SaveAusente_CriaPerfilNovoSemErro()
        {
            ProfileLoadResult result = new FileProfileStore(SavePath).Load();

            Assert.That(result.Status, Is.EqualTo(ProfileLoadStatus.Missing));
            Assert.That(result.Profile.Currency, Is.Zero);
            Assert.That(result.Profile.PurchasedNodes, Is.Empty);
        }

        [Test]
        public void SaveCorrompido_PreservaOArquivoESegueComPerfilNovo()
        {
            File.WriteAllText(SavePath, "{isso nao e json valido");

            ProfileLoadResult result = new FileProfileStore(SavePath).Load();

            Assert.That(result.Status, Is.EqualTo(ProfileLoadStatus.Corrupt));
            Assert.That(result.Profile.Currency, Is.Zero);
            Assert.That(result.Message, Is.Not.Empty);
            Assert.That(result.BackupPath, Is.Not.Null);
            Assert.That(File.Exists(result.BackupPath), Is.True, "o arquivo ilegivel deve ser preservado");
            Assert.That(File.Exists(SavePath), Is.False);
        }

        [Test]
        public void SaveDeVersaoFutura_ETratadoComoCorrompido()
        {
            File.WriteAllText(SavePath,
                "{\"version\":999,\"currency\":5,\"runsPlayed\":0,\"bestDay\":0,\"bestScore\":0,\"nodes\":[]}");

            ProfileLoadResult result = new FileProfileStore(SavePath).Load();

            Assert.That(result.Status, Is.EqualTo(ProfileLoadStatus.Corrupt));
        }

        [Test]
        public void TodaRunRendeMoeda_MesmoTerminandoNoDia3()
        {
            RunBundle bundle = TestContent.Run();
            bundle.Run.Stats.DaysSurvived = 3;
            RunScore score = ScoreCalculator.Calculate(bundle.Run);

            MetaProfile profile = MetaProfile.NewProfile();
            profile.RecordRun(score, 3);

            Assert.That(profile.Currency, Is.GreaterThan(0));
            Assert.That(profile.RunsPlayed, Is.EqualTo(1));
            Assert.That(profile.BestDayReached, Is.EqualTo(3));
        }

        [Test]
        public void NoBloqueadoPorPreRequisito_NaoPodeSerComprado()
        {
            MetaTree tree = new MetaTree(new List<MetaNodeDefinition>
            {
                Node("pai", 5),
                Node("filho", 5, new[] { "pai" })
            });
            MetaProfile profile = MetaProfile.NewProfile();
            profile.AddCurrency(1000);

            PurchaseResult result = tree.CanPurchase(profile, "filho");

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Rejection, Is.EqualTo(PurchaseRejection.MissingPrerequisite));
            Assert.That(result.MissingPrerequisite, Is.EqualTo("pai"));
        }

        [Test]
        public void CompraDebitaOSaldoEFicaPermanente()
        {
            MetaTree tree = new MetaTree(new List<MetaNodeDefinition> { Node("tronco", 12) });
            MetaProfile profile = MetaProfile.NewProfile();
            profile.AddCurrency(20);

            PurchaseResult result = tree.Purchase(profile, "tronco");

            Assert.That(result.Ok, Is.True);
            Assert.That(profile.Currency, Is.EqualTo(8));
            Assert.That(profile.HasNode("tronco"), Is.True);
            Assert.That(tree.CanPurchase(profile, "tronco").Rejection,
                Is.EqualTo(PurchaseRejection.AlreadyPurchased));
        }

        [Test]
        public void SaldoInsuficiente_ImpedeACompra()
        {
            MetaTree tree = new MetaTree(new List<MetaNodeDefinition> { Node("caro", 100) });
            MetaProfile profile = MetaProfile.NewProfile();
            profile.AddCurrency(10);

            Assert.That(tree.CanPurchase(profile, "caro").Rejection,
                Is.EqualTo(PurchaseRejection.NotEnoughCurrency));
            Assert.That(profile.Currency, Is.EqualTo(10));
        }

        [Test]
        public void ConteudoDeGalhoDeRaca_NaoApareceEmOutraRaca()
        {
            MetaTree tree = new MetaTree(new List<MetaNodeDefinition>
            {
                Node("anoes_1", 5, null, "dwarves"),
                Node("elfos_1", 5, null, "elves"),
                Node("tronco_1", 5)
            });
            MetaProfile profile = MetaProfile.NewProfile();
            profile.AddCurrency(100);
            tree.Purchase(profile, "anoes_1");
            tree.Purchase(profile, "tronco_1");

            HashSet<string> forDwarves = tree.UnlockedContent(profile, UnlockKind.Card, "dwarves");
            HashSet<string> forElves = tree.UnlockedContent(profile, UnlockKind.Card, "elves");

            Assert.That(forDwarves, Contains.Item("anoes_1_content"));
            Assert.That(forDwarves, Contains.Item("tronco_1_content"));
            Assert.That(forElves, Has.No.Member("anoes_1_content"));
            Assert.That(forElves, Contains.Item("tronco_1_content"), "o tronco vale para todas as racas");
        }

        [Test]
        public void GalhosSaoIndependentesEntreSi()
        {
            MetaTree tree = new MetaTree(new List<MetaNodeDefinition>
            {
                Node("elfos_1", 5, null, "elves"),
                Node("elfos_2", 5, new[] { "elfos_1" }, "elves"),
                Node("anoes_1", 5, null, "dwarves")
            });
            MetaProfile profile = MetaProfile.NewProfile();
            profile.AddCurrency(100);

            PurchaseResult dwarvesBefore = tree.CanPurchase(profile, "anoes_1");
            tree.Purchase(profile, "elfos_1");
            tree.Purchase(profile, "elfos_2");
            PurchaseResult dwarvesAfter = tree.CanPurchase(profile, "anoes_1");

            Assert.That(dwarvesBefore.Ok, Is.EqualTo(dwarvesAfter.Ok));
            Assert.That(dwarvesAfter.Ok, Is.True);
        }

        [Test]
        public void RacaAparecceAposCompraDoNo()
        {
            RaceDefinition orcs = TestContent.WithRules("orcs", new Dictionary<string, double>());
            ContentCatalog catalog = TestContent.Catalog();
            catalog.AddRace(orcs, unlockedFromStart: false);

            MetaTree tree = new MetaTree(new List<MetaNodeDefinition>
            {
                new MetaNodeDefinition("liberar_orcs", "Orcs", 10,
                    new List<Unlock> { new Unlock(UnlockKind.Race, "orcs") })
            });
            MetaProfile profile = MetaProfile.NewProfile();
            profile.AddCurrency(50);

            Assert.That(catalog.IsRaceUnlocked("orcs", tree, profile), Is.False);
            tree.Purchase(profile, "liberar_orcs");
            Assert.That(catalog.IsRaceUnlocked("orcs", tree, profile), Is.True);

            List<RaceDefinition> selectable = catalog.SelectableRaces(tree, profile);
            bool found = false;
            for (int i = 0; i < selectable.Count; i++)
            {
                if (selectable[i].Id == "orcs")
                {
                    found = true;
                }
            }

            Assert.That(found, Is.True);
        }

        [Test]
        public void NoDesbloqueiaConteudoSemAlterarValoresIniciais()
        {
            CardDefinition unlockable = TestContent.GoldCard("carta_meta");
            ContentCatalog catalog = TestContent.Catalog();
            catalog.AddCard(unlockable, unlockedFromStart: false);

            MetaTree tree = new MetaTree(new List<MetaNodeDefinition>
            {
                new MetaNodeDefinition("no_carta", "Carta", 5,
                    new List<Unlock> { new Unlock(UnlockKind.Card, "carta_meta") })
            });
            MetaProfile profile = MetaProfile.NewProfile();
            profile.AddCurrency(50);

            RunSetup setup = new RunSetup("humans", 21) { FirstThreatDay = 999 };
            RunBundle before = RunBuilder.Build(setup, catalog, tree, profile);
            before.Engine.StartRun();
            int goldBefore = before.Run.Gold;

            tree.Purchase(profile, "no_carta");
            RunBundle after = RunBuilder.Build(setup, catalog, tree, profile);
            after.Engine.StartRun();

            Assert.That(before.Run.Deck.AllCards(), Has.No.Member(unlockable));
            Assert.That(after.Run.Deck.AllCards(), Contains.Item(unlockable));
            Assert.That(after.Run.Gold, Is.EqualTo(goldBefore), "o no nao pode mexer no ouro inicial");
            Assert.That(after.Run.MaxIntegrity, Is.EqualTo(before.Run.MaxIntegrity));
        }

        [Test]
        public void CatalogoDeMetaSemBonusNumerico_NaoTemViolacao()
        {
            MetaTree tree = new MetaTree(new List<MetaNodeDefinition>
            {
                Node("tronco_1", 5),
                Node("tronco_2", 8, new[] { "tronco_1" }, null, UnlockKind.Building),
                Node("humanos_1", 6, null, "humans", UnlockKind.Event)
            });

            List<CatalogViolation> violations = MetaCatalogValidator.Validate(tree);

            Assert.That(violations, Is.Empty, string.Join(" | ", violations));
        }

        [Test]
        public void NoQueConcedeBonusNumerico_EReprovado()
        {
            MetaTree tree = new MetaTree(new List<MetaNodeDefinition>
            {
                new MetaNodeDefinition("ouro_inicial", "Ouro inicial", 5,
                    new List<Unlock> { new Unlock(UnlockKind.NumericBonus, "starting_gold_+20") })
            });

            List<CatalogViolation> violations = MetaCatalogValidator.Validate(tree);

            Assert.That(violations.Count, Is.EqualTo(1));
            Assert.That(violations[0].Rule, Is.EqualTo("bonus numerico"));
        }

        [Test]
        public void NoSemConteudo_EReprovado()
        {
            MetaTree tree = new MetaTree(new List<MetaNodeDefinition>
            {
                new MetaNodeDefinition("vazio", "Vazio", 5, new List<Unlock>())
            });

            List<CatalogViolation> violations = MetaCatalogValidator.Validate(tree);

            Assert.That(violations.Count, Is.EqualTo(1));
            Assert.That(violations[0].Rule, Is.EqualTo("conteudo"));
        }

        [Test]
        public void PreRequisitoInexistente_EReprovado()
        {
            MetaTree tree = new MetaTree(new List<MetaNodeDefinition>
            {
                Node("filho", 5, new[] { "pai_que_nao_existe" })
            });

            List<CatalogViolation> violations = MetaCatalogValidator.Validate(tree);

            Assert.That(violations.Count, Is.EqualTo(1));
            Assert.That(violations[0].Rule, Is.EqualTo("pre-requisito"));
        }
    }
}

using System.Collections.Generic;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    /// <summary>
    /// Poder permanente na arvore de meta, sob teto por nivel (grupo 6, design D6).
    /// </summary>
    public sealed class MetaPowerTests
    {
        private static MetaNodeDefinition BonusNode(
            string id, MetaBonus bonus, int cost = 5, int requiredLevel = 1, string prerequisite = null)
        {
            return new MetaNodeDefinition(
                id, id, cost, new List<Unlock>(),
                prerequisite != null ? new List<string> { prerequisite } : null,
                null, null, new List<MetaBonus> { bonus }, requiredLevel);
        }

        private static MetaProfile ProfileWith(MetaTree tree, params string[] nodes)
        {
            MetaProfile profile = MetaProfile.NewProfile();
            profile.AddCurrency(1000);
            profile.HighestUnlockedLevel = 9;
            for (int i = 0; i < nodes.Length; i++)
            {
                Assert.That(tree.Purchase(profile, nodes[i]).Ok, Is.True, "nao comprou " + nodes[i]);
            }

            return profile;
        }

        private static DifficultyLadder Ladder(params int[] caps)
        {
            List<DifficultyLevel> levels = new List<DifficultyLevel>();
            for (int i = 0; i < caps.Length; i++)
            {
                levels.Add(new DifficultyLevel(
                    "l" + (i + 1), i + 1, "Nivel " + (i + 1), "mais duro",
                    new List<RivalDefinition> { TestContent.Rival() }, metaPowerCap: caps[i]));
            }

            return new DifficultyLadder(levels);
        }

        // --- 6.1 ---

        [Test]
        public void BonusDeRecurso_AumentaOSaldoInicialDaRunSeguinte()
        {
            MetaTree tree = new MetaTree(new[] { BonusNode("comida", MetaBonus.Starting(ResourceKind.Food, 10)) });
            ContentCatalog catalog = StarterContent.BuildCatalog();

            int without = RunBuilder.Build(StarterContent.CreateSetup(1), catalog, tree, MetaProfile.NewProfile())
                .Run[ResourceKind.Food];
            int with = RunBuilder.Build(StarterContent.CreateSetup(1), catalog, tree, ProfileWith(tree, "comida"))
                .Run[ResourceKind.Food];

            Assert.That(with, Is.EqualTo(without + 10));
        }

        [Test]
        public void BonusDeIntegridade_AumentaOMaximoEOAtual()
        {
            MetaTree tree = new MetaTree(new[] { BonusNode("base", MetaBonus.Integrity(3)) });
            ContentCatalog catalog = StarterContent.BuildCatalog();

            RunState plain = RunBuilder.Build(StarterContent.CreateSetup(1), catalog, tree, MetaProfile.NewProfile()).Run;
            RunState buffed = RunBuilder.Build(StarterContent.CreateSetup(1), catalog, tree, ProfileWith(tree, "base")).Run;

            Assert.That(buffed.MaxIntegrity, Is.EqualTo(plain.MaxIntegrity + 3));
            Assert.That(buffed.Integrity, Is.EqualTo(buffed.MaxIntegrity));
        }

        [Test]
        public void NoNaoComprado_NaoConcedeNada()
        {
            MetaTree tree = new MetaTree(new[] { BonusNode("comida", MetaBonus.Starting(ResourceKind.Food, 10)) });

            Assert.That(tree.GrantedBonuses(MetaProfile.NewProfile(), "humans"), Is.Empty);
        }

        [Test]
        public void BonusDoNivel_NaoESubtraidoPelaEscassezDoNivel()
        {
            // A escassez aperta o que a raca traz; o que o jogador comprou entra depois.
            MetaTree tree = new MetaTree(new[] { BonusNode("ouro", MetaBonus.Starting(ResourceKind.Gold, 20)) });
            ContentCatalog catalog = StarterContent.BuildCatalog();

            int without = RunBuilder.Build(StarterContent.CreateSetup(1, 3), catalog, tree, MetaProfile.NewProfile())
                .Run[ResourceKind.Gold];
            int with = RunBuilder.Build(StarterContent.CreateSetup(1, 3), catalog, tree, ProfileWith(tree, "ouro"))
                .Run[ResourceKind.Gold];

            Assert.That(with, Is.EqualTo(without + 20));
        }

        // --- 6.2 ---

        [Test]
        public void ArvoreDentroDoTeto_Passa()
        {
            MetaTree tree = new MetaTree(new[] { BonusNode("a", MetaBonus.Starting(ResourceKind.Food, 10)) });

            Assert.That(MetaCatalogValidator.ValidatePowerCaps(tree, Ladder(5, 5)), Is.Empty);
        }

        [Test]
        public void ArvoreAcimaDoTeto_EReprovadaApontandoNivelEExcedente()
        {
            MetaTree tree = new MetaTree(new[] { BonusNode("a", MetaBonus.Starting(ResourceKind.Population, 2)) });

            List<CatalogViolation> violations = MetaCatalogValidator.ValidatePowerCaps(tree, Ladder(4, 20));

            Assert.That(violations.Count, Is.EqualTo(1));
            Assert.That(violations[0].SubjectId, Is.EqualTo("l1"));
            Assert.That(violations[0].Detail, Does.Contain("6 acima"));
        }

        [Test]
        public void PoderQueSoAbreNoNivelDois_NaoContaNoTetoDoNivelUm()
        {
            MetaTree tree = new MetaTree(new[]
            {
                BonusNode("cedo", MetaBonus.Starting(ResourceKind.Food, 10)),
                BonusNode("tarde", MetaBonus.Starting(ResourceKind.Population, 2), requiredLevel: 2)
            });

            Assert.That(tree.PowerAvailableAt(1), Is.EqualTo(2));
            Assert.That(tree.PowerAvailableAt(2), Is.EqualTo(12));
            Assert.That(MetaCatalogValidator.ValidatePowerCaps(tree, Ladder(2, 12)), Is.Empty);
        }

        [Test]
        public void NoQueExigeNivel_SoPodeSerCompradoDepoisDeLiberado()
        {
            MetaTree tree = new MetaTree(new[]
            {
                BonusNode("tarde", MetaBonus.Starting(ResourceKind.Food, 5), requiredLevel: 2)
            });
            MetaProfile profile = MetaProfile.NewProfile();
            profile.AddCurrency(100);

            Assert.That(tree.CanPurchase(profile, "tarde").Rejection, Is.EqualTo(PurchaseRejection.LevelLocked));

            profile.HighestUnlockedLevel = 2;

            Assert.That(tree.CanPurchase(profile, "tarde").Ok, Is.True);
        }

        [Test]
        public void NenhumBonusPositivo_PesaZero()
        {
            Assert.That(MetaBonus.Starting(ResourceKind.Gold, 1).Power, Is.GreaterThan(0));
            Assert.That(MetaBonus.Integrity(1).Power, Is.GreaterThan(0));
        }

        [Test]
        public void GalhosDeRaca_NaoSeSomamNoTeto()
        {
            MetaNodeDefinition humans = new MetaNodeDefinition(
                "h", "h", 1, new List<Unlock>(), null, "humans", null,
                new List<MetaBonus> { MetaBonus.Starting(ResourceKind.Food, 10) });
            MetaNodeDefinition orcs = new MetaNodeDefinition(
                "o", "o", 1, new List<Unlock>(), null, "orcs", null,
                new List<MetaBonus> { MetaBonus.Starting(ResourceKind.Food, 20) });

            MetaTree tree = new MetaTree(new[] { humans, orcs });

            Assert.That(tree.PowerAvailableAt(1), Is.EqualTo(4), "uma run tem uma raca so: vale o galho mais forte");
        }
    }
}

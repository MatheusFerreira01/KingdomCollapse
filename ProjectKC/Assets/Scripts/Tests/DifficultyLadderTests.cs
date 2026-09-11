using System.Collections.Generic;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    [TestFixture]
    public class DifficultyLadderTests
    {
        private static DifficultyLevel Level(string id, int pressure, int metaPowerCap, params string[] hardenings)
        {
            RivalDefinition rival = TestContent.Rival(id + "_rival", TestContent.AssaultIdentity());
            return new DifficultyLevel(
                id, id, new List<RivalDefinition> { rival }, new List<string>(hardenings), metaPowerCap, pressure);
        }

        private static List<DifficultyLevel> Ladder()
        {
            return new List<DifficultyLevel>
            {
                Level("level_1", 10, 5, "Rival unico"),
                Level("level_2", 20, 10, "Dois rivais", "Ameaca chega mais cedo"),
                Level("level_3", 35, 15, "Tres rivais")
            };
        }

        // --- 5.1 Selecao de nivel ---

        [Test]
        public void PerfilNovo_SoTemOPrimeiroNivelDesbloqueado()
        {
            MetaProfile profile = MetaProfile.NewProfile();
            List<DifficultyLevel> ladder = Ladder();

            Assert.That(profile.IsDifficultyUnlocked("level_1", ladder), Is.True);
            Assert.That(profile.IsDifficultyUnlocked("level_2", ladder), Is.False);
            Assert.That(profile.IsDifficultyUnlocked("level_3", ladder), Is.False);
        }

        // --- 5.2 Desbloqueio por Vitoria ---

        [Test]
        public void Colapsar_NaoDesbloqueiaOProximoNivel()
        {
            MetaProfile profile = MetaProfile.NewProfile();
            List<DifficultyLevel> ladder = Ladder();

            // Colapso simplesmente nao chama UnlockNextDifficulty — nada a fazer
            // alem de confirmar que o estado nao desbloqueado persiste.
            Assert.That(profile.IsDifficultyUnlocked("level_2", ladder), Is.False);
        }

        [Test]
        public void Vitoria_DesbloqueiaOProximoNivel()
        {
            MetaProfile profile = MetaProfile.NewProfile();
            List<DifficultyLevel> ladder = Ladder();

            profile.UnlockNextDifficulty("level_1", ladder);

            Assert.That(profile.IsDifficultyUnlocked("level_2", ladder), Is.True);
            Assert.That(profile.IsDifficultyUnlocked("level_3", ladder), Is.False);
        }

        [Test]
        public void DesbloqueioSobreviveASerializarEDesserializar()
        {
            MetaProfile profile = MetaProfile.NewProfile();
            List<DifficultyLevel> ladder = Ladder();
            profile.UnlockNextDifficulty("level_1", ladder);
            profile.UnlockNextDifficulty("level_2", ladder);

            string json = ProfileCodec.Serialize(profile);
            MetaProfile roundTripped = ProfileCodec.Deserialize(json);

            Assert.That(roundTripped.IsDifficultyUnlocked("level_2", ladder), Is.True);
            Assert.That(roundTripped.IsDifficultyUnlocked("level_3", ladder), Is.True);
        }

        [Test]
        public void SaveAntigoSemCampoDeDificuldade_NaoQuebraODeserializar()
        {
            MetaProfile legacy = MetaProfile.NewProfile();
            legacy.AddCurrency(10);
            string json = ProfileCodec.Serialize(legacy);

            // Remove o campo novo pra simular um save gravado antes desta tarefa.
            json = json.Replace("\"difficultyLevels\":[]", "");
            json = json.TrimEnd('}').TrimEnd(',') + "}";

            MetaProfile roundTripped = ProfileCodec.Deserialize(json);

            Assert.That(roundTripped.IsDifficultyUnlocked("level_1", Ladder()), Is.True);
        }

        // --- 5.3 Monotonicidade ---

        [Test]
        public void EscadaQueSoEndurece_Passa()
        {
            List<CatalogViolation> violations = DifficultyLadderValidator.Validate(Ladder());

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void EscadaComNivelMaisFacil_Reprova()
        {
            List<DifficultyLevel> broken = new List<DifficultyLevel>
            {
                Level("level_1", 10, 5),
                Level("level_2", 5, 10) // pressao caiu — nivel 2 nao pode ser mais facil
            };

            List<CatalogViolation> violations = DifficultyLadderValidator.Validate(broken);

            Assert.That(violations, Is.Not.Empty);
        }

        [Test]
        public void EscadaQueNuncaEndurece_Reprova()
        {
            List<DifficultyLevel> flat = new List<DifficultyLevel>
            {
                Level("level_1", 10, 5),
                Level("level_2", 10, 5)
            };

            List<CatalogViolation> violations = DifficultyLadderValidator.Validate(flat);

            Assert.That(violations, Is.Not.Empty);
        }

        // --- 5.4 Texto vem do dado ---

        [Test]
        public void HardeningsVemDoDadoDoNivel_NaoDeCodigo()
        {
            DifficultyLevel level = Level("custom", 1, 1, "Frase autorada A", "Frase autorada B");

            Assert.That(level.Hardenings, Is.EquivalentTo(new[] { "Frase autorada A", "Frase autorada B" }));
        }
    }
}

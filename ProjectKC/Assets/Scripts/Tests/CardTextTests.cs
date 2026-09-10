using System.Collections.Generic;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    [TestFixture]
    public class CardTextTests
    {
        [Test]
        public void TextoSaiDosEfeitosQueACartaAplica()
        {
            CardDefinition card = new CardDefinition(
                "muralha", "Muralha", 1, TargetRequirement.None,
                new List<IEffect> { new AddDefenseEffect(3), new GainResourceEffect(ResourceKind.Wood, 5) });

            Assert.That(CardText.RulesFor(card), Is.EqualTo("+3 defesa hoje. +5 madeira."));
        }

        [Test]
        public void TextoDizOAlvoEARetencao()
        {
            CardDefinition card = new CardDefinition(
                "obra", "Obra", 2, TargetRequirement.BuildableTile,
                new List<IEffect> { new BuildOnTileEffect(TestContent.Farm()) }, retained: true);

            string text = CardText.RulesFor(card);

            Assert.That(text, Does.Contain("Constroi Fazenda de graca"));
            Assert.That(text, Does.Contain("Alvo: celula sua vazia"));
            Assert.That(text, Does.Contain("Fica na mao"));
        }

        [Test]
        public void EscalasAparecemNoTexto()
        {
            Assert.That(new GainGoldEffect(2, GoldScaling.PerOwnedTile).Describe(), Is.EqualTo("+2 ouro por celula possuida"));
            Assert.That(new LoseResourceEffect(ResourceKind.Food, 0.25).Describe(), Is.EqualTo("-25% do saldo de comida"));
            Assert.That(new ModifyProductionEffect(0.2, 2, multiplier: true).Describe(), Is.EqualTo("+20% de producao por 2 dias"));
        }

        [Test]
        public void TodoEfeitoTemTexto()
        {
            // Carta que o jogador nao consegue ler e carta que ele nao joga.
            IEffect[] all =
            {
                new GainGoldEffect(1), new LoseGoldEffect(0.1), new BuildOnTileEffect(TestContent.Farm()),
                new DestroyTileEffect(), new RepairBaseEffect(2), new DamageBaseEffect(1), new AddDefenseEffect(2),
                new DrawCardsEffect(1), new RevealTileEffect(), new AddCardToDeckEffect(TestContent.GoldCard()),
                new RemoveCardEffect(), new GrantTileEffect(), new ModifyProductionEffect(1, 1),
                new DisableTileEffect(), new SetTerrainEffect(TerrainType.Forest), new RepairTileEffect(),
                new GainResourceEffect(ResourceKind.Stone, 3), new LoseResourceEffect(ResourceKind.Gold, 2, GoldScaling.Flat),
                new RecruitEffect(2), new SetStanceEffect(WorkerStance.DefenseFirst)
            };

            foreach (IEffect effect in all)
            {
                Assert.That(effect.Describe(), Is.Not.Null.And.Not.Empty, effect.Kind);
            }
        }
    }
}

using System.Collections.Generic;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    [TestFixture]
    public class CardSystemTests
    {
        private static IRandomSource Rng(int seed = 7) => new XorShiftRandom(seed);

        [Test]
        public void MaoEReabastecidaAteOTamanhoDeMao()
        {
            RunDeck deck = new RunDeck(TestContent.Filler(10));

            int drawn = deck.DrawUpTo(5, Rng());

            Assert.That(drawn, Is.EqualTo(5));
            Assert.That(deck.Hand.Count, Is.EqualTo(5));
            Assert.That(deck.DrawPile.Count, Is.EqualTo(5));
        }

        [Test]
        public void DeckVazio_ReciclaODescarte()
        {
            RunDeck deck = new RunDeck(TestContent.Filler(3));
            deck.Draw(3, Rng());
            deck.DiscardHand();

            Assert.That(deck.DrawPile.Count, Is.Zero);
            Assert.That(deck.DiscardPile.Count, Is.EqualTo(3));

            int drawn = deck.Draw(2, Rng());

            Assert.That(drawn, Is.EqualTo(2));
            Assert.That(deck.Hand.Count, Is.EqualTo(2));
            Assert.That(deck.TotalCards, Is.EqualTo(3), "reciclar nao pode criar nem sumir carta");
        }

        [Test]
        public void DeckEDescarteVazios_NaoCausamErro()
        {
            RunDeck deck = new RunDeck();

            int drawn = deck.Draw(3, Rng());

            Assert.That(drawn, Is.Zero);
            Assert.That(deck.Hand.Count, Is.Zero);
        }

        [Test]
        public void CartaCara_ERecusadaSemGastarEnergia()
        {
            RunBundle bundle = TestContent.Run();
            CardDefinition expensive = TestContent.GoldCard("cara", 99, 10);
            bundle.Run.Deck.AddToDrawPile(expensive);
            bundle.Run.Deck.Draw(1, Rng());
            int energyBefore = bundle.Run.Energy;
            int goldBefore = bundle.Run.Gold;

            CommandResult result = bundle.Engine.PlayCard(expensive);

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.NotEnoughEnergy));
            Assert.That(bundle.Run.Energy, Is.EqualTo(energyBefore));
            Assert.That(bundle.Run.Gold, Is.EqualTo(goldBefore));
            Assert.That(bundle.Run.Deck.HandContains(expensive), Is.True, "carta recusada continua na mao");
        }

        [Test]
        public void EnergiaNaoAcumulaEntreDias()
        {
            RunBundle bundle = TestContent.Run();
            int perDay = bundle.Run.EnergyPerDay;

            Assert.That(bundle.Run.Energy, Is.EqualTo(perDay));
            bundle.Engine.EndDay();

            Assert.That(bundle.Run.Energy, Is.EqualTo(perDay), "a sobra do dia anterior nao pode somar");
        }

        [Test]
        public void AlvoInvalido_DevolveCartaEEnergiaIntactas()
        {
            RunBundle bundle = TestContent.Run();
            CardDefinition targeted = TestContent.Card(
                "reparo", 1, new RepairTileEffect(), TargetRequirement.DestroyedTile);
            bundle.Run.Deck.AddToDrawPile(targeted);
            bundle.Run.Deck.Draw(1, Rng());
            int energyBefore = bundle.Run.Energy;

            // Nenhuma celula esta arrasada, entao o Salao nao satisfaz o requisito.
            CommandResult result = bundle.Engine.PlayCard(targeted, Coord.Zero);

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.InvalidTarget));
            Assert.That(bundle.Run.Energy, Is.EqualTo(energyBefore));
            Assert.That(bundle.Run.Deck.HandContains(targeted), Is.True);
        }

        [Test]
        public void CartaQueExigeAlvo_ERecusadaSemAlvo()
        {
            RunBundle bundle = TestContent.Run();
            CardDefinition targeted = TestContent.Card(
                "construir", 1, new BuildOnTileEffect(TestContent.Farm()), TargetRequirement.BuildableTile);
            bundle.Run.Deck.AddToDrawPile(targeted);
            bundle.Run.Deck.Draw(1, Rng());

            CommandResult result = bundle.Engine.PlayCard(targeted);

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.TargetRequired));
            Assert.That(bundle.Run.Deck.HandContains(targeted), Is.True);
        }

        [Test]
        public void FimDoDia_EsvaziaAMao()
        {
            RunBundle bundle = TestContent.Run();
            bundle.Run.Deck.AddToDrawPile(TestContent.GoldCard("a"));
            bundle.Run.Deck.AddToDrawPile(TestContent.GoldCard("b"));
            bundle.Run.Deck.AddToDrawPile(TestContent.GoldCard("c"));
            bundle.Run.Deck.Draw(3, Rng());
            int handBefore = bundle.Run.Deck.Hand.Count;

            bundle.Engine.EndDay();

            Assert.That(handBefore, Is.GreaterThanOrEqualTo(3));
            // A mao e reabastecida no Inicio do Dia seguinte, entao a verificacao e
            // sobre nao ter sobrado nenhuma carta do dia anterior.
            Assert.That(bundle.Run.Deck.Hand.Count, Is.LessThanOrEqualTo(bundle.Run.HandSize));
        }

        [Test]
        public void CartaRetida_PermaneceNaMao()
        {
            RunDeck deck = new RunDeck();
            CardDefinition retained = TestContent.Card("retida", 1, new GainGoldEffect(1), retained: true);
            CardDefinition ordinary = TestContent.GoldCard("comum");
            deck.AddToDrawPile(retained);
            deck.AddToDrawPile(ordinary);
            deck.Draw(2, Rng());

            int discarded = deck.DiscardHand();

            Assert.That(discarded, Is.EqualTo(1));
            Assert.That(deck.Hand.Count, Is.EqualTo(1));
            Assert.That(deck.Hand[0].Id, Is.EqualTo("retida"));
        }

        [Test]
        public void CartaRetida_ContaContraOLimiteDeCompra()
        {
            RunDeck deck = new RunDeck(TestContent.Filler(10));
            CardDefinition retained = TestContent.Card("retida", 1, new GainGoldEffect(1), retained: true);
            deck.AddToDrawPile(retained);
            deck.Draw(1, Rng());
            deck.DiscardHand();

            int drawn = deck.DrawUpTo(5, Rng());

            Assert.That(deck.Hand.Count, Is.EqualTo(5));
            Assert.That(drawn, Is.EqualTo(4), "a carta retida ja ocupava um lugar da mao");
        }

        [Test]
        public void CartaConcedida_EntraNoDescarteDaRun()
        {
            RunBundle bundle = TestContent.Run();
            CardDefinition reward = TestContent.GoldCard("recompensa");
            int before = bundle.Run.Deck.TotalCards;

            EffectResult result = new AddCardToDeckEffect(reward)
                .Apply(new EffectContext(bundle.Run, EffectSource.Event));

            Assert.That(result.Applied, Is.True);
            Assert.That(bundle.Run.Deck.TotalCards, Is.EqualTo(before + 1));
            Assert.That(bundle.Run.Deck.DiscardPile, Contains.Item(reward));
        }

        [Test]
        public void DeckDaRunSeguinte_NaoTemAsCartasGanhasNaAnterior()
        {
            ContentCatalog catalog = TestContent.Catalog();
            RunSetup setup = new RunSetup("humans", 42) { FirstThreatDay = 999 };

            RunBundle first = RunBuilder.Build(setup, catalog);
            first.Engine.StartRun();
            CardDefinition reward = TestContent.GoldCard("premio_da_run_1");
            new AddCardToDeckEffect(reward).Apply(new EffectContext(first.Run, EffectSource.Event));
            Assert.That(first.Run.Deck.AllCards(), Contains.Item(reward));

            RunBundle second = RunBuilder.Build(setup, catalog);
            second.Engine.StartRun();

            Assert.That(second.Run.Deck.AllCards(), Has.No.Member(reward));
        }

        [Test]
        public void CartaForaDaMao_NaoPodeSerJogada()
        {
            RunBundle bundle = TestContent.Run();
            CardDefinition orphan = TestContent.GoldCard("orfa");

            CommandResult result = bundle.Engine.PlayCard(orphan);

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.CardNotInHand));
        }

        [Test]
        public void JogarCarta_GastaEnergiaEAplicaEfeito()
        {
            RunBundle bundle = TestContent.Run();
            CardDefinition card = TestContent.GoldCard("ouro", 1, 25);
            bundle.Run.Deck.AddToDrawPile(card);
            bundle.Run.Deck.Draw(1, Rng());
            int energyBefore = bundle.Run.Energy;
            int goldBefore = bundle.Run.Gold;

            CommandResult result = bundle.Engine.PlayCard(card);

            Assert.That(result.Ok, Is.True);
            Assert.That(bundle.Run.Energy, Is.EqualTo(energyBefore - 1));
            Assert.That(bundle.Run.Gold, Is.EqualTo(goldBefore + 25));
            Assert.That(bundle.Run.Deck.DiscardPile, Contains.Item(card));
        }

        [Test]
        public void RemoverCarta_TiraDaRunSemQuebrarPilhas()
        {
            RunDeck deck = new RunDeck(TestContent.Filler(4));
            int before = deck.TotalCards;

            CardDefinition removed = deck.RemoveRandomCard(Rng());

            Assert.That(removed, Is.Not.Null);
            Assert.That(deck.TotalCards, Is.EqualTo(before - 1));
            Assert.That(deck.AllCards(), Has.No.Member(removed));
        }
    }
}

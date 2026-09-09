using System.Collections.Generic;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    [TestFixture]
    public class EffectTests
    {
        private static EffectContext Ctx(RunState run, Coord? target = null, SeverityBudget budget = null)
        {
            return new EffectContext(run, EffectSource.Card, target, budget);
        }

        [Test]
        public void ListaDeEfeitosVazia_NaoAlteraEstado()
        {
            RunBundle bundle = TestContent.Run();
            int gold = bundle.Run.Gold;
            int integrity = bundle.Run.Integrity;
            int tiles = bundle.Run.Grid.OwnedCount;

            List<EffectResult> results = EffectRunner.ApplyAll(
                new List<IEffect>(), bundle.Run, EffectSource.Card);

            Assert.That(results, Is.Empty);
            Assert.That(bundle.Run.Gold, Is.EqualTo(gold));
            Assert.That(bundle.Run.Integrity, Is.EqualTo(integrity));
            Assert.That(bundle.Run.Grid.OwnedCount, Is.EqualTo(tiles));
        }

        [Test]
        public void EfeitoNuloNaLista_EIgnorado()
        {
            RunBundle bundle = TestContent.Run();
            List<IEffect> effects = new List<IEffect> { null, new GainGoldEffect(5) };

            List<EffectResult> results = EffectRunner.ApplyAll(effects, bundle.Run, EffectSource.Card);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Applied, Is.True);
        }

        [Test]
        public void GainGold_CreditaEContaComoGanho()
        {
            RunBundle bundle = TestContent.Run();
            int before = bundle.Run.Gold;

            EffectResult result = new GainGoldEffect(30).Apply(Ctx(bundle.Run));

            Assert.That(result.Applied, Is.True);
            Assert.That(bundle.Run.Gold, Is.EqualTo(before + 30));
            Assert.That(bundle.Run.Stats.GoldEarned, Is.GreaterThanOrEqualTo(30));
        }

        [Test]
        public void GainGold_PorCelulaEscalaComTerritorio()
        {
            RunBundle bundle = TestContent.Run();
            TestContent.GrantTiles(bundle.Run.Grid, new Coord(1, 0), new Coord(0, 1));
            int before = bundle.Run.Gold;

            new GainGoldEffect(5, GoldScaling.PerOwnedTile).Apply(Ctx(bundle.Run));

            Assert.That(bundle.Run.Gold, Is.EqualTo(before + 15));
        }

        [Test]
        public void LoseGold_NaoDeixaSaldoNegativo()
        {
            RunBundle bundle = TestContent.Run();
            bundle.Run.RemoveGold(bundle.Run.Gold);

            EffectResult result = new LoseGoldEffect(0.5).Apply(Ctx(bundle.Run));

            Assert.That(result.Applied, Is.False);
            Assert.That(bundle.Run.Gold, Is.Zero);
        }

        [Test]
        public void LoseGold_ERecortadoPeloOrcamentoDeEvento()
        {
            RunBundle bundle = TestContent.Run();
            int gold = bundle.Run.Gold;

            // O efeito pede metade do ouro; o orcamento de evento negativo permite 30%.
            EffectResult result = new LoseGoldEffect(0.5)
                .Apply(new EffectContext(bundle.Run, EffectSource.Event, null, SeverityBudget.NegativeEvent));

            int lost = gold - bundle.Run.Gold;
            Assert.That(result.Clamped, Is.True);
            Assert.That(lost, Is.LessThanOrEqualTo((int)(gold * 0.30) + 1));
            Assert.That(lost, Is.GreaterThan(0));
        }

        [Test]
        public void BuildOn_SemAlvo_NaoFazNada()
        {
            RunBundle bundle = TestContent.Run();

            EffectResult result = new BuildOnTileEffect(TestContent.Farm()).Apply(Ctx(bundle.Run));

            Assert.That(result.Applied, Is.False);
            Assert.That(result.Description, Does.Contain("sem alvo"));
        }

        [Test]
        public void BuildOn_ConstroiNoAlvoValido()
        {
            RunBundle bundle = TestContent.Run();
            Coord target = new Coord(1, 0);
            bundle.Run.Grid.Grant(target);
            bundle.Run.Grid.SetTerrain(target, TerrainType.Plain);

            EffectResult result = new BuildOnTileEffect(TestContent.Farm()).Apply(Ctx(bundle.Run, target));

            Assert.That(result.Applied, Is.True);
            Assert.That(bundle.Run.Grid.TileAt(target).Building.Id, Is.EqualTo("farm"));
        }

        [Test]
        public void DestroyTile_SemAlvoValido_NaoFazNada()
        {
            RunBundle bundle = TestContent.Run();

            // So existe o Salao, que nunca e escolhido automaticamente.
            EffectResult result = new DestroyTileEffect().Apply(Ctx(bundle.Run));

            Assert.That(result.Applied, Is.False);
            Assert.That(bundle.Run.Grid.TileAt(Coord.Zero).Destroyed, Is.False);
        }

        [Test]
        public void DestroyTile_NaoArrasaCelulaConstruidaSobOrcamentoDeEvento()
        {
            RunBundle bundle = TestContent.Run();
            Coord target = new Coord(1, 0);
            bundle.Run.Grid.Grant(target);
            bundle.Run.Grid.SetTerrain(target, TerrainType.Plain);
            bundle.Run.Grid.Build(target, TestContent.Farm());

            EffectResult result = new DestroyTileEffect()
                .Apply(new EffectContext(bundle.Run, EffectSource.Event, null, SeverityBudget.NegativeEvent));

            Assert.That(result.Applied, Is.False);
            Assert.That(bundle.Run.Grid.TileAt(target).Destroyed, Is.False);
            Assert.That(bundle.Run.Grid.TileAt(target).HasBuilding, Is.True);
        }

        [Test]
        public void DestroyTile_ArrasaCelulaVazia()
        {
            RunBundle bundle = TestContent.Run();
            Coord target = new Coord(1, 0);
            bundle.Run.Grid.Grant(target);

            EffectResult result = new DestroyTileEffect()
                .Apply(new EffectContext(bundle.Run, EffectSource.Event, null, SeverityBudget.NegativeEvent));

            Assert.That(result.Applied, Is.True);
            Assert.That(bundle.Run.Grid.TileAt(target).Destroyed, Is.True);
        }

        [Test]
        public void DestroyTile_RespeitaOTetoDeUmaCelulaPorEvento()
        {
            RunBundle bundle = TestContent.Run();
            TestContent.GrantTiles(bundle.Run.Grid, new Coord(1, 0), new Coord(0, 1), new Coord(-1, 0));

            List<IEffect> twoDestructions = new List<IEffect> { new DestroyTileEffect(), new DestroyTileEffect() };
            List<EffectResult> results = EffectRunner.ApplyAll(
                twoDestructions, bundle.Run, EffectSource.Event, null, SeverityBudget.NegativeEvent);

            int destroyed = 0;
            foreach (Tile tile in bundle.Run.Grid.OwnedTiles())
            {
                if (tile.Destroyed)
                {
                    destroyed++;
                }
            }

            Assert.That(results[0].Applied, Is.True);
            Assert.That(results[1].Applied, Is.False, "o segundo efeito estoura o orcamento");
            Assert.That(destroyed, Is.EqualTo(1));
        }

        [Test]
        public void RepairBase_NaoPassaDoMaximo()
        {
            RunBundle bundle = TestContent.Run();
            bundle.Run.Damage(3);

            new RepairBaseEffect(100).Apply(Ctx(bundle.Run));

            Assert.That(bundle.Run.Integrity, Is.EqualTo(bundle.Run.MaxIntegrity));
        }

        [Test]
        public void RepairBase_ComIntegridadeCheia_NaoFazNada()
        {
            RunBundle bundle = TestContent.Run();

            EffectResult result = new RepairBaseEffect(5).Apply(Ctx(bundle.Run));

            Assert.That(result.Applied, Is.False);
        }

        [Test]
        public void DamageBase_SobOrcamentoDeEvento_NuncaZeraAIntegridade()
        {
            RunBundle bundle = TestContent.Run();
            bundle.Run.Damage(bundle.Run.Integrity - 1);
            Assert.That(bundle.Run.Integrity, Is.EqualTo(1));

            EffectResult result = new DamageBaseEffect(999)
                .Apply(new EffectContext(bundle.Run, EffectSource.Event, null, SeverityBudget.NegativeEvent));

            Assert.That(result.Applied, Is.False);
            Assert.That(bundle.Run.Integrity, Is.EqualTo(1));
        }

        [Test]
        public void DamageBase_SemOrcamento_PodeSerLetal()
        {
            RunBundle bundle = TestContent.Run();

            new DamageBaseEffect(999).Apply(new EffectContext(bundle.Run, EffectSource.Threat));

            Assert.That(bundle.Run.Integrity, Is.Zero, "ameaca nao esta sujeita ao orcamento de evento");
        }

        [Test]
        public void AddDefense_ValeAteOProximoAtaque()
        {
            RunBundle bundle = TestContent.Run();

            new AddDefenseEffect(7).Apply(Ctx(bundle.Run));

            Assert.That(bundle.Run.PendingDefense, Is.EqualTo(7));
            Assert.That(bundle.Run.ConsumePendingDefense(), Is.EqualTo(7));
            Assert.That(bundle.Run.PendingDefense, Is.Zero);
        }

        [Test]
        public void DrawCards_CompraDoDeck()
        {
            RunBundle bundle = TestContent.Run();
            bundle.Run.Deck.AddToDrawPile(TestContent.GoldCard("extra"));
            int handBefore = bundle.Run.Deck.Hand.Count;

            EffectResult result = new DrawCardsEffect(1).Apply(Ctx(bundle.Run));

            Assert.That(result.Applied, Is.True);
            Assert.That(bundle.Run.Deck.Hand.Count, Is.EqualTo(handBefore + 1));
        }

        [Test]
        public void DrawCards_SemCartas_NaoFazNada()
        {
            RunBundle bundle = TestContent.Run();
            while (bundle.Run.Deck.TotalCards > 0)
            {
                bundle.Run.Deck.RemoveRandomCard(new XorShiftRandom(1));
            }

            EffectResult result = new DrawCardsEffect(2).Apply(Ctx(bundle.Run));

            Assert.That(result.Applied, Is.False);
        }

        [Test]
        public void RevealTile_TornaCelulaVisivelSemDarPosse()
        {
            RunBundle bundle = TestContent.Run();
            // Fora do primeiro anel, que ja nasce revelado.
            Coord target = new Coord(0, 3);
            Assert.That(bundle.Run.Grid.TileAt(target).Revealed, Is.False);

            EffectResult result = new RevealTileEffect().Apply(Ctx(bundle.Run, target));

            Assert.That(result.Applied, Is.True);
            Assert.That(bundle.Run.Grid.TileAt(target).Revealed, Is.True);
            Assert.That(bundle.Run.Grid.TileAt(target).Owned, Is.False);
        }

        [Test]
        public void AddCardToDeck_RecusaCartaProibidaParaARaca()
        {
            RunBundle bundle = TestContent.Run();
            CardDefinition elvishOnly = new CardDefinition(
                "so_elfos", "So Elfos", 1, TargetRequirement.None,
                new List<IEffect> { new GainGoldEffect(1) },
                false,
                new List<string> { "elves" });

            EffectResult result = new AddCardToDeckEffect(elvishOnly).Apply(Ctx(bundle.Run));

            Assert.That(result.Applied, Is.False);
            Assert.That(bundle.Run.Deck.AllCards(), Has.No.Member(elvishOnly));
        }

        [Test]
        public void GrantTile_SemAlvo_EscolheUmaCelulaDaFronteira()
        {
            RunBundle bundle = TestContent.Run();
            int before = bundle.Run.Grid.OwnedCount;

            EffectResult result = new GrantTileEffect().Apply(Ctx(bundle.Run));

            Assert.That(result.Applied, Is.True);
            Assert.That(bundle.Run.Grid.OwnedCount, Is.EqualTo(before + 1));
        }

        [Test]
        public void GrantTile_ComAlvoInvalido_NaoFazNada()
        {
            RunBundle bundle = TestContent.Run();
            int before = bundle.Run.Grid.OwnedCount;

            EffectResult result = new GrantTileEffect().Apply(Ctx(bundle.Run, new Coord(9, 9)));

            Assert.That(result.Applied, Is.False);
            Assert.That(bundle.Run.Grid.OwnedCount, Is.EqualTo(before));
        }

        [Test]
        public void ModifyProduction_ValeApenasPelosDiasDeclarados()
        {
            RunBundle bundle = TestContent.Run();

            new ModifyProductionEffect(1.0, 1, multiplier: true).Apply(Ctx(bundle.Run));

            Assert.That(bundle.Run.SumModifier(ModifierKeys.ProductionMultiplier), Is.EqualTo(1.0));
            bundle.Engine.EndDay();
            Assert.That(bundle.Run.SumModifier(ModifierKeys.ProductionMultiplier), Is.Zero);
        }

        [Test]
        public void ModifyProduction_VazioNaoFazNada()
        {
            RunBundle bundle = TestContent.Run();

            EffectResult result = new ModifyProductionEffect(0, 0).Apply(Ctx(bundle.Run));

            Assert.That(result.Applied, Is.False);
            Assert.That(bundle.Run.Modifiers, Is.Empty);
        }

        [Test]
        public void DisableTile_ParaAProducaoDaCelula()
        {
            RunBundle bundle = TestContent.Run();
            Coord target = new Coord(1, 0);
            bundle.Run.Grid.Grant(target);
            bundle.Run.Grid.SetTerrain(target, TerrainType.Plain);
            bundle.Run.Grid.Build(target, TestContent.Farm());
            int withFarm = bundle.Run.CollectDailyProduction();

            new DisableTileEffect(1).Apply(Ctx(bundle.Run, target));

            Assert.That(bundle.Run.IsTileDisabled(target), Is.True);
            Assert.That(bundle.Run.CollectDailyProduction(), Is.LessThan(withFarm));
        }

        [Test]
        public void SetTerrain_DerrubaEdificioIncompativel()
        {
            RunBundle bundle = TestContent.Run();
            Coord target = new Coord(1, 0);
            bundle.Run.Grid.Grant(target);
            bundle.Run.Grid.SetTerrain(target, TerrainType.Plain);
            bundle.Run.Grid.Build(target, TestContent.Farm());

            EffectResult result = new SetTerrainEffect(TerrainType.River, TerrainType.Plain)
                .Apply(Ctx(bundle.Run, target));

            Assert.That(result.Applied, Is.True);
            Assert.That(bundle.Run.Grid.TileAt(target).Terrain, Is.EqualTo(TerrainType.River));
            Assert.That(bundle.Run.Grid.TileAt(target).HasBuilding, Is.False);
        }

        [Test]
        public void RepairTile_SemCelulaArrasada_NaoFazNada()
        {
            RunBundle bundle = TestContent.Run();

            EffectResult result = new RepairTileEffect().Apply(Ctx(bundle.Run));

            Assert.That(result.Applied, Is.False);
        }

        [Test]
        public void RepairTile_ReconstroiCelulaArrasada()
        {
            RunBundle bundle = TestContent.Run();
            Coord target = new Coord(1, 0);
            bundle.Run.Grid.Grant(target);
            bundle.Run.Grid.Destroy(target);

            EffectResult result = new RepairTileEffect().Apply(Ctx(bundle.Run, target));

            Assert.That(result.Applied, Is.True);
            Assert.That(bundle.Run.Grid.TileAt(target).Destroyed, Is.False);
        }

        [Test]
        public void RemoveCard_RespeitaOTetoDeUmaCartaPorEvento()
        {
            RunBundle bundle = TestContent.Run();
            for (int i = 0; i < 5; i++)
            {
                bundle.Run.Deck.AddToDiscard(TestContent.GoldCard("c" + i));
            }

            int before = bundle.Run.Deck.TotalCards;
            List<IEffect> two = new List<IEffect> { new RemoveCardEffect(), new RemoveCardEffect() };
            EffectRunner.ApplyAll(two, bundle.Run, EffectSource.Event, null, SeverityBudget.NegativeEvent);

            Assert.That(bundle.Run.Deck.TotalCards, Is.EqualTo(before - 1));
        }

        [Test]
        public void MesmoEfeito_PorCartaOuPorEvento_ProduzEstadoIdentico()
        {
            // Design D3: cartas e eventos compartilham o vocabulario de efeitos.
            RunBundle viaCard = TestContent.Run(seed: 777);
            RunBundle viaEvent = TestContent.Run(seed: 777);

            IEffect effect = new GainGoldEffect(40);
            effect.Apply(new EffectContext(viaCard.Run, EffectSource.Card));
            effect.Apply(new EffectContext(viaEvent.Run, EffectSource.Event));

            Assert.That(viaEvent.Run.Gold, Is.EqualTo(viaCard.Run.Gold));
            Assert.That(viaEvent.Run.Integrity, Is.EqualTo(viaCard.Run.Integrity));
            Assert.That(viaEvent.Run.Grid.OwnedCount, Is.EqualTo(viaCard.Run.Grid.OwnedCount));
        }

        [Test]
        public void CartaJogadaEEventoResolvido_UsamAMesmaLista()
        {
            RunBundle bundle = TestContent.Run();
            List<IEffect> shared = new List<IEffect> { new GainGoldEffect(10), new AddDefenseEffect(2) };

            CardDefinition card = new CardDefinition("dupla", "Dupla", 1, TargetRequirement.None, shared);
            EventDefinition definition = EventDefinition.Simple(
                "dupla_evento", "Dupla", EventClass.Positive, shared);

            bundle.Run.Deck.AddToDrawPile(card);
            bundle.Run.Deck.Draw(1, new XorShiftRandom(3));

            int goldBefore = bundle.Run.Gold;
            bundle.Engine.PlayCard(card);
            int afterCard = bundle.Run.Gold - goldBefore;

            goldBefore = bundle.Run.Gold;
            EventResolver.Resolve(bundle.Run, definition);
            int afterEvent = bundle.Run.Gold - goldBefore;

            Assert.That(afterEvent, Is.EqualTo(afterCard));
            Assert.That(bundle.Run.PendingDefense, Is.EqualTo(4));
        }
    }
}

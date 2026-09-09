using System.Collections.Generic;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    [TestFixture]
    public class KingdomGridTests
    {
        [Test]
        public void Harness_Runs()
        {
            // Tarefa 1.4: prova que a suite roda, no Unity e no harness dotnet.
            Assert.That(new Coord(1, 2).ToString(), Is.EqualTo("(1,2)"));
        }

        [Test]
        public void NovaCelula_NasceNaoPossuidaENaoRevelada()
        {
            KingdomGrid grid = TestContent.GridWithHall();

            Tile tile = grid.TileAt(new Coord(5, 5));

            Assert.That(tile.Owned, Is.False);
            Assert.That(tile.Revealed, Is.False);
            Assert.That(tile.HasBuilding, Is.False);
            Assert.That(tile.Destroyed, Is.False);
        }

        [Test]
        public void ReinoDeUmaCelula_OfereceExatamenteQuatroCompraveis()
        {
            KingdomGrid grid = TestContent.GridWithHall();

            List<Coord> purchasable = grid.PurchasableCoords();

            Assert.That(grid.OwnedCount, Is.EqualTo(1));
            Assert.That(purchasable.Count, Is.EqualTo(4));
            Assert.That(purchasable, Contains.Item(new Coord(0, 1)));
            Assert.That(purchasable, Contains.Item(new Coord(1, 0)));
            Assert.That(purchasable, Contains.Item(new Coord(0, -1)));
            Assert.That(purchasable, Contains.Item(new Coord(-1, 0)));
        }

        [Test]
        public void CelulaDistante_NaoEComprado()
        {
            KingdomGrid grid = TestContent.GridWithHall();

            GridResult result = grid.CanPurchase(new Coord(3, 3));

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Rejection, Is.EqualTo(GridRejection.NotAdjacent));
            Assert.That(grid.PurchasableCoords(), Has.No.Member(new Coord(3, 3)));
        }

        [Test]
        public void Reparo_DevolveACelulaAoJogo()
        {
            RunBundle bundle = TestContent.Run();
            Coord target = new Coord(1, 0);
            bundle.Run.Grid.Grant(target);
            bundle.Run.Grid.SetTerrain(target, TerrainType.Plain);
            bundle.Run.Grid.Destroy(target);
            int goldBefore = bundle.Run.Gold;
            int cost = bundle.Run.Grid.CostCurve.RepairCost;

            CommandResult result = bundle.Engine.RepairTile(target);

            Assert.That(result.Ok, Is.True);
            Assert.That(bundle.Run.Gold, Is.EqualTo(goldBefore - cost));
            Assert.That(bundle.Run.Grid.TileAt(target).Destroyed, Is.False);
            Assert.That(bundle.Engine.BuildOn(target, TestContent.Farm()).Ok, Is.True);
        }

        [Test]
        public void Reparo_SemOuro_ERecusado()
        {
            RunBundle bundle = TestContent.Run(TestContent.Humans(gold: 0));
            Coord target = new Coord(1, 0);
            bundle.Run.Grid.Grant(target);
            bundle.Run.Grid.Destroy(target);

            CommandResult result = bundle.Engine.RepairTile(target);

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.NotEnoughGold));
            Assert.That(bundle.Run.Grid.TileAt(target).Destroyed, Is.True);
        }

        [Test]
        public void Reparo_DeCelulaIntacta_ERecusado()
        {
            RunBundle bundle = TestContent.Run();

            CommandResult result = bundle.Engine.RepairTile(Coord.Zero);

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.InvalidTarget));
        }

        [Test]
        public void DestruicaoNaoEAmputacaoPermanente()
        {
            // Sem comando de reparo, uma run cujas celulas foram arrasadas ficava
            // travada mesmo com ouro sobrando, dependendo do sorteio de uma carta.
            RunBundle bundle = TestContent.Run();
            Coord target = new Coord(1, 0);
            bundle.Run.Grid.Grant(target);
            bundle.Run.Grid.SetTerrain(target, TerrainType.Plain);
            bundle.Run.Grid.Destroy(target);

            Assert.That(bundle.Run.Deck.Hand, Is.Empty.Or.Not.Empty);
            Assert.That(bundle.Run.CanAfford(bundle.Run.Grid.CostCurve.RepairCost), Is.True);
            Assert.That(bundle.Engine.RepairTile(target).Ok, Is.True,
                "existe acao que devolve o reino ao jogo sem depender de carta");
        }

        [Test]
        public void PrimeiroAnel_NasceRevelado()
        {
            KingdomGrid grid = TestContent.GridWithHall();

            foreach (Coord neighbor in Coord.Zero.Neighbors())
            {
                Assert.That(grid.TileAt(neighbor).Revealed, Is.True, neighbor + " deveria nascer revelada");
                Assert.That(grid.TileAt(neighbor).Owned, Is.False, "revelar nao pode dar posse");
            }
        }

        [Test]
        public void SegundoAnel_ContinuaOculto()
        {
            KingdomGrid grid = TestContent.GridWithHall();

            grid.Grant(new Coord(1, 0));

            // (2,0) so virou fronteira agora: e comprável, mas segue incognita.
            Tile beyond = grid.TileAt(new Coord(2, 0));
            Assert.That(grid.PurchasableCoords(), Contains.Item(new Coord(2, 0)));
            Assert.That(beyond.Revealed, Is.False);
        }

        [Test]
        public void Compra_TomaPosseERevelaTerreno()
        {
            Dictionary<Coord, TerrainType> terrain = new Dictionary<Coord, TerrainType>
            {
                { new Coord(1, 0), TerrainType.Mine }
            };
            KingdomGrid grid = TestContent.GridWithHall(TerrainType.Plain, terrain);

            GridResult result = grid.Purchase(new Coord(1, 0));

            Assert.That(result.Ok, Is.True);
            Assert.That(grid.OwnedCount, Is.EqualTo(2));
            Tile tile = grid.TileAt(new Coord(1, 0));
            Assert.That(tile.Owned, Is.True);
            Assert.That(tile.Revealed, Is.True);
            Assert.That(tile.Terrain, Is.EqualTo(TerrainType.Mine));
        }

        [Test]
        public void CustoDeExpansao_CresceComTerritorio()
        {
            TileCostCurve curve = new TileCostCurve();

            int costWithOne = curve.CostFor(1);
            int costWithNine = curve.CostFor(9);

            Assert.That(costWithNine, Is.GreaterThan(costWithOne));
        }

        [Test]
        public void CustoDeExpansao_EMonotonico()
        {
            TileCostCurve curve = new TileCostCurve();

            for (int owned = 1; owned < 30; owned++)
            {
                Assert.That(
                    curve.CostFor(owned + 1),
                    Is.GreaterThan(curve.CostFor(owned)),
                    "custo deve crescer de " + owned + " para " + (owned + 1));
            }
        }

        [Test]
        public void MesmaSemente_GeraMesmoMapa()
        {
            RunRandom a = new RunRandom(4242);
            RunRandom b = new RunRandom(4242);
            SeededTerrainGenerator genA = new SeededTerrainGenerator(a, Coord.Zero);
            SeededTerrainGenerator genB = new SeededTerrainGenerator(b, Coord.Zero);

            for (int x = -6; x <= 6; x++)
            {
                for (int y = -6; y <= 6; y++)
                {
                    Coord coord = new Coord(x, y);
                    Assert.That(genA.TerrainAt(coord), Is.EqualTo(genB.TerrainAt(coord)), "divergiu em " + coord);
                }
            }
        }

        [Test]
        public void SementesDiferentes_GeramMapasDiferentes()
        {
            SeededTerrainGenerator genA = new SeededTerrainGenerator(new RunRandom(1), Coord.Zero);
            SeededTerrainGenerator genB = new SeededTerrainGenerator(new RunRandom(2), Coord.Zero);

            int differences = 0;
            for (int x = -6; x <= 6; x++)
            {
                for (int y = -6; y <= 6; y++)
                {
                    Coord coord = new Coord(x, y);
                    if (genA.TerrainAt(coord) != genB.TerrainAt(coord))
                    {
                        differences++;
                    }
                }
            }

            Assert.That(differences, Is.GreaterThan(20), "mapas praticamente iguais para sementes distintas");
        }

        [Test]
        public void Terreno_NaoDependeDaOrdemDeCompra()
        {
            // A geracao e por coordenada, nao por sequencia: expandir para o outro
            // lado primeiro nao pode mudar o terreno de nenhuma celula.
            SeededTerrainGenerator generator = new SeededTerrainGenerator(new RunRandom(99), Coord.Zero);
            KingdomGrid gridA = new KingdomGrid(generator, Coord.Zero);
            gridA.PlaceHall(TestContent.Hall());
            KingdomGrid gridB = new KingdomGrid(generator, Coord.Zero);
            gridB.PlaceHall(TestContent.Hall());

            gridA.Grant(new Coord(1, 0));
            gridA.Grant(new Coord(2, 0));
            gridB.Grant(new Coord(0, 1));
            gridB.Grant(new Coord(1, 0));
            gridB.Grant(new Coord(2, 0));

            Assert.That(gridA.TileAt(new Coord(2, 0)).Terrain, Is.EqualTo(gridB.TileAt(new Coord(2, 0)).Terrain));
        }

        [Test]
        public void Construcao_ExigeTerrenoCompativel()
        {
            Dictionary<Coord, TerrainType> terrain = new Dictionary<Coord, TerrainType>
            {
                { new Coord(1, 0), TerrainType.Mine }
            };
            KingdomGrid grid = TestContent.GridWithHall(TerrainType.Plain, terrain);
            grid.Grant(new Coord(1, 0));

            GridResult result = grid.Build(new Coord(1, 0), TestContent.Sawmill());

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Rejection, Is.EqualTo(GridRejection.TerrainNotAllowed));
            Assert.That(grid.TileAt(new Coord(1, 0)).HasBuilding, Is.False);
        }

        [Test]
        public void Construcao_RecusaCelulaOcupada()
        {
            KingdomGrid grid = TestContent.GridWithHall();
            grid.Grant(new Coord(1, 0));
            grid.Build(new Coord(1, 0), TestContent.Farm());

            GridResult result = grid.Build(new Coord(1, 0), TestContent.Watchtower());

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Rejection, Is.EqualTo(GridRejection.TileOccupied));
            Assert.That(grid.TileAt(new Coord(1, 0)).Building.Id, Is.EqualTo("farm"));
        }

        [Test]
        public void Demolicao_LiberaCelulaParaNovaConstrucao()
        {
            KingdomGrid grid = TestContent.GridWithHall();
            grid.Grant(new Coord(1, 0));
            grid.Build(new Coord(1, 0), TestContent.Farm());

            Assert.That(grid.Demolish(new Coord(1, 0)).Ok, Is.True);
            Assert.That(grid.Build(new Coord(1, 0), TestContent.Watchtower()).Ok, Is.True);
            Assert.That(grid.TileAt(new Coord(1, 0)).Building.Id, Is.EqualTo("watchtower"));
        }

        [Test]
        public void Serraria_ProduzMaisComFlorestasAdjacentes()
        {
            Dictionary<Coord, TerrainType> withForest = new Dictionary<Coord, TerrainType>
            {
                { new Coord(1, 0), TerrainType.Forest },
                { new Coord(2, 0), TerrainType.Forest },
                { new Coord(1, 1), TerrainType.Forest }
            };
            KingdomGrid rich = TestContent.GridWithHall(TerrainType.Plain, withForest);
            rich.Grant(new Coord(1, 0));
            rich.Build(new Coord(1, 0), TestContent.Sawmill());

            Dictionary<Coord, TerrainType> lonely = new Dictionary<Coord, TerrainType>
            {
                { new Coord(1, 0), TerrainType.Forest }
            };
            KingdomGrid poor = TestContent.GridWithHall(TerrainType.Plain, lonely);
            poor.Grant(new Coord(1, 0));
            poor.Build(new Coord(1, 0), TestContent.Sawmill());

            ProductionBreakdown richProduction =
                ProductionCalculator.ForTile(rich, rich.TileAt(new Coord(1, 0)));
            ProductionBreakdown poorProduction =
                ProductionCalculator.ForTile(poor, poor.TileAt(new Coord(1, 0)));

            Assert.That(richProduction.Total, Is.GreaterThan(poorProduction.Total));
        }

        [Test]
        public void Producao_ExplicaCadaParcela()
        {
            Dictionary<Coord, TerrainType> terrain = new Dictionary<Coord, TerrainType>
            {
                { new Coord(1, 0), TerrainType.Forest },
                { new Coord(2, 0), TerrainType.Forest }
            };
            KingdomGrid grid = TestContent.GridWithHall(TerrainType.Plain, terrain);
            grid.Grant(new Coord(1, 0));
            grid.Build(new Coord(1, 0), TestContent.Sawmill());

            ProductionBreakdown breakdown = ProductionCalculator.ForTile(grid, grid.TileAt(new Coord(1, 0)));

            Assert.That(breakdown.Lines.Count, Is.EqualTo(2), "esperado base + adjacencia");
            Assert.That(breakdown.Lines[0].Reason, Is.EqualTo("Serraria"));
            Assert.That(breakdown.Lines[1].Reason, Does.Contain("adjacencia"));
            int sum = 0;
            for (int i = 0; i < breakdown.Lines.Count; i++)
            {
                sum += breakdown.Lines[i].Gold;
            }

            Assert.That(sum, Is.EqualTo(breakdown.Total));
        }

        [Test]
        public void CelulaArrasada_ParaDeProduzirEContinuaPossuida()
        {
            KingdomGrid grid = TestContent.GridWithHall();
            grid.Grant(new Coord(1, 0));
            grid.Build(new Coord(1, 0), TestContent.Farm());
            int before = ProductionCalculator.ForTile(grid, grid.TileAt(new Coord(1, 0))).Total;

            grid.Destroy(new Coord(1, 0));

            Tile tile = grid.TileAt(new Coord(1, 0));
            Assert.That(before, Is.GreaterThan(0));
            Assert.That(tile.Owned, Is.True, "celula arrasada continua possuida");
            Assert.That(tile.HasBuilding, Is.False);
            Assert.That(ProductionCalculator.ForTile(grid, tile).Total, Is.Zero);
            Assert.That(grid.OwnedCount, Is.EqualTo(2));
        }

        [Test]
        public void CelulaArrasada_PodeSerReconstruida()
        {
            KingdomGrid grid = TestContent.GridWithHall();
            grid.Grant(new Coord(1, 0));
            grid.Destroy(new Coord(1, 0));

            Assert.That(grid.Build(new Coord(1, 0), TestContent.Farm()).Rejection,
                Is.EqualTo(GridRejection.TileDestroyed));
            Assert.That(grid.Repair(new Coord(1, 0)).Ok, Is.True);
            Assert.That(grid.Build(new Coord(1, 0), TestContent.Farm()).Ok, Is.True);
        }

        [Test]
        public void RacaAnoes_LimitaORaioDoTerritorio()
        {
            RuleModifiers dwarves = new RuleModifiers(new Dictionary<string, double>
            {
                { RuleKeys.MaxDistanceFromHall, 2 }
            });
            KingdomGrid grid = TestContent.GridWithHall();
            grid.Grant(new Coord(1, 0));
            grid.Grant(new Coord(2, 0));

            GridResult beyond = grid.CanPurchase(new Coord(3, 0), dwarves);

            Assert.That(beyond.Ok, Is.False);
            Assert.That(beyond.Rejection, Is.EqualTo(GridRejection.BeyondRaceRadius));
            Assert.That(grid.CanPurchase(new Coord(3, 0)).Ok, Is.True, "sem a raca, a mesma compra e valida");
        }

        [Test]
        public void RacaAnoes_ReduzOCustoDaCelula()
        {
            RuleModifiers dwarves = new RuleModifiers(new Dictionary<string, double>
            {
                { RuleKeys.TileCostMultiplier, 0.7 }
            });
            KingdomGrid grid = TestContent.GridWithHall();

            Assert.That(grid.NextTileCost(dwarves), Is.LessThan(grid.NextTileCost()));
        }

        [Test]
        public void BorderTiles_SaoAsCelulasComVizinhoLivre()
        {
            KingdomGrid grid = TestContent.GridWithHall();
            TestContent.GrantTiles(grid,
                new Coord(1, 0), new Coord(-1, 0), new Coord(0, 1), new Coord(0, -1));

            List<Tile> border = grid.BorderTiles();

            // O Salao esta cercado pelos quatro lados, entao nao e borda.
            Assert.That(border.Count, Is.EqualTo(4));
            foreach (Tile tile in border)
            {
                Assert.That(tile.Coord, Is.Not.EqualTo(Coord.Zero));
            }
        }
    }
}

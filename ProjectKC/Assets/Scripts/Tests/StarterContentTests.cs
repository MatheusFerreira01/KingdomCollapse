using System.Collections.Generic;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    /// <summary>
    /// O conteudo embutido e o jogo que o jogador joga. Estes testes existem para que
    /// mexer num numero de carta, edificio ou rival nao quebre a run em silencio.
    /// </summary>
    public sealed class StarterContentTests
    {
        private static RunBundle Build(int seed = 7, int level = 1, MetaTree tree = null, MetaProfile profile = null)
        {
            return RunBuilder.Build(
                StarterContent.CreateSetup(seed, level), StarterContent.BuildCatalog(), tree, profile);
        }

        [Test]
        public void CatalogoEmbutido_PassaEmTodaValidacao()
        {
            List<CatalogViolation> violations = StarterContent.Validate();

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void RunComeca_ComOsCincoRecursosEGenteParaOperar()
        {
            RunState run = Build().Run;

            Assert.That(run[ResourceKind.Gold], Is.GreaterThan(0));
            Assert.That(run[ResourceKind.Wood], Is.GreaterThan(0));
            Assert.That(run[ResourceKind.Food], Is.GreaterThan(0));
            Assert.That(run[ResourceKind.Population], Is.GreaterThan(0));
            Assert.That(run.PopulationCapacity(), Is.GreaterThanOrEqualTo(run[ResourceKind.Population]),
                "o Salao precisa abrigar a gente inicial");
            Assert.That(run.Threats.HasRivals, Is.True, "toda run tem rivais nomeados");
        }

        [Test]
        public void PrimeiroDia_NaoTemFomeAnunciada()
        {
            RunBundle bundle = Build();
            bundle.Engine.StartRun();

            foreach (RunEvent runEvent in bundle.Engine.DrainEvents())
            {
                Assert.That(runEvent, Is.Not.InstanceOf<FamineWarningEvent>(),
                    "abrir a run ja devendo comida ensina o jogo errado");
            }
        }

        [Test]
        public void TodoTerrenoConstruivel_TemEdificioQueProduzNele()
        {
            List<BuildingDefinition> buildings = StarterContent.Buildings();

            foreach (TerrainType terrain in new[]
                     {
                         TerrainType.Plain, TerrainType.Forest, TerrainType.Mine, TerrainType.River, TerrainType.Ruin
                     })
            {
                bool any = false;
                for (int i = 0; i < buildings.Count; i++)
                {
                    if (buildings[i].Id != StarterContent.Hall && buildings[i].CanBuildOn(terrain))
                    {
                        any = true;
                    }
                }

                Assert.That(any, Is.True, "nada pode ser construido em " + terrain);
            }
        }

        [Test]
        public void CadaMaterial_TemUmaFonteEntreOsEdificiosIniciais()
        {
            ContentCatalog catalog = StarterContent.BuildCatalog();

            foreach (ResourceKind kind in new[] { ResourceKind.Gold, ResourceKind.Wood, ResourceKind.Stone, ResourceKind.Food })
            {
                bool produced = false;
                foreach (string id in catalog.StartingBuildings)
                {
                    if (catalog.Buildings[id].Production[kind] > 0)
                    {
                        produced = true;
                    }
                }

                Assert.That(produced, Is.True, "nenhum edificio inicial produz " + kind);
            }
        }

        [Test]
        public void ConteudoDaArvore_ComecaBloqueado()
        {
            ContentCatalog catalog = StarterContent.BuildCatalog();

            Assert.That(catalog.StartingBuildings, Does.Not.Contain(StarterContent.Market));
            Assert.That(catalog.StartingBuildings, Does.Not.Contain(StarterContent.Barracks));
            Assert.That(catalog.StartingCards, Does.Not.Contain(StarterContent.CardShieldWall));
        }

        [Test]
        public void TodoNoDaArvore_ApareceNoDesenhoDaTela()
        {
            MetaTree tree = StarterContent.BuildMetaTree();
            HashSet<string> drawn = new HashSet<string>();
            for (int c = 0; c < StarterContent.MetaTreeColumns.Length; c++)
            {
                for (int r = 0; r < StarterContent.MetaTreeColumns[c].Length; r++)
                {
                    Assert.That(tree.Get(StarterContent.MetaTreeColumns[c][r]), Is.Not.Null,
                        "a tela desenha um no que nao existe: " + StarterContent.MetaTreeColumns[c][r]);
                    drawn.Add(StarterContent.MetaTreeColumns[c][r]);
                }
            }

            // Os galhos de raca sao desenhados na aba da propria raca.
            for (int i = 0; i < StarterContent.RaceIds.Length; i++)
            {
                string[] branch = StarterContent.RaceBranch(StarterContent.RaceIds[i]);
                for (int r = 0; r < branch.Length; r++)
                {
                    Assert.That(tree.Get(branch[r]), Is.Not.Null, "galho desenha um no que nao existe: " + branch[r]);
                    Assert.That(tree.Get(branch[r]).RaceBranchId, Is.EqualTo(StarterContent.RaceIds[i]));
                    drawn.Add(branch[r]);
                }
            }

            foreach (string id in tree.Nodes.Keys)
            {
                Assert.That(drawn, Does.Contain(id), "no fora da tela: " + id);
            }

            Assert.That(StarterContent.MetaTreeColumnTitles.Length, Is.EqualTo(StarterContent.MetaTreeColumns.Length));
        }

        [Test]
        public void OBot_VenceONivelUmNumaFracaoRazoavelDasRuns()
        {
            // Alvo de calibracao (tarefa 11.2), com folga: fora desta faixa o nivel 1
            // virou passeio ou muro, e a causa e algum numero mexido sem medir.
            ContentCatalog catalog = StarterContent.BuildCatalog();
            SimulationReport report = SimulationHarness.RunBatch(
                160, seed => StarterContent.CreateSetup(seed + 1, 1), catalog, new StewardPolicy());

            Assert.That(report.WinFraction, Is.InRange(0.35, 0.85), report.Describe());
            Assert.That(report.MedianWinDay, Is.InRange(25, 50), "a campanha vencida deve durar perto de 35 dias");
        }

        [Test]
        public void NiveisMaisAltos_SaoMaisDurosParaOMesmoBot()
        {
            ContentCatalog catalog = StarterContent.BuildCatalog();
            double first = SimulationHarness.RunBatch(
                120, seed => StarterContent.CreateSetup(seed + 1, 1), catalog, new StewardPolicy()).WinFraction;
            double second = SimulationHarness.RunBatch(
                120, seed => StarterContent.CreateSetup(seed + 1, 2), catalog, new StewardPolicy()).WinFraction;

            Assert.That(second, Is.LessThan(first));
        }

        [Test]
        public void RunSimulada_NaoPassaDoLimiteDeDias()
        {
            SimulationResult result = SimulationHarness.RunOnce(
                StarterContent.CreateSetup(3, 1), StarterContent.BuildCatalog(), new StewardPolicy());

            Assert.That(result.CollapseDay, Is.LessThan(SimulationHarness.MaxDays),
                "uma run que nunca acaba significa campanha sem desfecho");
        }

        // --- Bot (7.1 a 7.3) ---

        [Test]
        public void Bot_GuarneceNoDiaDoAtaqueEVoltaAoCampoDepois()
        {
            RunBundle bundle = Build(11);
            bundle.Engine.StartRun();
            StewardPolicy bot = new StewardPolicy();

            bool sawAttackDay = false;
            bool sawCalmDay = false;

            for (int day = 0; day < 20 && !bundle.Run.IsOver; day++)
            {
                bool attackToday = false;
                foreach (ScheduledThreat threat in bundle.Run.Threats.Pending)
                {
                    RivalIdentity identity = threat.Rival?.Identity;
                    if (threat.DaysUntil(bundle.Run.Day) == 0 && (identity == null || identity.IsCounteredByDefense))
                    {
                        attackToday = true;
                    }
                }

                bot.PlanDay(bundle.Engine, bundle);

                if (attackToday)
                {
                    sawAttackDay = true;
                    Assert.That(bundle.Run.Stance, Is.EqualTo(WorkerStance.DefenseFirst));
                }
                else if (bundle.Run.Threats.Pending.Count == 0 || bundle.Run.Threats.NextThreat(bundle.Run.Day).DaysUntil(bundle.Run.Day) > 0)
                {
                    sawCalmDay = true;
                    Assert.That(bundle.Run.Stance, Is.EqualTo(WorkerStance.ProductionFirst));
                }

                bundle.Engine.EndDay();
            }

            Assert.That(sawAttackDay && sawCalmDay, Is.True, "o teste precisa ver os dois tipos de dia");
        }

        [Test]
        public void Bot_NaoDeixaAGentePassarFomeEmCondicoesFolgadas()
        {
            RunBundle bundle = Build(5);
            bundle.Engine.StartRun();
            StewardPolicy bot = new StewardPolicy();
            int starved = 0;

            for (int day = 0; day < 8 && !bundle.Run.IsOver; day++)
            {
                bot.PlanDay(bundle.Engine, bundle);
                bundle.Engine.DrainEvents();
                bundle.Engine.EndDay();
                foreach (RunEvent runEvent in bundle.Engine.DrainEvents())
                {
                    if (runEvent is FoodResolvedEvent food)
                    {
                        starved += food.Starved;
                    }
                }
            }

            Assert.That(starved, Is.Zero, "nos primeiros dias nada pressiona a comida: fome aqui e descuido do bot");
        }

        [Test]
        public void Bot_GuardaCartaDeDefesaParaODiaDoAtaque()
        {
            RunBundle bundle = Build(9);
            bundle.Engine.StartRun();
            StewardPolicy bot = new StewardPolicy();

            // No dia 1 nenhum ataque chega: a defesa temporaria precisa continuar zero.
            bot.PlanDay(bundle.Engine, bundle);

            Assert.That(bundle.Run.PendingDefense, Is.Zero);
        }
    }
}

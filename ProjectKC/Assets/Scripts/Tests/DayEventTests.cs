using System.Collections.Generic;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    [TestFixture]
    public class DayEventTests
    {
        private static IRandomSource Rng(int seed = 11) => new XorShiftRandom(seed);

        private static EventDefinition Positive(string id, params IEffect[] effects)
        {
            return EventDefinition.Simple(id, id, EventClass.Positive, new List<IEffect>(effects), cooldownDays: 1);
        }

        private static EventDefinition Neutral(string id, params IEffect[] effects)
        {
            return EventDefinition.Simple(id, id, EventClass.Neutral, new List<IEffect>(effects), cooldownDays: 1);
        }

        private static EventDefinition Negative(string id, params IEffect[] effects)
        {
            return EventDefinition.Simple(id, id, EventClass.Negative, new List<IEffect>(effects), cooldownDays: 1);
        }

        [Test]
        public void NoMaximoUmEventoPorDia()
        {
            EventPool pool = new EventPool(new List<EventDefinition>
            {
                Positive("a", new GainGoldEffect(5)),
                Positive("b", new GainGoldEffect(5)),
                Positive("c", new GainGoldEffect(5))
            });
            RunBundle bundle = TestContent.Run();

            EventDefinition drawn = pool.Draw(bundle.Run, Rng());

            Assert.That(drawn, Is.Not.Null);
        }

        [Test]
        public void PoolVazio_NaoSorteiaNemQuebra()
        {
            EventPool pool = new EventPool();
            RunBundle bundle = TestContent.Run();

            Assert.That(pool.Draw(bundle.Run, Rng()), Is.Null);
            bundle.Engine.EndDay();
            Assert.That(bundle.Run.IsOver, Is.False);
        }

        [Test]
        public void EfeitosSoSaoAplicadosDepoisDaEscolha()
        {
            RunBundle bundle = TestContent.Run();
            EventDefinition definition = new EventDefinition(
                "escolha", "Escolha", EventClass.Positive,
                new List<EventOption>
                {
                    new EventOption("Ouro", new List<IEffect> { new GainGoldEffect(50) }),
                    new EventOption("Defesa", new List<IEffect> { new AddDefenseEffect(9) })
                });
            int goldBefore = bundle.Run.Gold;

            EventOutcome outcome = EventResolver.Resolve(bundle.Run, definition, 1);

            Assert.That(outcome.Option.Label, Is.EqualTo("Defesa"));
            Assert.That(bundle.Run.Gold, Is.EqualTo(goldBefore), "a opcao nao escolhida nao pode produzir efeito");
            Assert.That(bundle.Run.PendingDefense, Is.EqualTo(9));
        }

        [Test]
        public void IndiceDeOpcaoInvalido_CaiNaPrimeiraOpcao()
        {
            RunBundle bundle = TestContent.Run();
            EventDefinition definition = new EventDefinition(
                "escolha", "Escolha", EventClass.Positive,
                new List<EventOption>
                {
                    new EventOption("Ouro", new List<IEffect> { new GainGoldEffect(10) }),
                    new EventOption("Defesa", new List<IEffect> { new AddDefenseEffect(5) })
                });

            EventOutcome outcome = EventResolver.Resolve(bundle.Run, definition, 99);

            Assert.That(outcome.Option.Label, Is.EqualTo("Ouro"));
        }

        [Test]
        public void EventoCondicional_FicaForaDoSorteioSemACondicao()
        {
            EventDefinition needsMine = EventDefinition.Simple(
                "mina", "Mina", EventClass.Positive,
                new List<IEffect> { new GainGoldEffect(10) },
                new List<EventCondition> { new OwnsTerrainCondition(TerrainType.Mine) });

            EventPool pool = new EventPool(new List<EventDefinition> { needsMine });
            RunBundle bundle = TestContent.Run();
            bundle.Run.Grid.SetTerrain(Coord.Zero, TerrainType.Plain);

            Assert.That(pool.EligibleFor(bundle.Run), Is.Empty);

            bundle.Run.Grid.Grant(new Coord(1, 0));
            bundle.Run.Grid.SetTerrain(new Coord(1, 0), TerrainType.Mine);

            Assert.That(pool.EligibleFor(bundle.Run).Count, Is.EqualTo(1));
        }

        [Test]
        public void EventoSorteado_NaoRepeteNoDiaSeguinte()
        {
            EventDefinition only = Positive("unico", new GainGoldEffect(1));
            EventPool pool = new EventPool(new List<EventDefinition> { only });
            RunBundle bundle = TestContent.Run();

            bundle.Run.Day = 10;
            EventDefinition first = pool.Draw(bundle.Run, Rng());
            Assert.That(first, Is.Not.Null);

            bundle.Run.Day = 11;
            EventDefinition second = pool.Draw(bundle.Run, Rng());

            Assert.That(second, Is.Null, "o mesmo evento nao pode sair no dia seguinte");
        }

        [Test]
        public void EventoVoltaAoPoolDepoisDoCooldown()
        {
            EventDefinition only = EventDefinition.Simple(
                "unico", "Unico", EventClass.Positive,
                new List<IEffect> { new GainGoldEffect(1) }, cooldownDays: 3);
            EventPool pool = new EventPool(new List<EventDefinition> { only });
            RunBundle bundle = TestContent.Run();

            bundle.Run.Day = 10;
            pool.Draw(bundle.Run, Rng());

            bundle.Run.Day = 13;
            Assert.That(pool.Draw(bundle.Run, Rng()), Is.Null, "ainda dentro do cooldown");

            bundle.Run.Day = 14;
            Assert.That(pool.Draw(bundle.Run, Rng()), Is.Not.Null);
        }

        [Test]
        public void EventoDeDanoLetal_DeixaAIntegridadeEmUmEAcRunContinua()
        {
            RunBundle bundle = TestContent.Run();
            bundle.Run.Damage(bundle.Run.Integrity - 1);
            EventDefinition lethal = Negative("catastrofe", new DamageBaseEffect(999));

            EventResolver.Resolve(bundle.Run, lethal);

            Assert.That(bundle.Run.Integrity, Is.EqualTo(1));
            Assert.That(bundle.Run.CheckCollapse(), Is.False);
        }

        [Test]
        public void EventoNuncaRemoveAUltimaCelula()
        {
            RunBundle bundle = TestContent.Run();
            EventDefinition destructive = Negative("erosao", new DestroyTileEffect());

            EventResolver.Resolve(bundle.Run, destructive);

            Assert.That(bundle.Run.StandingTileCount(), Is.GreaterThanOrEqualTo(1));
            Assert.That(bundle.Run.CheckCollapse(), Is.False);
        }

        [Test]
        public void EventoNegativo_NuncaArrasaCelulaConstruida()
        {
            RunBundle bundle = TestContent.Run();
            Coord target = new Coord(1, 0);
            bundle.Run.Grid.Grant(target);
            bundle.Run.Grid.SetTerrain(target, TerrainType.Plain);
            bundle.Run.Grid.Build(target, TestContent.Farm());

            EventResolver.Resolve(bundle.Run, Negative("erosao", new DestroyTileEffect()));

            Assert.That(bundle.Run.Grid.TileAt(target).Destroyed, Is.False);
            Assert.That(bundle.Run.Grid.TileAt(target).HasBuilding, Is.True);
        }

        [Test]
        public void SeveridadeNaoEscalaComODia()
        {
            EventDefinition tax = Negative("imposto", new LoseGoldEffect(0.2));

            RunBundle earlyRun = TestContent.Run();
            earlyRun.Run.Day = 5;
            int earlyGold = earlyRun.Run.Gold;
            EventResolver.Resolve(earlyRun.Run, tax);
            double earlyLossRatio = (earlyGold - earlyRun.Run.Gold) / (double)earlyGold;

            RunBundle lateRun = TestContent.Run();
            lateRun.Run.Day = 30;
            lateRun.Run.AddGold(500);
            int lateGold = lateRun.Run.Gold;
            EventResolver.Resolve(lateRun.Run, tax);
            double lateLossRatio = (lateGold - lateRun.Run.Gold) / (double)lateGold;

            Assert.That(lateLossRatio, Is.EqualTo(earlyLossRatio).Within(0.02),
                "o custo relativo do mesmo evento deve ser equivalente no dia 5 e no dia 30");
        }

        [Test]
        public void EventoNaoCriaAtaqueNoMesmoDia()
        {
            EventDefinition nasty = Negative("sabotagem", new DamageBaseEffect(1));
            ContentCatalog catalog = TestContent.Catalog(TestContent.Humans(), nasty);
            RunSetup setup = new RunSetup("humans", 5) { FirstThreatDay = 999 };
            RunBundle bundle = RunBuilder.Build(setup, catalog);
            bundle.Engine.StartRun();
            bundle.Engine.DrainEvents();

            bundle.Engine.EndDay();
            List<RunEvent> events = bundle.Engine.DrainEvents();

            for (int i = 0; i < events.Count; i++)
            {
                Assert.That(events[i], Is.Not.InstanceOf<AttackResolvedEvent>(),
                    "nenhum evento pode disparar ataque");
            }
        }

        [Test]
        public void ClasseEDeclaradaNoDado_NaoDeduzidaDosEfeitos()
        {
            EventDefinition definition = Neutral("nevoeiro", new RevealTileEffect());

            Assert.That(definition.Class, Is.EqualTo(EventClass.Neutral));
            Assert.That(SeverityBudget.ForClass(definition.Class), Is.SameAs(SeverityBudget.NeutralEvent));
        }

        [Test]
        public void RunDe30Dias_TemMaioriaDePositivosENeutros()
        {
            List<EventDefinition> catalog = new List<EventDefinition>
            {
                Positive("p1", new GainGoldEffect(5)),
                Positive("p2", new GainGoldEffect(5)),
                Positive("p3", new GainGoldEffect(5)),
                Neutral("n1", new RevealTileEffect()),
                Neutral("n2", new RevealTileEffect()),
                Neutral("n3", new RevealTileEffect()),
                Negative("b1", new LoseGoldEffect(0.1)),
                Negative("b2", new LoseGoldEffect(0.1)),
                Negative("b3", new LoseGoldEffect(0.1))
            };

            int positives = 0;
            int neutrals = 0;
            int negatives = 0;

            // Varias sementes: a spec fala da distribuicao de uma run tipica, entao
            // uma unica semente de sorte nao serve como evidencia.
            for (int seed = 0; seed < 40; seed++)
            {
                EventPool pool = new EventPool(catalog);
                RunBundle bundle = TestContent.Run(seed: seed);
                IRandomSource random = new XorShiftRandom(seed);

                for (int day = 1; day <= 30; day++)
                {
                    bundle.Run.Day = day;
                    EventDefinition drawn = pool.Draw(bundle.Run, random);
                    if (drawn == null)
                    {
                        continue;
                    }

                    switch (drawn.Class)
                    {
                        case EventClass.Positive:
                            positives++;
                            break;
                        case EventClass.Neutral:
                            neutrals++;
                            break;
                        default:
                            negatives++;
                            break;
                    }
                }
            }

            int total = positives + neutrals + negatives;
            Assert.That(total, Is.GreaterThan(0));
            Assert.That(positives + neutrals, Is.GreaterThan(negatives),
                "positivos+neutros=" + (positives + neutrals) + " negativos=" + negatives);
        }

        // --- Validacao de catalogo (tarefa 7.7) ---

        [Test]
        public void CatalogoDentroDoOrcamento_NaoTemViolacao()
        {
            List<EventDefinition> catalog = new List<EventDefinition>
            {
                Positive("caravana", new GainGoldEffect(20)),
                Neutral("nevoeiro", new RevealTileEffect()),
                Negative("imposto", new LoseGoldEffect(0.25)),
                Negative("erosao", new DestroyTileEffect()),
                Negative("incendio", new DamageBaseEffect(0.10, asFraction: true))
            };

            List<CatalogViolation> violations = EventCatalogValidator.Validate(catalog);

            Assert.That(violations, Is.Empty, string.Join(" | ", violations));
        }

        [Test]
        public void EventoAcimaDoOrcamentoDeOuro_EReprovado()
        {
            List<EventDefinition> catalog = new List<EventDefinition>
            {
                Negative("confisco", new LoseGoldEffect(0.80))
            };

            List<CatalogViolation> violations = EventCatalogValidator.Validate(catalog);

            Assert.That(violations.Count, Is.EqualTo(1));
            Assert.That(violations[0].Rule, Is.EqualTo("ouro"));
        }

        [Test]
        public void EventoAcimaDoOrcamentoDeIntegridade_EReprovado()
        {
            List<EventDefinition> catalog = new List<EventDefinition>
            {
                Negative("desastre", new DamageBaseEffect(0.50, asFraction: true))
            };

            List<CatalogViolation> violations = EventCatalogValidator.Validate(catalog);

            Assert.That(violations.Count, Is.EqualTo(1));
            Assert.That(violations[0].Rule, Is.EqualTo("integridade"));
        }

        [Test]
        public void EventoQueArrasaDuasCelulas_EReprovado()
        {
            List<EventDefinition> catalog = new List<EventDefinition>
            {
                Negative("terremoto", new DestroyTileEffect(), new DestroyTileEffect())
            };

            List<CatalogViolation> violations = EventCatalogValidator.Validate(catalog);

            Assert.That(violations.Count, Is.EqualTo(1));
            Assert.That(violations[0].Rule, Is.EqualTo("territorio"));
        }

        [Test]
        public void EventoQueEscalaComODia_EReprovado()
        {
            List<EventDefinition> catalog = new List<EventDefinition>
            {
                Negative("dizimo", new LoseGoldEffect(3, GoldScaling.PerOwnedTile))
            };

            List<CatalogViolation> violations = EventCatalogValidator.Validate(catalog);

            bool foundScaling = false;
            for (int i = 0; i < violations.Count; i++)
            {
                if (violations[i].Rule == "escalada")
                {
                    foundScaling = true;
                }
            }

            Assert.That(foundScaling, Is.True, string.Join(" | ", violations));
        }

        [Test]
        public void EventoNeutroQueTiraCoisa_EReprovado()
        {
            List<EventDefinition> catalog = new List<EventDefinition>
            {
                Neutral("falso_neutro", new LoseGoldEffect(0.10))
            };

            List<CatalogViolation> violations = EventCatalogValidator.Validate(catalog);

            Assert.That(violations.Count, Is.EqualTo(1));
            Assert.That(violations[0].Rule, Is.EqualTo("ouro"));
        }

        [Test]
        public void EventoSemOpcao_EReprovado()
        {
            List<EventDefinition> catalog = new List<EventDefinition>
            {
                new EventDefinition("vazio", "Vazio", EventClass.Neutral, new List<EventOption>())
            };

            List<CatalogViolation> violations = EventCatalogValidator.Validate(catalog);

            Assert.That(violations.Count, Is.EqualTo(1));
            Assert.That(violations[0].Rule, Is.EqualTo("opcoes"));
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    [TestFixture]
    public class RivalTests
    {
        [Test]
        public void TodaIdentidadeDoCatalogoDeclaraOEixoQuePressiona()
        {
            ContentCatalog catalog = new ContentCatalog()
                .AddRival(TestContent.Rival("brutos", TestContent.AssaultIdentity()))
                .AddRival(TestContent.Rival("saqueadores", TestContent.FoodRaidIdentity()));

            List<CatalogViolation> violations = RivalCatalogValidator.Validate(catalog.Rivals.Values);

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
            foreach (RivalDefinition rival in catalog.Rivals.Values)
            {
                Assert.That(rival.Identity.DeclaresAxis, Is.True, rival + " sem eixo");
            }
        }

        [Test]
        public void IdentidadeSemEixoReprovaOCatalogo()
        {
            // Um asset recem-criado nasce com o eixo zerado. Ele precisa reprovar, e
            // nao virar rival de assalto por omissao.
            RivalIdentity blank = new RivalIdentity("blank", "Sem eixo", "Nao decidiu.", PressureAxis.Undeclared);

            List<CatalogViolation> violations = RivalCatalogValidator.Validate(
                new[] { TestContent.Rival("esquecido", blank) });

            Assert.That(violations.Exists(v => v.SubjectId == "esquecido" && v.Rule == "eixo"), Is.True);
        }

        [Test]
        public void EixoDeRecursoPrecisaDizerQualRecurso()
        {
            RivalIdentity vague = new RivalIdentity("vague", "Vago", "Leva algo.", PressureAxis.Resource);

            List<CatalogViolation> violations = RivalCatalogValidator.Validate(
                new[] { TestContent.Rival("vago", vague) });

            Assert.That(violations.Exists(v => v.Rule == "eixo"), Is.True);
        }

        [Test]
        public void PopulacaoNaoEDeclaradaComoRecurso()
        {
            RivalIdentity slavers = new RivalIdentity(
                "slavers", "Captores", "Leva gente.", PressureAxis.Resource, ResourceKind.Population);

            List<CatalogViolation> violations = RivalCatalogValidator.Validate(
                new[] { TestContent.Rival("captores", slavers) });

            Assert.That(violations.Exists(v => v.Rule == "eixo"), Is.True);
        }

        [Test]
        public void RivalSemIdentidadeReprovaOCatalogo()
        {
            RivalDefinition orphan = new RivalDefinition("orfao", "Orfao", null, 3, new RivalCampaignCurve(6, 3));

            List<CatalogViolation> violations = RivalCatalogValidator.Validate(new[] { orphan });

            Assert.That(orphan.Axis, Is.EqualTo(PressureAxis.Undeclared));
            Assert.That(violations.Exists(v => v.Rule == "identidade"), Is.True);
        }

        [Test]
        public void IdentidadeSemDescricaoReprovaOCatalogo()
        {
            // A spec exige que o jogador saiba o que esperar antes do primeiro ataque.
            RivalIdentity mute = new RivalIdentity("mute", "Mudo", "", PressureAxis.Territory);

            List<CatalogViolation> violations = RivalCatalogValidator.Validate(
                new[] { TestContent.Rival("mudo", mute) });

            Assert.That(violations.Exists(v => v.Rule == "identidade"), Is.True);
        }

        [Test]
        public void CampanhaSemAtaquesReprovaOCatalogo()
        {
            List<CatalogViolation> violations = RivalCatalogValidator.Validate(
                new[] { TestContent.Rival("pacifico", attacks: 0) });

            Assert.That(violations.Exists(v => v.Rule == "campanha"), Is.True);
        }

        [Test]
        public void CampanhaQueNaoEscalaReprovaOCatalogo()
        {
            List<CatalogViolation> violations = RivalCatalogValidator.Validate(
                new[] { TestContent.Rival("estavel", attacks: 4, growth: 0) });

            Assert.That(violations.Exists(v => v.Rule == "escalada"), Is.True);
        }

        [Test]
        public void UltimoAtaqueDaCampanhaExigeMaisQueOPrimeiro()
        {
            RivalDefinition rival = TestContent.Rival(attacks: 4, firstForce: 6, growth: 3);

            int first = rival.Campaign.ForceFor(0);
            int last = rival.Campaign.ForceFor(rival.AttacksToDefeat - 1);

            Assert.That(first, Is.EqualTo(6));
            Assert.That(last, Is.GreaterThan(first));
        }

        [Test]
        public void CurvaContinuaCrescendoAlemDoNumeroDeAtaques()
        {
            // Quem falha continua recebendo ataques; se a curva parasse no ultimo
            // previsto, sobreviver sem repelir viraria estrategia.
            RivalCampaignCurve curve = new RivalCampaignCurve(6, 3);

            for (int i = 1; i < 30; i++)
            {
                Assert.That(curve.ForceFor(i), Is.GreaterThan(curve.ForceFor(i - 1)), "ataque " + i);
            }
        }

        [Test]
        public void AssaltoERespondidoComDefesaEEconomicoNao()
        {
            Assert.That(TestContent.AssaultIdentity().IsCounteredByDefense, Is.True);
            Assert.That(new RivalIdentity("i", "i", "d", PressureAxis.Integrity).IsCounteredByDefense, Is.True);
            Assert.That(TestContent.FoodRaidIdentity().IsCounteredByDefense, Is.False);
            Assert.That(new RivalIdentity("p", "p", "d", PressureAxis.Population).IsCounteredByDefense, Is.False);
        }

        // --- Campanha no relogio (4.2) ---

        [Test]
        public void RivalEConhecidoDesdeOComecoDaRun()
        {
            RivalDefinition rival = TestContent.Rival("brutos");

            RunBundle bundle = TestContent.Run(firstThreatDay: 4, rivals: new[] { rival });

            Assert.That(bundle.Run.RivalsAtWar(), Is.EqualTo(new[] { rival }));
        }

        [Test]
        public void RelogioIndicaDeQualRivalVemCadaAtaque()
        {
            RivalDefinition rival = TestContent.Rival("brutos");

            RunBundle bundle = TestContent.Run(firstThreatDay: 4, rivals: new[] { rival });

            IReadOnlyList<ScheduledThreat> pending = bundle.Run.Threats.Pending;
            Assert.That(pending.Count, Is.GreaterThan(0), "o primeiro ataque ja deveria estar anunciado");
            for (int i = 0; i < pending.Count; i++)
            {
                Assert.That(pending[i].Rival, Is.SameAs(rival), pending[i].ToString());
            }
        }

        [Test]
        public void TodoAtaqueDaCampanhaEAnunciadoComAntecedenciaMinima()
        {
            RivalDefinition rival = TestContent.Rival("brutos", attacks: 20, firstForce: 1, growth: 0.2);
            RunBundle bundle = TestContent.Run(firstThreatDay: 4, rivals: new[] { rival });

            for (int i = 0; i < 30 && !bundle.Run.IsOver; i++)
            {
                bundle.Engine.EndDay();
            }

            IReadOnlyList<ScheduledThreat> resolved = bundle.Run.Threats.Resolved;
            Assert.That(resolved.Count, Is.GreaterThan(2), "a campanha mal comecou");

            for (int i = 0; i < resolved.Count; i++)
            {
                Assert.That(resolved[i].Rival, Is.SameAs(rival));
                Assert.That(
                    resolved[i].LeadTime,
                    Is.GreaterThanOrEqualTo(ThreatClock.MinimumLeadDays),
                    resolved[i] + " foi anunciado com antecedencia de " + resolved[i].LeadTime);
            }
        }

        [Test]
        public void ForcaSegueAOrdemDosAtaquesDaCampanha()
        {
            // A forca vem da curva do rival pelo indice do ataque, e nao da curva
            // anonima por dia.
            RivalDefinition rival = TestContent.Rival("brutos", attacks: 3, firstForce: 6, growth: 3);
            ThreatClock clock = new ThreatClock(firstThreatDay: 4, intervalDays: 4, leadDays: 3, rivals: new[] { rival });

            List<ScheduledThreat> announced = new List<ScheduledThreat>();
            for (int day = 1; day <= 12; day++)
            {
                clock.AdmitDue(day);
                announced.AddRange(clock.AnnounceDue(day, 1, new XorShiftRandom(1)));
            }

            Assert.That(announced.Count, Is.EqualTo(3));
            Assert.That(announced[0].Force, Is.EqualTo(rival.Campaign.ForceFor(0)));
            Assert.That(announced[1].Force, Is.EqualTo(rival.Campaign.ForceFor(1)));
            Assert.That(announced[2].Force, Is.EqualTo(rival.Campaign.ForceFor(2)));
            Assert.That(clock.Campaigns[0].AttacksLaunched, Is.EqualTo(3));
        }

        [Test]
        public void RunSemRivaisMantemAAmeacaAnonima()
        {
            RunBundle bundle = TestContent.Run(firstThreatDay: 4);

            Assert.That(bundle.Run.RivalsAtWar(), Is.Empty);
            Assert.That(bundle.Run.Threats.Pending.Count, Is.GreaterThan(0));
            Assert.That(bundle.Run.Threats.Pending[0].Rival, Is.Null);
        }

        // --- Repelir e derrotar (4.3) ---

        private static ScheduledThreat Attack(RivalCampaign campaign, int day = 4, int force = 5)
        {
            return new ScheduledThreat(ThreatKind.Horde, day, force, day - 3, campaign);
        }

        [Test]
        public void RepelirTodosOsAtaquesDerrotaORival()
        {
            RivalCampaign campaign = new RivalCampaign(TestContent.Rival(attacks: 3));
            ThreatClock clock = new ThreatClock();

            Assert.That(clock.RecordOutcome(Attack(campaign), true), Is.Null);
            Assert.That(clock.RecordOutcome(Attack(campaign), true), Is.Null);
            Assert.That(campaign.AttacksRemaining, Is.EqualTo(1));

            RivalCampaign defeated = clock.RecordOutcome(Attack(campaign), true);

            Assert.That(defeated, Is.SameAs(campaign));
            Assert.That(campaign.IsDefeated, Is.True);
        }

        [Test]
        public void AtaqueQuePassaNaoContaEFalharNaoPerdeACampanha()
        {
            RivalCampaign campaign = new RivalCampaign(TestContent.Rival(attacks: 2));
            ThreatClock clock = new ThreatClock();

            clock.RecordOutcome(Attack(campaign), false);
            clock.RecordOutcome(Attack(campaign), false);

            Assert.That(campaign.AttacksRepelled, Is.EqualTo(0));
            Assert.That(campaign.AttacksSuffered, Is.EqualTo(2));
            Assert.That(campaign.IsDefeated, Is.False);
            Assert.That(campaign.AttacksRemaining, Is.EqualTo(2), "o total a repelir nao muda");

            // Sofrer nao encerra: repelindo os que faltam, o rival ainda cai.
            clock.RecordOutcome(Attack(campaign), true);
            RivalCampaign defeated = clock.RecordOutcome(Attack(campaign), true);

            Assert.That(defeated, Is.SameAs(campaign));
        }

        [Test]
        public void DerrotarLimpaOsAtaquesRestantesDaCampanha()
        {
            // Antecedencia longa para que haja mais de um ataque anunciado ao mesmo tempo.
            RivalDefinition rival = TestContent.Rival(attacks: 1);
            ThreatClock clock = new ThreatClock(
                firstThreatDay: 3, leadDays: 6, rivals: new[] { rival }, windowIntervals: new[] { 2 });
            clock.AdmitDue(1);
            clock.AnnounceDue(1, 1, new XorShiftRandom(1));

            Assert.That(clock.Pending.Count, Is.GreaterThan(1), "o arranjo precisa de mais de um ataque na fila");

            ScheduledThreat first = clock.Pending[0];
            clock.MarkResolved(first);
            RivalCampaign defeated = clock.RecordOutcome(first, true);

            Assert.That(defeated, Is.Not.Null);
            Assert.That(clock.Pending, Is.Empty);
        }

        [Test]
        public void RivalDerrotadoNaoAgendaMaisAtaques()
        {
            RivalDefinition rival = TestContent.Rival(attacks: 1);
            ThreatClock clock = new ThreatClock(firstThreatDay: 4, rivals: new[] { rival });
            clock.AdmitDue(1);
            clock.AnnounceDue(1, 1, new XorShiftRandom(1));

            ScheduledThreat first = clock.Pending[0];
            clock.MarkResolved(first);
            clock.RecordOutcome(first, true);

            for (int day = 2; day <= 30; day++)
            {
                Assert.That(clock.AdmitDue(day), Is.Empty, "dia " + day);
                Assert.That(clock.AnnounceDue(day, 1, new XorShiftRandom(day)), Is.Empty, "dia " + day);
            }
        }

        [Test]
        public void RunDerrotaORivalQuandoADefesaSegura()
        {
            // Forca 1 contra a defesa do Salao: todo ataque e repelido.
            RivalDefinition rival = TestContent.Rival("fracos", attacks: 2, firstForce: 1, growth: 0.4);
            RunBundle bundle = TestContent.Run(firstThreatDay: 4, rivals: new[] { rival });
            bundle.Engine.DrainEvents();

            List<RunEvent> events = new List<RunEvent>();
            for (int i = 0; i < 20 && !bundle.Run.IsOver && bundle.Run.Stats.RivalsDefeated == 0; i++)
            {
                bundle.Engine.EndDay();
                events.AddRange(bundle.Engine.DrainEvents());
            }

            Assert.That(bundle.Run.Stats.RivalsDefeated, Is.EqualTo(1));
            Assert.That(bundle.Run.Threats.Campaigns[0].AttacksSuffered, Is.EqualTo(0));
            Assert.That(events.Exists(e => e is RivalDefeatedEvent d && d.Rival == rival), Is.True);
            Assert.That(bundle.Run.Threats.Pending, Is.Empty);
        }

        // --- Entrada escalonada (4.4) ---

        /// <summary>Dois rivais que caem facil, para exercitar a entrada de campanhas.</summary>
        private static RivalDefinition[] WeakPair()
        {
            return new[]
            {
                TestContent.Rival("primeiro", TestContent.AssaultIdentity(), attacks: 1, firstForce: 1, growth: 0),
                TestContent.Rival("segundo", TestContent.FoodRaidIdentity(), attacks: 2, firstForce: 1, growth: 0.4)
            };
        }

        /// <summary>
        /// Tres rivais de forca 1 com campanhas longas: nenhum cai durante o teste, e o
        /// reino nunca sofre, para medir so o relogio.
        /// </summary>
        private static RivalDefinition[] ThreeAtWar(int attacks = 50)
        {
            return new[]
            {
                TestContent.Rival("a", TestContent.AssaultIdentity(), attacks: attacks, firstForce: 1, growth: 0.01),
                TestContent.Rival("b", TestContent.FoodRaidIdentity(), attacks: attacks, firstForce: 1, growth: 0.02),
                TestContent.Rival("c", new RivalIdentity("arson", "Incendio", "Queima.", PressureAxis.Integrity),
                    attacks: attacks, firstForce: 1, growth: 0.03)
            };
        }

        [Test]
        public void SoOPrimeiroRivalComecaEmGuerra()
        {
            RivalDefinition[] rivals = WeakPair();
            RunBundle bundle = TestContent.Run(firstThreatDay: 4, rivals: rivals);

            List<RunEvent> events = bundle.Engine.DrainEvents();
            List<RunEvent> declarations = events.FindAll(e => e is RivalDeclaredWarEvent);

            Assert.That(declarations.Count, Is.EqualTo(1));
            RivalDeclaredWarEvent declared = (RivalDeclaredWarEvent)declarations[0];
            Assert.That(declared.Rival, Is.SameAs(rivals[0]));
            Assert.That(declared.Rival.Identity.Description, Is.Not.Empty);
            Assert.That(declared.Campaign.AttacksRemaining, Is.EqualTo(rivals[0].AttacksToDefeat));
            Assert.That(bundle.Run.Threats.NextRivalToEnter, Is.SameAs(rivals[1]));
            Assert.That(bundle.Run.Threats.NextEntryDay, Is.EqualTo(ThreatClock.DefaultEntrySpacingDays));
        }

        [Test]
        public void EntrarNaoSubstituiQuemJaEstaEmGuerra()
        {
            RivalDefinition[] rivals = ThreeAtWar();
            ThreatClock clock = new ThreatClock(firstThreatDay: 4, rivals: rivals, entryDays: new[] { 1, 5, 9 });

            Assert.That(clock.AdmitDue(1).Count, Is.EqualTo(1));
            for (int day = 2; day <= 4; day++)
            {
                Assert.That(clock.AdmitDue(day), Is.Empty, "ninguem entra antes do dia com alguem em guerra");
            }

            List<RivalCampaign> second = clock.AdmitDue(5);

            Assert.That(second.Count, Is.EqualTo(1));
            Assert.That(second[0].Rival, Is.SameAs(rivals[1]));
            Assert.That(second[0].EnteredOnDay, Is.EqualTo(5));
            Assert.That(clock.ActiveCampaigns().Count, Is.EqualTo(2), "o segundo soma, nao substitui");
        }

        [Test]
        public void EntradaEAnunciadaAntesDoPrimeiroAtaqueDoRival()
        {
            RivalDefinition[] rivals = ThreeAtWar();
            RunSetup setup = new RunSetup("humans", 77)
            {
                FirstThreatDay = 4,
                Rivals = rivals,
                RivalEntryDays = new[] { 1, 5, 9 }
            };
            RunBundle bundle = RunBuilder.Build(setup, TestContent.Catalog(TestContent.Humans(integrity: 1000)));
            bundle.Engine.StartRun();

            List<RunEvent> events = new List<RunEvent>(bundle.Engine.DrainEvents());
            for (int i = 0; i < 30 && !bundle.Run.IsOver; i++)
            {
                bundle.Engine.EndDay();
                events.AddRange(bundle.Engine.DrainEvents());
            }

            for (int r = 0; r < rivals.Length; r++)
            {
                int declaredAt = events.FindIndex(e => e is RivalDeclaredWarEvent d && d.Rival == rivals[r]);
                int firstAnnounce = events.FindIndex(e => e is ThreatAnnouncedEvent a && a.Threat.Rival == rivals[r]);

                Assert.That(declaredAt, Is.GreaterThanOrEqualTo(0), rivals[r] + " nunca entrou");
                if (firstAnnounce >= 0)
                {
                    Assert.That(firstAnnounce, Is.GreaterThan(declaredAt), rivals[r] + " atacou antes de ser apresentado");
                }
            }
        }

        [Test]
        public void SemNinguemEmGuerraOProximoEntraAntesDoSeuDia()
        {
            // O segundo so entraria no dia 50; com o primeiro derrotado cedo, esperar
            // ate la deixaria a run com dezenas de dias mortos.
            RivalDefinition[] rivals = WeakPair();
            RunSetup setup = new RunSetup("humans", 1234)
            {
                FirstThreatDay = 4,
                Rivals = rivals,
                RivalEntryDays = new[] { 1, 50 }
            };
            RunBundle bundle = RunBuilder.Build(setup, TestContent.Catalog());
            bundle.Engine.StartRun();

            for (int i = 0; i < 40 && !bundle.Run.IsOver && bundle.Run.Stats.RivalsDefeated < 2; i++)
            {
                bundle.Engine.EndDay();
            }

            IReadOnlyList<ScheduledThreat> resolved = bundle.Run.Threats.Resolved;
            ScheduledThreat lastOfFirst = resolved.First(t => t.Rival == rivals[0]);
            ScheduledThreat firstOfSecond = resolved.FirstOrDefault(t => t.Rival == rivals[1]);
            RivalCampaign secondCampaign = bundle.Run.Threats.Campaigns[1];

            Assert.That(firstOfSecond, Is.Not.Null, "o segundo rival nunca atacou");
            Assert.That(secondCampaign.EnteredOnDay, Is.EqualTo(lastOfFirst.ArrivalDay + 1));
            Assert.That(firstOfSecond.ArrivalDay - lastOfFirst.ArrivalDay, Is.GreaterThanOrEqualTo(2),
                "precisa haver ao menos um dia sem ataque antes da entrada antecipada");
            Assert.That(firstOfSecond.LeadTime, Is.GreaterThanOrEqualTo(ThreatClock.MinimumLeadDays));
        }

        [Test]
        public void TodosEntraramECairamSoEntaoAcabou()
        {
            RivalDefinition rival = TestContent.Rival(attacks: 1);
            ThreatClock clock = new ThreatClock(firstThreatDay: 4, rivals: new[] { rival });
            clock.AdmitDue(1);
            clock.AnnounceDue(1, 1, new XorShiftRandom(1));

            Assert.That(clock.AllRivalsDefeated, Is.False);

            ScheduledThreat first = clock.Pending[0];
            clock.MarkResolved(first);
            clock.RecordOutcome(first, true);

            Assert.That(clock.AllRivalsDefeated, Is.True);
            Assert.That(clock.NextRivalToEnter, Is.Null);
            Assert.That(clock.AdmitDue(first.ArrivalDay + 1), Is.Empty);
        }

        [Test]
        public void OrdemDeEntradaExigeCampanhaMaisForteEIdentidadeUnica()
        {
            RivalDefinition weak = TestContent.Rival("fraco", TestContent.AssaultIdentity(), attacks: 3, firstForce: 6);
            RivalDefinition strong = TestContent.Rival("forte", TestContent.FoodRaidIdentity(), attacks: 3, firstForce: 12);
            RivalDefinition stronger = TestContent.Rival("clone", TestContent.AssaultIdentity(), attacks: 3, firstForce: 20);

            Assert.That(RivalCatalogValidator.ValidateEntryOrder("ok", new[] { weak, strong }), Is.Empty);

            List<CatalogViolation> weaker = RivalCatalogValidator.ValidateEntryOrder("invertida", new[] { strong, weak });
            Assert.That(weaker.Exists(v => v.Detail.Contains("nao exige mais")), Is.True);

            // A repeticao e proibida no nivel inteiro, e nao so entre vizinhos: com
            // rivais lutando juntos, o primeiro e o terceiro podem estar em guerra ao
            // mesmo tempo.
            List<CatalogViolation> repeated = RivalCatalogValidator.ValidateEntryOrder(
                "repetida", new[] { weak, strong, stronger });
            Assert.That(repeated.Exists(v => v.Detail.Contains("repete a identidade")), Is.True);
        }

        // --- Sorteio entre rivais em guerra (4.8) ---

        /// <summary>Roda so o relogio, dia a dia, com os tres rivais entrando cedo.</summary>
        private static List<ScheduledThreat> RunClock(int seed, int days, ThreatClock clock = null)
        {
            clock = clock ?? new ThreatClock(firstThreatDay: 4, rivals: ThreeAtWar(), entryDays: new[] { 1, 3, 5 });
            IRandomSource random = new XorShiftRandom(seed);
            List<ScheduledThreat> announced = new List<ScheduledThreat>();

            for (int day = 1; day <= days; day++)
            {
                clock.AdmitDue(day);
                announced.AddRange(clock.AnnounceDue(day, 1, random));
                foreach (ScheduledThreat due in clock.DueOn(day))
                {
                    clock.MarkResolved(due);
                    clock.RecordOutcome(due, true);
                }
            }

            return announced;
        }

        [Test]
        public void MesmaSementeMesmaGuerra()
        {
            List<ScheduledThreat> a = RunClock(seed: 42, days: 60);
            List<ScheduledThreat> b = RunClock(seed: 42, days: 60);

            Assert.That(a.Count, Is.EqualTo(b.Count));
            for (int i = 0; i < a.Count; i++)
            {
                Assert.That(b[i].Rival.Id, Is.EqualTo(a[i].Rival.Id), "ataque " + i);
                Assert.That(b[i].ArrivalDay, Is.EqualTo(a[i].ArrivalDay), "ataque " + i);
            }
        }

        [Test]
        public void TodoRivalEmGuerraApareceNoSorteio()
        {
            List<ScheduledThreat> announced = RunClock(seed: 7, days: 60);

            HashSet<string> attackers = new HashSet<string>(announced.Select(t => t.Rival.Id));

            Assert.That(attackers, Is.EquivalentTo(new[] { "a", "b", "c" }));
        }

        [Test]
        public void MaisRivaisEmGuerraEncurtamAsJanelas()
        {
            ThreatClock clock = new ThreatClock();

            Assert.That(clock.WindowIntervalFor(1), Is.EqualTo(4));
            Assert.That(clock.WindowIntervalFor(2), Is.EqualTo(3));
            Assert.That(clock.WindowIntervalFor(3), Is.EqualTo(2));
            Assert.That(clock.WindowIntervalFor(7), Is.EqualTo(2), "alem da tabela vale o ultimo intervalo");

            // Com os tres ja em guerra, os ataques vem a cada 2 dias.
            List<ScheduledThreat> announced = RunClock(seed: 3, days: 40);
            List<ScheduledThreat> late = announced.FindAll(t => t.ArrivalDay >= 12);
            for (int i = 1; i < late.Count; i++)
            {
                Assert.That(late[i].ArrivalDay - late[i - 1].ArrivalDay, Is.EqualTo(2));
            }
        }

        [Test]
        public void NuncaDoisAtaquesNoMesmoDia()
        {
            RivalDefinition[] rivals = ThreeAtWar();
            RunSetup setup = new RunSetup("humans", 99)
            {
                FirstThreatDay = 4,
                Rivals = rivals,
                RivalEntryDays = new[] { 1, 2, 3 },
                RivalWindowIntervals = new[] { 1 }
            };
            RunBundle bundle = RunBuilder.Build(setup, TestContent.Catalog(TestContent.Humans(integrity: 1000)));
            bundle.Engine.StartRun();

            for (int i = 0; i < 40 && !bundle.Run.IsOver; i++)
            {
                bundle.Engine.EndDay();
            }

            IReadOnlyList<ScheduledThreat> resolved = bundle.Run.Threats.Resolved;
            Assert.That(resolved.Count, Is.GreaterThan(20), "janela diaria deveria gerar ataque quase todo dia");
            Assert.That(resolved.Select(t => t.ArrivalDay).Distinct().Count(), Is.EqualTo(resolved.Count));
            for (int i = 0; i < resolved.Count; i++)
            {
                Assert.That(resolved[i].LeadTime, Is.GreaterThanOrEqualTo(ThreatClock.MinimumLeadDays), resolved[i].ToString());
            }
        }

        [Test]
        public void AdiamentoNaoEmpilhaAtaques()
        {
            RivalDefinition[] rivals = ThreeAtWar();
            ThreatClock clock = new ThreatClock(
                firstThreatDay: 4, leadDays: 6, rivals: rivals, entryDays: new[] { 1, 1, 1 }, windowIntervals: new[] { 1 });
            clock.AdmitDue(1);
            clock.AnnounceDue(1, 1, new XorShiftRandom(1));

            // Fila: dias 4, 5, 6 e 7. Adiar o do dia 4 em 1 cairia no dia 5.
            ScheduledThreat first = clock.DueOn(4)[0];
            int applied = clock.Delay(first, 1);

            Assert.That(first.ArrivalDay, Is.EqualTo(8), "segue para o primeiro dia livre");
            Assert.That(applied, Is.EqualTo(4));
            Assert.That(clock.Pending.Select(t => t.ArrivalDay).Distinct().Count(), Is.EqualTo(clock.Pending.Count));

            // A janela seguinte pula o dia que o adiamento ocupou.
            List<ScheduledThreat> next = clock.AnnounceDue(3, 1, new XorShiftRandom(3));
            Assert.That(next.Count, Is.EqualTo(1));
            Assert.That(next[0].ArrivalDay, Is.EqualTo(9));
        }

        [Test]
        public void RivalDerrotadoSaiDoSorteio()
        {
            RivalDefinition[] rivals =
            {
                TestContent.Rival("cai", TestContent.AssaultIdentity(), attacks: 1, firstForce: 1, growth: 0),
                TestContent.Rival("fica", TestContent.FoodRaidIdentity(), attacks: 50, firstForce: 1, growth: 0.1)
            };
            ThreatClock clock = new ThreatClock(firstThreatDay: 4, rivals: rivals, entryDays: new[] { 1, 1 });
            clock.AdmitDue(1);

            RivalCampaign falling = clock.Campaigns[0];
            clock.RecordOutcome(new ScheduledThreat(ThreatKind.Horde, 1, 1, 1, falling), true);

            Assert.That(clock.ActiveCampaigns().Count, Is.EqualTo(1));
            List<ScheduledThreat> announced = RunClock(seed: 5, days: 40, clock);
            Assert.That(announced.Count, Is.GreaterThan(0));
            Assert.That(announced.TrueForAll(t => t.Rival.Id == "fica"), Is.True);
        }

        // --- Resolucao por eixo (4.5) ---

        /// <summary>
        /// Resolve um ataque de forca dada contra uma defesa exata. A defesa vem toda
        /// de defesa temporaria somada a da run, para o teste nao depender dos
        /// edificios do catalogo.
        /// </summary>
        private static AttackReport ResolveAgainst(RunBundle bundle, RivalIdentity identity, int force, int defense)
        {
            RivalCampaign campaign = new RivalCampaign(TestContent.Rival("r", identity));
            ScheduledThreat threat = new ScheduledThreat(ThreatKind.Horde, bundle.Run.Day, force, 1, campaign);

            int missing = defense - bundle.Run.TotalDefense();
            Assert.That(missing, Is.GreaterThanOrEqualTo(0), "o arranjo quer menos defesa do que a run ja tem");
            bundle.Run.AddPendingDefense(missing);

            return CombatResolver.Resolve(bundle.Run, threat);
        }

        [Test]
        public void RivalEconomicoCausaPerdaMesmoComDefesaAlta()
        {
            RunBundle bundle = TestContent.Run();
            bundle.Run.Add(ResourceKind.Food, 100);
            int foodBefore = bundle.Run[ResourceKind.Food];

            AttackReport report = ResolveAgainst(bundle, TestContent.FoodRaidIdentity(), force: 12, defense: 50);

            Assert.That(report.Repelled, Is.True, "defesa alta repele, e isso conta para a campanha");
            Assert.That(report.ResourcesLost[ResourceKind.Food], Is.EqualTo(6), "metade da forca e saqueada");
            Assert.That(bundle.Run[ResourceKind.Food], Is.EqualTo(foodBefore - 6));
            Assert.That(report.TilesDestroyed, Is.Empty);
            Assert.That(report.BaseDamage, Is.EqualTo(0));
        }

        [Test]
        public void RivalDeAssaltoEAnuladoPorDefesaSuficiente()
        {
            RunBundle bundle = TestContent.Run();
            int integrityBefore = bundle.Run.Integrity;
            int ownedBefore = bundle.Run.Grid.OwnedCount;

            AttackReport report = ResolveAgainst(bundle, TestContent.AssaultIdentity(), force: 12, defense: 12);

            Assert.That(report.Repelled, Is.True);
            Assert.That(report.CausedLoss, Is.False);
            Assert.That(bundle.Run.Integrity, Is.EqualTo(integrityBefore));
            Assert.That(bundle.Run.Grid.OwnedCount, Is.EqualTo(ownedBefore));
        }

        [Test]
        public void MaisDefesaNaoReduzOSaqueDoRivalEconomico()
        {
            // O que distingue as identidades: contra o assalto, defesa e resposta;
            // contra o saque, a perda e a mesma com defesa alta ou altissima.
            RunBundle moderate = TestContent.Run();
            moderate.Run.Add(ResourceKind.Food, 100);
            RunBundle fortress = TestContent.Run();
            fortress.Run.Add(ResourceKind.Food, 100);

            AttackReport a = ResolveAgainst(moderate, TestContent.FoodRaidIdentity(), force: 12, defense: 12);
            AttackReport b = ResolveAgainst(fortress, TestContent.FoodRaidIdentity(), force: 12, defense: 200);

            Assert.That(a.ResourcesLost[ResourceKind.Food], Is.EqualTo(b.ResourcesLost[ResourceKind.Food]));
            Assert.That(a.ResourcesLost[ResourceKind.Food], Is.GreaterThan(0));
        }

        [Test]
        public void ExcedenteDoRivalEconomicoSomaAoSaqueSemTocarOTerritorio()
        {
            RunBundle bundle = TestContent.Run();
            bundle.Run.Add(ResourceKind.Food, 100);
            int ownedBefore = bundle.Run.Grid.OwnedCount;
            int defense = bundle.Run.TotalDefense();

            AttackReport report = ResolveAgainst(bundle, TestContent.FoodRaidIdentity(), force: defense + 10, defense: defense);

            int raid = (int)System.Math.Round((defense + 10) * 0.5, System.MidpointRounding.AwayFromZero);
            Assert.That(report.Repelled, Is.False);
            Assert.That(report.ResourcesLost[ResourceKind.Food], Is.EqualTo(raid + 10));
            Assert.That(bundle.Run.Grid.OwnedCount, Is.EqualTo(ownedBefore));
            Assert.That(report.BaseDamage, Is.EqualTo(0));
        }

        [Test]
        public void SaqueLevaSoOQueHaNoEstoque()
        {
            RunBundle bundle = TestContent.Run();
            bundle.Run.Remove(ResourceKind.Food, bundle.Run[ResourceKind.Food]);
            bundle.Run.Add(ResourceKind.Food, 2);
            int integrityBefore = bundle.Run.Integrity;

            AttackReport report = ResolveAgainst(bundle, TestContent.FoodRaidIdentity(), force: 12, defense: 50);

            Assert.That(report.ResourcesLost[ResourceKind.Food], Is.EqualTo(2));
            Assert.That(bundle.Run[ResourceKind.Food], Is.EqualTo(0));
            Assert.That(bundle.Run.Integrity, Is.EqualTo(integrityBefore), "celeiro vazio nao vira dano");
        }

        [Test]
        public void RivalDePopulacaoLevaGente()
        {
            RunBundle bundle = TestContent.Run();
            bundle.Run.Add(ResourceKind.Population, 20);
            int before = bundle.Run[ResourceKind.Population];
            RivalIdentity captors = new RivalIdentity(
                "captors", "Captores", "Leva gente.", PressureAxis.Population, raidFraction: 0.25);

            AttackReport report = ResolveAgainst(bundle, captors, force: 8, defense: 50);

            Assert.That(report.ResourcesLost[ResourceKind.Population], Is.EqualTo(2));
            Assert.That(bundle.Run[ResourceKind.Population], Is.EqualTo(before - 2));
        }

        [Test]
        public void RivalDeIntegridadeFereABaseSemComerTerritorio()
        {
            RunBundle bundle = TestContent.Run();
            int integrityBefore = bundle.Run.Integrity;
            int ownedBefore = bundle.Run.Grid.OwnedCount;
            int defense = bundle.Run.TotalDefense();
            RivalIdentity arsonists = new RivalIdentity("arson", "Incendiarios", "Queima a base.", PressureAxis.Integrity);

            AttackReport report = ResolveAgainst(bundle, arsonists, force: defense + 3, defense: defense);

            Assert.That(report.BaseDamage, Is.EqualTo(3));
            Assert.That(bundle.Run.Integrity, Is.EqualTo(integrityBefore - 3));
            Assert.That(bundle.Run.Grid.OwnedCount, Is.EqualTo(ownedBefore));
        }

        [Test]
        public void IdentidadeDeAssaltoNaoSaqueia()
        {
            RivalIdentity assault = new RivalIdentity(
                "a", "a", "d", PressureAxis.Territory, raidFraction: 0.9);

            Assert.That(assault.RaidFraction, Is.EqualTo(0));
        }

        [Test]
        public void IdentidadeEconomicaSemSaqueReprovaOCatalogo()
        {
            RivalIdentity toothless = new RivalIdentity(
                "toothless", "Banguela", "Nao leva nada.", PressureAxis.Resource, ResourceKind.Food, raidFraction: 0);

            List<CatalogViolation> violations = RivalCatalogValidator.Validate(
                new[] { TestContent.Rival("banguela", toothless) });

            Assert.That(violations.Exists(v => v.Rule == "saque"), Is.True);
        }

        [Test]
        public void AtaqueDeRivalEconomicoApareceComoSaqueNoRelogio()
        {
            RivalDefinition raiders = TestContent.Rival("saqueadores", TestContent.FoodRaidIdentity());
            RunBundle bundle = TestContent.Run(firstThreatDay: 4, rivals: new[] { raiders });

            Assert.That(bundle.Run.Threats.Pending[0].Kind, Is.EqualTo(ThreatKind.Raid));
        }

        // --- Vitoria (4.6) ---

        [Test]
        public void DerrotarOUltimoRivalEncerraImediatamenteEmVitoria()
        {
            RivalDefinition[] rivals = WeakPair();
            RunBundle bundle = TestContent.Run(firstThreatDay: 4, rivals: rivals);
            bundle.Engine.DrainEvents();

            List<RunEvent> events = new List<RunEvent>();
            for (int i = 0; i < 60 && !bundle.Run.IsOver; i++)
            {
                bundle.Engine.EndDay();
                events.AddRange(bundle.Engine.DrainEvents());
            }

            ScheduledThreat last = bundle.Run.Threats.Resolved[bundle.Run.Threats.Resolved.Count - 1];

            Assert.That(bundle.Run.Outcome, Is.EqualTo(RunOutcome.Victory));
            Assert.That(bundle.Run.Collapse, Is.EqualTo(CollapseReason.None));
            Assert.That(bundle.Run.Day, Is.EqualTo(last.ArrivalDay), "nao espera mais nenhum dia");
            Assert.That(bundle.Run.Stats.RivalsDefeated, Is.EqualTo(2));

            int defeatedAt = events.FindLastIndex(e => e is RivalDefeatedEvent);
            int wonAt = events.FindIndex(e => e is RunWonEvent);
            Assert.That(wonAt, Is.GreaterThan(defeatedAt));
            Assert.That(events.Exists(e => e is RunCollapsedEvent), Is.False);
            Assert.That(events.FindIndex(defeatedAt, e => e is DayEventDrawnEvent || e is HandDiscardedEvent),
                Is.EqualTo(-1), "nada do resto do dia roda depois da vitoria");

            Assert.That(bundle.Engine.FinalScore.Outcome, Is.EqualTo(RunOutcome.Victory));
            Assert.That(bundle.Engine.EndDay().Rejection, Is.EqualTo(CommandRejection.RunOver));
        }

        [Test]
        public void DerrotaDeRivalEVitoriaSaoMomentosQueInterrompemODia()
        {
            RivalDefinition[] rivals = WeakPair();
            RunBundle bundle = TestContent.Run(firstThreatDay: 4, rivals: rivals);
            bundle.Engine.DrainEvents();

            List<DayStep> steps = new List<DayStep>();
            for (int i = 0; i < 60 && !bundle.Run.IsOver; i++)
            {
                bundle.Engine.EndDay();
                steps.AddRange(DayPresentation.Build(bundle.Engine.DrainEvents()));
            }

            List<DayStep> moments = steps.FindAll(s => s.IsMoment);

            Assert.That(moments.Count, Is.EqualTo(3), "dois rivais derrotados e o fim da run");
            Assert.That(moments[0].Kind, Is.EqualTo(DayStepKind.RivalDefeated));
            Assert.That(moments[1].Kind, Is.EqualTo(DayStepKind.RivalDefeated));
            Assert.That(moments[2].Kind, Is.EqualTo(DayStepKind.Outcome));
            Assert.That(moments[2].Source, Is.InstanceOf<RunWonEvent>());

            // A derrota vem antes de o proximo rival declarar guerra.
            int firstDefeat = steps.IndexOf(moments[0]);
            int secondDeclaration = steps.FindIndex(
                s => s.Source is RivalDeclaredWarEvent d && d.Rival == rivals[1]);
            Assert.That(secondDeclaration, Is.GreaterThan(firstDefeat));

            Assert.That(steps.FindAll(s => s.Kind == DayStepKind.Attack).TrueForAll(s => !s.IsMoment),
                "ataque e destaque, mas corre sozinho: momento e so o que muda a guerra");
        }

        private static RunScore ScoreOf(RunOutcome outcome, int rivalsDefeated = 1)
        {
            RunBundle bundle = TestContent.Run();
            bundle.Run.Stats.DaysSurvived = 20;
            bundle.Run.Stats.RivalsDefeated = rivalsDefeated;

            if (outcome == RunOutcome.Victory)
            {
                bundle.Run.DeclareVictory();
            }
            else if (outcome == RunOutcome.Collapse)
            {
                bundle.Run.Damage(bundle.Run.Integrity);
                bundle.Run.CheckCollapse();
            }

            return ScoreCalculator.Calculate(bundle.Run);
        }

        [Test]
        public void VitoriaRendeMaisQueColapsoComOMesmoReino()
        {
            RunScore victory = ScoreOf(RunOutcome.Victory);
            RunScore collapse = ScoreOf(RunOutcome.Collapse);

            Assert.That(victory.Outcome, Is.EqualTo(RunOutcome.Victory));
            Assert.That(collapse.Outcome, Is.EqualTo(RunOutcome.Collapse));
            Assert.That(victory.MetaCurrency, Is.GreaterThan(collapse.MetaCurrency));
        }

        [Test]
        public void RivaisDerrotadosValemMaisQueDias()
        {
            RunScore twoRivals = ScoreOf(RunOutcome.Collapse, rivalsDefeated: 2);
            RunScore none = ScoreOf(RunOutcome.Collapse, rivalsDefeated: 0);

            Assert.That(twoRivals.TotalPoints, Is.GreaterThan(none.TotalPoints));
            Assert.That(ScoreCalculator.PointsPerRivalDefeated, Is.GreaterThan(ScoreCalculator.PointsPerDay));
        }

        [Test]
        public void ColapsoContinuaExistindoENaoDerrotaRivais()
        {
            RivalDefinition rival = TestContent.Rival("brutos", attacks: 5, firstForce: 500, growth: 10);
            RunBundle bundle = TestContent.Run(firstThreatDay: 4, rivals: new[] { rival });

            for (int i = 0; i < 60 && !bundle.Run.IsOver; i++)
            {
                bundle.Engine.EndDay();
            }

            Assert.That(bundle.Run.Outcome, Is.EqualTo(RunOutcome.Collapse));
            Assert.That(bundle.Run.Stats.RivalsDefeated, Is.EqualTo(0));
            Assert.That(bundle.Run.Threats.Campaigns[0].IsDefeated, Is.False);
        }

        // --- Contra-jogo no catalogo (4.7) ---

        [Test]
        public void CatalogoSoDeAssaltoReprova()
        {
            RivalIdentity arson = new RivalIdentity("arson", "Incendiarios", "Queima a base.", PressureAxis.Integrity);
            List<CatalogViolation> violations = RivalCatalogValidator.Validate(new[]
            {
                TestContent.Rival("brutos", TestContent.AssaultIdentity()),
                TestContent.Rival("incendiarios", arson)
            });

            Assert.That(violations.Exists(v => v.Rule == "contra-jogo"), Is.True);
        }

        [Test]
        public void UmRivalEconomicoBastaParaOContraJogo()
        {
            List<CatalogViolation> violations = RivalCatalogValidator.Validate(new[]
            {
                TestContent.Rival("brutos", TestContent.AssaultIdentity()),
                TestContent.Rival("saqueadores", TestContent.FoodRaidIdentity())
            });

            Assert.That(violations.Exists(v => v.Rule == "contra-jogo"), Is.False);
        }

        [Test]
        public void CatalogoSemRivaisReprova()
        {
            List<CatalogViolation> violations = RivalCatalogValidator.Validate(new RivalDefinition[0]);

            Assert.That(violations.Exists(v => v.Rule == "contra-jogo"), Is.True);
        }

        [Test]
        public void EconomicoSemSaqueNaoContaComoContraJogo()
        {
            // Um rival economico que nao saqueia e respondido com defesa, entao nao
            // pode ser o que salva o catalogo da regra.
            RivalIdentity toothless = new RivalIdentity(
                "toothless", "Banguela", "Nao leva nada.", PressureAxis.Resource, ResourceKind.Food, raidFraction: 0);
            List<CatalogViolation> violations = RivalCatalogValidator.Validate(new[]
            {
                TestContent.Rival("brutos", TestContent.AssaultIdentity()),
                TestContent.Rival("banguela", toothless)
            });

            Assert.That(violations.Exists(v => v.Rule == "saque"), Is.True);
            Assert.That(violations.Exists(v => v.Rule == "contra-jogo"), Is.True);
        }

        [Test]
        public void RecursoPressionadoSoExisteNoEixoDeRecurso()
        {
            RivalIdentity assault = new RivalIdentity(
                "a", "a", "d", PressureAxis.Territory, ResourceKind.Food);

            Assert.That(assault.PressuredResource, Is.Null);
            Assert.That(TestContent.FoodRaidIdentity().PressuredResource, Is.EqualTo(ResourceKind.Food));
        }
    }
}

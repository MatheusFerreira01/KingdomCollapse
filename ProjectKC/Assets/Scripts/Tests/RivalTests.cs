using System.Collections.Generic;
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

            Assert.That(bundle.Run.CurrentRival, Is.SameAs(rival));
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
                announced.AddRange(clock.AnnounceDue(day, 1, new XorShiftRandom(1)));
            }

            Assert.That(announced.Count, Is.EqualTo(3));
            Assert.That(announced[0].Force, Is.EqualTo(rival.Campaign.ForceFor(0)));
            Assert.That(announced[1].Force, Is.EqualTo(rival.Campaign.ForceFor(1)));
            Assert.That(announced[2].Force, Is.EqualTo(rival.Campaign.ForceFor(2)));
            Assert.That(clock.CurrentCampaign.AttacksLaunched, Is.EqualTo(3));
        }

        [Test]
        public void RunSemRivaisMantemAAmeacaAnonima()
        {
            RunBundle bundle = TestContent.Run(firstThreatDay: 4);

            Assert.That(bundle.Run.CurrentRival, Is.Null);
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
            ThreatClock clock = new ThreatClock(firstThreatDay: 3, intervalDays: 2, leadDays: 6, rivals: new[] { rival });
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
            clock.AnnounceDue(1, 1, new XorShiftRandom(1));

            ScheduledThreat first = clock.Pending[0];
            clock.MarkResolved(first);
            clock.RecordOutcome(first, true);

            for (int day = 2; day <= 30; day++)
            {
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
            Assert.That(bundle.Run.Threats.CurrentCampaign.AttacksSuffered, Is.EqualTo(0));
            Assert.That(events.Exists(e => e is RivalDefeatedEvent d && d.Rival == rival), Is.True);
            Assert.That(bundle.Run.Threats.Pending, Is.Empty);
        }

        // --- Sucessao (4.4) ---

        /// <summary>Dois rivais que caem facil, para exercitar a troca de campanha.</summary>
        private static RivalDefinition[] WeakSuccession()
        {
            return new[]
            {
                TestContent.Rival("primeiro", TestContent.AssaultIdentity(), attacks: 1, firstForce: 1, growth: 0),
                TestContent.Rival("segundo", TestContent.FoodRaidIdentity(), attacks: 2, firstForce: 1, growth: 0.4)
            };
        }

        [Test]
        public void PrimeiroRivalEApresentadoAoComecarARun()
        {
            RivalDefinition[] rivals = WeakSuccession();
            RunBundle bundle = TestContent.Run(firstThreatDay: 4, rivals: rivals);

            List<RunEvent> events = bundle.Engine.DrainEvents();

            RivalDeclaredWarEvent declared = (RivalDeclaredWarEvent)events.Find(e => e is RivalDeclaredWarEvent);
            Assert.That(declared, Is.Not.Null);
            Assert.That(declared.Rival, Is.SameAs(rivals[0]));
            Assert.That(declared.Rival.Identity.Description, Is.Not.Empty);
            Assert.That(declared.Campaign.AttacksRemaining, Is.EqualTo(rivals[0].AttacksToDefeat));
        }

        [Test]
        public void DerrotadoORivalOProximoDeclaraGuerra()
        {
            RivalDefinition[] rivals = WeakSuccession();
            RunBundle bundle = TestContent.Run(firstThreatDay: 4, rivals: rivals);
            bundle.Engine.DrainEvents();

            List<RunEvent> events = new List<RunEvent>();
            for (int i = 0; i < 20 && bundle.Run.Stats.RivalsDefeated == 0; i++)
            {
                bundle.Engine.EndDay();
                events.AddRange(bundle.Engine.DrainEvents());
            }

            int defeatedAt = events.FindIndex(e => e is RivalDefeatedEvent);
            int declaredAt = events.FindIndex(e => e is RivalDeclaredWarEvent);

            Assert.That(defeatedAt, Is.GreaterThanOrEqualTo(0));
            Assert.That(declaredAt, Is.GreaterThan(defeatedAt), "o proximo declara depois da derrota");
            Assert.That(((RivalDeclaredWarEvent)events[declaredAt]).Rival, Is.SameAs(rivals[1]));
            Assert.That(bundle.Run.CurrentRival, Is.SameAs(rivals[1]));
        }

        [Test]
        public void HaAoMenosUmDiaSemAtaqueEntreCampanhas()
        {
            RivalDefinition[] rivals = WeakSuccession();
            RunBundle bundle = TestContent.Run(firstThreatDay: 4, rivals: rivals);

            for (int i = 0; i < 40 && !bundle.Run.IsOver && bundle.Run.Stats.RivalsDefeated < 2; i++)
            {
                bundle.Engine.EndDay();
            }

            IReadOnlyList<ScheduledThreat> resolved = bundle.Run.Threats.Resolved;
            ScheduledThreat lastOfFirst = resolved[0];
            ScheduledThreat firstOfSecond = null;
            for (int i = 0; i < resolved.Count; i++)
            {
                if (resolved[i].Rival == rivals[1])
                {
                    firstOfSecond = resolved[i];
                    break;
                }

                lastOfFirst = resolved[i];
            }

            Assert.That(firstOfSecond, Is.Not.Null, "o segundo rival nunca atacou");
            Assert.That(firstOfSecond.ArrivalDay - lastOfFirst.ArrivalDay, Is.GreaterThanOrEqualTo(2),
                "precisa haver ao menos um dia sem ataque entre campanhas");
            Assert.That(firstOfSecond.LeadTime, Is.GreaterThanOrEqualTo(ThreatClock.MinimumLeadDays));
        }

        [Test]
        public void RespiroDeclaradoEOQueSeparaAsCampanhas()
        {
            RivalDefinition[] rivals = WeakSuccession();
            ThreatClock clock = new ThreatClock(firstThreatDay: 4, rivals: rivals, breatherDays: 6);
            clock.AnnounceDue(1, 1, new XorShiftRandom(1));

            ScheduledThreat first = clock.Pending[0];
            clock.MarkResolved(first);
            clock.RecordOutcome(first, true);
            clock.DeclareNextWar(first.ArrivalDay);

            List<ScheduledThreat> next = new List<ScheduledThreat>();
            for (int day = first.ArrivalDay + 1; next.Count == 0 && day < 40; day++)
            {
                next.AddRange(clock.AnnounceDue(day, 1, new XorShiftRandom(day)));
            }

            Assert.That(next[0].Rival, Is.SameAs(rivals[1]));
            Assert.That(next[0].ArrivalDay, Is.EqualTo(first.ArrivalDay + 6 + 1));
        }

        [Test]
        public void SemRivaisRestantesNaoHaProximaGuerra()
        {
            RivalDefinition rival = TestContent.Rival(attacks: 1);
            ThreatClock clock = new ThreatClock(firstThreatDay: 4, rivals: new[] { rival });
            clock.AnnounceDue(1, 1, new XorShiftRandom(1));

            ScheduledThreat first = clock.Pending[0];
            clock.MarkResolved(first);
            clock.RecordOutcome(first, true);

            Assert.That(clock.DeclareNextWar(first.ArrivalDay), Is.Null);
            Assert.That(clock.AllRivalsDefeated, Is.True);
        }

        [Test]
        public void NaoHaProximaGuerraSemDerrota()
        {
            ThreatClock clock = new ThreatClock(firstThreatDay: 4, rivals: WeakSuccession());

            Assert.That(clock.DeclareNextWar(1), Is.Null);
            Assert.That(clock.CurrentCampaign.Rival.Id, Is.EqualTo("primeiro"));
        }

        [Test]
        public void SucessaoExigeCampanhaMaisForteEIdentidadeDiferente()
        {
            RivalDefinition weak = TestContent.Rival("fraco", TestContent.AssaultIdentity(), attacks: 3, firstForce: 6);
            RivalDefinition strong = TestContent.Rival("forte", TestContent.FoodRaidIdentity(), attacks: 3, firstForce: 12);
            RivalDefinition sameIdentity = TestContent.Rival("clone", TestContent.FoodRaidIdentity(), attacks: 3, firstForce: 20);

            Assert.That(RivalCatalogValidator.ValidateSuccession("ok", new[] { weak, strong }), Is.Empty);

            List<CatalogViolation> weaker = RivalCatalogValidator.ValidateSuccession("invertida", new[] { strong, weak });
            Assert.That(weaker.Exists(v => v.Detail.Contains("nao exige mais")), Is.True);

            List<CatalogViolation> repeated = RivalCatalogValidator.ValidateSuccession("repetida", new[] { strong, sameIdentity });
            Assert.That(repeated.Exists(v => v.Detail.Contains("repete a identidade")), Is.True);
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
            RivalDefinition[] rivals = WeakSuccession();
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
            Assert.That(bundle.Run.Threats.CurrentCampaign.IsDefeated, Is.False);
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

using System.Collections.Generic;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    [TestFixture]
    public class ThreatClockTests
    {
        private static IRandomSource Rng(int seed = 5) => new XorShiftRandom(seed);

        [Test]
        public void ToraAmeacaResolvida_FoiAnunciadaComAntecedenciaMinima()
        {
            RunBundle bundle = TestContent.Run(firstThreatDay: 4);

            for (int i = 0; i < 30 && !bundle.Run.IsOver; i++)
            {
                bundle.Engine.EndDay();
            }

            IReadOnlyList<ScheduledThreat> resolved = bundle.Run.Threats.Resolved;
            Assert.That(resolved.Count, Is.GreaterThan(0), "nenhuma ameaca chegou a resolver");

            for (int i = 0; i < resolved.Count; i++)
            {
                Assert.That(
                    resolved[i].LeadTime,
                    Is.GreaterThanOrEqualTo(ThreatClock.MinimumLeadDays),
                    resolved[i] + " foi anunciada com antecedencia de " + resolved[i].LeadTime);
            }
        }

        [Test]
        public void NenhumaAmeacaResolveSemTerSidoAnunciada()
        {
            ThreatClock clock = new ThreatClock(firstThreatDay: 4);

            // Sem chamar AnnounceDue, nada pode estar vencendo.
            List<ScheduledThreat> due = clock.DueOn(4);

            Assert.That(due, Is.Empty);
        }

        [Test]
        public void MaisCelulasNoMesmoDia_GeramAmeacaMaisForte()
        {
            ThreatCurve curve = new ThreatCurve();

            int small = curve.ForceFor(10, 4);
            int large = curve.ForceFor(10, 12);

            Assert.That(large, Is.GreaterThan(small));
        }

        [Test]
        public void ForcaNaoEstabilizaComOAvancoDosDias()
        {
            ThreatCurve curve = new ThreatCurve();

            int previous = curve.ForceFor(1, 5);
            for (int day = 2; day <= 60; day++)
            {
                int current = curve.ForceFor(day, 5);
                Assert.That(current, Is.GreaterThan(previous), "forca parou de crescer no dia " + day);
                previous = current;
            }
        }

        [Test]
        public void DefesaSuficiente_AnulaOAtaque()
        {
            RunBundle bundle = TestContent.Run();
            bundle.Run.AddPendingDefense(1000);
            ScheduledThreat threat = new ScheduledThreat(ThreatKind.Horde, 1, 20, 1);
            int integrityBefore = bundle.Run.Integrity;

            AttackReport report = CombatResolver.Resolve(bundle.Run, threat);

            Assert.That(report.Repelled, Is.True);
            Assert.That(report.TilesDestroyed, Is.Empty);
            Assert.That(report.BaseDamage, Is.Zero);
            Assert.That(bundle.Run.Integrity, Is.EqualTo(integrityBefore));
        }

        [Test]
        public void ExcedenteDeHorda_ArrasaCelulasDaBordaEDepoisFereABase()
        {
            RunBundle bundle = TestContent.Run();
            TestContent.GrantTiles(bundle.Run.Grid,
                new Coord(1, 0), new Coord(-1, 0), new Coord(0, 1), new Coord(0, -1));

            // Excedente para exatamente duas celulas, com sobra de 3 para a base.
            // A defesa do Salao entra na conta, senao o teste mede a coisa errada.
            int defense = bundle.Run.TotalDefense();
            int force = defense + CombatResolver.ForcePerTile * 2 + 3;
            ScheduledThreat threat = new ScheduledThreat(ThreatKind.Horde, 1, force, 1);
            int integrityBefore = bundle.Run.Integrity;

            AttackReport report = CombatResolver.Resolve(bundle.Run, threat);

            Assert.That(report.TilesDestroyed.Count, Is.EqualTo(2));
            Assert.That(report.BaseDamage, Is.EqualTo(3));
            Assert.That(bundle.Run.Integrity, Is.EqualTo(integrityBefore - 3));
        }

        [Test]
        public void HordaNuncaComeOSalao()
        {
            RunBundle bundle = TestContent.Run();
            TestContent.GrantTiles(bundle.Run.Grid, new Coord(1, 0));
            ScheduledThreat threat = new ScheduledThreat(ThreatKind.Horde, 1, 500, 1);

            AttackReport report = CombatResolver.Resolve(bundle.Run, threat);

            Assert.That(report.TilesDestroyed, Has.No.Member(Coord.Zero));
            Assert.That(bundle.Run.Grid.TileAt(Coord.Zero).Destroyed, Is.False);
        }

        [Test]
        public void IncendioFereABaseSemComerTerritorio()
        {
            RunBundle bundle = TestContent.Run();
            TestContent.GrantTiles(bundle.Run.Grid, new Coord(1, 0), new Coord(0, 1));
            ScheduledThreat threat = new ScheduledThreat(ThreatKind.Fire, 1, 5, 1);

            AttackReport report = CombatResolver.Resolve(bundle.Run, threat);

            Assert.That(report.TilesDestroyed, Is.Empty);
            Assert.That(report.BaseDamage, Is.GreaterThan(0));
        }

        [Test]
        public void RelatorioDiscriminaForcaDefesaEPerdas()
        {
            RunBundle bundle = TestContent.Run();
            TestContent.GrantTiles(bundle.Run.Grid, new Coord(1, 0));
            bundle.Run.AddPendingDefense(4);
            ScheduledThreat threat = new ScheduledThreat(ThreatKind.Horde, 1, 20, 1);

            AttackReport report = CombatResolver.Resolve(bundle.Run, threat);

            Assert.That(report.Force, Is.EqualTo(20));
            Assert.That(report.Defense, Is.GreaterThanOrEqualTo(4));
            Assert.That(report.Overflow, Is.EqualTo(20 - report.Defense));
            Assert.That(report.ToString(), Does.Contain("forca"));
        }

        [Test]
        public void DefesaTemporaria_EConsumidaPeloAtaque()
        {
            RunBundle bundle = TestContent.Run();
            bundle.Run.AddPendingDefense(10);
            ScheduledThreat threat = new ScheduledThreat(ThreatKind.Horde, 1, 1, 1);

            CombatResolver.Resolve(bundle.Run, threat);

            Assert.That(bundle.Run.PendingDefense, Is.Zero, "defesa temporaria nao pode valer dois ataques");
        }

        [Test]
        public void RelogioFicaVisivelDuranteOPlanejamento()
        {
            RunBundle bundle = TestContent.Run(firstThreatDay: 4);

            Assert.That(bundle.Run.Phase, Is.EqualTo(DayPhase.Planning));
            ScheduledThreat next = bundle.Run.Threats.NextThreat(bundle.Run.Day);

            Assert.That(next, Is.Not.Null, "o jogador precisa enxergar a ameaca ja no dia 1");
            Assert.That(next.DaysUntil(bundle.Run.Day), Is.GreaterThanOrEqualTo(ThreatClock.MinimumLeadDays));
            Assert.That(next.Force, Is.GreaterThan(0));
        }

        [Test]
        public void EventoPodeAlterarForcaDeAmeacaAnunciada()
        {
            // Dia 4 com antecedencia de 3 cai dentro da janela de anuncio do dia 1.
            ThreatClock clock = new ThreatClock(firstThreatDay: 4);
            clock.AnnounceDue(1, 3, Rng());
            ScheduledThreat threat = clock.NextThreat(1);
            Assert.That(threat, Is.Not.Null);
            int before = threat.Force;

            bool changed = clock.ModifyForce(threat, 7);

            Assert.That(changed, Is.True);
            Assert.That(clock.NextThreat(1).Force, Is.EqualTo(before + 7));
        }

        [Test]
        public void ModificarAmeacaInexistente_Falha()
        {
            ThreatClock clock = new ThreatClock();
            ScheduledThreat orphan = new ScheduledThreat(ThreatKind.Fire, 9, 10, 1);

            Assert.That(clock.ModifyForce(orphan, 5), Is.False);
            Assert.That(clock.ModifyForce(null, 5), Is.False);
        }

        [Test]
        public void AnuncioNuncaEncurtaAAntecedenciaMinima()
        {
            // Relogio pedido com antecedencia curta demais: a classe eleva ao minimo
            // em vez de aceitar um aviso que a spec proibe.
            ThreatClock clock = new ThreatClock(firstThreatDay: 1, intervalDays: 1, leadDays: 0);

            List<ScheduledThreat> announced = clock.AnnounceDue(1, 1, Rng());

            for (int i = 0; i < announced.Count; i++)
            {
                Assert.That(announced[i].LeadTime, Is.GreaterThanOrEqualTo(ThreatClock.MinimumLeadDays));
            }
        }
    }
}

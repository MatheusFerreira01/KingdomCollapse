using System.Collections.Generic;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    [TestFixture]
    public class RivalKingdomsTests
    {
        /// <summary>Roda dias ate um ataque resolver, adicionando a mesma defesa
        /// pendente em todo dia (consumida sozinha nos dias sem ataque).</summary>
        private static void StepUntilNextAttack(RunBundle bundle, int defenseToAdd, int maxDays = 60)
        {
            int resolvedBefore = bundle.Run.Threats.Resolved.Count;
            for (int i = 0; i < maxDays && !bundle.Run.IsOver; i++)
            {
                if (defenseToAdd > 0)
                {
                    bundle.Run.AddPendingDefense(defenseToAdd);
                }

                bundle.Engine.EndDay();

                if (bundle.Run.Threats.Resolved.Count > resolvedBefore || bundle.Run.IsOver)
                {
                    return;
                }
            }
        }

        // --- 4.1 Identidade e catalogo ---

        [Test]
        public void IdentidadeDeclaraOEixoQuePressiona()
        {
            RivalIdentity assault = TestContent.AssaultIdentity();
            RivalIdentity economic = TestContent.ResourceIdentity();

            Assert.That(assault.Axis, Is.EqualTo(RivalAxis.Territory));
            Assert.That(assault.ResolvedByDefense, Is.True);
            Assert.That(economic.Axis, Is.EqualTo(RivalAxis.Resource));
            Assert.That(economic.ResolvedByDefense, Is.False);
        }

        [Test]
        public void CatalogoSoComEixoDefensivo_Reprova()
        {
            List<RivalDefinition> catalog = new List<RivalDefinition>
            {
                TestContent.Rival("assault", TestContent.AssaultIdentity()),
                TestContent.Rival("siege", TestContent.SiegeIdentity())
            };

            List<CatalogViolation> violations = RivalCatalogValidator.Validate(catalog);

            Assert.That(violations, Is.Not.Empty);
        }

        [Test]
        public void CatalogoComEixoNaoDefensivo_Passa()
        {
            List<RivalDefinition> catalog = new List<RivalDefinition>
            {
                TestContent.Rival("assault", TestContent.AssaultIdentity()),
                TestContent.Rival("economic", TestContent.ResourceIdentity())
            };

            List<CatalogViolation> violations = RivalCatalogValidator.Validate(catalog);

            Assert.That(violations, Is.Empty);
        }

        // --- 4.2 Campanha preenche o relogio ---

        [Test]
        public void PrimeiroAtaqueDaCampanha_AntecedenciaMinimaEIndicaORival()
        {
            RivalDefinition rival = TestContent.Rival("r1", TestContent.AssaultIdentity());
            RunBundle bundle = TestContent.RunWithRivals(new List<RivalDefinition> { rival });

            Assert.That(bundle.Run.Threats.Pending.Count, Is.EqualTo(1),
                "primeiro ataque devia estar agendado desde o inicio da run");

            ScheduledThreat scheduled = bundle.Run.Threats.Pending[0];
            Assert.That(scheduled.LeadTime, Is.GreaterThanOrEqualTo(ThreatClock.MinimumLeadDays));
            Assert.That(scheduled.Identity, Is.EqualTo(rival.Identity));
            Assert.That(scheduled.RivalId, Is.EqualTo(rival.Id));
        }

        // --- 4.3 Contagem de repelidos e derrota ---

        [Test]
        public void RepelirOsAtaquesNecessarios_DerrotaORival()
        {
            RivalDefinition rival = TestContent.Rival(
                "r1", TestContent.AssaultIdentity(), attackCount: 1, curve: TestContent.FlatCampaignCurve(5));
            RunBundle bundle = TestContent.RunWithRivals(new List<RivalDefinition> { rival });

            StepUntilNextAttack(bundle, defenseToAdd: 100);

            Assert.That(bundle.Run.Campaign.AllDefeated, Is.True);
        }

        [Test]
        public void FalharUmAtaque_NaoPerdeACampanha_EAindaPermiteDerrotar()
        {
            RivalDefinition rival = TestContent.Rival(
                "r1", TestContent.AssaultIdentity(), attackCount: 2, curve: TestContent.FlatCampaignCurve(10));
            RunBundle bundle = TestContent.RunWithRivals(new List<RivalDefinition> { rival });

            // Primeiro ataque: sem defesa nenhuma, nao repele.
            StepUntilNextAttack(bundle, defenseToAdd: 0);
            Assert.That(bundle.Run.Campaign.RepelsAchieved, Is.Zero);
            Assert.That(bundle.Run.Campaign.Current, Is.EqualTo(rival),
                "falhar um ataque nao pode encerrar nem trocar a campanha");
            Assert.That(bundle.Run.IsOver, Is.False);

            // Segundo ataque: defesa de sobra, repele — mas so 1 de 2 necessarios.
            StepUntilNextAttack(bundle, defenseToAdd: 100);
            Assert.That(bundle.Run.Campaign.RepelsAchieved, Is.EqualTo(1));
            Assert.That(bundle.Run.IsOver, Is.False);

            // Terceiro ataque: repele de novo, agora sim derrota (2 de 2).
            StepUntilNextAttack(bundle, defenseToAdd: 100);
            Assert.That(bundle.Run.Campaign.AllDefeated, Is.True);
        }

        [Test]
        public void DerrotarLimpaOsAtaquesRestantesDaquelaCampanha()
        {
            ThreatClock clock = new ThreatClock();
            RivalIdentity identity = TestContent.AssaultIdentity();

            clock.Schedule(10, 5, 1, identity, "rival_x");
            clock.Schedule(14, 5, 1, identity, "rival_x");
            clock.Schedule(10, 5, 1, TestContent.SiegeIdentity(), "rival_y");

            List<ScheduledThreat> removed = clock.ClearPendingForRival("rival_x");

            Assert.That(removed.Count, Is.EqualTo(2));
            Assert.That(clock.Pending.Count, Is.EqualTo(1));
            Assert.That(clock.Pending[0].RivalId, Is.EqualTo("rival_y"));
        }

        // --- 4.4 Sucessao de rivais ---

        [Test]
        public void ProximoRivalEAnunciadoComRespiro()
        {
            RivalDefinition first = TestContent.Rival(
                "first", TestContent.AssaultIdentity(), attackCount: 1, curve: TestContent.FlatCampaignCurve(5));
            RivalDefinition second = TestContent.Rival(
                "second", TestContent.SiegeIdentity(), attackCount: 1, curve: TestContent.FlatCampaignCurve(5));
            RunBundle bundle = TestContent.RunWithRivals(
                new List<RivalDefinition> { first, second }, campaignRestDays: 2);

            StepUntilNextAttack(bundle, defenseToAdd: 100);
            int dayFirstDefeated = bundle.Run.Day;

            Assert.That(bundle.Run.Campaign.Current, Is.EqualTo(second));
            Assert.That(bundle.Run.Campaign.AllDefeated, Is.False);

            Assert.That(bundle.Run.Threats.Pending.Count, Is.EqualTo(1),
                "o proximo rival ja devia ter o primeiro ataque agendado");
            Assert.That(bundle.Run.Threats.Pending[0].ArrivalDay, Is.GreaterThan(dayFirstDefeated),
                "tem que existir ao menos um dia sem ataque entre as campanhas");
        }

        // --- 4.5 Resolucao por eixo ---

        [Test]
        public void RivalDeAssalto_EAnuladoPorDefesaSuficiente()
        {
            RunBundle bundle = TestContent.Run();
            bundle.Run.AddPendingDefense(1000);
            ScheduledThreat threat = new ScheduledThreat(
                ThreatKind.Horde, 1, 20, 1, TestContent.AssaultIdentity(), "r1");

            AttackReport report = CombatResolver.Resolve(bundle.Run, threat);

            Assert.That(report.Repelled, Is.True);
            Assert.That(report.TilesDestroyed, Is.Empty);
            Assert.That(bundle.Run.Integrity, Is.EqualTo(bundle.Run.MaxIntegrity));
        }

        [Test]
        public void RivalEconomico_CausaPerdaDeRecursoMesmoComDefesaAlta()
        {
            RunBundle bundle = TestContent.Run();
            bundle.Run.Add(ResourceKind.Food, 100);
            bundle.Run.AddPendingDefense(9999);
            ScheduledThreat threat = new ScheduledThreat(
                ThreatKind.Horde, 1, 10, 1, TestContent.ResourceIdentity(resource: ResourceKind.Food), "r1");
            int foodBefore = bundle.Run[ResourceKind.Food];

            AttackReport report = CombatResolver.Resolve(bundle.Run, threat);

            Assert.That(report.Repelled, Is.True, "defesa alta ainda repele pra fins de campanha");
            Assert.That(report.ResourceLostKind, Is.EqualTo(ResourceKind.Food));
            Assert.That(report.ResourceLostAmount, Is.GreaterThan(0));
            Assert.That(bundle.Run[ResourceKind.Food], Is.LessThan(foodBefore),
                "defesa nao pode impedir a perda de um rival economico");
        }

        [Test]
        public void RivalDePopulacao_CausaPerdaDePopulacaoMesmoComDefesaAlta()
        {
            RunBundle bundle = TestContent.Run();
            bundle.Run.Add(ResourceKind.Population, 20);
            bundle.Run.AddPendingDefense(9999);
            ScheduledThreat threat = new ScheduledThreat(
                ThreatKind.Horde, 1, 10, 1, TestContent.PopulationIdentity(), "r1");
            int popBefore = bundle.Run[ResourceKind.Population];

            AttackReport report = CombatResolver.Resolve(bundle.Run, threat);

            Assert.That(report.PopulationLost, Is.GreaterThan(0));
            Assert.That(bundle.Run[ResourceKind.Population], Is.LessThan(popBefore));
        }

        // --- 4.6 Vitoria ---

        [Test]
        public void DerrotarUltimoRival_VenceARunImediatamente()
        {
            RivalDefinition rival = TestContent.Rival(
                "r1", TestContent.AssaultIdentity(), attackCount: 1, curve: TestContent.FlatCampaignCurve(5));
            RunBundle bundle = TestContent.RunWithRivals(new List<RivalDefinition> { rival });

            StepUntilNextAttack(bundle, defenseToAdd: 100);

            Assert.That(bundle.Run.Outcome, Is.EqualTo(RunOutcome.Victory));
            Assert.That(bundle.Run.IsOver, Is.True);
            Assert.That(bundle.Engine.FinalScore, Is.Not.Null);
        }

        [Test]
        public void VitoriaRendeMaisMoedaQueColapsoComOMesmoReino()
        {
            RivalDefinition rival = TestContent.Rival(
                "r1", TestContent.AssaultIdentity(), attackCount: 1, curve: TestContent.FlatCampaignCurve(5));
            RunBundle victoryBundle = TestContent.RunWithRivals(new List<RivalDefinition> { rival });
            StepUntilNextAttack(victoryBundle, defenseToAdd: 100);
            int victoryCoins = victoryBundle.Engine.FinalScore.MetaCurrency;

            RunBundle collapseBundle = TestContent.Run();
            collapseBundle.Run.Damage(collapseBundle.Run.Integrity);
            collapseBundle.Run.CheckCollapse();
            RunScore collapseScore = ScoreCalculator.Calculate(collapseBundle.Run);

            Assert.That(victoryCoins, Is.GreaterThan(collapseScore.MetaCurrency));
        }
    }
}

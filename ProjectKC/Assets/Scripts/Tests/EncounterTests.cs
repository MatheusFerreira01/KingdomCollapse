using System.Collections.Generic;
using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    [TestFixture]
    public class EncounterTests
    {
        private static EncounterOption Option(string label, string nature, params IEffect[] effects) =>
            new EncounterOption(label, nature, new List<IEffect>(effects));

        // --- 8.1 Identidade e opções assimétricas ---

        [Test]
        public void EncontroSemNomeOuDescricao_EReprovado()
        {
            EncounterDefinition sem = new EncounterDefinition(
                "sem_nome", string.Empty, string.Empty,
                new List<EncounterOption> { Option("Continuar", "imediato", new GainGoldEffect(5)) });

            List<CatalogViolation> violations = EncounterCatalogValidator.Validate(new[] { sem });

            Assert.That(violations, Has.Some.Matches<CatalogViolation>(v => v.Rule == "identidade"));
        }

        [Test]
        public void EncontroComNomeEDescricao_NaoEReprovadoPorIdentidade()
        {
            EncounterDefinition minotauro = new EncounterDefinition(
                "minotauro_ferido", "Minotauro Ferido", "Um minotauro sangra à beira do território.",
                new List<EncounterOption>
                {
                    Option("Curar", "aliado_futuro", new GainGoldEffect(0)),
                    Option("Abater", "recurso_imediato", new GainGoldEffect(20))
                });

            List<CatalogViolation> violations = EncounterCatalogValidator.Validate(new[] { minotauro });

            Assert.That(violations, Has.None.Matches<CatalogViolation>(v => v.Rule == "identidade"));
        }

        // --- 8.6 Opção dominante ---

        [Test]
        public void OpcoesDaMesmaNatureza_SaoReprovadasComoDominantes()
        {
            EncounterDefinition ruim = new EncounterDefinition(
                "ruim", "Encontro Ruim", "Descrição qualquer.",
                new List<EncounterOption>
                {
                    Option("A", "recurso_imediato", new GainGoldEffect(10)),
                    Option("B", "recurso_imediato", new GainGoldEffect(20))
                });

            List<CatalogViolation> violations = EncounterCatalogValidator.Validate(new[] { ruim });

            Assert.That(violations, Has.Some.Matches<CatalogViolation>(v => v.Rule == "opção dominante"));
        }

        [Test]
        public void OpcoesDeNaturezasDiferentes_NaoSaoReprovadas()
        {
            EncounterDefinition bom = new EncounterDefinition(
                "bom", "Encontro Bom", "Descrição qualquer.",
                new List<EncounterOption>
                {
                    Option("A", "recurso_imediato", new GainGoldEffect(10)),
                    Option("B", "aliado_futuro", new GainGoldEffect(0))
                });

            List<CatalogViolation> violations = EncounterCatalogValidator.Validate(new[] { bom });

            Assert.That(violations, Has.None.Matches<CatalogViolation>(v => v.Rule == "opção dominante"));
        }

        // --- 8.2 Presença persistente ---

        [Test]
        public void PresencaAgeNoIntervaloDeclaradoESomeAoSerResolvida()
        {
            EncounterState state = new EncounterState();
            EncounterPresence dragon = new EncounterPresence(
                "dragao_1", "dragao_se_instala", "Dragão", actionIntervalDays: 3,
                periodicEffects: new List<IEffect> { new LoseGoldEffect(5) },
                startDay: 1, resolutionCost: ResourceAmounts.Of(ResourceKind.Gold, 50));
            state.AddPresence(dragon);

            Assert.That(state.Presences.Count, Is.EqualTo(1));
            Assert.That(dragon.NextActionDay, Is.EqualTo(4));

            RunBundle bundle = TestContent.Run();
            bundle.Run.Add(ResourceKind.Gold, 100);
            int goldBeforeEarly = bundle.Run.Gold;

            // Antes do dia 4, a presenca nao age.
            List<EffectResult> earlyResults = state.TickPresences(bundle.Run, 3, SeverityBudget.NegativeEvent);
            Assert.That(earlyResults, Is.Empty);
            Assert.That(bundle.Run.Gold, Is.EqualTo(goldBeforeEarly));

            // No dia agendado, age e reagenda para o proximo intervalo.
            List<EffectResult> dueResults = state.TickPresences(bundle.Run, 4, SeverityBudget.NegativeEvent);
            Assert.That(dueResults, Is.Not.Empty);
            Assert.That(bundle.Run.Gold, Is.LessThan(goldBeforeEarly));
            Assert.That(dragon.NextActionDay, Is.EqualTo(7));

            // Resolvida, some da lista e para de agir.
            bool resolved = state.ResolvePresence(bundle.Run, "dragao_1");
            Assert.That(resolved, Is.True);
            Assert.That(state.Presences, Is.Empty);

            List<EffectResult> afterResolved = state.TickPresences(bundle.Run, 7, SeverityBudget.NegativeEvent);
            Assert.That(afterResolved, Is.Empty);
        }

        [Test]
        public void ResolverPresenca_SemSaldoParaOCusto_NaoRemoveNadaERecusa()
        {
            EncounterState state = new EncounterState();
            state.AddPresence(new EncounterPresence(
                "p1", "src", "Presença", 3, new List<IEffect>(), 1,
                ResourceAmounts.Of(ResourceKind.Gold, 1000)));

            RunBundle bundle = TestContent.Run();

            bool resolved = state.ResolvePresence(bundle.Run, "p1");

            Assert.That(resolved, Is.False);
            Assert.That(state.Presences.Count, Is.EqualTo(1));
        }

        // --- 8.3 Encadeamento ---

        [Test]
        public void ContinuacaoAconteceNoDiaAgendado_MesmoComOutrosEncontrosNoIntervalo()
        {
            EncounterState state = new EncounterState();
            EncounterDefinition continuation = new EncounterDefinition(
                "herói_volta", "O Herói Volta", "Ele retorna, como prometido.",
                new List<EncounterOption> { Option("Continuar", "imediato", new GainGoldEffect(5)) });

            state.ScheduleChain(continuation, day: 10, previousChoiceContext: "ajudou o herói");

            // Outros encontros "acontecem" no intervalo (nada aqui interage com a
            // fila) — drenar em dias anteriores nao antecipa nem perde a cadeia.
            Assert.That(state.DrainDueChains(5), Is.Empty);
            Assert.That(state.DrainDueChains(9), Is.Empty);

            List<ScheduledEncounter> due = state.DrainDueChains(10);

            Assert.That(due.Count, Is.EqualTo(1));
            Assert.That(due[0].Definition, Is.EqualTo(continuation));
            Assert.That(due[0].PreviousChoiceContext, Is.EqualTo("ajudou o herói"));
            Assert.That(state.ScheduledChains, Is.Empty, "a continuacao consumida nao pode sobrar na fila");
        }

        // --- 8.4 Orçamento de severidade ---

        [Test]
        public void EncontroNuncaZeraAIntegridade_MesmoComDanoEnorme()
        {
            RunBundle bundle = TestContent.Run();
            EncounterDefinition brutal = new EncounterDefinition(
                "ruina_amaldicoada", "Ruína Amaldiçoada", "Uma maldição pesa sobre a pedra.",
                new List<EncounterOption>
                {
                    Option("Explorar", "arriscado", new DamageBaseEffect(9999))
                },
                severityClass: EventClass.Negative);

            EncounterOutcome outcome = EncounterResolver.Resolve(bundle.Run, brutal);

            Assert.That(outcome, Is.Not.Null);
            Assert.That(bundle.Run.Integrity, Is.GreaterThanOrEqualTo(1));
            Assert.That(bundle.Run.IsOver, Is.False);
        }

        [Test]
        public void PresencaHostil_CustoPorCobrancaRespeitaOTetoDaCategoria()
        {
            RunBundle bundle = TestContent.Run();
            EncounterState state = new EncounterState();
            state.AddPresence(new EncounterPresence(
                "cobrador", "src", "Cobrador Implacável", 1,
                new List<IEffect> { new LoseGoldEffect(999999) }, bundle.Run.Day,
                ResourceAmounts.Of(ResourceKind.Gold, 30)));

            int goldBefore = bundle.Run.Gold;
            state.TickPresences(bundle.Run, bundle.Run.Day + 1, SeverityBudget.NegativeEvent);

            int lost = goldBefore - bundle.Run.Gold;
            int allowedMax = (int)(goldBefore * SeverityBudget.NegativeEvent.MaxGoldLossFraction) + 1;
            Assert.That(lost, Is.LessThanOrEqualTo(allowedMax), "a cobranca nao pode exceder o teto da categoria");
        }

        // --- 8.5 Encontros não tocam a campanha ---

        [Test]
        public void ResolverEncontroOuPresenca_NuncaMexeNaCampanhaDoRival()
        {
            RivalDefinition rival = TestContent.Rival("r1", TestContent.AssaultIdentity());
            RunBundle bundle = TestContent.RunWithRivals(new List<RivalDefinition> { rival });
            int pendingBefore = bundle.Run.Threats.Pending.Count;
            string currentRivalBefore = bundle.Run.Campaign.Current.Id;

            EncounterDefinition aggressive = new EncounterDefinition(
                "encontro_agressivo", "Encontro Agressivo", "Parece perigoso.",
                new List<EncounterOption>
                {
                    Option("Enfrentar", "arriscado", new DamageBaseEffect(3), new LoseGoldEffect(10))
                });
            EncounterResolver.Resolve(bundle.Run, aggressive);

            EncounterState state = new EncounterState();
            state.AddPresence(new EncounterPresence(
                "p", "src", "Presença", 1, new List<IEffect> { new LoseGoldEffect(5) }, bundle.Run.Day,
                new ResourceAmounts()));
            state.TickPresences(bundle.Run, bundle.Run.Day + 1, SeverityBudget.NegativeEvent);

            Assert.That(bundle.Run.Threats.Pending.Count, Is.EqualTo(pendingBefore));
            Assert.That(bundle.Run.Campaign.Current.Id, Is.EqualTo(currentRivalBefore));
            Assert.That(bundle.Run.Campaign.RepelsAchieved, Is.Zero);
        }
    }
}

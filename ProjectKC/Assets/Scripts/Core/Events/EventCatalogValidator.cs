using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    public sealed class CatalogViolation
    {
        public CatalogViolation(string subjectId, string rule, string detail)
        {
            SubjectId = subjectId;
            Rule = rule;
            Detail = detail;
        }

        public string SubjectId { get; }

        public string Rule { get; }

        public string Detail { get; }

        public override string ToString() => SubjectId + " viola " + Rule + ": " + Detail;
    }

    /// <summary>
    /// Verifica o catalogo de eventos contra o orcamento de severidade (design D10).
    /// Roda como teste: o objetivo e que um evento fora do orcamento reprove a suite
    /// no momento em que for criado, e nao apareca como reclamacao de jogador meses
    /// depois.
    /// </summary>
    public static class EventCatalogValidator
    {
        public static List<CatalogViolation> Validate(IEnumerable<EventDefinition> catalog)
        {
            List<CatalogViolation> violations = new List<CatalogViolation>();

            foreach (EventDefinition definition in catalog)
            {
                if (definition == null)
                {
                    continue;
                }

                if (definition.Options.Count == 0)
                {
                    violations.Add(new CatalogViolation(definition.Id, "opcoes", "evento sem nenhuma opcao"));
                    continue;
                }

                if (definition.HasOffer && !definition.HasDeclineOption)
                {
                    violations.Add(new CatalogViolation(
                        definition.Id,
                        "oferta sem saida",
                        "toda opcao paga precisa de uma alternativa sem custo para recusar"));
                }

                SeverityBudget budget = SeverityBudget.ForClass(definition.Class);
                ValidateEffects(definition, budget, violations);
            }

            return violations;
        }

        private static void ValidateEffects(
            EventDefinition definition,
            SeverityBudget budget,
            List<CatalogViolation> violations)
        {
            for (int optionIndex = 0; optionIndex < definition.Options.Count; optionIndex++)
            {
                EventOption option = definition.Options[optionIndex];
                double goldLoss = 0;
                double baseDamage = 0;
                int tilesDestroyed = 0;
                int cardsRemoved = 0;

                for (int i = 0; i < option.Effects.Count; i++)
                {
                    if (!(option.Effects[i] is ISeverityDeclaring declaring))
                    {
                        continue;
                    }

                    SeverityClaim claim = declaring.DeclareSeverity();
                    goldLoss += claim.GoldLossFraction;
                    baseDamage += claim.BaseDamageFraction;
                    tilesDestroyed += claim.TilesDestroyed;
                    cardsRemoved += claim.CardsRemoved;

                    if (claim.ScalesWithDay)
                    {
                        violations.Add(new CatalogViolation(
                            definition.Id,
                            "escalada",
                            "efeito " + option.Effects[i].Kind + " escala com o avanco da run"));
                    }

                    if (claim.DestroysBuiltTile && !budget.CanDestroyBuiltTile)
                    {
                        violations.Add(new CatalogViolation(
                            definition.Id,
                            "celula construida",
                            "efeito " + option.Effects[i].Kind + " pode arrasar celula com edificio"));
                    }
                }

                string where = "opcao " + optionIndex;

                // Preco aceito nao conta como severidade; dano continua contando.
                if (option.IsOffer && definition.HasDeclineOption)
                {
                    goldLoss = 0;
                }

                if (goldLoss > budget.MaxGoldLossFraction)
                {
                    violations.Add(new CatalogViolation(
                        definition.Id,
                        "ouro",
                        where + " pede " + goldLoss.ToString("0.##") +
                        " do ouro, teto e " + budget.MaxGoldLossFraction.ToString("0.##")));
                }

                if (baseDamage > budget.MaxBaseDamageFraction)
                {
                    violations.Add(new CatalogViolation(
                        definition.Id,
                        "integridade",
                        where + " pede " + baseDamage.ToString("0.##") +
                        " da integridade, teto e " + budget.MaxBaseDamageFraction.ToString("0.##")));
                }

                if (tilesDestroyed > budget.MaxTilesDestroyed)
                {
                    violations.Add(new CatalogViolation(
                        definition.Id,
                        "territorio",
                        where + " arrasa " + tilesDestroyed + " celulas, teto e " + budget.MaxTilesDestroyed));
                }

                if (cardsRemoved > budget.MaxCardsRemoved)
                {
                    violations.Add(new CatalogViolation(
                        definition.Id,
                        "deck",
                        where + " remove " + cardsRemoved + " cartas, teto e " + budget.MaxCardsRemoved));
                }
            }
        }
    }
}

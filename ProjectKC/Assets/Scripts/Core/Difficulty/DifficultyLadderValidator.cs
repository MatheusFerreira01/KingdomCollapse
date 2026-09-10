using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Verifica a escada de dificuldade. Cada nivel precisa ser jogavel sozinho, e
    /// cada degrau precisa ser mais duro que o anterior em ao menos um eixo e mais
    /// facil em nenhum (spec difficulty-ladder). Sem esta verificacao, basta um nivel
    /// autorado com um rival a menos para a "escada" ter um degrau para baixo.
    /// </summary>
    public static class DifficultyLadderValidator
    {
        public static List<CatalogViolation> Validate(DifficultyLadder ladder)
        {
            List<CatalogViolation> violations = new List<CatalogViolation>();
            IReadOnlyList<DifficultyLevel> levels = ladder.Levels;

            if (levels.Count == 0)
            {
                violations.Add(new CatalogViolation("escada", "niveis", "nenhum nivel de dificuldade"));
                return violations;
            }

            for (int i = 0; i < levels.Count; i++)
            {
                DifficultyLevel level = levels[i];

                if (level.Rank != i + 1)
                {
                    violations.Add(new CatalogViolation(
                        level.Id, "ordem", "esperado nivel " + (i + 1) + ", declarado " + level.Rank));
                }

                ValidateLevel(level, violations);

                if (i > 0)
                {
                    CompareWithPrevious(levels[i - 1], level, violations);
                }
            }

            return violations;
        }

        private static void ValidateLevel(DifficultyLevel level, List<CatalogViolation> violations)
        {
            if (level.Rivals.Count == 0)
            {
                violations.Add(new CatalogViolation(level.Id, "rivais", "nivel sem rivais nao tem como ser vencido"));
                return;
            }

            // O jogador precisa ler no que esta se metendo. O primeiro nivel nao
            // endurece nada, entao so ele pode ficar sem texto.
            if (level.Rank > 1 && string.IsNullOrEmpty(level.Hardening))
            {
                violations.Add(new CatalogViolation(
                    level.Id, "endurecimento", "nivel sem texto do que endurece em relacao ao anterior"));
            }

            violations.AddRange(RivalCatalogValidator.ValidateEntryOrder(level.Id, level.Rivals));

            bool anyEconomic = false;
            for (int i = 0; i < level.Rivals.Count; i++)
            {
                RivalIdentity identity = level.Rivals[i]?.Identity;
                if (identity != null && identity.DeclaresAxis && !identity.IsCounteredByDefense && identity.RaidFraction > 0)
                {
                    anyEconomic = true;
                }
            }

            // A regra de contra-jogo vale por nivel, e nao so no catalogo: um nivel so
            // de assalto e vencido com a mesma build de sempre, mesmo que o catalogo
            // tenha um rival economico guardado em outro nivel.
            if (!anyEconomic)
            {
                violations.Add(new CatalogViolation(
                    level.Id, "contra-jogo", "nenhum rival do nivel pressiona algo alem da defesa"));
            }
        }

        /// <summary>
        /// Compara dois degraus consecutivos nos eixos que o dado permite medir:
        /// numero de rivais, forca das campanhas, ritmo de entrada e escassez inicial.
        /// </summary>
        private static void CompareWithPrevious(DifficultyLevel lower, DifficultyLevel higher, List<CatalogViolation> violations)
        {
            List<string> easier = new List<string>();
            List<string> harder = new List<string>();

            Compare("numero de rivais", higher.Rivals.Count, lower.Rivals.Count, true, easier, harder);
            Compare("forca das campanhas", higher.CampaignDemand(), lower.CampaignDemand(), true, easier, harder);
            Compare("escassez inicial", higher.StartingResourceFactor, lower.StartingResourceFactor, false, easier, harder);

            // Ritmo de entrada: posicao a posicao, entrar mais cedo e mais duro.
            int shared = System.Math.Min(lower.Rivals.Count, higher.Rivals.Count);
            bool enteredLater = false;
            bool enteredEarlier = false;
            for (int k = 1; k < shared; k++)
            {
                int a = lower.EntryDayAt(k);
                int b = higher.EntryDayAt(k);
                enteredLater |= b > a;
                enteredEarlier |= b < a;
            }

            if (enteredLater)
            {
                easier.Add("ritmo de entrada");
            }
            else if (enteredEarlier)
            {
                harder.Add("ritmo de entrada");
            }

            for (int i = 0; i < easier.Count; i++)
            {
                violations.Add(new CatalogViolation(
                    higher.Id, "escada", "mais facil que " + lower.Id + " em " + easier[i]));
            }

            if (harder.Count == 0)
            {
                violations.Add(new CatalogViolation(
                    higher.Id, "escada", "nao endurece nenhum eixo em relacao a " + lower.Id));
            }
        }

        private static void Compare(
            string axis, double higher, double lower, bool biggerIsHarder, List<string> easier, List<string> harder)
        {
            if (higher == lower)
            {
                return;
            }

            bool isHarder = biggerIsHarder ? higher > lower : higher < lower;
            (isHarder ? harder : easier).Add(axis);
        }
    }
}

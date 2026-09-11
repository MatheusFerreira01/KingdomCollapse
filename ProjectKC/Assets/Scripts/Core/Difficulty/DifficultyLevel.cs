using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Um degrau da escada de dificuldade: seu roster de rivais, o que ele endurece
    /// em texto (pra UI ler do dado, spec — "Requirement" de exibir o texto do nivel)
    /// e o teto de poder permanente que a arvore de meta pode conceder nele
    /// (design D6).
    /// </summary>
    public sealed class DifficultyLevel
    {
        public DifficultyLevel(
            string id,
            string displayName,
            List<RivalDefinition> rivals,
            List<string> hardenings,
            int metaPowerCap,
            int pressure)
        {
            Id = id;
            DisplayName = displayName;
            Rivals = rivals ?? new List<RivalDefinition>();
            Hardenings = hardenings ?? new List<string>();
            MetaPowerCap = metaPowerCap;
            Pressure = pressure;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public IReadOnlyList<RivalDefinition> Rivals { get; }

        /// <summary>Frases descrevendo o que este nivel endurece frente ao anterior.
        /// Autorado, nao derivado — e o que deixa a UI ler do dado (task 5.4).</summary>
        public IReadOnlyList<string> Hardenings { get; }

        /// <summary>Teto de poder permanente acumulado que a arvore de meta pode
        /// conceder enquanto este nivel for o mais alto desbloqueado (design D6).</summary>
        public int MetaPowerCap { get; }

        /// <summary>
        /// Quao dificil o nivel e, num eixo unico e autorado. Usado so pra verificar
        /// que a escada nao afrouxa (spec difficulty-ladder) — nao influencia a regra.
        /// </summary>
        public int Pressure { get; }
    }

    /// <summary>
    /// Verifica que a escada so endurece: nenhum nivel pode ser mais facil que o
    /// anterior em pressao ou em teto de poder, e a escada como um todo precisa
    /// subir em pelo menos um dos dois em algum ponto, ou ela nao e uma escada.
    /// </summary>
    public static class DifficultyLadderValidator
    {
        public static List<CatalogViolation> Validate(IReadOnlyList<DifficultyLevel> levels)
        {
            List<CatalogViolation> violations = new List<CatalogViolation>();

            if (levels == null || levels.Count == 0)
            {
                return violations;
            }

            bool everHarder = false;

            for (int i = 1; i < levels.Count; i++)
            {
                DifficultyLevel previous = levels[i - 1];
                DifficultyLevel current = levels[i];

                if (current.Pressure < previous.Pressure)
                {
                    violations.Add(new CatalogViolation(
                        current.Id, "pressao",
                        "nivel " + current.Id + " (" + current.Pressure + ") e mais facil que " +
                        previous.Id + " (" + previous.Pressure + ")"));
                }

                if (current.MetaPowerCap < previous.MetaPowerCap)
                {
                    violations.Add(new CatalogViolation(
                        current.Id, "teto de poder",
                        "nivel " + current.Id + " (" + current.MetaPowerCap + ") tem teto menor que " +
                        previous.Id + " (" + previous.MetaPowerCap + ")"));
                }

                if (current.Pressure > previous.Pressure || current.MetaPowerCap > previous.MetaPowerCap)
                {
                    everHarder = true;
                }
            }

            if (levels.Count > 1 && !everHarder)
            {
                violations.Add(new CatalogViolation(
                    "escada", "monotonicidade",
                    "nenhum nivel endurece em pressao nem em teto de poder frente ao anterior"));
            }

            return violations;
        }
    }
}

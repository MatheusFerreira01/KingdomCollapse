using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Uma opção de um encontro. "Nature" é o que distingue uma escolha assimétrica
    /// de um evento disfarçado: duas opções da mesma natureza (ex.: as duas rendem
    /// "recurso imediato") não são uma escolha de verdade, só dois números
    /// diferentes (spec encounters — "Requirement: Escolha assimétrica").
    /// </summary>
    public sealed class EncounterOption
    {
        public EncounterOption(
            string label,
            string nature,
            IReadOnlyList<IEffect> effects,
            bool leavesPresence = false,
            bool isOffer = false)
        {
            Label = label;
            Nature = nature ?? string.Empty;
            Effects = effects ?? new List<IEffect>();
            LeavesPresence = leavesPresence;
            IsOffer = isOffer;
        }

        public string Label { get; }

        /// <summary>Natureza do ganho/custo desta opção (ex.: "imediato", "futuro",
        /// "seguro", "arriscado"). Autorado, não derivado dos efeitos.</summary>
        public string Nature { get; }

        public IReadOnlyList<IEffect> Effects { get; }

        /// <summary>Esta opção deixa uma presença na run (spec — "Presença persistente").</summary>
        public bool LeavesPresence { get; }

        /// <summary>Opção paga: isenta do teto de ouro do orçamento, igual a EventOption.</summary>
        public bool IsOffer { get; }
    }

    /// <summary>
    /// Definição pura de um encontro: identidade própria (nome, descrição), opções
    /// assimétricas, e o orçamento de severidade que suas opções obedecem — a
    /// mesma classificação Positive/Neutral/Negative dos eventos de fim de dia
    /// (design D10, spec encounters).
    /// </summary>
    public sealed class EncounterDefinition
    {
        public EncounterDefinition(
            string id,
            string displayName,
            string flavorText,
            IReadOnlyList<EncounterOption> options,
            EventClass severityClass = EventClass.Neutral,
            double weight = 1.0,
            int cooldownDays = 10,
            IReadOnlyList<EventCondition> conditions = null)
        {
            Id = id;
            DisplayName = displayName;
            FlavorText = flavorText ?? string.Empty;
            Options = options ?? new List<EncounterOption>();
            SeverityClass = severityClass;
            Weight = weight;
            CooldownDays = cooldownDays;
            Conditions = conditions ?? new List<EventCondition>();
        }

        public string Id { get; }

        public string DisplayName { get; }

        /// <summary>Texto de apresentação. Vazio reprova no catálogo — é a
        /// "identidade" que a spec exige, não um ajuste de recurso anônimo.</summary>
        public string FlavorText { get; }

        public IReadOnlyList<EncounterOption> Options { get; }

        public EventClass SeverityClass { get; }

        public double Weight { get; }

        public int CooldownDays { get; }

        public IReadOnlyList<EventCondition> Conditions { get; }

        public bool HasChoice => Options.Count > 1;

        /// <summary>Existe ao menos uma saída sem custo — mesma regra de EventDefinition,
        /// decide se uma opção paga fica isenta do teto de ouro.</summary>
        public bool HasDeclineOption
        {
            get
            {
                for (int i = 0; i < Options.Count; i++)
                {
                    if (!Options[i].IsOffer)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsEligible(RunState run)
        {
            for (int i = 0; i < Conditions.Count; i++)
            {
                if (!Conditions[i].IsSatisfied(run))
                {
                    return false;
                }
            }

            return true;
        }

        public override string ToString() => Id;
    }

    /// <summary>
    /// Verifica o catálogo de encontros: identidade obrigatória, e nenhuma escolha
    /// pode ter todas as opções da mesma natureza — o sintoma direto de opção
    /// dominante em toda situação (task 8.6, spec encounters).
    /// </summary>
    public static class EncounterCatalogValidator
    {
        public static List<CatalogViolation> Validate(IEnumerable<EncounterDefinition> catalog)
        {
            List<CatalogViolation> violations = new List<CatalogViolation>();

            foreach (EncounterDefinition encounter in catalog)
            {
                if (encounter == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(encounter.DisplayName) || string.IsNullOrEmpty(encounter.FlavorText))
                {
                    violations.Add(new CatalogViolation(
                        encounter.Id, "identidade", "encontro sem nome ou texto de apresentação"));
                }

                if (!encounter.HasChoice)
                {
                    continue;
                }

                bool allSameNature = true;
                string firstNature = encounter.Options[0].Nature;
                for (int i = 1; i < encounter.Options.Count; i++)
                {
                    if (encounter.Options[i].Nature != firstNature)
                    {
                        allSameNature = false;
                        break;
                    }
                }

                if (allSameNature)
                {
                    violations.Add(new CatalogViolation(
                        encounter.Id, "opção dominante",
                        "todas as opções têm a mesma natureza (" + firstNature +
                        "); nenhuma escolha real existe, uma sempre domina"));
                }
            }

            return violations;
        }
    }
}

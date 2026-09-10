using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Verifica o catalogo de rivais. Roda como teste, como a validacao de eventos:
    /// um rival mal declarado precisa reprovar a suite quando e criado, e nao
    /// aparecer como um ataque que nao se sabe onde cai.
    /// </summary>
    public static class RivalCatalogValidator
    {
        public const string CatalogSubject = "catalogo de rivais";

        public static List<CatalogViolation> Validate(IEnumerable<RivalDefinition> catalog)
        {
            List<CatalogViolation> violations = new List<CatalogViolation>();
            int count = 0;
            bool anyEconomic = false;

            foreach (RivalDefinition rival in catalog)
            {
                if (rival == null)
                {
                    continue;
                }

                count++;
                if (rival.Identity != null && rival.Identity.DeclaresAxis &&
                    !rival.Identity.IsCounteredByDefense && rival.Identity.RaidFraction > 0)
                {
                    anyEconomic = true;
                }

                if (string.IsNullOrEmpty(rival.DisplayName))
                {
                    violations.Add(new CatalogViolation(rival.Id, "nome", "rival sem nome"));
                }

                ValidateIdentity(rival, violations);

                if (rival.AttacksToDefeat < 1)
                {
                    violations.Add(new CatalogViolation(
                        rival.Id,
                        "campanha",
                        "campanha precisa de ao menos 1 ataque, declara " + rival.AttacksToDefeat));
                }

                ValidateCampaign(rival, violations);
            }

            // Se todo rival fosse respondido com defesa, a resposta otima voltaria a
            // ser sempre mais defesa, e os cinco recursos virariam contabilidade
            // decorativa (design D4). Catalogo vazio tambem reprova: sem rival
            // nenhum a run cai na ameaca anonima, que nao tem contra-jogo algum.
            if (!anyEconomic)
            {
                violations.Add(new CatalogViolation(
                    CatalogSubject,
                    "contra-jogo",
                    count == 0
                        ? "nenhum rival no catalogo"
                        : "nenhum dos " + count + " rivais pressiona algo alem da defesa"));
            }

            return violations;
        }

        /// <summary>
        /// Verifica os rivais de um nivel na ordem em que entram em guerra: cada um
        /// exige mais que o que entrou antes, e nenhuma identidade se repete (spec
        /// rival-kingdoms). Com rivais lutando juntos, dois da mesma identidade seriam
        /// respondidos pela mesma build, e o nivel teria um tipo de pressao a menos
        /// do que parece.
        /// </summary>
        public static List<CatalogViolation> ValidateEntryOrder(string levelId, IReadOnlyList<RivalDefinition> ordered)
        {
            List<CatalogViolation> violations = new List<CatalogViolation>();
            Dictionary<string, string> identityOwner = new Dictionary<string, string>();

            for (int i = 0; i < ordered.Count; i++)
            {
                RivalDefinition rival = ordered[i];
                if (rival == null)
                {
                    violations.Add(new CatalogViolation(levelId, "entrada", "posicao " + i + " sem rival"));
                    continue;
                }

                if (rival.Identity != null)
                {
                    if (identityOwner.TryGetValue(rival.Identity.Id, out string owner))
                    {
                        violations.Add(new CatalogViolation(
                            levelId,
                            "entrada",
                            rival.Id + " repete a identidade " + rival.Identity.Id + " de " + owner));
                    }
                    else
                    {
                        identityOwner[rival.Identity.Id] = rival.Id;
                    }
                }

                RivalDefinition previous = i > 0 ? ordered[i - 1] : null;
                if (previous != null && rival.PeakForce <= previous.PeakForce)
                {
                    violations.Add(new CatalogViolation(
                        levelId,
                        "entrada",
                        rival.Id + " (pico " + rival.PeakForce + ") nao exige mais que " +
                        previous.Id + " (pico " + previous.PeakForce + "), que entra antes"));
                }
            }

            return violations;
        }

        private static void ValidateIdentity(RivalDefinition rival, List<CatalogViolation> violations)
        {
            RivalIdentity identity = rival.Identity;
            if (identity == null)
            {
                violations.Add(new CatalogViolation(rival.Id, "identidade", "rival sem identidade"));
                return;
            }

            if (!identity.DeclaresAxis)
            {
                violations.Add(new CatalogViolation(
                    rival.Id,
                    "eixo",
                    "identidade " + identity.Id + " nao declara o eixo que pressiona"));
            }

            if (identity.Axis == PressureAxis.Resource && identity.PressuredResource == null)
            {
                violations.Add(new CatalogViolation(
                    rival.Id,
                    "eixo",
                    "identidade " + identity.Id + " pressiona recurso sem dizer qual"));
            }

            // Populacao tem eixo proprio porque a perda de gente desfaz alocacao, e
            // nao e so um saldo que cai. Aceitar pelos dois caminhos daria duas regras
            // para a mesma perda.
            if (identity.Axis == PressureAxis.Resource && identity.PressuredResource == ResourceKind.Population)
            {
                violations.Add(new CatalogViolation(
                    rival.Id,
                    "eixo",
                    "identidade " + identity.Id + " declara populacao como recurso; use o eixo Population"));
            }

            // Rival economico sem saque e rival de assalto disfarcado: a defesa
            // voltaria a ser resposta suficiente, que e o que a identidade existe
            // para impedir (design D4).
            if (identity.DeclaresAxis && !identity.IsCounteredByDefense && identity.RaidFraction <= 0)
            {
                violations.Add(new CatalogViolation(
                    rival.Id,
                    "saque",
                    "identidade economica " + identity.Id + " nao saqueia; a defesa bastaria contra ela"));
            }

            if (string.IsNullOrEmpty(identity.Description))
            {
                violations.Add(new CatalogViolation(
                    rival.Id,
                    "identidade",
                    "identidade " + identity.Id + " sem descricao; o jogador precisa saber o que esperar"));
            }
        }

        private static void ValidateCampaign(RivalDefinition rival, List<CatalogViolation> violations)
        {
            if (rival.Campaign == null)
            {
                violations.Add(new CatalogViolation(rival.Id, "campanha", "rival sem curva de campanha"));
                return;
            }

            if (rival.AttacksToDefeat < 2)
            {
                return;
            }

            int first = rival.Campaign.ForceFor(0);
            int last = rival.Campaign.ForceFor(rival.AttacksToDefeat - 1);
            if (last <= first)
            {
                violations.Add(new CatalogViolation(
                    rival.Id,
                    "escalada",
                    "ultimo ataque (forca " + last + ") nao exige mais que o primeiro (forca " + first + ")"));
            }
        }
    }
}

using System;
using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Onde o excedente de um ataque bem-sucedido cai (design D4). Territory e
    /// Integrity sao anulados por defesa suficiente — o overflow so existe quando a
    /// forca excede a defesa, igual ao combate de hoje. Population e Resource cobram
    /// um pedagio a cada ataque que chega, defesa alta ou nao: e o que obriga o
    /// jogador a responder com estoque e redundancia em vez de so erguer torre.
    /// </summary>
    public enum RivalAxis
    {
        Territory = 0,
        Integrity = 1,
        Population = 2,
        Resource = 3
    }

    /// <summary>
    /// Quem o rival e, alem do numero. Cada identidade declara o eixo que pressiona,
    /// para que o jogador saiba que resposta preparar antes do primeiro ataque
    /// (spec rival-kingdoms — "Requirement: Identidade pressiona recursos diferentes").
    /// </summary>
    public sealed class RivalIdentity
    {
        public RivalIdentity(string id, string displayName, RivalAxis axis, ResourceKind resource = ResourceKind.Gold)
        {
            Id = id;
            DisplayName = displayName;
            Axis = axis;
            Resource = resource;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public RivalAxis Axis { get; }

        /// <summary>Recurso pressionado. So importa quando Axis == Resource.</summary>
        public ResourceKind Resource { get; }

        /// <summary>
        /// Se defesa suficiente anula o efeito deste eixo. Territory/Integrity: sim,
        /// como o combate de hoje. Population/Resource: nao — e a exigencia da spec de
        /// que ao menos um rival nao seja resolvido por defesa.
        /// </summary>
        public bool ResolvedByDefense => Axis == RivalAxis.Territory || Axis == RivalAxis.Integrity;
    }

    /// <summary>
    /// Um reino rival: identidade mais a curva da campanha. A campanha e dado, nao
    /// codigo — RivalCampaignState consome isto para preencher o relogio de ameaca
    /// (design D3).
    /// </summary>
    public sealed class RivalDefinition
    {
        public RivalDefinition(
            string id,
            string displayName,
            RivalIdentity identity,
            int attackCount,
            ThreatCurve campaignCurve = null)
        {
            Id = id;
            DisplayName = displayName;
            Identity = identity;
            AttackCount = Math.Max(1, attackCount);
            CampaignCurve = campaignCurve ?? new ThreatCurve();
        }

        public string Id { get; }

        public string DisplayName { get; }

        public RivalIdentity Identity { get; }

        /// <summary>Quantos ataques repelidos derrotam este rival.</summary>
        public int AttackCount { get; }

        public ThreatCurve CampaignCurve { get; }
    }

    /// <summary>
    /// Verifica o catalogo de rivais. Falha se algum rival nao declarar identidade,
    /// ou se nenhum rival do catalogo pressionar um eixo diferente de defesa — sem
    /// isso o desenho degenera de volta para "so ergue torre" (design D4).
    /// </summary>
    public static class RivalCatalogValidator
    {
        public static List<CatalogViolation> Validate(IEnumerable<RivalDefinition> catalog)
        {
            List<CatalogViolation> violations = new List<CatalogViolation>();
            List<RivalDefinition> rivals = new List<RivalDefinition>(catalog);
            bool anyNonDefense = false;

            foreach (RivalDefinition rival in rivals)
            {
                if (rival == null)
                {
                    continue;
                }

                if (rival.Identity == null)
                {
                    violations.Add(new CatalogViolation(rival.Id, "identidade", "rival sem identidade declarada"));
                    continue;
                }

                if (!rival.Identity.ResolvedByDefense)
                {
                    anyNonDefense = true;
                }
            }

            if (rivals.Count > 0 && !anyNonDefense)
            {
                violations.Add(new CatalogViolation(
                    "catalogo", "eixo nao-defensivo",
                    "nenhum rival pressiona um eixo diferente de defesa (Territory/Integrity)"));
            }

            return violations;
        }
    }
}

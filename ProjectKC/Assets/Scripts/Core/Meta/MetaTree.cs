using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    public enum PurchaseRejection
    {
        None = 0,
        UnknownNode,
        AlreadyPurchased,
        MissingPrerequisite,
        NotEnoughCurrency
    }

    public readonly struct PurchaseResult
    {
        private PurchaseResult(bool ok, PurchaseRejection rejection, string missingPrerequisite)
        {
            Ok = ok;
            Rejection = rejection;
            MissingPrerequisite = missingPrerequisite;
        }

        public bool Ok { get; }

        public PurchaseRejection Rejection { get; }

        /// <summary>Qual pre-requisito falta. A UI mostra isso em vez de so recusar.</summary>
        public string MissingPrerequisite { get; }

        public static readonly PurchaseResult Success =
            new PurchaseResult(true, PurchaseRejection.None, null);

        public static PurchaseResult Fail(PurchaseRejection rejection, string missing = null)
        {
            return new PurchaseResult(false, rejection, missing);
        }
    }

    /// <summary>
    /// Arvore de meta: catalogo de nos, regras de compra e resolucao do conteudo
    /// desbloqueado. Os galhos de raca sao independentes entre si, entao investir
    /// numa raca nunca encarece nem trava outra.
    /// </summary>
    public sealed class MetaTree
    {
        private readonly Dictionary<string, MetaNodeDefinition> _nodes =
            new Dictionary<string, MetaNodeDefinition>();

        public MetaTree(IEnumerable<MetaNodeDefinition> nodes = null)
        {
            if (nodes == null)
            {
                return;
            }

            foreach (MetaNodeDefinition node in nodes)
            {
                if (node != null)
                {
                    _nodes[node.Id] = node;
                }
            }
        }

        public IReadOnlyDictionary<string, MetaNodeDefinition> Nodes => _nodes;

        public void Add(MetaNodeDefinition node)
        {
            if (node != null)
            {
                _nodes[node.Id] = node;
            }
        }

        public MetaNodeDefinition Get(string nodeId)
        {
            return _nodes.TryGetValue(nodeId, out MetaNodeDefinition node) ? node : null;
        }

        public PurchaseResult CanPurchase(MetaProfile profile, string nodeId)
        {
            MetaNodeDefinition node = Get(nodeId);
            if (node == null)
            {
                return PurchaseResult.Fail(PurchaseRejection.UnknownNode);
            }

            if (profile.HasNode(nodeId))
            {
                return PurchaseResult.Fail(PurchaseRejection.AlreadyPurchased);
            }

            for (int i = 0; i < node.PrerequisiteIds.Count; i++)
            {
                string prerequisite = node.PrerequisiteIds[i];
                if (!profile.HasNode(prerequisite))
                {
                    return PurchaseResult.Fail(PurchaseRejection.MissingPrerequisite, prerequisite);
                }
            }

            if (profile.Currency < node.Cost)
            {
                return PurchaseResult.Fail(PurchaseRejection.NotEnoughCurrency);
            }

            return PurchaseResult.Success;
        }

        public PurchaseResult Purchase(MetaProfile profile, string nodeId)
        {
            PurchaseResult check = CanPurchase(profile, nodeId);
            if (!check.Ok)
            {
                return check;
            }

            MetaNodeDefinition node = Get(nodeId);
            profile.SpendCurrency(node.Cost);
            profile.MarkPurchased(nodeId);
            return PurchaseResult.Success;
        }

        /// <summary>
        /// Conteudo liberado para uma raca: tronco geral mais o galho dela. O galho
        /// das outras racas fica de fora mesmo estando comprado.
        /// </summary>
        public HashSet<string> UnlockedContent(MetaProfile profile, UnlockKind kind, string raceId)
        {
            HashSet<string> unlocked = new HashSet<string>();

            foreach (KeyValuePair<string, MetaNodeDefinition> pair in _nodes)
            {
                MetaNodeDefinition node = pair.Value;
                if (!profile.HasNode(node.Id))
                {
                    continue;
                }

                if (node.IsRaceBranch && node.RaceBranchId != raceId)
                {
                    continue;
                }

                for (int i = 0; i < node.Unlocks.Count; i++)
                {
                    if (node.Unlocks[i].Kind == kind)
                    {
                        unlocked.Add(node.Unlocks[i].ContentId);
                    }
                }
            }

            return unlocked;
        }

        /// <summary>Racas desbloqueadas. Nao dependem de galho, entao valem sempre.</summary>
        public HashSet<string> UnlockedRaces(MetaProfile profile)
        {
            HashSet<string> races = new HashSet<string>();

            foreach (KeyValuePair<string, MetaNodeDefinition> pair in _nodes)
            {
                if (!profile.HasNode(pair.Key))
                {
                    continue;
                }

                MetaNodeDefinition node = pair.Value;
                for (int i = 0; i < node.Unlocks.Count; i++)
                {
                    if (node.Unlocks[i].Kind == UnlockKind.Race)
                    {
                        races.Add(node.Unlocks[i].ContentId);
                    }
                }
            }

            return races;
        }

        public List<MetaNodeDefinition> BranchFor(string raceId)
        {
            List<MetaNodeDefinition> branch = new List<MetaNodeDefinition>();
            foreach (KeyValuePair<string, MetaNodeDefinition> pair in _nodes)
            {
                if (pair.Value.RaceBranchId == raceId)
                {
                    branch.Add(pair.Value);
                }
            }

            branch.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return branch;
        }
    }

    /// <summary>
    /// Valida o catalogo de meta. A regra central e que nenhum no venda poder
    /// numerico: a progressao entre runs cresce variedade, nao forca (design/spec).
    /// </summary>
    public static class MetaCatalogValidator
    {
        public static List<CatalogViolation> Validate(MetaTree tree)
        {
            List<CatalogViolation> violations = new List<CatalogViolation>();

            foreach (KeyValuePair<string, MetaNodeDefinition> pair in tree.Nodes)
            {
                MetaNodeDefinition node = pair.Value;

                if (node.Unlocks.Count == 0)
                {
                    violations.Add(new CatalogViolation(node.Id, "conteudo", "no nao desbloqueia nada"));
                }

                for (int i = 0; i < node.Unlocks.Count; i++)
                {
                    if (node.Unlocks[i].Kind == UnlockKind.NumericBonus)
                    {
                        violations.Add(new CatalogViolation(
                            node.Id,
                            "bonus numerico",
                            "meta desbloqueia conteudo, nunca poder permanente (" +
                            node.Unlocks[i].ContentId + ")"));
                    }
                }

                for (int i = 0; i < node.PrerequisiteIds.Count; i++)
                {
                    string prerequisite = node.PrerequisiteIds[i];
                    if (tree.Get(prerequisite) == null)
                    {
                        violations.Add(new CatalogViolation(
                            node.Id, "pre-requisito", "aponta para no inexistente " + prerequisite));
                        continue;
                    }

                    if (prerequisite == node.Id)
                    {
                        violations.Add(new CatalogViolation(node.Id, "pre-requisito", "no depende de si mesmo"));
                    }
                }

                if (node.Cost < 0)
                {
                    violations.Add(new CatalogViolation(node.Id, "custo", "custo negativo"));
                }
            }

            return violations;
        }
    }
}

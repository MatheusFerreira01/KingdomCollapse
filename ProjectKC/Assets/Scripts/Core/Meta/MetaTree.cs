using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    public enum PurchaseRejection
    {
        None = 0,
        UnknownNode,
        AlreadyPurchased,
        MissingPrerequisite,
        NotEnoughCurrency,
        LevelLocked
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

            if (profile.HighestUnlockedLevel < node.RequiredLevel)
            {
                return PurchaseResult.Fail(PurchaseRejection.LevelLocked);
            }

            if (profile.Currency < node.Cost)
            {
                return PurchaseResult.Fail(PurchaseRejection.NotEnoughCurrency);
            }

            return PurchaseResult.Success;
        }

        /// <summary>
        /// Bonus permanentes que valem para uma run desta raca: tronco geral mais o
        /// galho dela, na ordem dos ids para que a aplicacao seja deterministica.
        /// </summary>
        public List<MetaBonus> GrantedBonuses(MetaProfile profile, string raceId)
        {
            List<MetaBonus> bonuses = new List<MetaBonus>();
            if (profile == null)
            {
                return bonuses;
            }

            List<string> ids = new List<string>(_nodes.Keys);
            ids.Sort(string.CompareOrdinal);

            for (int n = 0; n < ids.Count; n++)
            {
                MetaNodeDefinition node = _nodes[ids[n]];
                if (!profile.HasNode(node.Id) || (node.IsRaceBranch && node.RaceBranchId != raceId))
                {
                    continue;
                }

                for (int i = 0; i < node.Bonuses.Count; i++)
                {
                    bonuses.Add(node.Bonuses[i]);
                }
            }

            return bonuses;
        }

        /// <summary>
        /// Poder maximo que um jogador pode ter acumulado ao jogar o nivel dado: todo
        /// no do tronco geral ja compravel, mais o galho de raca mais forte (galhos
        /// nao se somam, porque uma run so tem uma raca).
        /// </summary>
        public int PowerAvailableAt(int levelRank)
        {
            int trunk = 0;
            Dictionary<string, int> branches = new Dictionary<string, int>();

            foreach (KeyValuePair<string, MetaNodeDefinition> pair in _nodes)
            {
                MetaNodeDefinition node = pair.Value;
                if (node.RequiredLevel > levelRank)
                {
                    continue;
                }

                if (!node.IsRaceBranch)
                {
                    trunk += node.Power;
                    continue;
                }

                branches.TryGetValue(node.RaceBranchId, out int current);
                branches[node.RaceBranchId] = current + node.Power;
            }

            int strongest = 0;
            foreach (KeyValuePair<string, int> pair in branches)
            {
                if (pair.Value > strongest)
                {
                    strongest = pair.Value;
                }
            }

            return trunk + strongest;
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

                if (node.Unlocks.Count == 0 && node.Bonuses.Count == 0)
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
                            "poder permanente e declarado como bonus do no, que pesa no teto do nivel (" +
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

        /// <summary>
        /// O teto de poder por nivel (spec difficulty-ladder, design D6). Para cada
        /// degrau, o poder que a arvore ja pode ter concedido precisa caber no teto
        /// que o degrau declara: e isso que impede o grind de trivializar a escada.
        /// </summary>
        public static List<CatalogViolation> ValidatePowerCaps(MetaTree tree, DifficultyLadder ladder)
        {
            List<CatalogViolation> violations = new List<CatalogViolation>();
            if (tree == null || ladder == null)
            {
                return violations;
            }

            for (int i = 0; i < ladder.Levels.Count; i++)
            {
                DifficultyLevel level = ladder.Levels[i];
                int power = tree.PowerAvailableAt(level.Rank);
                if (power > level.MetaPowerCap)
                {
                    violations.Add(new CatalogViolation(
                        level.Id,
                        "teto de poder",
                        "a arvore concede " + power + " de poder ate o nivel " + level.Rank +
                        ", " + (power - level.MetaPowerCap) + " acima do teto de " + level.MetaPowerCap));
                }
            }

            int lastRank = ladder.Levels.Count;
            foreach (KeyValuePair<string, MetaNodeDefinition> pair in tree.Nodes)
            {
                if (lastRank > 0 && pair.Value.RequiredLevel > lastRank)
                {
                    violations.Add(new CatalogViolation(
                        pair.Key, "nivel exigido", "exige o nivel " + pair.Value.RequiredLevel +
                        ", que nao existe na escada"));
                }
            }

            return violations;
        }
    }
}

using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// O que um no da arvore pode desbloquear. NumericBonus concede poder permanente
    /// ao estado inicial da proxima run — permitido desde que a escada de
    /// dificuldade exista para absorve-lo (design D6, reversao da proibicao
    /// original). O teto por nivel e quem impede virar grind-para-vencer.
    /// </summary>
    public enum UnlockKind
    {
        Card = 0,
        Building = 1,
        Event = 2,
        Race = 3,
        Terrain = 4,
        NumericBonus = 99
    }

    /// <summary>Chaves de bonus numerico reconhecidas por RunBuilder (task 6.1). Um
    /// no de meta usa uma destas como ContentId do Unlock NumericBonus.</summary>
    public static class MetaBonusKeys
    {
        public const string StartingIntegrity = "starting_integrity";

        /// <summary>Bonus permanente num recurso inicial especifico.</summary>
        public static string StartingResource(ResourceKind kind) => "starting_resource_" + kind;
    }

    public sealed class Unlock
    {
        public Unlock(UnlockKind kind, string contentId, int amount = 0)
        {
            Kind = kind;
            ContentId = contentId;
            Amount = amount;
        }

        public UnlockKind Kind { get; }

        public string ContentId { get; }

        /// <summary>Magnitude do bonus, quando Kind == NumericBonus. Ignorado nos
        /// demais tipos, que desbloqueiam conteudo e nao numero.</summary>
        public int Amount { get; }

        public override string ToString() => Kind + ":" + ContentId + (Amount != 0 ? "+" + Amount : string.Empty);
    }

    /// <summary>
    /// No da arvore de meta. Pode pertencer ao tronco geral (RaceBranchId nulo) ou
    /// ao galho de uma raca, caso em que o conteudo so aparece em runs dela.
    /// </summary>
    public sealed class MetaNodeDefinition
    {
        public MetaNodeDefinition(
            string id,
            string displayName,
            int cost,
            IReadOnlyList<Unlock> unlocks,
            IReadOnlyList<string> prerequisiteIds = null,
            string raceBranchId = null,
            string description = null)
        {
            Id = id;
            DisplayName = displayName;
            Cost = cost;
            Unlocks = unlocks ?? new List<Unlock>();
            PrerequisiteIds = prerequisiteIds ?? new List<string>();
            RaceBranchId = raceBranchId;
            Description = description ?? string.Empty;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public int Cost { get; }

        public IReadOnlyList<Unlock> Unlocks { get; }

        public IReadOnlyList<string> PrerequisiteIds { get; }

        /// <summary>Nulo significa tronco geral, valido para todas as racas.</summary>
        public string RaceBranchId { get; }

        public string Description { get; }

        public bool IsRaceBranch => !string.IsNullOrEmpty(RaceBranchId);

        public override string ToString() => Id;
    }
}

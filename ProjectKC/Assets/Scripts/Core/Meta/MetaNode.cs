using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// O que um no da arvore pode desbloquear. NumericBonus existe deliberadamente
    /// como categoria proibida: a validacao de catalogo precisa conseguir descrever
    /// e reprovar o caso, e nao apenas nao te-lo (spec meta-progression).
    /// </summary>
    public enum UnlockKind
    {
        Card = 0,
        Building = 1,
        Event = 2,
        Race = 3,
        Terrain = 4,

        /// <summary>Proibido. Meta desbloqueia conteudo, nunca poder direto.</summary>
        NumericBonus = 99
    }

    public sealed class Unlock
    {
        public Unlock(UnlockKind kind, string contentId)
        {
            Kind = kind;
            ContentId = contentId;
        }

        public UnlockKind Kind { get; }

        public string ContentId { get; }

        public override string ToString() => Kind + ":" + ContentId;
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

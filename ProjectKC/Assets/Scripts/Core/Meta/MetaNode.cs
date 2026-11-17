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

        /// <summary>
        /// Proibido como desbloqueio solto. Poder permanente existe, mas e declarado em
        /// <see cref="MetaBonus"/>, que tem peso e entra na conta do teto por nivel.
        /// </summary>
        NumericBonus = 99
    }

    /// <summary>O que um bonus permanente altera no estado inicial da run.</summary>
    public enum MetaBonusKind
    {
        /// <summary>Soma ao saldo inicial de um recurso.</summary>
        StartingResource = 0,

        /// <summary>Soma a integridade maxima (e inicial) da base.</summary>
        MaxIntegrity = 1
    }

    /// <summary>
    /// Bonus numerico permanente concedido por um no (design D6). Cada bonus tem um
    /// peso em pontos de poder; a soma dos pesos e o que o teto de cada nivel limita.
    /// </summary>
    public sealed class MetaBonus
    {
        private MetaBonus(MetaBonusKind kind, ResourceKind resource, int amount)
        {
            Kind = kind;
            Resource = resource;
            Amount = amount;
        }

        public MetaBonusKind Kind { get; }

        /// <summary>So tem sentido quando Kind e StartingResource.</summary>
        public ResourceKind Resource { get; }

        public int Amount { get; }

        public static MetaBonus Starting(ResourceKind resource, int amount)
        {
            return new MetaBonus(MetaBonusKind.StartingResource, resource, amount);
        }

        public static MetaBonus Integrity(int amount)
        {
            return new MetaBonus(MetaBonusKind.MaxIntegrity, ResourceKind.Gold, amount);
        }

        /// <summary>
        /// Peso do bonus em pontos de poder. A tabela e grosseira de proposito: ela
        /// nao precisa acertar o valor de cada recurso, so impedir que a arvore
        /// cresca sem que a escada cresca junto.
        /// </summary>
        public int Power
        {
            get
            {
                if (Amount <= 0)
                {
                    return 0;
                }

                if (Kind == MetaBonusKind.MaxIntegrity)
                {
                    return Amount * 2;
                }

                switch (Resource)
                {
                    case ResourceKind.Population:
                        return Amount * 5;
                    default:
                        // Arredonda para cima: nenhum bonus positivo pesa zero.
                        return (Amount + 4) / 5;
                }
            }
        }

        public string Describe()
        {
            return Kind == MetaBonusKind.MaxIntegrity
                ? "+" + Amount + " de integridade da base"
                : "+" + Amount + " de " + ResourceKinds.DisplayName(Resource) + " no inicio";
        }

        public override string ToString() => Describe();
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
            string description = null,
            IReadOnlyList<MetaBonus> bonuses = null,
            int requiredLevel = 1)
        {
            Bonuses = bonuses ?? new List<MetaBonus>();
            RequiredLevel = requiredLevel < 1 ? 1 : requiredLevel;
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

        /// <summary>Bonus permanentes que o no concede ao estado inicial da run.</summary>
        public IReadOnlyList<MetaBonus> Bonuses { get; }

        /// <summary>
        /// Nivel de dificuldade que o perfil precisa ter liberado para comprar o no.
        /// E o que amarra o poder a escada: o teto de cada nivel so conta os nos que
        /// ja podem ser comprados nele.
        /// </summary>
        public int RequiredLevel { get; }

        /// <summary>Soma dos pesos dos bonus do no.</summary>
        public int Power
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Bonuses.Count; i++)
                {
                    total += Bonuses[i].Power;
                }

                return total;
            }
        }

        public bool IsRaceBranch => !string.IsNullOrEmpty(RaceBranchId);

        public override string ToString() => Id;
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Os cinco recursos do reino. Populacao entra aqui porque tambem e produzida,
    /// consumida e cobrada como qualquer outro; o que a distingue e ser exigida como
    /// trabalho pelos edificios, e isso vive na alocacao, nao no recurso.
    /// </summary>
    public enum ResourceKind
    {
        Gold = 0,
        Wood = 1,
        Stone = 2,
        Food = 3,
        Population = 4
    }

    /// <summary>
    /// Metadados dos recursos: a lista completa em ordem estavel e o nome de exibicao.
    ///
    /// Nao se chama "Resources" porque esse nome colide com UnityEngine.Resources, e
    /// a camada Game usa os dois namespaces. Um alias por arquivo resolveria hoje e
    /// voltaria a doer em todo arquivo novo.
    /// </summary>
    public static class ResourceKinds
    {
        /// <summary>Todos os tipos, em ordem estavel. Usado por iteracao e por UI.</summary>
        public static readonly ResourceKind[] All =
        {
            ResourceKind.Gold,
            ResourceKind.Wood,
            ResourceKind.Stone,
            ResourceKind.Food,
            ResourceKind.Population
        };

        public const int Count = 5;

        public static string DisplayName(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Gold:
                    return "ouro";
                case ResourceKind.Wood:
                    return "madeira";
                case ResourceKind.Stone:
                    return "pedra";
                case ResourceKind.Food:
                    return "comida";
                case ResourceKind.Population:
                    return "populacao";
                default:
                    return kind.ToString();
            }
        }
    }

    /// <summary>
    /// Um conjunto de quantidades por recurso. Serve de custo, de producao e de
    /// saldo, o que evita um tipo diferente para cada papel e mantem toda soma e
    /// comparacao num lugar so.
    /// </summary>
    public sealed class ResourceAmounts
    {
        private readonly int[] _values = new int[ResourceKinds.Count];

        public ResourceAmounts()
        {
        }

        public ResourceAmounts(ResourceAmounts other)
        {
            if (other != null)
            {
                Array.Copy(other._values, _values, ResourceKinds.Count);
            }
        }

        public static ResourceAmounts Of(ResourceKind kind, int amount)
        {
            ResourceAmounts amounts = new ResourceAmounts();
            amounts[kind] = amount;
            return amounts;
        }

        public static ResourceAmounts Of(params (ResourceKind Kind, int Amount)[] entries)
        {
            ResourceAmounts amounts = new ResourceAmounts();
            if (entries == null)
            {
                return amounts;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                amounts[entries[i].Kind] += entries[i].Amount;
            }

            return amounts;
        }

        public int this[ResourceKind kind]
        {
            get => _values[(int)kind];
            set => _values[(int)kind] = value;
        }

        public bool IsEmpty
        {
            get
            {
                for (int i = 0; i < _values.Length; i++)
                {
                    if (_values[i] != 0)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public void Add(ResourceAmounts other)
        {
            if (other == null)
            {
                return;
            }

            for (int i = 0; i < _values.Length; i++)
            {
                _values[i] += other._values[i];
            }
        }

        public void Clear()
        {
            Array.Clear(_values, 0, _values.Length);
        }

        /// <summary>Recursos com quantidade diferente de zero, em ordem estavel.</summary>
        public IEnumerable<ResourceKind> NonZero()
        {
            for (int i = 0; i < ResourceKinds.All.Length; i++)
            {
                if (_values[(int)ResourceKinds.All[i]] != 0)
                {
                    yield return ResourceKinds.All[i];
                }
            }
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            foreach (ResourceKind kind in NonZero())
            {
                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }

                int value = this[kind];
                builder.Append(value > 0 ? "+" : string.Empty).Append(value).Append(' ')
                    .Append(ResourceKinds.DisplayName(kind));
            }

            return builder.Length == 0 ? "nada" : builder.ToString();
        }
    }

    /// <summary>
    /// O que faltou para pagar um custo. Existe para que a recusa possa dizer qual
    /// recurso travou a acao: "ouro insuficiente" e inutil quando o que falta e pedra.
    /// </summary>
    public readonly struct ResourceShortage
    {
        public ResourceShortage(ResourceKind kind, int missing)
        {
            Kind = kind;
            Missing = missing;
        }

        public ResourceKind Kind { get; }

        public int Missing { get; }

        public bool Any => Missing > 0;

        public static readonly ResourceShortage None = new ResourceShortage(ResourceKind.Gold, 0);

        public override string ToString()
        {
            return Any ? "faltam " + Missing + " de " + ResourceKinds.DisplayName(Kind) : "nada falta";
        }
    }

    /// <summary>
    /// Saldo do reino. Nunca fica negativo: debitar mais do que existe e recusado por
    /// inteiro, e nao aplicado pela metade — meio pagamento deixaria o jogador sem o
    /// recurso e sem o que ele comprou.
    /// </summary>
    public sealed class ResourcePool
    {
        private readonly ResourceAmounts _amounts = new ResourceAmounts();

        public ResourcePool()
        {
        }

        public ResourcePool(ResourceAmounts initial)
        {
            _amounts.Add(initial);
        }

        public int this[ResourceKind kind] => _amounts[kind];

        public ResourceAmounts Snapshot() => new ResourceAmounts(_amounts);

        public void Add(ResourceKind kind, int amount)
        {
            if (amount > 0)
            {
                _amounts[kind] += amount;
            }
        }

        public void Add(ResourceAmounts amounts)
        {
            if (amounts == null)
            {
                return;
            }

            foreach (ResourceKind kind in amounts.NonZero())
            {
                int value = amounts[kind];
                if (value > 0)
                {
                    Add(kind, value);
                }
                else
                {
                    Remove(kind, -value);
                }
            }
        }

        /// <summary>Retira o que puder, sem deixar saldo negativo. Devolve quanto saiu.</summary>
        public int Remove(ResourceKind kind, int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            int removed = Math.Min(amount, _amounts[kind]);
            _amounts[kind] -= removed;
            return removed;
        }

        public bool Has(ResourceKind kind, int amount) => _amounts[kind] >= amount;

        /// <summary>
        /// Se o custo cabe no saldo. Reporta o primeiro recurso que falta, em ordem
        /// estavel, para que a mesma situacao produza sempre a mesma mensagem.
        /// </summary>
        public bool CanAfford(ResourceAmounts cost, out ResourceShortage shortage)
        {
            shortage = ResourceShortage.None;

            if (cost == null)
            {
                return true;
            }

            for (int i = 0; i < ResourceKinds.All.Length; i++)
            {
                ResourceKind kind = ResourceKinds.All[i];
                int needed = cost[kind];

                if (needed > 0 && _amounts[kind] < needed)
                {
                    shortage = new ResourceShortage(kind, needed - _amounts[kind]);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Paga o custo inteiro ou nao paga nada. Debitar parcialmente deixaria o
        /// jogador sem o recurso e sem o que ele tentou comprar.
        /// </summary>
        public bool TrySpend(ResourceAmounts cost, out ResourceShortage shortage)
        {
            if (!CanAfford(cost, out shortage))
            {
                return false;
            }

            if (cost == null)
            {
                return true;
            }

            for (int i = 0; i < ResourceKinds.All.Length; i++)
            {
                ResourceKind kind = ResourceKinds.All[i];
                int needed = cost[kind];
                if (needed > 0)
                {
                    _amounts[kind] -= needed;
                }
            }

            return true;
        }

        public void Set(ResourceKind kind, int amount)
        {
            _amounts[kind] = Math.Max(0, amount);
        }

        public override string ToString() => _amounts.ToString();
    }
}

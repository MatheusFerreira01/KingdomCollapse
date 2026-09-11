using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// O que a carta exige como alvo. Determina quais celulas a UI realca e o que
    /// conta como alvo invalido.
    /// </summary>
    public enum TargetRequirement
    {
        /// <summary>Nao precisa de alvo. Resolve direto.</summary>
        None = 0,

        /// <summary>Qualquer celula possuida.</summary>
        OwnedTile,

        /// <summary>Celula possuida, sem edificio e nao arrasada.</summary>
        BuildableTile,

        /// <summary>Celula possuida que tem edificio.</summary>
        TileWithBuilding,

        /// <summary>Celula arrasada, para reparo.</summary>
        DestroyedTile,

        /// <summary>Celula nao possuida, adjacente ao territorio.</summary>
        PurchasableTile,

        /// <summary>Qualquer celula conhecida do mapa, possuida ou nao.</summary>
        AnyTile
    }

    /// <summary>
    /// Definicao pura de uma carta. O ScriptableObject da camada Unity converte para
    /// este tipo; assim o Core nao conhece o Unity e os testes montam cartas na mao.
    /// </summary>
    public sealed class CardDefinition
    {
        public CardDefinition(
            string id,
            string displayName,
            int energyCost,
            TargetRequirement target,
            IReadOnlyList<IEffect> effects,
            bool retained = false,
            IReadOnlyList<string> allowedRaceIds = null,
            string rulesText = null)
        {
            Id = id;
            DisplayName = displayName;
            EnergyCost = energyCost;
            Target = target;
            Effects = effects ?? new List<IEffect>();
            Retained = retained;
            AllowedRaceIds = allowedRaceIds ?? new List<string>();
            RulesText = rulesText ?? string.Empty;
        }

        public string Id { get; }

        public string DisplayName { get; }

        /// <summary>Texto de regra para exibir na carta. A UI le daqui em vez de ir
        /// direto no asset, para nao precisar conhecer o Unity.</summary>
        public string RulesText { get; }

        public int EnergyCost { get; }

        public TargetRequirement Target { get; }

        public IReadOnlyList<IEffect> Effects { get; }

        /// <summary>Carta retida nao vai para o descarte no Fim do Dia.</summary>
        public bool Retained { get; }

        /// <summary>Vazio significa disponivel para qualquer raca.</summary>
        public IReadOnlyList<string> AllowedRaceIds { get; }

        public bool IsAllowedFor(string raceId)
        {
            if (AllowedRaceIds.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < AllowedRaceIds.Count; i++)
            {
                if (AllowedRaceIds[i] == raceId)
                {
                    return true;
                }
            }

            return false;
        }

        public bool NeedsTarget => Target != TargetRequirement.None;

        public override string ToString() => Id;
    }

    public static class TargetRules
    {
        /// <summary>
        /// Se a celula satisfaz o requisito. Alvo invalido nunca gasta energia: a
        /// validacao acontece antes de qualquer efeito ser aplicado.
        /// </summary>
        public static bool IsValidTarget(KingdomGrid grid, TargetRequirement requirement, Coord coord, RuleModifiers rules = null)
        {
            Tile tile = grid.TileAt(coord);

            switch (requirement)
            {
                case TargetRequirement.None:
                    return false;
                case TargetRequirement.OwnedTile:
                    return tile.Owned;
                case TargetRequirement.BuildableTile:
                    return tile.IsBuildable;
                case TargetRequirement.TileWithBuilding:
                    return tile.Owned && tile.HasBuilding;
                case TargetRequirement.DestroyedTile:
                    return tile.Owned && tile.Destroyed;
                case TargetRequirement.PurchasableTile:
                    return grid.CanPurchase(coord, rules).Ok;
                case TargetRequirement.AnyTile:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Todos os alvos validos, em ordem estavel. A UI usa para realcar.</summary>
        public static List<Coord> ValidTargets(KingdomGrid grid, TargetRequirement requirement, RuleModifiers rules = null)
        {
            List<Coord> targets = new List<Coord>();

            if (requirement == TargetRequirement.None)
            {
                return targets;
            }

            if (requirement == TargetRequirement.PurchasableTile)
            {
                return grid.PurchasableCoords(rules);
            }

            foreach (Tile tile in grid.OwnedTilesOrdered())
            {
                if (IsValidTarget(grid, requirement, tile.Coord, rules))
                {
                    targets.Add(tile.Coord);
                }
            }

            if (requirement == TargetRequirement.AnyTile)
            {
                foreach (Coord coord in grid.PurchasableCoords(rules))
                {
                    targets.Add(coord);
                }
            }

            return targets;
        }
    }
}

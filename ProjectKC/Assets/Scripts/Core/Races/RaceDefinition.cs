using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Uma raca jogavel. Alem dos valores iniciais, carrega listas de conteudo
    /// proibido e os modificadores de regra que os sistemas consultam (design D8).
    /// </summary>
    public sealed class RaceDefinition
    {
        public RaceDefinition(
            string id,
            string displayName,
            int startingGold,
            int startingIntegrity,
            int handSize,
            int energyPerDay,
            IReadOnlyList<CardDefinition> startingCards = null,
            RuleModifiers rules = null,
            IReadOnlyList<string> forbiddenCardIds = null,
            IReadOnlyList<string> forbiddenBuildingIds = null,
            IReadOnlyList<string> forbiddenEventIds = null,
            ResourceAmounts startingResources = null)
        {
            Id = id;
            DisplayName = displayName;
            StartingGold = startingGold;
            StartingIntegrity = startingIntegrity;

            // O ouro inicial continua sendo declarado a parte por conveniencia de
            // autoria; ele e dobrado no conjunto de recursos, que e a fonte de verdade.
            StartingResources = new ResourceAmounts(startingResources);
            if (StartingResources[ResourceKind.Gold] == 0)
            {
                StartingResources[ResourceKind.Gold] = startingGold;
            }

            HandSize = handSize;
            EnergyPerDay = energyPerDay;
            StartingCards = startingCards ?? new List<CardDefinition>();
            Rules = rules ?? RuleModifiers.None;
            ForbiddenCardIds = forbiddenCardIds ?? new List<string>();
            ForbiddenBuildingIds = forbiddenBuildingIds ?? new List<string>();
            ForbiddenEventIds = forbiddenEventIds ?? new List<string>();
        }

        public string Id { get; }

        public string DisplayName { get; }

        public int StartingGold { get; }

        /// <summary>Saldo com que a run comeca, em todos os recursos.</summary>
        public ResourceAmounts StartingResources { get; }

        public int StartingIntegrity { get; }

        public int HandSize { get; }

        public int EnergyPerDay { get; }

        public IReadOnlyList<CardDefinition> StartingCards { get; }

        public RuleModifiers Rules { get; }

        public IReadOnlyList<string> ForbiddenCardIds { get; }

        public IReadOnlyList<string> ForbiddenBuildingIds { get; }

        public IReadOnlyList<string> ForbiddenEventIds { get; }

        public bool AllowsCard(CardDefinition card)
        {
            return card != null && card.IsAllowedFor(Id) && !Contains(ForbiddenCardIds, card.Id);
        }

        public bool AllowsBuilding(BuildingDefinition building)
        {
            return building != null && !Contains(ForbiddenBuildingIds, building.Id);
        }

        public bool AllowsEvent(EventDefinition definition)
        {
            return definition != null && !Contains(ForbiddenEventIds, definition.Id);
        }

        private static bool Contains(IReadOnlyList<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == value)
                {
                    return true;
                }
            }

            return false;
        }

        public override string ToString() => Id;
    }
}

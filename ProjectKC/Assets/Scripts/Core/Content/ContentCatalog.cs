using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Todo o conteudo existente do jogo, indexado por id, mais o conjunto que ja
    /// nasce desbloqueado. A meta-progressao soma a este conjunto inicial; nada
    /// fora dele entra numa run sem no comprado (spec run-loop).
    /// </summary>
    public sealed class ContentCatalog
    {
        private readonly Dictionary<string, CardDefinition> _cards = new Dictionary<string, CardDefinition>();
        private readonly Dictionary<string, BuildingDefinition> _buildings = new Dictionary<string, BuildingDefinition>();
        private readonly Dictionary<string, EventDefinition> _events = new Dictionary<string, EventDefinition>();
        private readonly Dictionary<string, RaceDefinition> _races = new Dictionary<string, RaceDefinition>();

        private readonly HashSet<string> _startingCards = new HashSet<string>();
        private readonly HashSet<string> _startingBuildings = new HashSet<string>();
        private readonly HashSet<string> _startingEvents = new HashSet<string>();
        private readonly HashSet<string> _startingRaces = new HashSet<string>();

        public IReadOnlyDictionary<string, CardDefinition> Cards => _cards;

        public IReadOnlyDictionary<string, BuildingDefinition> Buildings => _buildings;

        public IReadOnlyDictionary<string, EventDefinition> Events => _events;

        public IReadOnlyDictionary<string, RaceDefinition> Races => _races;

        public ContentCatalog AddCard(CardDefinition card, bool unlockedFromStart = false)
        {
            if (card != null)
            {
                _cards[card.Id] = card;
                if (unlockedFromStart)
                {
                    _startingCards.Add(card.Id);
                }
            }

            return this;
        }

        public ContentCatalog AddBuilding(BuildingDefinition building, bool unlockedFromStart = false)
        {
            if (building != null)
            {
                _buildings[building.Id] = building;
                if (unlockedFromStart)
                {
                    _startingBuildings.Add(building.Id);
                }
            }

            return this;
        }

        public ContentCatalog AddEvent(EventDefinition definition, bool unlockedFromStart = false)
        {
            if (definition != null)
            {
                _events[definition.Id] = definition;
                if (unlockedFromStart)
                {
                    _startingEvents.Add(definition.Id);
                }
            }

            return this;
        }

        public ContentCatalog AddRace(RaceDefinition race, bool unlockedFromStart = false)
        {
            if (race != null)
            {
                _races[race.Id] = race;
                if (unlockedFromStart)
                {
                    _startingRaces.Add(race.Id);
                }
            }

            return this;
        }

        public IReadOnlyCollection<string> StartingCards => _startingCards;

        public IReadOnlyCollection<string> StartingBuildings => _startingBuildings;

        public IReadOnlyCollection<string> StartingEvents => _startingEvents;

        public IReadOnlyCollection<string> StartingRaces => _startingRaces;

        /// <summary>Ids liberados de um tipo: os iniciais mais os desbloqueados na meta.</summary>
        public HashSet<string> AvailableIds(UnlockKind kind, MetaTree tree, MetaProfile profile, string raceId)
        {
            HashSet<string> available = new HashSet<string>(StartingSetFor(kind));

            if (tree != null && profile != null)
            {
                foreach (string id in tree.UnlockedContent(profile, kind, raceId))
                {
                    available.Add(id);
                }
            }

            return available;
        }

        private IReadOnlyCollection<string> StartingSetFor(UnlockKind kind)
        {
            switch (kind)
            {
                case UnlockKind.Card:
                    return _startingCards;
                case UnlockKind.Building:
                    return _startingBuildings;
                case UnlockKind.Event:
                    return _startingEvents;
                case UnlockKind.Race:
                    return _startingRaces;
                default:
                    return new HashSet<string>();
            }
        }

        /// <summary>Racas selecionaveis: as iniciais mais as desbloqueadas por no.</summary>
        public List<RaceDefinition> SelectableRaces(MetaTree tree, MetaProfile profile)
        {
            HashSet<string> allowed = new HashSet<string>(_startingRaces);
            if (tree != null && profile != null)
            {
                foreach (string raceId in tree.UnlockedRaces(profile))
                {
                    allowed.Add(raceId);
                }
            }

            List<RaceDefinition> races = new List<RaceDefinition>();
            foreach (KeyValuePair<string, RaceDefinition> pair in _races)
            {
                if (allowed.Contains(pair.Key))
                {
                    races.Add(pair.Value);
                }
            }

            races.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return races;
        }

        public bool IsRaceUnlocked(string raceId, MetaTree tree, MetaProfile profile)
        {
            if (_startingRaces.Contains(raceId))
            {
                return true;
            }

            return tree != null && profile != null && tree.UnlockedRaces(profile).Contains(raceId);
        }
    }
}

using System;
using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>Parametros de uma nova run.</summary>
    public sealed class RunSetup
    {
        public RunSetup(string raceId, int seed)
        {
            RaceId = raceId;
            Seed = seed;
        }

        public string RaceId { get; }

        public int Seed { get; }

        public TileCostCurve CostCurve { get; set; }

        public ThreatCurve ThreatCurve { get; set; }

        public EventClassWeights EventWeights { get; set; }

        public int FirstThreatDay { get; set; } = 4;

        public int ThreatIntervalDays { get; set; } = 4;

        public int ThreatLeadDays { get; set; } = 3;
    }

    public sealed class RunBundle
    {
        public RunBundle(RunState run, RunEngine engine, ContentCatalog catalog, HashSet<string> availableBuildingIds)
        {
            Run = run;
            Engine = engine;
            Catalog = catalog;
            AvailableBuildingIds = availableBuildingIds;
        }

        public RunState Run { get; }

        public RunEngine Engine { get; }

        public ContentCatalog Catalog { get; }

        /// <summary>Edificios que a UI pode oferecer nesta run.</summary>
        public HashSet<string> AvailableBuildingIds { get; }

        public List<BuildingDefinition> AvailableBuildings()
        {
            List<BuildingDefinition> buildings = new List<BuildingDefinition>();
            foreach (string id in AvailableBuildingIds)
            {
                if (Catalog.Buildings.TryGetValue(id, out BuildingDefinition building))
                {
                    buildings.Add(building);
                }
            }

            buildings.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return buildings;
        }
    }

    /// <summary>
    /// Monta uma run a partir da raca escolhida e do que a meta-progressao liberou.
    /// Conteudo bloqueado ou proibido pela raca nunca entra em nenhum pool: o filtro
    /// acontece uma vez, aqui, e nao espalhado por cada sistema.
    /// </summary>
    public static class RunBuilder
    {
        public const string HallBuildingId = "hall";

        public static RunBundle Build(
            RunSetup setup,
            ContentCatalog catalog,
            MetaTree tree = null,
            MetaProfile profile = null)
        {
            if (!catalog.Races.TryGetValue(setup.RaceId, out RaceDefinition race))
            {
                throw new ArgumentException("raca desconhecida: " + setup.RaceId);
            }

            if (!catalog.IsRaceUnlocked(setup.RaceId, tree, profile))
            {
                throw new InvalidOperationException("raca bloqueada: " + setup.RaceId);
            }

            RunRandom random = new RunRandom(setup.Seed);

            SeededTerrainGenerator terrain = new SeededTerrainGenerator(random, Coord.Zero);
            KingdomGrid grid = new KingdomGrid(terrain, Coord.Zero, setup.CostCurve);
            grid.PlaceHall(ResolveHall(catalog));

            RunDeck deck = new RunDeck(ResolveStartingDeck(race, catalog, tree, profile));
            deck.Shuffle(random.Channel(RandomChannel.Cards));

            ThreatClock threats = new ThreatClock(
                setup.ThreatCurve,
                setup.FirstThreatDay,
                setup.ThreatIntervalDays,
                setup.ThreatLeadDays);

            RunState run = new RunState(race, grid, deck, random, threats);

            EventPool pool = new EventPool(
                ResolveEvents(race, catalog, tree, profile), setup.EventWeights);

            RunEngine engine = new RunEngine(run, pool);

            HashSet<string> buildings = ResolveBuildingIds(race, catalog, tree, profile);

            return new RunBundle(run, engine, catalog, buildings);
        }

        private static BuildingDefinition ResolveHall(ContentCatalog catalog)
        {
            if (catalog.Buildings.TryGetValue(HallBuildingId, out BuildingDefinition hall))
            {
                return hall;
            }

            // Fallback para que uma run possa ser montada mesmo com catalogo minimo,
            // como acontece nos testes de regra.
            return new BuildingDefinition(
                HallBuildingId, "Salao do Reino", new List<TerrainType>(), 0, 2, 2);
        }

        private static List<CardDefinition> ResolveStartingDeck(
            RaceDefinition race, ContentCatalog catalog, MetaTree tree, MetaProfile profile)
        {
            HashSet<string> available = catalog.AvailableIds(UnlockKind.Card, tree, profile, race.Id);
            List<CardDefinition> deck = new List<CardDefinition>();

            // As cartas iniciais da raca definem a identidade dela, entao entram
            // sempre; o desbloqueio governa o que aparece alem disso.
            for (int i = 0; i < race.StartingCards.Count; i++)
            {
                CardDefinition card = race.StartingCards[i];
                if (race.AllowsCard(card))
                {
                    deck.Add(card);
                }
            }

            foreach (string cardId in available)
            {
                if (!catalog.Cards.TryGetValue(cardId, out CardDefinition card))
                {
                    continue;
                }

                if (!race.AllowsCard(card) || Contains(deck, card.Id))
                {
                    continue;
                }

                deck.Add(card);
            }

            return deck;
        }

        private static bool Contains(List<CardDefinition> deck, string cardId)
        {
            for (int i = 0; i < deck.Count; i++)
            {
                if (deck[i].Id == cardId)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<EventDefinition> ResolveEvents(
            RaceDefinition race, ContentCatalog catalog, MetaTree tree, MetaProfile profile)
        {
            HashSet<string> available = catalog.AvailableIds(UnlockKind.Event, tree, profile, race.Id);
            List<EventDefinition> events = new List<EventDefinition>();

            foreach (string eventId in available)
            {
                if (!catalog.Events.TryGetValue(eventId, out EventDefinition definition))
                {
                    continue;
                }

                if (race.AllowsEvent(definition))
                {
                    events.Add(definition);
                }
            }

            events.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return events;
        }

        private static HashSet<string> ResolveBuildingIds(
            RaceDefinition race, ContentCatalog catalog, MetaTree tree, MetaProfile profile)
        {
            HashSet<string> available = catalog.AvailableIds(UnlockKind.Building, tree, profile, race.Id);
            HashSet<string> allowed = new HashSet<string>();

            foreach (string buildingId in available)
            {
                if (!catalog.Buildings.TryGetValue(buildingId, out BuildingDefinition building))
                {
                    continue;
                }

                if (race.AllowsBuilding(building))
                {
                    allowed.Add(buildingId);
                }
            }

            return allowed;
        }
    }
}

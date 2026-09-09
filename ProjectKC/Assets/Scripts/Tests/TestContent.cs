using System.Collections.Generic;
using KingdomCollapse.Core;

namespace KingdomCollapse.Tests
{
    /// <summary>
    /// Conteudo minimo compartilhado pelos testes. Deliberadamente separado do
    /// conteudo real do jogo: os testes verificam regras, e nao devem quebrar toda
    /// vez que o balanceamento mudar um numero num ScriptableObject.
    /// </summary>
    internal static class TestContent
    {
        public static BuildingDefinition Hall()
        {
            return new BuildingDefinition("hall", "Salao do Reino", new List<TerrainType>(), 0, 2, 1);
        }

        public static BuildingDefinition Farm()
        {
            return new BuildingDefinition(
                "farm",
                "Fazenda",
                new List<TerrainType> { TerrainType.Plain },
                20,
                3,
                0);
        }

        /// <summary>Serraria: exige floresta e ganha por floresta adjacente.</summary>
        public static BuildingDefinition Sawmill()
        {
            return new BuildingDefinition(
                "sawmill",
                "Serraria",
                new List<TerrainType> { TerrainType.Forest },
                25,
                2,
                0,
                new List<AdjacencyBonus> { AdjacencyBonus.ForTerrain(TerrainType.Forest, 2) });
        }

        public static BuildingDefinition Watchtower()
        {
            return new BuildingDefinition("watchtower", "Torre", new List<TerrainType>(), 30, 0, 5);
        }

        /// <summary>Grid com o Salao ja posicionado na origem e terreno controlado.</summary>
        public static KingdomGrid GridWithHall(
            TerrainType fallback = TerrainType.Plain,
            Dictionary<Coord, TerrainType> explicitTerrain = null,
            TileCostCurve costCurve = null)
        {
            FixedTerrainGenerator generator = new FixedTerrainGenerator(fallback, explicitTerrain);
            KingdomGrid grid = new KingdomGrid(generator, Coord.Zero, costCurve);
            grid.PlaceHall(Hall());
            return grid;
        }

        /// <summary>Compra celulas em sequencia, ignorando ouro. Atalho de arranjo de teste.</summary>
        public static void GrantTiles(KingdomGrid grid, params Coord[] coords)
        {
            for (int i = 0; i < coords.Length; i++)
            {
                grid.Grant(coords[i]);
            }
        }

        // --- Cartas ---

        public static CardDefinition Card(
            string id,
            int energyCost,
            IEffect effect,
            TargetRequirement target = TargetRequirement.None,
            bool retained = false)
        {
            return new CardDefinition(
                id,
                id,
                energyCost,
                target,
                new List<IEffect> { effect },
                retained);
        }

        public static CardDefinition GoldCard(string id = "gold", int energyCost = 1, int gold = 10)
        {
            return Card(id, energyCost, new GainGoldEffect(gold));
        }

        /// <summary>Cartas numeradas, para exercitar compra e reciclagem do deck.</summary>
        public static List<CardDefinition> Filler(int count, string prefix = "filler")
        {
            List<CardDefinition> cards = new List<CardDefinition>();
            for (int i = 0; i < count; i++)
            {
                cards.Add(GoldCard(prefix + i, 1, 1));
            }

            return cards;
        }

        // --- Racas ---

        public static RaceDefinition Humans(
            int gold = 50,
            int integrity = 20,
            int handSize = 5,
            int energy = 3,
            IReadOnlyList<CardDefinition> startingCards = null,
            RuleModifiers rules = null)
        {
            return new RaceDefinition(
                "humans", "Humanos", gold, integrity, handSize, energy, startingCards, rules);
        }

        public static RaceDefinition WithRules(string id, IDictionary<string, double> rules)
        {
            return new RaceDefinition(
                id,
                id,
                50,
                20,
                5,
                3,
                new List<CardDefinition>(),
                new RuleModifiers(new Dictionary<string, double>(rules)));
        }

        // --- Runs completas ---

        /// <summary>
        /// Catalogo minimo com Salao, alguns edificios e a raca base, tudo liberado
        /// desde o inicio. Os testes que verificam bloqueio montam o proprio catalogo.
        /// </summary>
        public static ContentCatalog Catalog(RaceDefinition race = null, params EventDefinition[] events)
        {
            ContentCatalog catalog = new ContentCatalog();
            catalog.AddBuilding(Hall(), true);
            catalog.AddBuilding(Farm(), true);
            catalog.AddBuilding(Sawmill(), true);
            catalog.AddBuilding(Watchtower(), true);
            catalog.AddRace(race ?? Humans(), true);

            for (int i = 0; i < events.Length; i++)
            {
                catalog.AddEvent(events[i], true);
            }

            return catalog;
        }

        /// <summary>
        /// Run pronta para jogar. Por padrao sem eventos e sem ameacas proximas, para
        /// que cada teste introduza apenas a pressao que pretende medir.
        /// </summary>
        public static RunBundle Run(
            RaceDefinition race = null,
            ContentCatalog catalog = null,
            int seed = 1234,
            int firstThreatDay = 999,
            EventClassWeights weights = null)
        {
            RaceDefinition chosen = race ?? Humans();
            ContentCatalog content = catalog ?? Catalog(chosen);

            RunSetup setup = new RunSetup(chosen.Id, seed)
            {
                FirstThreatDay = firstThreatDay,
                EventWeights = weights
            };

            RunBundle bundle = RunBuilder.Build(setup, content);
            bundle.Engine.StartRun();
            return bundle;
        }
    }
}

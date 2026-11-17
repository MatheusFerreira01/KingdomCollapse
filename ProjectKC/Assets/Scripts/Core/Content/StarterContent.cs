using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// O conteudo do jogo, escrito em codigo: edificios, cartas, eventos, climas, a
    /// raca inicial, os rivais, a escada de dificuldade e a arvore de meta.
    ///
    /// Vive no Core, e nao em assets, por um motivo pratico: e o unico jeito de o
    /// MESMO conteudo ser jogado no Unity, simulado em lote fora dele e coberto por
    /// teste. Enquanto o conteudo morava so em ScriptableObjects, o balanceamento
    /// media um catalogo de teste e o jogador jogava outro.
    ///
    /// O GameDatabase continua existindo como fonte alternativa (ver GameBootstrap):
    /// quem quiser autorar no Inspector troca a origem la.
    /// </summary>
    public static class StarterContent
    {
        public const string HumansId = "humans";

        // --- Edificios ---

        public const string Hall = RunBuilder.HallBuildingId;
        public const string Farm = "farm";
        public const string Sawmill = "sawmill";
        public const string Quarry = "mine";
        public const string Docks = "docks";
        public const string House = "house";
        public const string Outpost = "outpost";
        public const string Watchtower = "watchtower";
        public const string Wall = "wall";
        public const string Market = "market";
        public const string Barracks = "barracks";

        private static readonly TerrainType[] AnyDry =
        {
            TerrainType.Plain, TerrainType.Forest, TerrainType.Mine, TerrainType.Ruin
        };

        public static List<BuildingDefinition> Buildings()
        {
            return new List<BuildingDefinition>
            {
                // O Salao abriga a primeira gente e rende um fio de ouro: o reino
                // nunca fica completamente parado, mas tambem nao vive dele.
                new BuildingDefinition(
                    Hall, "Salão do Reino", new List<TerrainType>(), 0, 0, 2,
                    production: ResourceAmounts.Of(ResourceKind.Gold, 3),
                    populationCapacity: 6),

                new BuildingDefinition(
                    Farm, "Fazenda", new List<TerrainType> { TerrainType.Plain }, 0, 0, 0,
                    new List<AdjacencyBonus> { AdjacencyBonus.ForTerrain(TerrainType.River, 1, ResourceKind.Food) },
                    cost: ResourceAmounts.Of((ResourceKind.Gold, 10), (ResourceKind.Wood, 6)),
                    production: ResourceAmounts.Of(ResourceKind.Food, 3),
                    workersRequired: 1),

                new BuildingDefinition(
                    Sawmill, "Serraria", new List<TerrainType> { TerrainType.Forest }, 0, 0, 0,
                    new List<AdjacencyBonus> { AdjacencyBonus.ForTerrain(TerrainType.Forest, 1, ResourceKind.Wood) },
                    cost: ResourceAmounts.Of(ResourceKind.Gold, 12),
                    production: ResourceAmounts.Of(ResourceKind.Wood, 3),
                    workersRequired: 1),

                new BuildingDefinition(
                    Quarry, "Pedreira", new List<TerrainType> { TerrainType.Mine }, 0, 0, 0,
                    new List<AdjacencyBonus> { AdjacencyBonus.ForTerrain(TerrainType.Mine, 1, ResourceKind.Stone) },
                    cost: ResourceAmounts.Of((ResourceKind.Gold, 12), (ResourceKind.Wood, 8)),
                    production: ResourceAmounts.Of((ResourceKind.Stone, 3), (ResourceKind.Gold, 1)),
                    workersRequired: 1),

                new BuildingDefinition(
                    Docks, "Ancoradouro", new List<TerrainType> { TerrainType.River }, 0, 0, 0,
                    new List<AdjacencyBonus> { AdjacencyBonus.ForTerrain(TerrainType.River, 1, ResourceKind.Gold) },
                    cost: ResourceAmounts.Of((ResourceKind.Gold, 10), (ResourceKind.Wood, 8)),
                    production: ResourceAmounts.Of((ResourceKind.Food, 2), (ResourceKind.Gold, 2)),
                    workersRequired: 1),

                // Casas nao produzem: sao o teto de gente, e gente e o que opera tudo.
                new BuildingDefinition(
                    House, "Casas", AnyDry, 0, 0, 0,
                    cost: ResourceAmounts.Of((ResourceKind.Gold, 6), (ResourceKind.Wood, 10)),
                    populationCapacity: 4),

                new BuildingDefinition(
                    Outpost, "Paliçada", AnyDry, 0, 0, 4,
                    cost: ResourceAmounts.Of(ResourceKind.Wood, 12),
                    workersRequired: 1),

                new BuildingDefinition(
                    Watchtower, "Torre de Vigia", AnyDry, 0, 0, 9,
                    cost: ResourceAmounts.Of((ResourceKind.Wood, 8), (ResourceKind.Stone, 12)),
                    workersRequired: 1),

                // Muralha defende sem guarnicao: e para onde vai a pedra de quem nao
                // tem gente sobrando.
                new BuildingDefinition(
                    Wall, "Muralha", AnyDry, 0, 0, 3,
                    cost: ResourceAmounts.Of(ResourceKind.Stone, 14)),

                new BuildingDefinition(
                    Market, "Mercado", new List<TerrainType> { TerrainType.Plain, TerrainType.Ruin }, 0, 0, 0,
                    new List<AdjacencyBonus> { AdjacencyBonus.ForBuilding(House, 1, ResourceKind.Gold) },
                    cost: ResourceAmounts.Of((ResourceKind.Gold, 18), (ResourceKind.Wood, 10), (ResourceKind.Stone, 6)),
                    production: ResourceAmounts.Of(ResourceKind.Gold, 6),
                    workersRequired: 1),

                new BuildingDefinition(
                    Barracks, "Quartel", AnyDry, 0, 0, 16,
                    cost: ResourceAmounts.Of((ResourceKind.Gold, 20), (ResourceKind.Wood, 14), (ResourceKind.Stone, 14)),
                    workersRequired: 2)
            };
        }

        /// <summary>Edificios que exigem um no da arvore de meta para aparecer.</summary>
        private static readonly HashSet<string> LockedBuildings = new HashSet<string> { Market, Barracks };

        // --- Cartas ---

        public const string CardShieldWall = "card_shieldwall";
        public const string CardFeast = "card_feast";
        public const string CardMasterBuilder = "card_masterbuilder";

        private static readonly HashSet<string> LockedCards =
            new HashSet<string>
            {
                CardShieldWall, CardFeast, CardMasterBuilder, CardPillage, CardForestSong,
                CardReserves, CardCouncil, CardLastStand, CardWarCry, CardBooty, CardThorns, CardSap,
                CardFair, CardCaravan, CardEngineers, CardDwarfSteel, CardVault, CardPicks
            };

        public const string CardDwarfSteel = "card_dwarfsteel";
        public const string CardVault = "card_vault";
        public const string CardPicks = "card_picks";

        public const string CardFair = "card_fair";
        public const string CardCaravan = "card_caravan";
        public const string CardEngineers = "card_engineers";

        public const string CardReserves = "card_reserves";
        public const string CardCouncil = "card_council";
        public const string CardLastStand = "card_laststand";
        public const string CardWarCry = "card_warcry";
        public const string CardBooty = "card_booty";
        public const string CardThorns = "card_thorns";
        public const string CardSap = "card_sap";

        public const string CardPillage = "card_pillage";
        public const string CardForestSong = "card_forest_song";

        public static List<CardDefinition> Cards()
        {
            return new List<CardDefinition>
            {
                Card("card_harvest", "Colheita", 1, new GainResourceEffect(ResourceKind.Food, 8)),
                Card("card_levy", "Arrecadação", 1, new GainResourceEffect(ResourceKind.Gold, 3, GoldScaling.PerOwnedTile)),
                Card("card_lumber", "Lenha", 1, new GainResourceEffect(ResourceKind.Wood, 8)),
                Card("card_stonework", "Cantaria", 1, new GainResourceEffect(ResourceKind.Stone, 6)),
                Card("card_rally", "Convocação", 1, new AddDefenseEffect(6)),
                Card("card_barricade", "Barricada", 2, new AddDefenseEffect(14)),
                Card("card_recruit", "Milícia", 0, new AddDefenseEffect(3)),
                Card("card_settlers", "Colonos", 2, new RecruitEffect(2)),
                Card("card_scouts", "Batedores", 0, new RevealTileEffect(3)),
                new CardDefinition("card_groundwork", "Terraplanagem", 1, TargetRequirement.BuildableTile,
                    new List<IEffect> { new SetTerrainEffect(TerrainType.Plain, TerrainType.Plain, false) }),
                new CardDefinition("card_rebuild", "Reconstruir", 1, TargetRequirement.DestroyedTile,
                    new List<IEffect> { new RepairTileEffect() }),
                new CardDefinition("card_claim", "Reivindicar", 2, TargetRequirement.PurchasableTile,
                    new List<IEffect> { new GrantTileEffect() }),
                Card("card_overtime", "Mutirão", 1, new ModifyProductionEffect(0.5, 1, true)),
                Card("card_inspire", "Inspirar", 1, new DrawCardsEffect(2)),
                Card("card_emergency", "Obras de Emergência", 2, new RepairBaseEffect(5)),
                Card("card_fortify", "Fortificar", 2, new AddDefenseEffect(8), new RepairBaseEffect(1)),

                // --- Liberadas pela arvore de meta ---
                Card(CardShieldWall, "Muralha de Escudos", 1, new AddDefenseEffect(10)),
                Card(CardFeast, "Banquete", 2, new GainResourceEffect(ResourceKind.Food, 12), new DrawCardsEffect(1)),
                Card(CardMasterBuilder, "Mestre de Obras", 1,
                    new GainResourceEffect(ResourceKind.Wood, 6), new GainResourceEffect(ResourceKind.Stone, 6)),
                Card(CardPillage, "Pilhagem", 1,
                    new GainResourceEffect(ResourceKind.Food, 6), new GainResourceEffect(ResourceKind.Gold, 5)),
                Card(CardForestSong, "Canto da Floresta", 1,
                    new GainResourceEffect(ResourceKind.Wood, 6), new GainResourceEffect(ResourceKind.Food, 5)),
                Card(CardReserves, "Reservas", 1,
                    new GainResourceEffect(ResourceKind.Gold, 5), new GainResourceEffect(ResourceKind.Wood, 5),
                    new GainResourceEffect(ResourceKind.Food, 5)),
                Card(CardCouncil, "Conselho", 1, new DrawCardsEffect(3)),
                Card(CardLastStand, "Última Linha", 3, new AddDefenseEffect(22)),
                Card(CardWarCry, "Grito de Guerra", 1, new AddDefenseEffect(9), new DrawCardsEffect(1)),
                Card(CardBooty, "Butim", 1, new GainResourceEffect(ResourceKind.Gold, 10)),
                Card(CardThorns, "Barreira de Espinhos", 1, new AddDefenseEffect(9), new RepairBaseEffect(1)),
                Card(CardSap, "Bênção da Seiva", 1, new GainResourceEffect(ResourceKind.Food, 8), new DrawCardsEffect(1)),
                Card(CardFair, "Feira Livre", 1, new GainResourceEffect(ResourceKind.Gold, 7), new DrawCardsEffect(1)),
                Card(CardCaravan, "Caravana", 1,
                    new GainResourceEffect(ResourceKind.Gold, 4), new GainResourceEffect(ResourceKind.Wood, 4),
                    new GainResourceEffect(ResourceKind.Stone, 4)),
                Card(CardEngineers, "Engenheiros", 1,
                    new GainResourceEffect(ResourceKind.Wood, 6), new RepairBaseEffect(2)),
                Card(CardDwarfSteel, "Aço Anão", 1, new AddDefenseEffect(12)),
                Card(CardVault, "Cofre da Montanha", 1, new GainResourceEffect(ResourceKind.Gold, 9)),
                Card(CardPicks, "Picaretas", 1, new GainResourceEffect(ResourceKind.Stone, 8))
            };
        }

        private static CardDefinition Card(string id, string name, int energy, params IEffect[] effects)
        {
            return new CardDefinition(id, name, energy, TargetRequirement.None, new List<IEffect>(effects));
        }

        private static CardDefinition FindCard(List<CardDefinition> cards, string id)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].Id == id)
                {
                    return cards[i];
                }
            }

            return null;
        }

        // --- Clima ---

        public static List<WeatherDefinition> Weather()
        {
            return new List<WeatherDefinition>
            {
                new WeatherDefinition("weather_sun", "Sol",
                    new Dictionary<TerrainType, int> { { TerrainType.Plain, 1 }, { TerrainType.River, -1 } },
                    weight: 1.2, flavorText: "O sol seca os campos e apressa a colheita."),
                new WeatherDefinition("weather_rain", "Chuva",
                    new Dictionary<TerrainType, int> { { TerrainType.River, 2 }, { TerrainType.Forest, 1 } },
                    buildCostMultiplier: 1.25, threatDelayDays: 1, weight: 1.2,
                    flavorText: "A chuva enche os rios e atola as estradas."),
                new WeatherDefinition("weather_drought", "Seca",
                    new Dictionary<TerrainType, int> { { TerrainType.Mine, 2 }, { TerrainType.Plain, -2 } },
                    weight: 0.8, flavorText: "A terra racha, mas a rocha fica exposta."),
                new WeatherDefinition("weather_storm", "Tempestade",
                    productionMultiplier: -0.25, baseDamage: 1, threatForceDelta: -4, weight: 0.7,
                    flavorText: "Ninguém trabalha, e ninguém marcha."),
                new WeatherDefinition("weather_fog", "Neblina",
                    energyDelta: 1, blocksPurchase: true, blocksReveal: true, weight: 0.7,
                    flavorText: "Sem horizonte, resta cuidar do que já é seu."),
                new WeatherDefinition("weather_calm", "Dia limpo",
                    weight: 1.4, isCalmDay: true,
                    flavorText: "Nada de especial. É o que faz os outros dias pesarem.")
            };
        }

        // --- Eventos ---

        public static List<EventDefinition> Events(List<CardDefinition> cards)
        {
            CardDefinition rally = FindCard(cards, "card_rally");
            CardDefinition harvest = FindCard(cards, "card_harvest");

            return new List<EventDefinition>
            {
                // --- Positivos ---
                Simple("event_caravan", "Caravana de mercadores", EventClass.Positive,
                    "Rodas rangem na estrada e param diante do Salão.",
                    new GainResourceEffect(ResourceKind.Gold, 4, GoldScaling.PerOwnedTile)),
                Simple("event_harvest", "Safra farta", EventClass.Positive,
                    "Os celeiros não dão conta.",
                    new GainResourceEffect(ResourceKind.Food, 12)),
                Simple("event_refugees", "Refugiados", EventClass.Positive,
                    "Chegam com pouco, e com vontade de ficar.",
                    new RecruitEffect(2), new AddCardToDeckEffect(harvest)),
                Simple("event_vein", "Veio exposto", EventClass.Positive,
                    "A erosão mostrou o que estava embaixo.",
                    new GainResourceEffect(ResourceKind.Stone, 10)),
                Simple("event_ceded", "Terreno cedido", EventClass.Positive,
                    "Um vizinho decide que aquela faixa não vale a briga.",
                    new GrantTileEffect()),
                Simple("event_walls", "Reforço de muralha", EventClass.Positive,
                    "Os pedreiros trabalharam de graça, por medo.",
                    new RepairBaseEffect(4)),
                Simple("event_driftwood", "Madeira à deriva", EventClass.Positive,
                    "O rio trouxe o que a enchente levou de alguém.",
                    new GainResourceEffect(ResourceKind.Wood, 12)),

                // --- Neutros ---
                Simple("event_migration", "Migração", EventClass.Neutral,
                    "Gente nova, ideias novas.",
                    new DrawCardsEffect(2)),
                Simple("event_survey", "Levantamento", EventClass.Neutral,
                    "Um cartógrafo passa a tarde no alto da torre.",
                    new RevealTileEffect(3)),

                // --- Ofertas: o mercado do jogo ---
                Offer("event_mercenaries", "Mercenários à porta", EventClass.Neutral,
                    "Eles cobram caro e não ficam para o segundo ataque.",
                    "Recusar", "Contratar por 30% do ouro (defendem amanhã)",
                    new LoseResourceEffect(ResourceKind.Gold, 0.3), new AddDefenseEffect(14)),
                Offer("event_peddler", "Mascate", EventClass.Neutral,
                    "Ele abre a mala e espera.",
                    "Deixar passar", "Comprar a carta por 25% do ouro",
                    new LoseResourceEffect(ResourceKind.Gold, 0.25), new AddCardToDeckEffect(rally)),
                new EventDefinition(
                    "event_engineer", "Engenheiro itinerante", EventClass.Neutral,
                    new List<EventOption>
                    {
                        new EventOption("Dispensar", new List<IEffect>()),
                        new EventOption("Pagar a obra com 35% do ouro", new List<IEffect>
                        {
                            new LoseResourceEffect(ResourceKind.Gold, 0.35), new RepairTileEffect(), new RepairBaseEffect(4)
                        }, true)
                    },
                    cooldownDays: 4,
                    conditions: new List<EventCondition> { new HasDamageCondition() },
                    flavorText: "Ele conserta o que a horda quebrou, por um preço."),
                Offer("event_surveyor", "Agrimensor", EventClass.Positive,
                    "Por uma taxa, ele marca a terra como sua.",
                    "Recusar", "Pagar a demarcação com 40% do ouro",
                    new LoseResourceEffect(ResourceKind.Gold, 0.4), new GrantTileEffect()),
                Offer("event_grain", "Mercador de grãos", EventClass.Neutral,
                    "As sacas estão na carroça. O preço, na cara dele.",
                    "Recusar", "Trocar 30% do ouro por 14 de comida",
                    new LoseResourceEffect(ResourceKind.Gold, 0.3), new GainResourceEffect(ResourceKind.Food, 14)),
                Offer("event_masons", "Pedreiros de passagem", EventClass.Neutral,
                    "Trabalham por comida e seguem viagem.",
                    "Recusar", "Dar 30% da comida por 12 de pedra",
                    new LoseResourceEffect(ResourceKind.Food, 0.3), new GainResourceEffect(ResourceKind.Stone, 12)),

                // --- Negativos ---
                Simple("event_tax", "Imposto do reino vizinho", EventClass.Negative,
                    "O cobrador vem acompanhado.",
                    new LoseResourceEffect(ResourceKind.Gold, 0.2)),
                Simple("event_rats", "Ratos no celeiro", EventClass.Negative,
                    "Ninguém viu entrar. Todo mundo viu o estrago.",
                    new LoseResourceEffect(ResourceKind.Food, 0.2)),
                Simple("event_plague", "Praga", EventClass.Negative,
                    "Um edifício fecha as portas por dois dias.",
                    new DisableTileEffect(2)),
                Simple("event_erosion", "Erosão", EventClass.Negative,
                    "A borda do território cede. Só a borda vazia.",
                    new DestroyTileEffect()),
                Simple("event_fire", "Incêndio pequeno", EventClass.Negative,
                    "Contido antes de chegar ao Salão.",
                    new DamageBaseEffect(0.1, true)),
                Simple("event_sabotage", "Sabotagem", EventClass.Negative,
                    "As ferramentas amanhecem quebradas.",
                    new ModifyProductionEffect(-0.2, 2, true))
            };
        }

        private static EventDefinition Simple(
            string id, string name, EventClass eventClass, string flavor, params IEffect[] effects)
        {
            return EventDefinition.Simple(id, name, eventClass, new List<IEffect>(effects), flavorText: flavor);
        }

        // --- Encontros ---
        //
        // Um encontro e alguem (ou algo) que aparece no reino e pede uma resposta.
        // Tem sempre tres saidas assimetricas, e a que acolhe deixa uma carta no
        // baralho: e assim que o encontro continua existindo depois do dia dele.

        public const string EncounterPrefix = "encounter_";

        private static EventOption Choice(string label, bool commits, params IEffect[] effects)
        {
            return new EventOption(label, new List<IEffect>(effects), commits);
        }

        private static EventDefinition Encounter(
            string id, string name, EventClass eventClass, string flavor, int minimumDay, params EventOption[] options)
        {
            return new EventDefinition(
                EncounterPrefix + id, name, eventClass, new List<EventOption>(options),
                conditions: new List<EventCondition> { new MinimumDayCondition(minimumDay) },
                weight: 0.8,
                cooldownDays: 12,
                flavorText: flavor);
        }

        public static List<CardDefinition> EncounterCards()
        {
            return new List<CardDefinition>
            {
                Card("card_minotaur", "Minotauro Aliado", 1, new AddDefenseEffect(12)),
                Card("card_dragonfire", "Fôlego do Dragão", 2, new AddDefenseEffect(20)),
                Card("card_hero", "Herói Errante", 1, new AddDefenseEffect(8), new DrawCardsEffect(1)),
                Card("card_hermit", "Conselho do Eremita", 0, new DrawCardsEffect(2)),
                Card("card_relic", "Relíquia da Ruína", 1,
                    new GainResourceEffect(ResourceKind.Gold, 6), new GainResourceEffect(ResourceKind.Stone, 6))
            };
        }

        public static List<EventDefinition> Encounters()
        {
            List<CardDefinition> cards = EncounterCards();
            CardDefinition minotaur = cards[0];
            CardDefinition dragon = cards[1];
            CardDefinition hero = cards[2];
            CardDefinition hermit = cards[3];
            CardDefinition relic = cards[4];

            return new List<EventDefinition>
            {
                Encounter("minotaur", "Minotauro ferido", EventClass.Neutral,
                    "Ele sangra na beira do campo e não ergue o machado. Só olha.", 3,
                    Choice("Deixar que siga o caminho", false),
                    Choice("Tratar as feridas com a comida do reino", true,
                        new LoseResourceEffect(ResourceKind.Food, 8, GoldScaling.Flat), new AddCardToDeckEffect(minotaur)),
                    Choice("Abater e vender os chifres (ele quebra as cercas antes)", true,
                        new GainResourceEffect(ResourceKind.Gold, 14), new LoseResourceEffect(ResourceKind.Wood, 6, GoldScaling.Flat))),

                Encounter("dragon", "Dragão sobre o reino", EventClass.Negative,
                    "A sombra dá duas voltas e pousa na colina. Ele quer alguma coisa.", 10,
                    Choice("Pagar o tributo: 30% do ouro", false, new LoseResourceEffect(ResourceKind.Gold, 0.3)),
                    Choice("Oferecer o rebanho e ganhar um aliado caro", true,
                        new LoseResourceEffect(ResourceKind.Food, 12, GoldScaling.Flat), new AddCardToDeckEffect(dragon)),
                    Choice("Enfrentar: a base sofre, o tesouro dele é seu", true,
                        new DamageBaseEffect(0.15, true), new GainResourceEffect(ResourceKind.Gold, 26))),

                Encounter("hero", "Herói errante", EventClass.Positive,
                    "Armadura amassada, espada boa, bolso vazio.", 4,
                    Choice("Desejar boa viagem", false),
                    Choice("Contratar por 25% do ouro", true,
                        new LoseResourceEffect(ResourceKind.Gold, 0.25), new AddCardToDeckEffect(hero)),
                    Choice("Dar pouso por uma noite, em troca de reparos", true,
                        new LoseResourceEffect(ResourceKind.Food, 6, GoldScaling.Flat), new RepairBaseEffect(3))),

                Encounter("ruin", "Ruína amaldiçoada", EventClass.Neutral,
                    "Os pedreiros acharam uma porta de pedra debaixo do campo. Ela está morna.", 6,
                    Choice("Enterrar de novo", false),
                    Choice("Abrir e levar o que houver (os operários adoecem)", true,
                        new AddCardToDeckEffect(relic), new LoseResourceEffect(ResourceKind.Food, 6, GoldScaling.Flat)),
                    Choice("Selar com pedra do reino", true,
                        new LoseResourceEffect(ResourceKind.Stone, 6, GoldScaling.Flat), new RepairBaseEffect(3))),

                Encounter("caravan", "Caravana perdida", EventClass.Positive,
                    "Três carroças, um guia que jura conhecer o caminho, e nenhum mapa.", 2,
                    Choice("Apontar a estrada", false, new GainResourceEffect(ResourceKind.Gold, 4)),
                    Choice("Escoltar até a fronteira", true,
                        new LoseResourceEffect(ResourceKind.Food, 6, GoldScaling.Flat), new GainResourceEffect(ResourceKind.Gold, 16)),
                    Choice("Tomar a carga e pagar o silêncio do guia", true,
                        new GainResourceEffect(ResourceKind.Wood, 10), new GainResourceEffect(ResourceKind.Stone, 8),
                        new LoseResourceEffect(ResourceKind.Gold, 0.15))),

                Encounter("hermit", "Eremita da colina", EventClass.Positive,
                    "Ele desce uma vez por estação, e só fala com quem traz pão.", 5,
                    Choice("Deixar em paz", false),
                    Choice("Levar pão e ouvir", true,
                        new LoseResourceEffect(ResourceKind.Food, 5, GoldScaling.Flat), new AddCardToDeckEffect(hermit)),
                    Choice("Convidar para morar no reino", true,
                        new LoseResourceEffect(ResourceKind.Food, 4, GoldScaling.Flat), new RecruitEffect(2)))
            };
        }

        private static EventDefinition Offer(
            string id, string name, EventClass eventClass, string flavor,
            string declineLabel, string offerLabel, params IEffect[] offerEffects)
        {
            return new EventDefinition(
                id, name, eventClass,
                new List<EventOption>
                {
                    new EventOption(declineLabel, new List<IEffect>()),
                    new EventOption(offerLabel, new List<IEffect>(offerEffects), true)
                },
                cooldownDays: 4,
                flavorText: flavor);
        }

        // --- Raca ---

        // Cartas de identidade: cada raca abre a run com duas que so ela tem.
        private static List<CardDefinition> HumanCards()
        {
            return new List<CardDefinition>
            {
                Card("card_merchants", "Mercadores", 1, new GainResourceEffect(ResourceKind.Gold, 7)),
                Card("card_census", "Censo", 0, new DrawCardsEffect(1), new GainResourceEffect(ResourceKind.Gold, 2))
            };
        }

        private static List<CardDefinition> OrcCards()
        {
            return new List<CardDefinition>
            {
                Card("card_charge", "Investida", 1, new AddDefenseEffect(7), new GainResourceEffect(ResourceKind.Food, 3)),
                Card("card_drums", "Tambores de Guerra", 1, new DrawCardsEffect(2))
            };
        }

        private static List<CardDefinition> ElfCards()
        {
            return new List<CardDefinition>
            {
                Card("card_wildharvest", "Colheita Silvestre", 1,
                    new GainResourceEffect(ResourceKind.Food, 5), new GainResourceEffect(ResourceKind.Wood, 4)),
                Card("card_volley", "Saraivada", 1, new AddDefenseEffect(8))
            };
        }

        public static RaceDefinition Humans()
        {
            return new RaceDefinition(
                HumansId, "Humanos", 48, 20, 5, 3,
                startingCards: HumanCards(),
                rules: new RuleModifiers(new Dictionary<string, double> { { RuleKeys.ExtraCardsPerDay, 1 } }),
                startingResources: ResourceAmounts.Of(
                    (ResourceKind.Gold, 48),
                    (ResourceKind.Wood, 24),
                    (ResourceKind.Stone, 8),
                    (ResourceKind.Food, 14),
                    (ResourceKind.Population, 4)));
        }

        public const string OrcsId = "orcs";
        public const string ElvesId = "elves";

        /// <summary>
        /// Orcs vivem de saque: cada ataque repelido rende comida e ouro, e a
        /// guarnicao deles vale mais. Em troca nao pescam nem comerciam, e jogam
        /// com uma carta a menos por dia.
        /// </summary>
        public static RaceDefinition Orcs()
        {
            return new RaceDefinition(
                OrcsId, "Orcs", 38, 24, 5, 3,
                startingCards: OrcCards(),
                rules: new RuleModifiers(new Dictionary<string, double>
                {
                    { RuleKeys.Plunder(ResourceKind.Food), 10 },
                    { RuleKeys.Plunder(ResourceKind.Gold), 8 },
                    { RuleKeys.GarrisonDefenseMultiplier, 1.25 }
                }),
                forbiddenBuildingIds: new List<string> { Docks, Market },
                startingResources: ResourceAmounts.Of(
                    (ResourceKind.Gold, 38),
                    (ResourceKind.Wood, 24),
                    (ResourceKind.Stone, 10),
                    (ResourceKind.Food, 22),
                    (ResourceKind.Population, 5)));
        }

        /// <summary>
        /// Elfos preservam: floresta possuida rende madeira e comida sozinha, sem
        /// gente, mas nao aceita obra nenhuma.
        /// </summary>
        public static RaceDefinition Elves()
        {
            return new RaceDefinition(
                ElvesId, "Elfos", 38, 20, 5, 3,
                startingCards: ElfCards(),
                rules: new RuleModifiers(new Dictionary<string, double>
                {
                    { RuleKeys.ExtraCardsPerDay, 1 },
                    { RuleKeys.NoBuildOnForest, 1 },
                    { RuleKeys.GarrisonDefenseMultiplier, 1.4 },
                    { RuleKeys.TerrainYield(TerrainType.Forest, ResourceKind.Wood), 3 },
                    { RuleKeys.TerrainYield(TerrainType.Forest, ResourceKind.Gold), 2 },
                    { RuleKeys.TerrainYield(TerrainType.Forest, ResourceKind.Food), 2 }
                }),
                forbiddenBuildingIds: new List<string> { Sawmill },
                startingResources: ResourceAmounts.Of(
                    (ResourceKind.Gold, 38),
                    (ResourceKind.Wood, 30),
                    (ResourceKind.Stone, 6),
                    (ResourceKind.Food, 16),
                    (ResourceKind.Population, 4)));
        }

        public const string DwarvesId = "dwarves";

        private static List<CardDefinition> DwarfCards()
        {
            return new List<CardDefinition>
            {
                Card("card_goldvein", "Veio de Ouro", 1,
                    new GainResourceEffect(ResourceKind.Gold, 6), new GainResourceEffect(ResourceKind.Stone, 3)),
                Card("card_anvil", "Bigorna", 1, new AddDefenseEffect(8))
            };
        }

        /// <summary>
        /// Anoes jogam alto em pouco chao: o territorio e caro e nao vai longe do
        /// Salao, mas rochedo possuido rende pedra e ouro sozinho, a guarnicao vale
        /// muito e a base e a mais dura do jogo.
        /// </summary>
        public static RaceDefinition Dwarves()
        {
            return new RaceDefinition(
                DwarvesId, "Anões", 44, 24, 5, 3,
                startingCards: DwarfCards(),
                rules: new RuleModifiers(new Dictionary<string, double>
                {
                    { RuleKeys.TileCostMultiplier, 1.5 },
                    { RuleKeys.MaxDistanceFromHall, 3 },
                    { RuleKeys.GarrisonDefenseMultiplier, 1.15 },
                    { RuleKeys.TerrainYield(TerrainType.Mine, ResourceKind.Stone), 2 },
                    { RuleKeys.TerrainYield(TerrainType.Mine, ResourceKind.Gold), 2 }
                }),
                startingResources: ResourceAmounts.Of(
                    (ResourceKind.Gold, 44),
                    (ResourceKind.Wood, 20),
                    (ResourceKind.Stone, 20),
                    (ResourceKind.Food, 16),
                    (ResourceKind.Population, 4)));
        }

        public static readonly string[] RaceIds = { HumansId, OrcsId, ElvesId, DwarvesId };

        public static string RaceName(string raceId)
        {
            return raceId == OrcsId ? "Orcs" : raceId == ElvesId ? "Elfos" : raceId == DwarvesId ? "Anões" : "Humanos";
        }

        /// <summary>O que muda na forma de jogar. Texto da tela de escolha.</summary>
        public static string RaceSummary(string raceId)
        {
            switch (raceId)
            {
                case OrcsId:
                    return "Vivem de saque. Cada ataque repelido rende +10 de comida e +8 de ouro, e a guarnição defende 25% a mais. " +
                           "Base mais dura (24). Não constroem Ancoradouro nem Mercado, e compram uma carta a menos por dia.";
                case ElvesId:
                    return "Preservam a floresta. Cada floresta possuída rende +3 de madeira, +2 de comida e +2 de ouro por dia sem precisar de gente, " +
                           "mas não aceita construção nenhuma. Arqueiros: a guarnição defende 40% a mais. Não têm Serraria.";
                case DwarvesId:
                    return "Jogam alto em pouco chão. Célula custa 50% a mais e não passa de 3 casas do Salão, mas rochedo possuído " +
                           "rende +2 de pedra e +2 de ouro sozinho, a guarnição defende 15% a mais e a base é dura (24).";
                default:
                    return "Equilibrados. Compram uma carta a mais por dia e constroem em qualquer terreno.";
            }
        }

        // --- Rivais ---

        public static readonly RivalIdentity Assault = new RivalIdentity(
            "assault", "Assalto",
            "Ataca as muralhas e devora a borda do reino. Defesa segura.",
            PressureAxis.Territory);

        public static readonly RivalIdentity Raid = new RivalIdentity(
            "raid", "Saque",
            "Não quer suas terras, quer seus celeiros: leva comida mesmo quando é repelido.",
            PressureAxis.Resource, ResourceKind.Food, 0.5, 1.0);

        public static readonly RivalIdentity Siege = new RivalIdentity(
            "siege", "Cerco",
            "Poucos ataques, muito fortes. Mira direto na base.",
            PressureAxis.Integrity);

        public static readonly RivalIdentity Reapers = new RivalIdentity(
            "reapers", "Captura",
            "Leva gente. Cada ataque esvazia casas, repelido ou não.",
            PressureAxis.Population, null, 0.08, 1.0);

        private static RivalDefinition Rival(
            string id, string name, RivalIdentity identity, int attacks, int firstForce, double growth)
        {
            return new RivalDefinition(id, name, identity, attacks, new RivalCampaignCurve(firstForce, growth));
        }

        /// <summary>Os tres rivais do primeiro nivel, na ordem em que entram em guerra.</summary>
        public static List<RivalDefinition> Rivals()
        {
            return new List<RivalDefinition>
            {
                Rival("rival_axe_clan", "Clã do Machado", Assault, 3, 9, 6),
                Rival("rival_valley_raiders", "Saqueadores do Vale", Raid, 3, 21, 7),
                Rival("rival_iron_legion", "Legião de Ferro", Siege, 3, 38, 11)
            };
        }

        // --- Escada de dificuldade ---

        public static DifficultyLadder BuildLadder()
        {
            List<RivalDefinition> first = Rivals();

            List<RivalDefinition> second = new List<RivalDefinition>
            {
                Rival("rival_axe_clan", "Clã do Machado", Assault, 3, 13, 8),
                Rival("rival_valley_raiders", "Saqueadores do Vale", Raid, 4, 27, 8),
                Rival("rival_iron_legion", "Legião de Ferro", Siege, 4, 46, 13)
            };

            List<RivalDefinition> third = new List<RivalDefinition>
            {
                Rival("rival_axe_clan", "Clã do Machado", Assault, 4, 16, 9),
                Rival("rival_valley_raiders", "Saqueadores do Vale", Raid, 4, 30, 10),
                Rival("rival_grey_host", "Hoste Cinzenta", Reapers, 4, 44, 11),
                Rival("rival_iron_legion", "Legião de Ferro", Siege, 4, 60, 15)
            };

            return new DifficultyLadder(new List<DifficultyLevel>
            {
                new DifficultyLevel(
                    "level_frontier", 1, "Fronteira",
                    "Três rivais, um de cada vez. É onde se aprende o reino.",
                    first, new List<int> { 1, 9, 18 }, 1.0, 16),
                new DifficultyLevel(
                    "level_march", 2, "Marcha",
                    "Campanhas mais longas e mais fortes, rivais entram mais cedo, e você começa com 15% a menos de recursos.",
                    second, new List<int> { 1, 8, 15 }, 0.85, 28),
                new DifficultyLevel(
                    "level_siege", 3, "Cerco Total",
                    "Um quarto rival que leva gente, todos mais fortes, e 30% a menos de recursos no início.",
                    third, new List<int> { 1, 7, 13, 20 }, 0.7, 40)
            });
        }

        // --- Arvore de meta: tronco geral ---

        public static MetaTree BuildMetaTree()
        {
            return new MetaTree(new List<MetaNodeDefinition>
            {
                // Economia
                Node("meta_granary", "Celeiros Cheios", 12,
                    "A run começa com mais comida guardada.",
                    bonuses: new[] { MetaBonus.Starting(ResourceKind.Food, 10) }),
                Node("meta_treasury", "Tesouro Real", 25,
                    "Mais ouro no primeiro dia.",
                    prerequisites: new[] { "meta_granary" },
                    bonuses: new[] { MetaBonus.Starting(ResourceKind.Gold, 15) }),
                Node("meta_market", "Carta de Feira", 45,
                    "Libera o Mercado, que transforma casas vizinhas em ouro, e a carta Banquete.",
                    prerequisites: new[] { "meta_treasury" },
                    unlocks: new[]
                    {
                        new Unlock(UnlockKind.Building, Market),
                        new Unlock(UnlockKind.Card, CardFeast)
                    },
                    requiredLevel: 2),

                // Construcao
                Node("meta_lumber", "Estoque de Madeira", 12,
                    "A run começa com mais madeira.",
                    bonuses: new[] { MetaBonus.Starting(ResourceKind.Wood, 10) }),
                Node("meta_masons", "Guilda dos Pedreiros", 25,
                    "Mais pedra no início e a carta Mestre de Obras no baralho.",
                    prerequisites: new[] { "meta_lumber" },
                    bonuses: new[] { MetaBonus.Starting(ResourceKind.Stone, 8) },
                    unlocks: new[] { new Unlock(UnlockKind.Card, CardMasterBuilder) }),
                Node("meta_settlers", "Primeiros Colonos", 45,
                    "Uma pessoa a mais desde o primeiro dia.",
                    prerequisites: new[] { "meta_masons" },
                    bonuses: new[] { MetaBonus.Starting(ResourceKind.Population, 1) },
                    requiredLevel: 2),

                // Defesa
                Node("meta_shields", "Muralha de Escudos", 12,
                    "Adiciona a carta Muralha de Escudos ao baralho.",
                    unlocks: new[] { new Unlock(UnlockKind.Card, CardShieldWall) }),
                Node("meta_foundations", "Fundações Antigas", 25,
                    "A base aguenta mais dano.",
                    prerequisites: new[] { "meta_shields" },
                    bonuses: new[] { MetaBonus.Integrity(3) }),
                Node("meta_barracks", "Quartel", 45,
                    "Libera o Quartel: muita defesa num só lugar, para quem tem gente e pedra.",
                    prerequisites: new[] { "meta_foundations" },
                    unlocks: new[] { new Unlock(UnlockKind.Building, Barracks) },
                    requiredLevel: 2),

                // Quarto degrau de cada coluna: so abre depois de vencer o nivel 2.
                Node("meta_trade", "Rotas Comerciais", 70,
                    "Ainda mais ouro no primeiro dia.",
                    prerequisites: new[] { "meta_market" },
                    bonuses: new[] { MetaBonus.Starting(ResourceKind.Gold, 10) }, requiredLevel: 3),
                Node("meta_yard", "Canteiro Real", 70,
                    "Ainda mais madeira no início.",
                    prerequisites: new[] { "meta_settlers" },
                    bonuses: new[] { MetaBonus.Starting(ResourceKind.Wood, 8) },
                    requiredLevel: 3),
                Node("meta_guard", "Guarda Real", 70,
                    "A base aguenta ainda mais dano.",
                    prerequisites: new[] { "meta_barracks" },
                    bonuses: new[] { MetaBonus.Integrity(2) }, requiredLevel: 3),

                // Estrategia: so cartas, nenhum poder numerico.
                Node("meta_reserves", "Reservas do Reino", 15,
                    "Adiciona a carta Reservas ao baralho: um pouco de ouro, madeira e comida.",
                    unlocks: new[] { new Unlock(UnlockKind.Card, CardReserves) }),
                Node("meta_council", "Conselho de Guerra", 30,
                    "Adiciona a carta Conselho ao baralho: compra três cartas.",
                    prerequisites: new[] { "meta_reserves" },
                    unlocks: new[] { new Unlock(UnlockKind.Card, CardCouncil) }),
                Node("meta_laststand", "Última Linha", 50,
                    "Adiciona a carta Última Linha ao baralho: muita defesa num dia só, por toda a energia.",
                    prerequisites: new[] { "meta_council" },
                    unlocks: new[] { new Unlock(UnlockKind.Card, CardLastStand) }, requiredLevel: 2),

                // Galho dos Humanos: comercio e oficios. So vale em run de Humanos.
                Node("human_fair", "Feira Livre", 12,
                    "Adiciona a carta Feira Livre: ouro e uma carta nova.",
                    unlocks: new[] { new Unlock(UnlockKind.Card, CardFair) }, race: HumansId),
                Node("human_caravan", "Caravana", 25,
                    "Adiciona a carta Caravana: um pouco de ouro, madeira e pedra.",
                    prerequisites: new[] { "human_fair" },
                    unlocks: new[] { new Unlock(UnlockKind.Card, CardCaravan) }, race: HumansId),
                Node("human_walls", "Muros da Cidade", 40,
                    "A base aguenta mais dano.",
                    prerequisites: new[] { "human_caravan" },
                    bonuses: new[] { MetaBonus.Integrity(2) }, requiredLevel: 3, race: HumansId),
                Node("human_engineers", "Engenheiros", 15,
                    "Adiciona a carta Engenheiros: madeira e um reparo na base.",
                    unlocks: new[] { new Unlock(UnlockKind.Card, CardEngineers) }, race: HumansId),
                Node("human_stores", "Armazém da Guilda", 30,
                    "A run começa com mais comida.",
                    prerequisites: new[] { "human_engineers" },
                    bonuses: new[] { MetaBonus.Starting(ResourceKind.Food, 6) }, requiredLevel: 2, race: HumansId),
                Node("human_masons", "Mestres Canteiros", 50,
                    "A run começa com mais pedra.",
                    prerequisites: new[] { "human_stores" },
                    bonuses: new[] { MetaBonus.Starting(ResourceKind.Stone, 6) }, requiredLevel: 3, race: HumansId),

                // Galho dos Anoes: forja e minas. So vale em run de Anoes.
                Node("dwarf_forge", "Forja Ancestral", 12,
                    "Adiciona a carta Aço Anão: muita defesa por pouca energia.",
                    unlocks: new[] { new Unlock(UnlockKind.Card, CardDwarfSteel) }, race: DwarvesId),
                Node("dwarf_vault", "Cofre da Montanha", 25,
                    "Adiciona a carta Cofre da Montanha: ouro na hora.",
                    prerequisites: new[] { "dwarf_forge" },
                    unlocks: new[] { new Unlock(UnlockKind.Card, CardVault) }, race: DwarvesId),
                Node("dwarf_keep", "Fortaleza de Pedra", 40,
                    "A base aguenta mais dano.",
                    prerequisites: new[] { "dwarf_vault" },
                    bonuses: new[] { MetaBonus.Integrity(2) }, requiredLevel: 3, race: DwarvesId),
                Node("dwarf_picks", "Picaretas", 15,
                    "Adiciona a carta Picaretas: pedra na hora.",
                    unlocks: new[] { new Unlock(UnlockKind.Card, CardPicks) }, race: DwarvesId),
                Node("dwarf_stores", "Despensa Funda", 30,
                    "A run começa com mais comida.",
                    prerequisites: new[] { "dwarf_picks" },
                    bonuses: new[] { MetaBonus.Starting(ResourceKind.Food, 6) }, requiredLevel: 2, race: DwarvesId),
                Node("dwarf_deep", "Galerias Profundas", 50,
                    "A run começa com mais pedra.",
                    prerequisites: new[] { "dwarf_stores" },
                    bonuses: new[] { MetaBonus.Starting(ResourceKind.Stone, 6) }, requiredLevel: 3, race: DwarvesId),

                // Galho dos Orcs: so vale em run de Orcs.
                Node("orc_warband", "Bando de Guerra", 12,
                    "Adiciona a carta Pilhagem ao baralho dos Orcs.",
                    unlocks: new[] { new Unlock(UnlockKind.Card, CardPillage) }, race: OrcsId),
                Node("orc_spoils", "Despojos Guardados", 25,
                    "A run começa com mais comida, tomada da última guerra.",
                    prerequisites: new[] { "orc_warband" },
                    bonuses: new[] { MetaBonus.Starting(ResourceKind.Food, 6) }, requiredLevel: 2, race: OrcsId),
                Node("orc_bastion", "Paliçada de Ossos", 40,
                    "A base aguenta mais dano.",
                    prerequisites: new[] { "orc_spoils" },
                    bonuses: new[] { MetaBonus.Integrity(2) }, requiredLevel: 3, race: OrcsId),
                Node("orc_warcry", "Grito de Guerra", 15,
                    "Adiciona a carta Grito de Guerra: defesa e uma carta nova.",
                    unlocks: new[] { new Unlock(UnlockKind.Card, CardWarCry) }, race: OrcsId),
                Node("orc_booty", "Butim", 30,
                    "Adiciona a carta Butim: ouro na hora.",
                    prerequisites: new[] { "orc_warcry" },
                    unlocks: new[] { new Unlock(UnlockKind.Card, CardBooty) }, race: OrcsId),
                Node("orc_horde", "Horda", 50,
                    "Uma pessoa a mais desde o primeiro dia.",
                    prerequisites: new[] { "orc_booty" },
                    bonuses: new[] { MetaBonus.Starting(ResourceKind.Population, 1) }, requiredLevel: 3, race: OrcsId),

                // Galho dos Elfos: so vale em run de Elfos.
                Node("elf_groves", "Bosques Antigos", 12,
                    "Adiciona a carta Canto da Floresta ao baralho dos Elfos.",
                    unlocks: new[] { new Unlock(UnlockKind.Card, CardForestSong) }, race: ElvesId),
                Node("elf_stores", "Frutos do Outono", 25,
                    "A run começa com mais comida.",
                    prerequisites: new[] { "elf_groves" },
                    bonuses: new[] { MetaBonus.Starting(ResourceKind.Food, 6) }, requiredLevel: 2, race: ElvesId),
                Node("elf_roots", "Raízes Profundas", 40,
                    "A base aguenta mais dano.",
                    prerequisites: new[] { "elf_stores" },
                    bonuses: new[] { MetaBonus.Integrity(2) }, requiredLevel: 3, race: ElvesId),
                Node("elf_thorns", "Barreira de Espinhos", 15,
                    "Adiciona a carta Barreira de Espinhos: defesa e um reparo na base.",
                    unlocks: new[] { new Unlock(UnlockKind.Card, CardThorns) }, race: ElvesId),
                Node("elf_sap", "Bênção da Seiva", 30,
                    "Adiciona a carta Bênção da Seiva: comida e uma carta nova.",
                    prerequisites: new[] { "elf_thorns" },
                    unlocks: new[] { new Unlock(UnlockKind.Card, CardSap) }, race: ElvesId),
                Node("elf_stonesong", "Canto da Pedra", 50,
                    "A run começa com mais pedra.",
                    prerequisites: new[] { "elf_sap" },
                    bonuses: new[] { MetaBonus.Starting(ResourceKind.Stone, 6) }, requiredLevel: 3, race: ElvesId)
            });
        }

        private static MetaNodeDefinition Node(
            string id, string name, int cost, string description,
            string[] prerequisites = null, MetaBonus[] bonuses = null, Unlock[] unlocks = null,
            int requiredLevel = 1, string race = null)
        {
            return new MetaNodeDefinition(
                id, name, cost,
                unlocks != null ? new List<Unlock>(unlocks) : new List<Unlock>(),
                prerequisites != null ? new List<string>(prerequisites) : new List<string>(),
                race, description,
                bonuses != null ? new List<MetaBonus>(bonuses) : new List<MetaBonus>(),
                requiredLevel);
        }

        /// <summary>
        /// Como a arvore e desenhada: tres colunas, tres degraus. A ordem aqui e a
        /// ordem de leitura da tela, da esquerda para a direita e de baixo para cima.
        /// </summary>
        public static readonly string[][] MetaTreeColumns =
        {
            new[] { "meta_granary", "meta_treasury", "meta_market", "meta_trade" },
            new[] { "meta_lumber", "meta_masons", "meta_settlers", "meta_yard" },
            new[] { "meta_shields", "meta_foundations", "meta_barracks", "meta_guard" },
            new[] { "meta_reserves", "meta_council", "meta_laststand" }
        };

        public static readonly string[] MetaTreeColumnTitles = { "Economia", "Construção", "Defesa", "Estratégia" };

        /// <summary>Colunas do galho de uma raca, cada uma de cima para baixo.</summary>
        public static string[][] RaceColumns(string raceId)
        {
            switch (raceId)
            {
                case HumansId:
                    return new[]
                    {
                        new[] { "human_fair", "human_caravan", "human_walls" },
                        new[] { "human_engineers", "human_stores", "human_masons" }
                    };
                case OrcsId:
                    return new[]
                    {
                        new[] { "orc_warband", "orc_spoils", "orc_bastion" },
                        new[] { "orc_warcry", "orc_booty", "orc_horde" }
                    };
                case ElvesId:
                    return new[]
                    {
                        new[] { "elf_groves", "elf_stores", "elf_roots" },
                        new[] { "elf_thorns", "elf_sap", "elf_stonesong" }
                    };
                case DwarvesId:
                    return new[]
                    {
                        new[] { "dwarf_forge", "dwarf_vault", "dwarf_keep" },
                        new[] { "dwarf_picks", "dwarf_stores", "dwarf_deep" }
                    };
                default:
                    return new string[0][];
            }
        }

        public static string[] RaceColumnTitles(string raceId)
        {
            return raceId == HumansId ? new[] { "Comércio", "Ofícios" }
                : raceId == OrcsId ? new[] { "Saque", "Guerra" }
                : raceId == ElvesId ? new[] { "Floresta", "Magia" }
                : raceId == DwarvesId ? new[] { "Forja", "Minas" }
                : new string[0];
        }

        /// <summary>Degraus do galho de uma raca, de cima para baixo. Vazio para quem nao tem galho.</summary>
        public static string[] RaceBranch(string raceId)
        {
            List<string> all = new List<string>();
            string[][] columns = RaceColumns(raceId);
            for (int c = 0; c < columns.Length; c++)
            {
                all.AddRange(columns[c]);
            }

            return all.ToArray();
        }

        // --- Montagem ---

        public static ContentCatalog BuildCatalog()
        {
            ContentCatalog catalog = new ContentCatalog();

            List<BuildingDefinition> buildings = Buildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                catalog.AddBuilding(buildings[i], !LockedBuildings.Contains(buildings[i].Id));
            }

            List<CardDefinition> cards = Cards();
            for (int i = 0; i < cards.Count; i++)
            {
                catalog.AddCard(cards[i], !LockedCards.Contains(cards[i].Id));
            }

            List<EventDefinition> events = Events(cards);
            events.AddRange(Encounters());
            for (int i = 0; i < events.Count; i++)
            {
                catalog.AddEvent(events[i], true);
            }

            // As cartas de encontro existem no catalogo para terem nome e texto,
            // mas so entram no baralho pela escolha feita no encontro.
            List<CardDefinition> encounterCards = EncounterCards();
            for (int i = 0; i < encounterCards.Count; i++)
            {
                catalog.AddCard(encounterCards[i], false);
            }

            List<WeatherDefinition> weather = Weather();
            for (int i = 0; i < weather.Count; i++)
            {
                catalog.AddWeather(weather[i]);
            }

            // Cartas de identidade entram no catalogo so para terem nome e texto na
            // interface; nao sao liberadas para o baralho geral.
            List<CardDefinition> identity = new List<CardDefinition>();
            identity.AddRange(HumanCards());
            identity.AddRange(OrcCards());
            identity.AddRange(ElfCards());
            identity.AddRange(DwarfCards());
            for (int i = 0; i < identity.Count; i++)
            {
                catalog.AddCard(identity[i], false);
            }

            catalog.AddRace(Humans(), true);
            catalog.AddRace(Orcs(), true);
            catalog.AddRace(Elves(), true);
            catalog.AddRace(Dwarves(), true);

            DifficultyLadder ladder = BuildLadder();
            for (int l = 0; l < ladder.Levels.Count; l++)
            {
                for (int r = 0; r < ladder.Levels[l].Rivals.Count; r++)
                {
                    RivalDefinition rival = ladder.Levels[l].Rivals[r];
                    if (!catalog.Rivals.ContainsKey(rival.Id))
                    {
                        catalog.AddRival(rival);
                    }
                }
            }

            return catalog;
        }

        /// <summary>Curvas e ritmo da run. O relogio anonimo nao e usado: toda run tem rivais.</summary>
        public static RunSetup CreateSetup(int seed, int levelRank = 1, string raceId = HumansId)
        {
            DifficultyLadder ladder = BuildLadder();
            return new RunSetup(raceId, seed)
            {
                Level = ladder.LevelOfRank(levelRank) ?? ladder.LevelOfRank(1),
                CostCurve = new TileCostCurve(10, 1.06, 2, 6),
                EventWeights = new EventClassWeights(0.40, 0.34, 0.26),
                ThreatLeadDays = 3,
                EventChance = 0.36
            };
        }

        /// <summary>Todas as regras de catalogo sobre o conteudo embutido.</summary>
        public static List<CatalogViolation> Validate()
        {
            ContentCatalog catalog = BuildCatalog();
            DifficultyLadder ladder = BuildLadder();
            MetaTree tree = BuildMetaTree();

            List<CatalogViolation> violations = new List<CatalogViolation>();
            violations.AddRange(EventCatalogValidator.Validate(new List<EventDefinition>(catalog.Events.Values)));
            violations.AddRange(WeatherCatalogValidator.Validate(catalog.Weather));
            violations.AddRange(RivalCatalogValidator.Validate(new List<RivalDefinition>(catalog.Rivals.Values)));
            violations.AddRange(DifficultyLadderValidator.Validate(ladder));
            violations.AddRange(MetaCatalogValidator.Validate(tree));
            violations.AddRange(MetaCatalogValidator.ValidatePowerCaps(tree, ladder));
            return violations;
        }
    }
}

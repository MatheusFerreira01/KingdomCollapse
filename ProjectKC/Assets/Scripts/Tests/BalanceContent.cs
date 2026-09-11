using System.Collections.Generic;
using KingdomCollapse.Core;

namespace KingdomCollapse.Tests
{
    /// <summary>
    /// Espelho do conteudo real gerado pelo ContentSeeder (task 11.1) — deliberadamente
    /// separado de TestContent, que fica minimo de proposito para os testes de regra.
    /// Este catalogo existe so para o simulador de balanceamento medir o jogo que o
    /// jogador realmente joga, e nao um catalogo de brinquedo que nunca usa madeira ou
    /// pedra. Numeros aqui devem bater com Assets/Scripts/Editor/ContentSeeder.cs; se um
    /// mudar, o outro muda junto.
    /// </summary>
    internal static class BalanceContent
    {
        private static ResourceAmounts Resources(ResourceKind kind, int amount)
        {
            ResourceAmounts amounts = new ResourceAmounts();
            amounts[kind] = amount;
            return amounts;
        }

        public static List<BuildingDefinition> Buildings()
        {
            return new List<BuildingDefinition>
            {
                new BuildingDefinition(
                    "hall", "Salao do Reino", new List<TerrainType>(), 0, 2, 2,
                    workersRequired: 0, populationCapacity: 5),

                new BuildingDefinition(
                    "farm", "Fazenda", new List<TerrainType> { TerrainType.Plain }, 12, 1, 0,
                    production: Resources(ResourceKind.Food, 4), workersRequired: 1),

                new BuildingDefinition(
                    "sawmill", "Serraria", new List<TerrainType> { TerrainType.Forest }, 14, 1, 0,
                    adjacencyBonuses: new List<AdjacencyBonus> { AdjacencyBonus.ForTerrain(TerrainType.Forest, 2) },
                    production: Resources(ResourceKind.Wood, 3), workersRequired: 1),

                new BuildingDefinition(
                    "mine", "Mina", new List<TerrainType> { TerrainType.Mine }, 16, 1, 0,
                    production: Resources(ResourceKind.Stone, 4), workersRequired: 1),

                new BuildingDefinition(
                    "docks", "Ancoradouro", new List<TerrainType> { TerrainType.River }, 13, 1, 0,
                    adjacencyBonuses: new List<AdjacencyBonus> { AdjacencyBonus.ForTerrain(TerrainType.River, 2) },
                    production: Resources(ResourceKind.Food, 2), workersRequired: 1),

                new BuildingDefinition(
                    "outpost", "Posto Avancado", new List<TerrainType>(), 10, 1, 3,
                    populationCapacity: 2),

                new BuildingDefinition(
                    "watchtower", "Torre de Vigia", new List<TerrainType>(), 30, 0, 7,
                    workersRequired: 1)
            };
        }

        public static List<CardDefinition> Cards()
        {
            return new List<CardDefinition>
            {
                new CardDefinition("card_harvest", "Colheita", 1, TargetRequirement.None,
                    new List<IEffect> { new GainGoldEffect(12) }),

                new CardDefinition("card_rally", "Convocacao", 1, TargetRequirement.None,
                    new List<IEffect> { new AddDefenseEffect(5) }),

                new CardDefinition("card_barricade", "Barricada", 2, TargetRequirement.None,
                    new List<IEffect> { new AddDefenseEffect(12) }),

                new CardDefinition("card_lumberjack", "Lenhador", 1, TargetRequirement.None,
                    new List<IEffect> { new GainResourceEffect(ResourceKind.Wood, 6) }),

                new CardDefinition("card_quarry", "Cantaria", 1, TargetRequirement.None,
                    new List<IEffect> { new GainResourceEffect(ResourceKind.Stone, 6) }),

                new CardDefinition("card_reap", "Colheita de Grao", 1, TargetRequirement.None,
                    new List<IEffect> { new GainResourceEffect(ResourceKind.Food, 6) }),

                new CardDefinition("card_trade_wood", "Escambo de Madeira", 1, TargetRequirement.None,
                    new List<IEffect>
                    {
                        new LoseResourceEffect(ResourceKind.Wood, 5),
                        new GainGoldEffect(8)
                    }),

                new CardDefinition("card_migrants", "Migrantes", 2, TargetRequirement.None,
                    new List<IEffect> { new RecruitEffect(2) }),

                new CardDefinition("card_refugees", "Refugiados", 3, TargetRequirement.None,
                    new List<IEffect> { new RecruitEffect(3, ignoreCapacity: true) }),

                new CardDefinition("card_stonewall", "Muralha de Pedra", 1, TargetRequirement.None,
                    new List<IEffect>
                    {
                        new LoseResourceEffect(ResourceKind.Stone, 6),
                        new AddDefenseEffect(10)
                    })
            };
        }

        public static List<EventDefinition> Events()
        {
            return new List<EventDefinition>
            {
                EventDefinition.Simple(
                    "event_windfall", "Achado no bosque", EventClass.Positive,
                    new List<IEffect> { new GainResourceEffect(ResourceKind.Wood, 8) }),

                EventDefinition.Simple(
                    "event_lode", "Filao rico", EventClass.Positive,
                    new List<IEffect> { new GainResourceEffect(ResourceKind.Stone, 8) }),

                EventDefinition.Simple(
                    "event_spoilage", "Estrago no celeiro", EventClass.Negative,
                    new List<IEffect> { new LoseResourceEffect(ResourceKind.Food, 0.25, GoldScaling.FractionOfCurrent) }),

                EventDefinition.Simple(
                    "event_tax", "Imposto do reino vizinho", EventClass.Negative,
                    new List<IEffect> { new LoseGoldEffect(0.2, GoldScaling.FractionOfCurrent) })
            };
        }

        /// <summary>
        /// Humanos com populacao inicial (task 11.2). O `humans.asset` real esta sem
        /// nenhuma entrada em `_startingResources`: populacao comeca em zero, e sem
        /// gente nenhuma fazenda/mina/serraria consegue ter trabalhador, entao a
        /// comida nunca sobra pra crescer populacao (RunEngine.ResolvePopulationGrowth
        /// so age com excedente) — impasse que so uma carta de recrutamento sorteada
        /// cedo resolve. Isto e a causa raiz medida pelo simulador (EDIFICIO SEM
        /// GENTE / POPULACAO OCIOSA / COMIDA NO LIMITE mesmo depois do conteudo de
        /// recursos entrar). Precisa do mesmo ajuste no asset real — ver aviso pro
        /// usuario.
        /// </summary>
        public static RaceDefinition Humans()
        {
            ResourceAmounts starting = new ResourceAmounts();
            starting[ResourceKind.Population] = 3;

            // Comida pra sustentar 3 de populacao por ~6 dias: e quanto leva pra
            // comprar a primeira celula, erguer a fazenda e ela produzir — sem
            // buffer nenhum, todo mundo morre de fome no dia 2 antes de a economia
            // ter chance de existir (achado durante 11.2/11.3).
            starting[ResourceKind.Food] = 30;

            return new RaceDefinition("humans", "Humanos", 50, 20, 5, 3, startingResources: starting);
        }

        /// <summary>
        /// Os 3 rivais do nivel 1, espelhando os RivalAsset autorados no Editor
        /// (Rival_Maraures/Besieger/Saboteur) — task 11.2/11.3. Numeros aqui devem
        /// bater com os assets; se um mudar, o outro muda junto.
        /// </summary>
        public static List<RivalDefinition> Level1Rivals()
        {
            RivalIdentity assault = new RivalIdentity("Rival_Maraures", "Saqueadores", RivalAxis.Territory);
            RivalIdentity siege = new RivalIdentity("Rival_Besieger", "Sitiantes", RivalAxis.Integrity);
            RivalIdentity economic = new RivalIdentity(
                "Rival_Saboteur", "Sabotadores", RivalAxis.Resource, ResourceKind.Food);

            return new List<RivalDefinition>
            {
                new RivalDefinition("Rival_Maraures", "Saqueadores", assault, 4,
                    new ThreatCurve(baseForce: 2, perDay: 0.8, dayExponent: 1.1)),
                new RivalDefinition("Rival_Besieger", "Sitiantes", siege, 2,
                    new ThreatCurve(baseForce: 5, perDay: 1.3, dayExponent: 1.25)),
                new RivalDefinition("Rival_Saboteur", "Sabotadores", economic, 3,
                    new ThreatCurve(baseForce: 2, perDay: 0.8, dayExponent: 1.15))
            };
        }

        /// <summary>Catalogo completo, com tudo liberado desde o inicio — o simulador
        /// mede o jogo pronto, nao a arvore de meta.</summary>
        public static ContentCatalog Catalog(RaceDefinition race = null)
        {
            RaceDefinition chosen = race ?? Humans();
            ContentCatalog catalog = new ContentCatalog();
            catalog.AddRace(chosen, true);

            List<BuildingDefinition> buildings = Buildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                catalog.AddBuilding(buildings[i], true);
            }

            List<CardDefinition> cards = Cards();
            for (int i = 0; i < cards.Count; i++)
            {
                catalog.AddCard(cards[i], true);
            }

            List<EventDefinition> events = Events();
            for (int i = 0; i < events.Count; i++)
            {
                catalog.AddEvent(events[i], true);
            }

            return catalog;
        }

        /// <summary>
        /// Mesmo catalogo, so que sem nenhum edificio que produza recurso (fica so
        /// Salao/Posto/Torre) — task 11.3: prova que empilhar so defesa perde para o
        /// rival de eixo Resource, que cobra pedagio independente de defesa (design
        /// D4).
        /// </summary>
        public static ContentCatalog TowersOnlyCatalog(RaceDefinition race = null)
        {
            RaceDefinition chosen = race ?? Humans();
            ContentCatalog catalog = new ContentCatalog();
            catalog.AddRace(chosen, true);

            catalog.AddBuilding(Buildings().Find(b => b.Id == "hall"), true);
            catalog.AddBuilding(Buildings().Find(b => b.Id == "outpost"), true);
            catalog.AddBuilding(Buildings().Find(b => b.Id == "watchtower"), true);

            // So cartas de ouro/defesa: cartas de recurso (lenhador, cantaria,
            // colheita de grao...) dariam comida sem depender de fazenda nenhuma,
            // escondendo exatamente a fraqueza que este catalogo existe pra medir.
            HashSet<string> resourceCardIds = new HashSet<string>
            {
                "card_lumberjack", "card_quarry", "card_reap", "card_trade_wood",
                "card_migrants", "card_refugees", "card_stonewall"
            };

            List<CardDefinition> cards = Cards();
            for (int i = 0; i < cards.Count; i++)
            {
                if (!resourceCardIds.Contains(cards[i].Id))
                {
                    catalog.AddCard(cards[i], true);
                }
            }

            return catalog;
        }
    }
}

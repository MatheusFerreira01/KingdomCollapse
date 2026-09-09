using System.Collections.Generic;
using System.IO;
using KingdomCollapse.Core;
using KingdomCollapse.Game;
using UnityEditor;
using UnityEngine;

namespace KingdomCollapse.EditorTools
{
    /// <summary>
    /// Cria os assets de conteudo inicial pela API do Unity, em vez de o conteudo ser
    /// digitado campo a campo no Inspector.
    ///
    /// Gera pela API e nao escrevendo o YAML na mao de proposito: o arquivo .asset
    /// referencia o script por GUID e fileID, e errar um deles produz um asset com
    /// "script missing" que nao aponta a causa. Aqui o Unity resolve as referencias.
    ///
    /// E um semeador de uma vez so: depois de gerado, o conteudo e dos assets, e
    /// ajustar numero e texto acontece no Inspector. Rodar de novo nao sobrescreve o
    /// que ja existe.
    /// </summary>
    public static class ContentSeeder
    {
        private const string DataFolder = "Assets/Data";

        [MenuItem("Kingdom Collapse/Gerar conteudo inicial")]
        public static void Seed()
        {
            if (!AssetDatabase.IsValidFolder(DataFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Data");
            }

            List<CardAsset> cards = SeedCards();
            List<WeatherAsset> weather = SeedWeather();
            List<EventAsset> events = SeedEvents(cards);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Register(cards, weather, events);

            Debug.Log(
                "Conteudo inicial gerado: " + cards.Count + " cartas, " + weather.Count +
                " climas, " + events.Count + " eventos.");

            ReportViolations();
        }

        /// <summary>
        /// Valida logo apos gerar. Descobrir na hora que um evento furou o orcamento
        /// vale mais que descobrir depois de montar a run e nao entender o numero.
        /// </summary>
        private static void ReportViolations()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameDatabase");
            if (guids.Length == 0)
            {
                return;
            }

            GameDatabase database = AssetDatabase.LoadAssetAtPath<GameDatabase>(
                AssetDatabase.GUIDToAssetPath(guids[0]));

            List<CatalogViolation> violations = new List<CatalogViolation>();
            violations.AddRange(EventCatalogValidator.Validate(database.BuildEventDefinitions()));
            violations.AddRange(WeatherCatalogValidator.Validate(database.BuildCatalog().Weather));
            violations.AddRange(MetaCatalogValidator.Validate(database.BuildMetaTree()));

            if (violations.Count == 0)
            {
                Debug.Log("Catalogo valido: nenhum conteudo fora do orcamento.", database);
                return;
            }

            for (int i = 0; i < violations.Count; i++)
            {
                Debug.LogError(violations[i].ToString(), database);
            }
        }

        // --- Utilidades ---

        private static T GetOrCreate<T>(string id) where T : ScriptableObject
        {
            string path = Path.Combine(DataFolder, id + ".asset").Replace('\\', '/');
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);

            if (existing != null)
            {
                return existing;
            }

            T created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static EffectEntry Effect(
            EffectEntryKind kind,
            float amount = 0,
            GoldScaling scaling = GoldScaling.Flat,
            int days = 1,
            bool asFraction = false,
            bool asMultiplier = false,
            int revealCount = 0,
            CardAsset card = null,
            TerrainType targetTerrain = TerrainType.Plain,
            bool requireSourceTerrain = false)
        {
            return new EffectEntry
            {
                Kind = kind,
                Amount = amount,
                Scaling = scaling,
                Days = days,
                AsFraction = asFraction,
                AsMultiplier = asMultiplier,
                RevealCount = revealCount,
                Card = card,
                TargetTerrain = targetTerrain,
                RequireSourceTerrain = requireSourceTerrain
            };
        }

        private static List<EffectEntry> Effects(params EffectEntry[] entries)
        {
            return new List<EffectEntry>(entries);
        }

        // --- Cartas ---

        private static List<CardAsset> SeedCards()
        {
            List<CardAsset> cards = new List<CardAsset>();

            cards.Add(Card("card_harvest", "Colheita", "Ganha 12 de ouro.", 1,
                TargetRequirement.None, Effects(Effect(EffectEntryKind.GainGold, 12))));

            cards.Add(Card("card_levy", "Arrecadacao", "Ganha 4 de ouro por celula possuida.", 1,
                TargetRequirement.None,
                Effects(Effect(EffectEntryKind.GainGold, 4, GoldScaling.PerOwnedTile))));

            cards.Add(Card("card_rally", "Convocacao", "Ganha 5 de defesa ate o proximo ataque.", 1,
                TargetRequirement.None, Effects(Effect(EffectEntryKind.AddDefense, 5))));

            cards.Add(Card("card_barricade", "Barricada", "Ganha 12 de defesa ate o proximo ataque.", 2,
                TargetRequirement.None, Effects(Effect(EffectEntryKind.AddDefense, 12))));

            cards.Add(Card("card_recruit", "Recrutamento", "Ganha 3 de defesa. Nao custa energia.", 0,
                TargetRequirement.None, Effects(Effect(EffectEntryKind.AddDefense, 3))));

            cards.Add(Card("card_scouts", "Batedores", "Revela 3 celulas da fronteira.", 0,
                TargetRequirement.None, Effects(Effect(EffectEntryKind.RevealTile, revealCount: 3))));

            cards.Add(Card("card_survey", "Prospeccao", "Revela a celula alvo.", 1,
                TargetRequirement.AnyTile, Effects(Effect(EffectEntryKind.RevealTile))));

            cards.Add(Card("card_groundwork", "Terraplanagem",
                "O terreno da celula alvo vira planicie.", 2,
                TargetRequirement.BuildableTile,
                Effects(Effect(EffectEntryKind.SetTerrain, targetTerrain: TerrainType.Plain,
                    requireSourceTerrain: false))));

            cards.Add(Card("card_rebuild", "Reconstruir", "Repara a celula arrasada alvo.", 1,
                TargetRequirement.DestroyedTile, Effects(Effect(EffectEntryKind.RepairTile))));

            cards.Add(Card("card_overtime", "Mutirao", "Dobra a producao de amanha.", 1,
                TargetRequirement.None,
                Effects(Effect(EffectEntryKind.ModifyProduction, 1f, days: 2, asMultiplier: true))));

            cards.Add(Card("card_stockpile", "Estoque", "+5 de producao por 3 dias.", 1,
                TargetRequirement.None,
                Effects(Effect(EffectEntryKind.ModifyProduction, 5f, days: 3))));

            cards.Add(Card("card_claim", "Reivindicar", "Ganha a celula compravel alvo de graca.", 2,
                TargetRequirement.PurchasableTile, Effects(Effect(EffectEntryKind.GrantTile))));

            cards.Add(Card("card_inspire", "Inspirar", "Compra 2 cartas.", 1,
                TargetRequirement.None, Effects(Effect(EffectEntryKind.DrawCards, 2))));

            cards.Add(Card("card_emergency", "Obras de Emergencia", "Recupera 5 de integridade.", 2,
                TargetRequirement.None, Effects(Effect(EffectEntryKind.RepairBase, 5))));

            cards.Add(Card("card_fortify", "Fortificar",
                "Ganha 8 de defesa e 1 de integridade.", 2, TargetRequirement.None,
                Effects(
                    Effect(EffectEntryKind.AddDefense, 8),
                    Effect(EffectEntryKind.RepairBase, 1))));

            return cards;
        }

        private static CardAsset Card(
            string id, string name, string rules, int energy,
            TargetRequirement target, List<EffectEntry> effects)
        {
            CardAsset asset = GetOrCreate<CardAsset>(id);
            asset.EditorConfigure(name, rules, energy, target, effects);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        // --- Clima ---

        private static List<WeatherAsset> SeedWeather()
        {
            List<WeatherAsset> weather = new List<WeatherAsset>();

            weather.Add(Weather("weather_sun", "Sol",
                "O sol seca os campos e apressa a colheita.",
                new Dictionary<TerrainType, int> { { TerrainType.Plain, 1 }, { TerrainType.River, -1 } },
                weight: 1.2f));

            weather.Add(Weather("weather_rain", "Chuva",
                "A chuva enche os rios e atola as estradas.",
                new Dictionary<TerrainType, int> { { TerrainType.River, 2 }, { TerrainType.Forest, 1 } },
                buildCostMultiplier: 1.25f,
                threatDelayDays: 1,
                weight: 1.2f));

            weather.Add(Weather("weather_drought", "Seca",
                "A terra racha, mas a rocha fica exposta.",
                new Dictionary<TerrainType, int> { { TerrainType.Mine, 2 }, { TerrainType.Plain, -2 } },
                weight: 0.8f));

            weather.Add(Weather("weather_storm", "Tempestade",
                "Ninguem trabalha, e ninguem marcha.",
                null,
                productionMultiplier: -0.25f,
                baseDamage: 1,
                threatForceDelta: -4,
                weight: 0.7f));

            weather.Add(Weather("weather_fog", "Neblina",
                "Sem horizonte, resta cuidar do que ja e seu.",
                null,
                energyDelta: 1,
                blocksPurchase: true,
                blocksReveal: true,
                weight: 0.7f));

            weather.Add(Weather("weather_calm", "Dia limpo",
                "Nada de especial. E o que faz os outros dias pesarem.",
                null,
                weight: 1.4f,
                isCalmDay: true));

            return weather;
        }

        private static WeatherAsset Weather(
            string id, string name, string flavor,
            Dictionary<TerrainType, int> terrain,
            float productionMultiplier = 0f,
            float buildCostMultiplier = 1f,
            int energyDelta = 0,
            int baseDamage = 0,
            bool blocksPurchase = false,
            bool blocksReveal = false,
            int threatForceDelta = 0,
            int threatDelayDays = 0,
            float weight = 1f,
            bool isCalmDay = false)
        {
            WeatherAsset asset = GetOrCreate<WeatherAsset>(id);
            asset.EditorConfigure(
                name, flavor, terrain, productionMultiplier, buildCostMultiplier, energyDelta,
                baseDamage, blocksPurchase, blocksReveal, threatForceDelta, threatDelayDays,
                weight, isCalmDay);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        // --- Eventos ---

        private static List<EventAsset> SeedEvents(List<CardAsset> cards)
        {
            List<EventAsset> events = new List<EventAsset>();
            CardAsset rallyCard = cards.Find(c => c.Id == "card_rally");
            CardAsset harvestCard = cards.Find(c => c.Id == "card_harvest");

            // --- Positivos ---

            events.Add(Simple("event_caravan", "Caravana de mercadores", EventClass.Positive,
                "Rodas rangem na estrada e param diante do Salao.",
                Effects(Effect(EffectEntryKind.GainGold, 6, GoldScaling.PerOwnedTile))));

            events.Add(Simple("event_harvest", "Safra farta", EventClass.Positive,
                "Os celeiros nao dao conta.",
                Effects(Effect(EffectEntryKind.ModifyProduction, 1f, days: 2, asMultiplier: true))));

            events.Add(Simple("event_refugees", "Refugiados", EventClass.Positive,
                "Chegam com pouco, e com uma ideia.",
                Effects(Effect(EffectEntryKind.AddCardToDeck, card: harvestCard))));

            events.Add(Simple("event_vein", "Veio exposto", EventClass.Positive,
                "A erosao mostrou o que estava embaixo.",
                Effects(Effect(EffectEntryKind.RevealTile, revealCount: 4))));

            events.Add(Simple("event_ceded", "Terreno cedido", EventClass.Positive,
                "Um vizinho decide que aquela faixa nao vale a briga.",
                Effects(Effect(EffectEntryKind.GrantTile))));

            events.Add(Simple("event_walls", "Reforco de muralha", EventClass.Positive,
                "Os pedreiros trabalharam de graca, por medo.",
                Effects(Effect(EffectEntryKind.AddDefense, 8))));

            // --- Neutros ---

            events.Add(Simple("event_flood", "Cheia do rio", EventClass.Neutral,
                "A agua encontrou caminho novo.",
                Effects(Effect(EffectEntryKind.SetTerrain, targetTerrain: TerrainType.River,
                    requireSourceTerrain: false))));

            events.Add(Simple("event_dig", "Escavacao", EventClass.Neutral,
                "Sob a ruina havia rocha boa.",
                Effects(Effect(EffectEntryKind.SetTerrain, targetTerrain: TerrainType.Mine,
                    requireSourceTerrain: false))));

            events.Add(Simple("event_migration", "Migracao", EventClass.Neutral,
                "Gente nova, ideias novas, as antigas vao embora.",
                Effects(Effect(EffectEntryKind.DrawCards, 2))));

            events.Add(Simple("event_survey", "Levantamento", EventClass.Neutral,
                "Um cartografo passa a tarde no alto da torre.",
                Effects(Effect(EffectEntryKind.RevealTile, revealCount: 2))));

            // --- Ofertas: o mercado do jogo ---

            events.Add(Offer("event_mercenaries", "Mercenarios a porta", EventClass.Neutral,
                "Eles cobram caro e nao ficam para o segundo ataque.",
                "Recusar", Effects(),
                "Contratar (30% do ouro)",
                Effects(
                    Effect(EffectEntryKind.LoseGold, 0.3f, GoldScaling.FractionOfCurrent),
                    Effect(EffectEntryKind.AddDefense, 14))));

            events.Add(Offer("event_peddler", "Mascate", EventClass.Neutral,
                "Ele abre a mala e espera.",
                "Deixar passar", Effects(),
                "Comprar a carta (25% do ouro)",
                Effects(
                    Effect(EffectEntryKind.LoseGold, 0.25f, GoldScaling.FractionOfCurrent),
                    Effect(EffectEntryKind.AddCardToDeck, card: rallyCard))));

            events.Add(Offer("event_engineer", "Engenheiro itinerante", EventClass.Neutral,
                "Ele conserta o que a horda quebrou, por um preco.",
                "Dispensar", Effects(),
                "Pagar a obra (35% do ouro)",
                Effects(
                    Effect(EffectEntryKind.LoseGold, 0.35f, GoldScaling.FractionOfCurrent),
                    Effect(EffectEntryKind.RepairTile),
                    Effect(EffectEntryKind.RepairBase, 3))));

            events.Add(Offer("event_surveyor", "Agrimensor", EventClass.Positive,
                "Por uma taxa, ele marca a terra como sua.",
                "Recusar", Effects(),
                "Pagar a demarcacao (40% do ouro)",
                Effects(
                    Effect(EffectEntryKind.LoseGold, 0.4f, GoldScaling.FractionOfCurrent),
                    Effect(EffectEntryKind.GrantTile))));

            // --- Negativos ---

            events.Add(Simple("event_tax", "Imposto do reino vizinho", EventClass.Negative,
                "O cobrador vem acompanhado.",
                Effects(Effect(EffectEntryKind.LoseGold, 0.2f, GoldScaling.FractionOfCurrent))));

            events.Add(Simple("event_plague", "Praga", EventClass.Negative,
                "Um edificio fecha as portas por um dia.",
                Effects(Effect(EffectEntryKind.DisableTile, days: 2))));

            events.Add(Simple("event_desertion", "Desercao", EventClass.Negative,
                "Alguem levou os planos junto.",
                Effects(Effect(EffectEntryKind.RemoveCard))));

            events.Add(Simple("event_erosion", "Erosao", EventClass.Negative,
                "A borda do territorio cede. So a borda vazia.",
                Effects(Effect(EffectEntryKind.DestroyTile))));

            events.Add(Simple("event_fire", "Incendio pequeno", EventClass.Negative,
                "Contido antes de chegar ao Salao.",
                Effects(Effect(EffectEntryKind.DamageBase, 0.1f, asFraction: true))));

            events.Add(Simple("event_sabotage", "Sabotagem", EventClass.Negative,
                "As ferramentas amanhecem quebradas.",
                Effects(Effect(EffectEntryKind.ModifyProduction, -0.2f, days: 2, asMultiplier: true))));

            return events;
        }

        private static EventAsset Simple(
            string id, string name, EventClass eventClass, string flavor, List<EffectEntry> effects)
        {
            EventAsset asset = GetOrCreate<EventAsset>(id);
            asset.EditorConfigure(
                name, flavor, eventClass,
                new List<EventAsset.Option>
                {
                    new EventAsset.Option { Label = "Continuar", Effects = effects }
                });
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static EventAsset Offer(
            string id, string name, EventClass eventClass, string flavor,
            string declineLabel, List<EffectEntry> declineEffects,
            string offerLabel, List<EffectEntry> offerEffects)
        {
            EventAsset asset = GetOrCreate<EventAsset>(id);
            asset.EditorConfigure(
                name, flavor, eventClass,
                new List<EventAsset.Option>
                {
                    new EventAsset.Option { Label = declineLabel, Effects = declineEffects },
                    new EventAsset.Option { Label = offerLabel, IsOffer = true, Effects = offerEffects }
                },
                cooldownDays: 4);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        // --- Registro no GameDatabase ---

        private static void Register(
            List<CardAsset> cards, List<WeatherAsset> weather, List<EventAsset> events)
        {
            string[] guids = AssetDatabase.FindAssets("t:GameDatabase");
            if (guids.Length == 0)
            {
                Debug.LogWarning("Nenhum GameDatabase encontrado: registre o conteudo novo a mao.");
                return;
            }

            GameDatabase database = AssetDatabase.LoadAssetAtPath<GameDatabase>(
                AssetDatabase.GUIDToAssetPath(guids[0]));

            SerializedObject serialized = new SerializedObject(database);

            // Cartas e eventos entram tambem nas listas de "desbloqueado desde o
            // inicio": conteudo fora delas existe mas nunca aparece numa run, e o
            // silencio disso ja custou uma rodada de diagnostico.
            Fill(serialized, "_cards", cards);
            Fill(serialized, "_startingCards", cards);
            Fill(serialized, "_events", events);
            Fill(serialized, "_startingEvents", events);
            Fill(serialized, "_weather", weather);

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
        }

        private static void Fill<T>(SerializedObject serialized, string field, List<T> values)
            where T : Object
        {
            SerializedProperty list = serialized.FindProperty(field);
            if (list == null)
            {
                Debug.LogWarning("Campo '" + field + "' nao encontrado no GameDatabase.");
                return;
            }

            HashSet<Object> present = new HashSet<Object>();
            for (int i = 0; i < list.arraySize; i++)
            {
                Object existing = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (existing != null)
                {
                    present.Add(existing);
                }
            }

            for (int i = 0; i < values.Count; i++)
            {
                if (present.Contains(values[i]))
                {
                    continue;
                }

                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = values[i];
            }
        }
    }
}

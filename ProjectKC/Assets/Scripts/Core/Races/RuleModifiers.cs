using System;
using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Nomes dos modificadores de regra que as racas podem declarar (design D8).
    /// Um modificador ausente significa comportamento padrao, nunca erro: e o que
    /// permite adicionar uma raca nova sem tocar nos sistemas existentes.
    /// </summary>
    public static class RuleKeys
    {
        /// <summary>Multiplica o custo de comprar uma celula. Padrao 1.</summary>
        public const string TileCostMultiplier = "tile_cost_multiplier";

        /// <summary>Raio maximo de Manhattan a partir do Salao. Padrao 0 = sem limite.</summary>
        public const string MaxDistanceFromHall = "max_distance_from_hall";

        /// <summary>Fracao do ouro perdida num dia sem combate nem saque. Padrao 0.</summary>
        public const string StagnationGoldLossFraction = "stagnation_gold_loss_fraction";

        /// <summary>Producao passiva de floresta possuida sem edificio. Padrao 0.</summary>
        public const string ForestPassiveProduction = "forest_passive_production";

        /// <summary>Dias extras ate um edificio ficar pronto. Padrao 0.</summary>
        public const string BuildDelayDays = "build_delay_days";

        /// <summary>Cartas compradas alem do tamanho de mao base. Padrao 0.</summary>
        public const string ExtraCardsPerDay = "extra_cards_per_day";

        /// <summary>Comida consumida por habitante por dia. Padrao 1.</summary>
        public const string FoodPerPopulation = "food_per_population";

        /// <summary>Fracao da populacao perdida por unidade de comida em falta. Padrao 1.</summary>
        public const string StarvationSeverity = "starvation_severity";

        /// <summary>Comida excedente necessaria para crescer um habitante. Padrao 5.</summary>
        public const string FoodPerGrowth = "food_per_growth";

        /// <summary>Multiplica a defesa vinda de edificios guarnecidos. Padrao 1.</summary>
        public const string GarrisonDefenseMultiplier = "garrison_defense_multiplier";

        // --- Pontos de extensao por raca ---
        //
        // Existem para que raca continue sendo verbo, e nao multiplicador. Sem eles,
        // Orcs que se alimentam do saque exigiriam abrir o calculo de ataque no meio,
        // e Elfos que so vivem de floresta exigiriam abrir o de producao. Cada um e
        // consultado por nome: chave ausente significa comportamento padrao.

        /// <summary>Recurso ganho a cada ataque repelido. Ex.: "plunder_food".</summary>
        public static string Plunder(ResourceKind kind)
        {
            return "plunder_" + kind.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// Recurso extra por quadrado possuido de um terreno, mesmo sem edificio.
        /// Ex.: "terrain_forest_wood".
        /// </summary>
        public static string TerrainYield(TerrainType terrain, ResourceKind kind)
        {
            return "terrain_" + terrain.ToString().ToLowerInvariant() + "_" +
                   kind.ToString().ToLowerInvariant();
        }
    }

    /// <summary>
    /// Conjunto de modificadores consultado pelos sistemas. Guarda tudo como double
    /// e expoe leitura tipada: o custo de uma chave desconhecida e sempre o padrao
    /// passado pelo chamador, entao nenhum sistema quebra por falta de declaracao.
    /// </summary>
    public sealed class RuleModifiers
    {
        private readonly Dictionary<string, double> _values;

        public RuleModifiers(IReadOnlyDictionary<string, double> values = null)
        {
            _values = new Dictionary<string, double>();
            if (values == null)
            {
                return;
            }

            foreach (KeyValuePair<string, double> pair in values)
            {
                _values[pair.Key] = pair.Value;
            }
        }

        /// <summary>Baseline: nenhum modificador declarado.</summary>
        public static readonly RuleModifiers None = new RuleModifiers();

        public bool Has(string key) => _values.ContainsKey(key);

        public double GetDouble(string key, double fallback)
        {
            return _values.TryGetValue(key, out double value) ? value : fallback;
        }

        public int GetInt(string key, int fallback)
        {
            return _values.TryGetValue(key, out double value) ? (int)value : fallback;
        }

        public bool GetBool(string key, bool fallback)
        {
            return _values.TryGetValue(key, out double value) ? value != 0 : fallback;
        }

        public IReadOnlyDictionary<string, double> Values => _values;

        /// <summary>
        /// Le um conjunto de recursos declarado por chaves nomeadas. Devolve vazio
        /// quando nenhuma chave existe, que e o caso da maioria das racas.
        /// </summary>
        public ResourceAmounts ResourcesBy(Func<ResourceKind, string> keyFor)
        {
            ResourceAmounts amounts = new ResourceAmounts();

            for (int i = 0; i < Resources.All.Length; i++)
            {
                ResourceKind kind = Resources.All[i];
                int value = GetInt(keyFor(kind), 0);
                if (value != 0)
                {
                    amounts[kind] = value;
                }
            }

            return amounts;
        }

        /// <summary>Recursos ganhos ao repelir um ataque. Vazio para quem nao saqueia.</summary>
        public ResourceAmounts PlunderOnRepel() => ResourcesBy(RuleKeys.Plunder);

        /// <summary>Recursos que um terreno possuido rende por si so.</summary>
        public ResourceAmounts TerrainYield(TerrainType terrain)
        {
            return ResourcesBy(kind => RuleKeys.TerrainYield(terrain, kind));
        }
    }
}

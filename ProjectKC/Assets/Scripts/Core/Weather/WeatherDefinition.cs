using System;
using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// A condicao natural de um dia. Sempre traz vantagem e desvantagem juntas, o
    /// que a distingue do evento de fim de dia: evento tem classe (bom, neutro ou
    /// ruim), clima tem os dois lados por definicao (design D12).
    /// </summary>
    public sealed class WeatherDefinition
    {
        public WeatherDefinition(
            string id,
            string displayName,
            IReadOnlyDictionary<TerrainType, int> terrainProduction = null,
            double productionMultiplier = 0,
            double buildCostMultiplier = 1.0,
            int energyDelta = 0,
            int baseDamage = 0,
            bool blocksPurchase = false,
            bool blocksReveal = false,
            int threatForceDelta = 0,
            int threatDelayDays = 0,
            double weight = 1.0,
            string flavorText = null,
            bool isCalmDay = false,
            double foodUpkeepMultiplier = 1.0)
        {
            FoodUpkeepMultiplier = foodUpkeepMultiplier;
            Id = id;
            DisplayName = displayName;
            TerrainProduction = terrainProduction ?? new Dictionary<TerrainType, int>();
            ProductionMultiplier = productionMultiplier;
            BuildCostMultiplier = buildCostMultiplier;
            EnergyDelta = energyDelta;
            BaseDamage = baseDamage;
            BlocksPurchase = blocksPurchase;
            BlocksReveal = blocksReveal;
            ThreatForceDelta = threatForceDelta;
            ThreatDelayDays = threatDelayDays;
            Weight = weight;
            FlavorText = flavorText ?? string.Empty;
            IsCalmDay = isCalmDay;
        }

        public string Id { get; }

        public string DisplayName { get; }

        /// <summary>Ouro somado por quadrado produtivo de cada terreno. Pode ser negativo.</summary>
        public IReadOnlyDictionary<TerrainType, int> TerrainProduction { get; }

        /// <summary>Somado a 1 no multiplicador do dia. 0,2 significa +20%.</summary>
        public double ProductionMultiplier { get; }

        public double BuildCostMultiplier { get; }

        public int EnergyDelta { get; }

        /// <summary>Dano a base. Limitado e nunca letal (spec weather).</summary>
        public int BaseDamage { get; }

        public bool BlocksPurchase { get; }

        public bool BlocksReveal { get; }

        /// <summary>
        /// Ajuste na forca das ameacas que chegam neste dia. Negativo enfraquece.
        /// Aplicado na previsao, nunca na chegada (design D13).
        /// </summary>
        public int ThreatForceDelta { get; }

        /// <summary>Dias de adiamento das ameacas que chegariam neste dia.</summary>
        public int ThreatDelayDays { get; }

        /// <summary>
        /// Multiplica o consumo de comida do dia. Frio faz comer mais; o efeito entra
        /// no consumo, e nao na producao, porque sao coisas que o jogador responde de
        /// formas diferentes: estoque contra consumo, terreno contra producao.
        /// </summary>
        public double FoodUpkeepMultiplier { get; }

        public double Weight { get; }

        public string FlavorText { get; }

        /// <summary>
        /// Dia limpo: unico clima autorizado a nao ter os dois lados. Existe para dar
        /// respiro, e e o que faz os outros climas pesarem por contraste.
        /// </summary>
        public bool IsCalmDay { get; }

        public bool TouchesThreats => ThreatForceDelta != 0 || ThreatDelayDays != 0;

        public int ProductionFor(TerrainType terrain)
        {
            return TerrainProduction.TryGetValue(terrain, out int bonus) ? bonus : 0;
        }

        /// <summary>Algo que ajuda o jogador neste clima.</summary>
        public bool HasUpside()
        {
            if (ProductionMultiplier > 0 || EnergyDelta > 0 || BuildCostMultiplier < 1.0)
            {
                return true;
            }

            if (FoodUpkeepMultiplier < 1.0)
            {
                return true;
            }

            if (ThreatForceDelta < 0 || ThreatDelayDays > 0)
            {
                return true;
            }

            foreach (KeyValuePair<TerrainType, int> pair in TerrainProduction)
            {
                if (pair.Value > 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Algo que atrapalha o jogador neste clima.</summary>
        public bool HasDownside()
        {
            if (ProductionMultiplier < 0 || EnergyDelta < 0 || BuildCostMultiplier > 1.0)
            {
                return true;
            }

            if (FoodUpkeepMultiplier > 1.0)
            {
                return true;
            }

            if (BaseDamage > 0 || BlocksPurchase || BlocksReveal)
            {
                return true;
            }

            if (ThreatForceDelta > 0 || ThreatDelayDays < 0)
            {
                return true;
            }

            foreach (KeyValuePair<TerrainType, int> pair in TerrainProduction)
            {
                if (pair.Value < 0)
                {
                    return true;
                }
            }

            return false;
        }

        public override string ToString() => Id;
    }

    /// <summary>
    /// Teto de severidade do clima, análogo ao dos eventos. Clima acontece todo dia,
    /// entao um teto frouxo aqui pesaria muito mais que num evento esporadico.
    /// </summary>
    public static class WeatherLimits
    {
        /// <summary>Variacao maxima do multiplicador de producao, para mais ou para menos.</summary>
        public const double MaxProductionSwing = 0.35;

        /// <summary>Variacao maxima do custo de construcao.</summary>
        public const double MaxBuildCostSwing = 0.50;

        /// <summary>Variacao maxima do consumo de comida.</summary>
        public const double MaxFoodUpkeepSwing = 0.50;

        public const int MaxBaseDamage = 2;

        public const int MaxEnergyDelta = 1;

        public const int MaxThreatDelayDays = 2;

        /// <summary>Clima nunca fortalece ameaca alem disto, nem enfraquece alem do simetrico.</summary>
        public const int MaxThreatForceSwing = 6;
    }

    /// <summary>
    /// Verifica o catalogo de climas. Mesma ideia da validacao de eventos: um clima
    /// fora do teto deve reprovar a suite quando for criado, e nao virar reclamacao
    /// de jogador depois.
    /// </summary>
    public static class WeatherCatalogValidator
    {
        public static List<CatalogViolation> Validate(IEnumerable<WeatherDefinition> catalog)
        {
            List<CatalogViolation> violations = new List<CatalogViolation>();

            foreach (WeatherDefinition weather in catalog)
            {
                if (weather == null)
                {
                    continue;
                }

                if (!weather.IsCalmDay && (!weather.HasUpside() || !weather.HasDownside()))
                {
                    violations.Add(new CatalogViolation(
                        weather.Id,
                        "dois lados",
                        "clima precisa de vantagem e desvantagem; so o dia limpo e isento"));
                }

                if (Math.Abs(weather.ProductionMultiplier) > WeatherLimits.MaxProductionSwing)
                {
                    violations.Add(new CatalogViolation(
                        weather.Id, "producao",
                        "multiplicador " + weather.ProductionMultiplier.ToString("0.##") +
                        " excede o teto de " + WeatherLimits.MaxProductionSwing));
                }

                if (Math.Abs(weather.BuildCostMultiplier - 1.0) > WeatherLimits.MaxBuildCostSwing)
                {
                    violations.Add(new CatalogViolation(
                        weather.Id, "custo de obra",
                        "multiplicador " + weather.BuildCostMultiplier.ToString("0.##") + " excede o teto"));
                }

                if (Math.Abs(weather.FoodUpkeepMultiplier - 1.0) > WeatherLimits.MaxFoodUpkeepSwing)
                {
                    violations.Add(new CatalogViolation(
                        weather.Id, "consumo de comida",
                        "multiplicador " + weather.FoodUpkeepMultiplier.ToString("0.##") +
                        " excede o teto"));
                }

                if (weather.BaseDamage > WeatherLimits.MaxBaseDamage)
                {
                    violations.Add(new CatalogViolation(
                        weather.Id, "dano",
                        "dano " + weather.BaseDamage + " excede o teto de " + WeatherLimits.MaxBaseDamage));
                }

                if (Math.Abs(weather.EnergyDelta) > WeatherLimits.MaxEnergyDelta)
                {
                    violations.Add(new CatalogViolation(weather.Id, "energia", "variacao de energia alta demais"));
                }

                if (Math.Abs(weather.ThreatForceDelta) > WeatherLimits.MaxThreatForceSwing)
                {
                    violations.Add(new CatalogViolation(
                        weather.Id, "ameaca", "altera a forca da ameaca alem do teto"));
                }

                if (weather.ThreatDelayDays < 0 || weather.ThreatDelayDays > WeatherLimits.MaxThreatDelayDays)
                {
                    violations.Add(new CatalogViolation(
                        weather.Id, "adiamento",
                        "adiamento deve ficar entre 0 e " + WeatherLimits.MaxThreatDelayDays +
                        "; clima nao antecipa ataque"));
                }
            }

            return violations;
        }
    }
}

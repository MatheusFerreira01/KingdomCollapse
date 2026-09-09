using System;
using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    public enum DayPhase
    {
        DayStart = 0,
        Planning = 1,
        Resolution = 2,
        Event = 3,
        DayEnd = 4,
        Collapsed = 5
    }

    public enum CollapseReason
    {
        None = 0,
        IntegrityLost,
        TerritoryLost
    }

    /// <summary>Modificador com prazo. Some sozinho quando os dias acabam.</summary>
    public sealed class TimedModifier
    {
        public TimedModifier(string key, double value, int daysRemaining, Coord? coord = null)
        {
            Key = key;
            Value = value;
            DaysRemaining = daysRemaining;
            Coord = coord;
        }

        public string Key { get; }

        public double Value { get; }

        public int DaysRemaining { get; internal set; }

        public Coord? Coord { get; }
    }

    public static class ModifierKeys
    {
        /// <summary>Somado a producao total do dia.</summary>
        public const string ProductionFlat = "production_flat";

        /// <summary>Multiplica a producao total do dia. 1.0 = dobra.</summary>
        public const string ProductionMultiplier = "production_multiplier";

        /// <summary>A celula alvo nao produz enquanto durar.</summary>
        public const string TileDisabled = "tile_disabled";

        /// <summary>Cartas compradas a mais no proximo Inicio do Dia.</summary>
        public const string ExtraDraw = "extra_draw";

        /// <summary>Energia a menos no proximo Inicio do Dia.</summary>
        public const string EnergyPenalty = "energy_penalty";
    }

    /// <summary>Contadores usados pela pontuacao de Colapso.</summary>
    public sealed class RunStats
    {
        public int DaysSurvived { get; internal set; }

        public int PeakTilesOwned { get; internal set; }

        public int BuildingsBuilt { get; internal set; }

        public int GoldEarned { get; internal set; }

        public int AttacksSurvived { get; internal set; }

        public int MilestonesReached { get; internal set; }

        public int EventsSeen { get; internal set; }
    }

    /// <summary>
    /// Todo o estado de uma partida. Nao referencia Unity (design D1): uma run
    /// inteira pode ser resolvida dentro de um teste em milissegundos.
    /// </summary>
    public sealed class RunState
    {
        private readonly List<TimedModifier> _modifiers = new List<TimedModifier>();

        public RunState(
            RaceDefinition race,
            KingdomGrid grid,
            RunDeck deck,
            RunRandom random,
            ThreatClock threats,
            WeatherSystem weather = null)
        {
            Weather = weather ?? new WeatherSystem();
            Race = race;
            Grid = grid;
            Deck = deck;
            Random = random;
            Threats = threats;

            Day = 1;
            Phase = DayPhase.DayStart;
            Pool = new ResourcePool(race.StartingResources);
            MaxIntegrity = race.StartingIntegrity;
            Integrity = race.StartingIntegrity;
            EnergyPerDay = race.EnergyPerDay;
            Energy = 0;
            HandSize = race.HandSize + race.Rules.GetInt(RuleKeys.ExtraCardsPerDay, 0);
            Stats = new RunStats();
            Stats.PeakTilesOwned = grid.OwnedCount;
        }

        public RaceDefinition Race { get; }

        public RuleModifiers Rules => Race.Rules;

        public KingdomGrid Grid { get; }

        public RunDeck Deck { get; }

        public RunRandom Random { get; }

        public ThreatClock Threats { get; }

        public WeatherSystem Weather { get; }

        /// <summary>Clima vigente, ou nulo se a run roda sem catalogo de clima.</summary>
        public WeatherDefinition TodayWeather => Weather.Today;

        public RunStats Stats { get; }

        public int Day { get; internal set; }

        public DayPhase Phase { get; internal set; }

        /// <summary>Saldo dos cinco recursos do reino.</summary>
        public ResourcePool Pool { get; private set; }

        /// <summary>
        /// Atalho para o ouro. Existe porque ouro continua sendo o recurso liquido,
        /// usado por compra de celula e por oferta de evento; os demais recursos sao
        /// lidos pelo Pool.
        /// </summary>
        public int Gold => Pool[ResourceKind.Gold];

        public int this[ResourceKind kind] => Pool[kind];

        public int Integrity { get; private set; }

        public int MaxIntegrity { get; private set; }

        public int Energy { get; internal set; }

        public int EnergyPerDay { get; internal set; }

        public int HandSize { get; internal set; }

        /// <summary>
        /// Defesa temporaria de hoje. Consumida ao resolver um ataque e descartada no
        /// Fim do Dia: e o que obriga o jogador a preparar no dia certo em vez de
        /// estocar defesa nos dias calmos.
        /// </summary>
        public int PendingDefense { get; private set; }

        public bool IsOver => Phase == DayPhase.Collapsed;

        public CollapseReason Collapse { get; private set; } = CollapseReason.None;

        /// <summary>
        /// Um dia com combate ou saque. Zera no Inicio do Dia; a regra de estagnacao
        /// dos Orcs le este valor no Fim do Dia.
        /// </summary>
        public bool ActedAggressivelyToday { get; internal set; }

        public IReadOnlyList<TimedModifier> Modifiers => _modifiers;

        // --- Ouro ---

        public void AddGold(int amount) => Add(ResourceKind.Gold, amount);

        /// <summary>Retira ouro sem deixar saldo negativo. Devolve quanto saiu de fato.</summary>
        public int RemoveGold(int amount) => Remove(ResourceKind.Gold, amount);

        public bool CanAfford(int amount) => Pool.Has(ResourceKind.Gold, amount);

        // --- Recursos ---

        public void Add(ResourceKind kind, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Pool.Add(kind, amount);

            if (kind == ResourceKind.Gold)
            {
                Stats.GoldEarned += amount;
            }
        }

        public void Add(ResourceAmounts amounts)
        {
            if (amounts == null)
            {
                return;
            }

            foreach (ResourceKind kind in amounts.NonZero())
            {
                int value = amounts[kind];
                if (value > 0)
                {
                    Add(kind, value);
                }
                else
                {
                    Remove(kind, -value);
                }
            }
        }

        public int Remove(ResourceKind kind, int amount) => Pool.Remove(kind, amount);

        public bool CanAfford(ResourceAmounts cost, out ResourceShortage shortage)
        {
            return Pool.CanAfford(cost, out shortage);
        }

        /// <summary>Paga o custo inteiro ou nao paga nada.</summary>
        public bool TrySpend(ResourceAmounts cost, out ResourceShortage shortage)
        {
            return Pool.TrySpend(cost, out shortage);
        }

        // --- Integridade ---

        public void Heal(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Integrity = Math.Min(MaxIntegrity, Integrity + amount);
        }

        /// <summary>
        /// Aplica dano a base. Nao decide o Colapso: quem resolve o dia consulta
        /// CheckCollapse depois, para que a ordem das fases fique num lugar so.
        /// </summary>
        public int Damage(int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            int applied = Math.Min(amount, Integrity);
            Integrity -= applied;
            return applied;
        }

        // --- Defesa ---

        public void AddPendingDefense(int amount)
        {
            if (amount > 0)
            {
                PendingDefense += amount;
            }
        }

        public int ConsumePendingDefense()
        {
            int value = PendingDefense;
            PendingDefense = 0;
            return value;
        }

        /// <summary>
        /// Postura de trabalho: quem recebe gente primeiro quando ela nao da para
        /// todos. E a decisao que faz guarnecer custar producao.
        /// </summary>
        public WorkerStance Stance { get; set; } = WorkerStance.Balanced;

        /// <summary>Alocacao de hoje, derivada do estado atual.</summary>
        public WorkerAllocation Allocation()
        {
            return WorkerAllocation.For(Grid, this[ResourceKind.Population], Stance);
        }

        public int TotalDefense() => ProductionCalculator.TotalDefense(Grid, Allocation()) + PendingDefense;

        /// <summary>Teto de populacao dado pelos edificios em pe.</summary>
        public int PopulationCapacity() => ProductionCalculator.PopulationCapacity(Grid);

        /// <summary>Comida que a populacao consome por dia.</summary>
        public int DailyFoodUpkeep()
        {
            double perHead = Rules.GetDouble(RuleKeys.FoodPerPopulation, DefaultFoodPerPopulation);
            return (int)Math.Ceiling(this[ResourceKind.Population] * perHead);
        }

        /// <summary>Comida consumida por habitante por dia.</summary>
        public const double DefaultFoodPerPopulation = 1.0;

        // --- Modificadores temporarios ---

        public void AddModifier(string key, double value, int days, Coord? coord = null)
        {
            _modifiers.Add(new TimedModifier(key, value, days, coord));
        }

        public double SumModifier(string key, Coord? coord = null)
        {
            double sum = 0;
            for (int i = 0; i < _modifiers.Count; i++)
            {
                TimedModifier modifier = _modifiers[i];
                if (modifier.Key != key)
                {
                    continue;
                }

                if (coord.HasValue && modifier.Coord.HasValue && modifier.Coord.Value != coord.Value)
                {
                    continue;
                }

                if (coord.HasValue && !modifier.Coord.HasValue)
                {
                    continue;
                }

                sum += modifier.Value;
            }

            return sum;
        }

        public bool IsTileDisabled(Coord coord)
        {
            for (int i = 0; i < _modifiers.Count; i++)
            {
                TimedModifier modifier = _modifiers[i];
                if (modifier.Key == ModifierKeys.TileDisabled && modifier.Coord.HasValue && modifier.Coord.Value == coord)
                {
                    return true;
                }
            }

            return false;
        }

        internal void TickModifiers()
        {
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                _modifiers[i].DaysRemaining--;
                if (_modifiers[i].DaysRemaining <= 0)
                {
                    _modifiers.RemoveAt(i);
                }
            }
        }

        // --- Producao ---

        /// <summary>
        /// Producao do dia: soma das celulas, com celulas desabilitadas fora da conta,
        /// depois os modificadores temporarios.
        /// </summary>
        /// <summary>
        /// Producao do dia em todos os recursos. Celula desabilitada, arrasada ou sem
        /// gente nao entra; o clima entra por terreno, e nao no total, que e o que faz
        /// mina, rio e floresta terem personalidade em vez de numeros diferentes.
        /// </summary>
        public ResourceAmounts CollectDailyProductionByResource(
            List<ProductionBreakdown> breakdowns = null)
        {
            WeatherDefinition weather = TodayWeather;
            WorkerAllocation allocation = Allocation();
            ResourceAmounts raw = new ResourceAmounts();

            foreach (Tile tile in Grid.OwnedTilesOrdered())
            {
                if (IsTileDisabled(tile.Coord))
                {
                    continue;
                }

                ProductionBreakdown breakdown = ProductionCalculator.ForTile(
                    Grid, tile, Rules, allocation.IsStaffed(tile.Coord));

                if (weather != null && !breakdown.IsEmpty)
                {
                    int byWeather = weather.ProductionFor(tile.Terrain);
                    if (byWeather != 0)
                    {
                        breakdown.Add(weather.DisplayName, WeatherResourceFor(tile.Terrain), byWeather);
                    }
                }

                breakdowns?.Add(breakdown);
                raw.Add(breakdown.Totals);
            }

            double multiplier = 1.0 + SumModifier(ModifierKeys.ProductionMultiplier)
                                + (weather?.ProductionMultiplier ?? 0);
            int flat = (int)SumModifier(ModifierKeys.ProductionFlat);

            ResourceAmounts total = new ResourceAmounts();
            foreach (ResourceKind kind in Resources.All)
            {
                int scaled = (int)Math.Round(raw[kind] * multiplier, MidpointRounding.AwayFromZero);

                // O modificador plano historicamente valia ouro, e continua valendo.
                if (kind == ResourceKind.Gold)
                {
                    scaled += flat;
                }

                total[kind] = Math.Max(0, scaled);
            }

            return total;
        }

        /// <summary>Qual recurso o clima favorece num terreno.</summary>
        public static ResourceKind WeatherResourceFor(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Forest:
                    return ResourceKind.Wood;
                case TerrainType.Mine:
                    return ResourceKind.Stone;
                case TerrainType.Plain:
                case TerrainType.River:
                    return ResourceKind.Food;
                default:
                    return ResourceKind.Gold;
            }
        }

        /// <summary>Producao do dia em ouro. Atalho para leitura antiga e UI resumida.</summary>
        public int CollectDailyProduction(List<ProductionBreakdown> breakdowns = null)
        {
            return CollectDailyProductionByResource(breakdowns)[ResourceKind.Gold];
        }

        // --- Colapso ---

        /// <summary>
        /// Verifica as duas condicoes de fim: integridade zerada ou territorio perdido.
        /// Chamado depois de cada passo que pode causar perda.
        /// </summary>
        public bool CheckCollapse()
        {
            if (IsOver)
            {
                return true;
            }

            if (Integrity <= 0)
            {
                Collapse = CollapseReason.IntegrityLost;
                Phase = DayPhase.Collapsed;
                return true;
            }

            // Uma celula arrasada continua possuida (spec kingdom-grid), entao o
            // territorio se perde quando nao sobra nenhuma celula de pe, e nao
            // quando a posse chega a zero.
            if (StandingTileCount() <= 0)
            {
                Collapse = CollapseReason.TerritoryLost;
                Phase = DayPhase.Collapsed;
                return true;
            }

            return false;
        }

        /// <summary>Celulas possuidas que ainda nao foram arrasadas.</summary>
        public int StandingTileCount()
        {
            int standing = 0;
            foreach (Tile tile in Grid.OwnedTiles())
            {
                if (!tile.Destroyed)
                {
                    standing++;
                }
            }

            return standing;
        }
    }
}

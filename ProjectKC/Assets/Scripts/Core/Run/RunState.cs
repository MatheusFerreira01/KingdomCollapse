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
        Collapsed = 5,
        Victory = 6
    }

    public enum CollapseReason
    {
        None = 0,
        IntegrityLost,
        TerritoryLost,

        /// <summary>Comida em zero por dias seguidos demais (design D4 seguido ate o
        /// fim: rival de eixo Recurso/Populacao precisa poder matar a run mesmo com
        /// defesa alta, e nao so incomodar para sempre).</summary>
        Starvation,

        /// <summary>Ouro em zero por dias seguidos demais — mesma logica, mas para a
        /// economia geral em vez de comida especificamente.</summary>
        Bankruptcy
    }

    /// <summary>Como a run termina. Vitoria e Colapso sao desfechos de primeira
    /// classe (design D5) — nenhum dos dois e "o outro, mas sem nome".</summary>
    public enum RunOutcome
    {
        Ongoing = 0,
        Victory = 1,
        Collapse = 2
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
            WeatherSystem weather = null,
            RivalCampaignState campaign = null)
        {
            Weather = weather ?? new WeatherSystem();
            Race = race;
            Grid = grid;
            Deck = deck;
            Random = random;
            Threats = threats;
            Campaign = campaign;

            Day = 1;
            Phase = DayPhase.DayStart;
            Pool = new ResourcePool(race.StartingResources);

            // Sem isto, uma raca que ja comeca com gente e starva no proprio
            // primeiro dia (antes de UpdateChronicShortageStreaks rodar pela
            // primeira vez) nunca marcava ter tido populacao, e a extincao nunca
            // era detectada.
            _everHadPopulation = race.StartingResources[ResourceKind.Population] > 0;

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

        /// <summary>Sucessao de rivais, ou nulo quando a run nao usa campanha (ameaca
        /// anonima da curva antiga — testes de regra continuam funcionando).</summary>
        public RivalCampaignState Campaign { get; }

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

        public bool IsOver => Phase == DayPhase.Collapsed || Phase == DayPhase.Victory;

        public CollapseReason Collapse { get; private set; } = CollapseReason.None;

        /// <summary>Dias seguidos (ate agora) com comida zerada. A UI le isto para
        /// avisar antes do colapso por fome cronica chegar.</summary>
        public int DaysWithoutFood { get; private set; }

        /// <summary>Dias seguidos (ate agora) com ouro zerado.</summary>
        public int DaysWithoutGold { get; private set; }

        /// <summary>Populacao ja passou de zero nesta run. Kits de teste minimos que
        /// nunca tiveram gente nenhuma nao devem colapsar so por nunca ter comecado
        /// a ter populacao — extincao so importa pra quem chegou a ter povo.</summary>
        private bool _everHadPopulation;

        /// <summary>Dias seguidos (ate agora) com populacao zerada, depois de ja ter
        /// tido gente. Da uma folga curta pra recrutar de volta antes do colapso —
        /// nao e instantaneo, porque um dia ruim de fome nao devia ser sentenca.</summary>
        public int DaysAtZeroPopulation { get; private set; }

        /// <summary>Vitoria e Colapso sao desfechos distintos (design D5); isto e
        /// quem consulta qual dos dois terminou a run, sem inferir pelo Phase.</summary>
        public RunOutcome Outcome { get; private set; } = RunOutcome.Ongoing;

        /// <summary>
        /// Encerra a run em Vitoria. So RunEngine chama isto, ao derrotar o ultimo
        /// rival do roster — nunca sobrescreve um desfecho ja decidido.
        /// </summary>
        internal void DeclareVictory()
        {
            if (IsOver)
            {
                return;
            }

            Outcome = RunOutcome.Victory;
            Phase = DayPhase.Victory;
        }

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

        /// <summary>
        /// Aumenta o teto de integridade permanentemente e cura o mesmo tanto. So
        /// RunBuilder chama isto, aplicando bonus de meta ao montar a run (task 6.1,
        /// design D6) — nunca durante a run em si.
        /// </summary>
        internal void IncreaseMaxIntegrity(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            MaxIntegrity += amount;
            Integrity += amount;
        }

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

        public int TotalDefense()
        {
            return ProductionCalculator.TotalDefense(Grid, Allocation(), Rules) + PendingDefense;
        }

        /// <summary>Teto de populacao dado pelos edificios em pe.</summary>
        public int PopulationCapacity() => ProductionCalculator.PopulationCapacity(Grid);

        /// <summary>Comida que a populacao consome por dia.</summary>
        public int DailyFoodUpkeep()
        {
            double perHead = Rules.GetDouble(RuleKeys.FoodPerPopulation, DefaultFoodPerPopulation);
            double weather = TodayWeather?.FoodUpkeepMultiplier ?? 1.0;
            return (int)Math.Ceiling(this[ResourceKind.Population] * perHead * weather);
        }

        /// <summary>
        /// Consumo previsto para amanha, ja com o clima previsto. E o que permite o
        /// aviso de fome sair antes de o jogador decidir.
        /// </summary>
        public int PredictedFoodUpkeep()
        {
            double perHead = Rules.GetDouble(RuleKeys.FoodPerPopulation, DefaultFoodPerPopulation);
            double weather = Weather.Tomorrow?.FoodUpkeepMultiplier ?? 1.0;
            return (int)Math.Ceiling(this[ResourceKind.Population] * perHead * weather);
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
            foreach (ResourceKind kind in ResourceKinds.All)
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
        /// Atualiza as sequencias de dias sem comida/ouro. Chamado uma vez por Fim do
        /// Dia, depois de producao e consumo ja terem acontecido — sem isso um rival
        /// de eixo Recurso/Populacao (design D4) poderia drenar o reino pra sempre
        /// sem nunca terminar a run, porque defesa alta o mantinha "repelido" enquanto
        /// o pedagio continuava corroendo por fora da regra de defesa.
        /// </summary>
        public void UpdateChronicShortageStreaks()
        {
            if (this[ResourceKind.Population] > 0)
            {
                _everHadPopulation = true;
            }

            // So conta fome sem ninguem pra alimentar: populacao zero nao "passa
            // fome", so nao tem gente — sem esta guarda, todo catalogo minimo sem
            // populacao nenhuma (comum em teste de regra) colapsava por fome cronica
            // mesmo sem ninguem sofrendo com isso.
            bool starving = this[ResourceKind.Population] > 0 && this[ResourceKind.Food] <= 0;
            DaysWithoutFood = starving ? DaysWithoutFood + 1 : 0;
            DaysWithoutGold = Gold <= 0 ? DaysWithoutGold + 1 : 0;

            bool extinctToday = _everHadPopulation && this[ResourceKind.Population] <= 0;
            DaysAtZeroPopulation = extinctToday ? DaysAtZeroPopulation + 1 : 0;
        }

        /// <summary>
        /// Verifica as condicoes de fim: integridade zerada, territorio perdido, ou
        /// fome/falencia cronica demais. Chamado depois de cada passo que pode causar
        /// perda.
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
                Outcome = RunOutcome.Collapse;
                Phase = DayPhase.Collapsed;
                return true;
            }

            // Uma celula arrasada continua possuida (spec kingdom-grid), entao o
            // territorio se perde quando nao sobra nenhuma celula de pe, e nao
            // quando a posse chega a zero.
            if (StandingTileCount() <= 0)
            {
                Collapse = CollapseReason.TerritoryLost;
                Outcome = RunOutcome.Collapse;
                Phase = DayPhase.Collapsed;
                return true;
            }

            // Extincao usa metade do limite de fome cronica: quem chegou a zero
            // depois de ja ter tido gente esta pior do que quem so passou fome com
            // o povo ainda de pe (spec — sem isso extincao e "so mais um dia de
            // fome" em vez de um resultado pior).
            int extinctionLimit = Math.Max(
                1, (int)Math.Max(1, Rules.GetDouble(RuleKeys.StarvationCollapseDays, 10.0)) / 2);
            if (DaysAtZeroPopulation >= extinctionLimit)
            {
                Collapse = CollapseReason.Starvation;
                Outcome = RunOutcome.Collapse;
                Phase = DayPhase.Collapsed;
                return true;
            }

            int starvationLimit = (int)Math.Max(1, Rules.GetDouble(RuleKeys.StarvationCollapseDays, 10.0));
            if (DaysWithoutFood >= starvationLimit)
            {
                Collapse = CollapseReason.Starvation;
                Outcome = RunOutcome.Collapse;
                Phase = DayPhase.Collapsed;
                return true;
            }

            int bankruptcyLimit = (int)Math.Max(1, Rules.GetDouble(RuleKeys.BankruptcyCollapseDays, 10.0));
            if (DaysWithoutGold >= bankruptcyLimit)
            {
                Collapse = CollapseReason.Bankruptcy;
                Outcome = RunOutcome.Collapse;
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

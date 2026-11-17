using System;
using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Bot que sabe jogar a economia de cinco recursos. Existe para que o simulador
    /// meca o jogo de verdade: a GreedyPolicy so enxerga ouro, e medir a economia
    /// nova com ela era medir um jogador que nao planta, nao abriga e nao guarnece.
    ///
    /// Nao tenta jogar bem; tenta jogar como alguem atento jogaria:
    ///   - comida antes de tudo, porque fome desmonta o resto;
    ///   - casa quando falta gente para operar o que ja existe;
    ///   - defesa dimensionada pelo proximo ataque que defesa resolve;
    ///   - estoque, e nao torre, contra quem saqueia (design D8);
    ///   - carta de defesa so no dia do ataque, que e quando ela vale.
    /// </summary>
    public sealed class StewardPolicy : IRunPolicy
    {
        /// <summary>Quantos dias a frente o bot olha para dimensionar a defesa.</summary>
        public const int DefenseHorizonDays = 5;

        private const int MaxActionsPerDay = 24;

        public StewardPolicy(double expansionReserveRatio = 1.2)
        {
            ExpansionReserveRatio = expansionReserveRatio;
        }

        public double ExpansionReserveRatio { get; }

        public void PlanDay(RunEngine engine, RunBundle bundle)
        {
            RunState run = engine.Run;

            Repair(engine, run);
            ChooseStance(run);
            PlayCards(engine, run);

            for (int guard = 0; guard < MaxActionsPerDay; guard++)
            {
                if (!BuildOne(engine, bundle, run) && !ExpandOne(engine, run))
                {
                    break;
                }
            }

            ChooseStance(run);
        }

        public int ChooseEventOption(RunState run, EventDefinition definition)
        {
            if (definition == null || !definition.HasOffer)
            {
                return 0;
            }

            // Aceita oferta quando o que ela cobra esta sobrando. Comparar por id
            // seria mais preciso e muito mais fragil.
            for (int i = 0; i < definition.Options.Count; i++)
            {
                if (!definition.Options[i].IsOffer)
                {
                    continue;
                }

                bool paysFood = Pays(definition.Options[i], ResourceKind.Food);
                bool paysGold = Pays(definition.Options[i], ResourceKind.Gold);

                if (paysFood && run[ResourceKind.Food] < run.DailyFoodUpkeep() * 5)
                {
                    continue;
                }

                if (paysGold && run.Gold < 30)
                {
                    continue;
                }

                return i;
            }

            return 0;
        }

        private static bool Pays(EventOption option, ResourceKind kind)
        {
            for (int i = 0; i < option.Effects.Count; i++)
            {
                if (option.Effects[i] is LoseResourceEffect lose && lose.Resource == kind)
                {
                    return true;
                }

                if (kind == ResourceKind.Gold && option.Effects[i] is LoseGoldEffect)
                {
                    return true;
                }
            }

            return false;
        }

        // --- Leitura do estado ---

        /// <summary>
        /// Defesa que falta para o ataque mais forte, dentre os que defesa resolve,
        /// dentro do horizonte. Ataque de saque nao entra: torre nao impede o saque.
        /// </summary>
        public static int DefenseGap(RunState run, int horizonDays = DefenseHorizonDays)
        {
            int structural = run.TotalDefense() - run.PendingDefense;
            int gap = 0;

            IReadOnlyList<ScheduledThreat> pending = run.Threats.Pending;
            for (int i = 0; i < pending.Count; i++)
            {
                ScheduledThreat threat = pending[i];
                int days = threat.DaysUntil(run.Day);
                if (days < 0 || days > horizonDays)
                {
                    continue;
                }

                gap = Math.Max(gap, threat.Force - structural);
            }

            // Sem ataque anunciado, ainda assim se prepara para o proximo degrau da
            // campanha mais adiantada: quem so reage ao anuncio constroi tarde.
            if (gap <= 0)
            {
                foreach (RivalCampaign campaign in run.Threats.ActiveCampaigns())
                {
                    int next = campaign.Rival.Campaign.ForceFor(campaign.AttacksLaunched);
                    gap = Math.Max(gap, next - structural);
                }
            }

            return Math.Max(0, gap);
        }

        private static ScheduledThreat ThreatToday(RunState run)
        {
            IReadOnlyList<ScheduledThreat> pending = run.Threats.Pending;
            ScheduledThreat strongest = null;
            for (int i = 0; i < pending.Count; i++)
            {
                if (pending[i].DaysUntil(run.Day) == 0 && (strongest == null || pending[i].Force > strongest.Force))
                {
                    strongest = pending[i];
                }
            }

            return strongest;
        }

        private static int FoodBalance(RunState run)
        {
            return run.CollectDailyProductionByResource()[ResourceKind.Food] - run.DailyFoodUpkeep();
        }

        /// <summary>Comida que o proximo saque anunciado deve levar.</summary>
        private static int ExpectedRaid(RunState run)
        {
            int raid = 0;
            IReadOnlyList<ScheduledThreat> pending = run.Threats.Pending;
            for (int i = 0; i < pending.Count; i++)
            {
                RivalIdentity identity = pending[i].Rival?.Identity;
                if (identity == null || identity.LostResource != ResourceKind.Food)
                {
                    continue;
                }

                raid += (int)Math.Ceiling(pending[i].Force * identity.RaidFraction * identity.LossPerForce);
            }

            return raid;
        }

        private static bool WantsFood(RunState run)
        {
            int upkeep = Math.Max(1, run.DailyFoodUpkeep());
            int balance = FoodBalance(run);
            int buffer = upkeep * 2 + ExpectedRaid(run);
            return balance < 1 || (run[ResourceKind.Food] < buffer && balance < 3);
        }

        // --- Postura ---

        /// <summary>
        /// Guarnece no dia do ataque e devolve a gente ao campo depois (tarefa 7.1).
        /// </summary>
        private static void ChooseStance(RunState run)
        {
            ScheduledThreat today = ThreatToday(run);
            bool countered = today != null && (today.Rival == null || today.Rival.Identity.IsCounteredByDefense
                                               || today.Rival.Identity.RaidFraction < 1.0);
            run.Stance = countered ? WorkerStance.DefenseFirst : WorkerStance.ProductionFirst;
        }

        // --- Cartas ---

        private static void PlayCards(RunEngine engine, RunState run)
        {
            ScheduledThreat today = ThreatToday(run);

            for (int pass = 0; pass < 2; pass++)
            {
                List<CardDefinition> hand = new List<CardDefinition>(run.Deck.Hand);
                for (int i = 0; i < hand.Count; i++)
                {
                    CardDefinition card = hand[i];
                    if (card.EnergyCost > run.Energy || !run.Deck.HandContains(card))
                    {
                        continue;
                    }

                    bool defensive = Has(card, EffectKinds.AddDefense);
                    if (defensive)
                    {
                        // Defesa temporaria dura o dia: fora do dia do ataque e
                        // energia jogada fora.
                        if (today == null || today.Force <= run.TotalDefense())
                        {
                            continue;
                        }
                    }
                    else if (pass == 0 && today != null && today.Force > run.TotalDefense())
                    {
                        // No dia do ataque, a energia vai primeiro para a defesa.
                        continue;
                    }

                    if (Has(card, EffectKinds.RepairBase) && run.Integrity >= run.MaxIntegrity && !defensive)
                    {
                        continue;
                    }

                    if (Has(card, ResourceEffectKinds.Recruit) &&
                        run[ResourceKind.Population] >= run.PopulationCapacity())
                    {
                        continue;
                    }

                    if (!card.NeedsTarget)
                    {
                        engine.PlayCard(card);
                        continue;
                    }

                    // Terraplanagem as cegas estraga terreno bom: so joga carta com
                    // alvo quando o alvo e obviamente bom.
                    if (card.Target == TargetRequirement.BuildableTile)
                    {
                        continue;
                    }

                    List<Coord> targets = TargetRules.ValidTargets(run.Grid, card.Target, run.Rules);
                    if (targets.Count > 0)
                    {
                        engine.PlayCard(card, targets[0]);
                    }
                }
            }
        }

        private static bool Has(CardDefinition card, string effectKind)
        {
            for (int i = 0; i < card.Effects.Count; i++)
            {
                if (card.Effects[i].Kind == effectKind)
                {
                    return true;
                }
            }

            return false;
        }

        // --- Reparo ---

        private static void Repair(RunEngine engine, RunState run)
        {
            int cost = run.Grid.CostCurve.RepairCost;
            foreach (Tile tile in run.Grid.OwnedTilesOrdered())
            {
                if (tile.Destroyed && run.CanAfford(cost))
                {
                    engine.RepairTile(tile.Coord);
                }
            }
        }

        // --- Construcao ---

        private bool BuildOne(RunEngine engine, RunBundle bundle, RunState run)
        {
            List<BuildingDefinition> buildings = bundle.AvailableBuildings();
            WorkerAllocation allocation = run.Allocation();
            int freeWorkers = allocation.Unassigned;
            bool housingFull = run[ResourceKind.Population] >= run.PopulationCapacity();
            bool wantsFood = WantsFood(run);
            int gap = DefenseGap(run);
            ResourceAmounts income = run.CollectDailyProductionByResource();

            BuildingDefinition best = null;
            Coord bestCoord = default;
            double bestScore = 0;

            foreach (Tile tile in run.Grid.OwnedTilesOrdered())
            {
                if (!tile.IsBuildable)
                {
                    continue;
                }

                // Raca que preserva a floresta: a celula rende sozinha e recusa obra.
                if (tile.Terrain == TerrainType.Forest && run.Rules.GetBool(RuleKeys.NoBuildOnForest, false))
                {
                    continue;
                }

                for (int i = 0; i < buildings.Count; i++)
                {
                    BuildingDefinition candidate = buildings[i];
                    if (candidate.Id == RunBuilder.HallBuildingId || !candidate.CanBuildOn(tile.Terrain) ||
                        !run.Race.AllowsBuilding(candidate))
                    {
                        continue;
                    }

                    if (!run.CanAfford(engine.BuildCostOf(candidate), out _))
                    {
                        continue;
                    }

                    // Edificio que exige gente so vale se houver gente livre para ele.
                    if (candidate.WorkersRequired > freeWorkers)
                    {
                        continue;
                    }

                    double score = Score(run, candidate, tile, wantsFood, gap, housingFull, freeWorkers, income);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                        bestCoord = tile.Coord;
                    }
                }
            }

            return best != null && engine.BuildOn(bestCoord, best).Ok;
        }

        private static double Score(
            RunState run, BuildingDefinition building, Tile tile, bool wantsFood, int gap,
            bool housingFull, int freeWorkers, ResourceAmounts income)
        {
            double score = 0;

            int food = building.Production[ResourceKind.Food];
            if (food > 0)
            {
                score += food * (wantsFood ? 12 : 1.5);
            }

            if (building.PopulationCapacity > 0)
            {
                // Casa vale quando o teto travou o crescimento e a comida aguenta
                // mais bocas. Com gente sobrando, e desperdicio.
                if (housingFull && freeWorkers <= 1 && !wantsFood)
                {
                    score += 20 + building.PopulationCapacity * 2;
                }
            }

            if (building.Defense > 0 && gap > 0)
            {
                score += Math.Min(building.Defense, gap) * 6;
            }

            if (wantsFood && food == 0)
            {
                // Com fome a vista, nada que consuma gente passa na frente da comida.
                return building.WorkersRequired > 0 ? 0 : score * 0.5;
            }

            score += Want(run, income, ResourceKind.Wood, building, 6);
            score += Want(run, income, ResourceKind.Stone, building, 5);
            score += building.Production[ResourceKind.Gold] * (income[ResourceKind.Gold] < 10 ? 2.5 : 1.0);

            return score;
        }

        /// <summary>Valor de produzir um material: alto enquanto a renda dele for baixa.</summary>
        private static double Want(
            RunState run, ResourceAmounts income, ResourceKind kind, BuildingDefinition building, int enough)
        {
            int produces = building.Production[kind];
            if (produces <= 0)
            {
                return 0;
            }

            return income[kind] >= enough ? produces * 0.8 : produces * 4.0;
        }

        // --- Expansao ---

        private bool ExpandOne(RunEngine engine, RunState run)
        {
            if (run.TodayWeather != null && run.TodayWeather.BlocksPurchase)
            {
                return false;
            }

            // So expande quando nao ha onde construir: terra vazia nao rende nada.
            int buildable = 0;
            foreach (Tile tile in run.Grid.OwnedTilesOrdered())
            {
                if (tile.IsBuildable)
                {
                    buildable++;
                }
            }

            if (buildable >= 2)
            {
                return false;
            }

            List<Coord> purchasable = run.Grid.PurchasableCoords(run.Rules);
            if (purchasable.Count == 0)
            {
                return false;
            }

            int cost = run.Grid.NextTileCost(run.Rules);
            if (run.Gold < cost * ExpansionReserveRatio)
            {
                return false;
            }

            // Prefere o que ja foi revelado e serve para o que falta.
            Coord pick = purchasable[0];
            double bestValue = -1;
            bool wantsFood = WantsFood(run);
            for (int i = 0; i < purchasable.Count; i++)
            {
                Tile tile = run.Grid.TileAt(purchasable[i]);
                double value = 1;
                if (tile.Revealed)
                {
                    value = tile.Terrain == TerrainType.Plain ? (wantsFood ? 5 : 3)
                        : tile.Terrain == TerrainType.River ? (wantsFood ? 4 : 2)
                        : tile.Terrain == TerrainType.Forest ? 3
                        : tile.Terrain == TerrainType.Mine ? 3
                        : 1.5;
                }

                if (value > bestValue)
                {
                    bestValue = value;
                    pick = purchasable[i];
                }
            }

            return engine.BuyTile(pick).Ok;
        }
    }
}

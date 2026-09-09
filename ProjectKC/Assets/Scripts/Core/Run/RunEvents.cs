using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Evento de dominio emitido pelo nucleo. A camada de apresentacao reage a estes
    /// eventos em vez de reler o estado: e o que permite encenar a resolucao do dia
    /// com animacao sem que a ordem da regra dependa do tempo de animacao (design D2).
    /// </summary>
    public abstract class RunEvent
    {
        public abstract string Kind { get; }

        public override string ToString() => Kind;
    }

    public sealed class DayStartedEvent : RunEvent
    {
        public DayStartedEvent(int day, int energy, int cardsDrawn)
        {
            Day = day;
            Energy = energy;
            CardsDrawn = cardsDrawn;
        }

        public int Day { get; }

        public int Energy { get; }

        public int CardsDrawn { get; }

        public override string Kind => "day_started";
    }

    public sealed class PhaseChangedEvent : RunEvent
    {
        public PhaseChangedEvent(DayPhase phase)
        {
            Phase = phase;
        }

        public DayPhase Phase { get; }

        public override string Kind => "phase_changed";

        public override string ToString() => "phase_changed:" + Phase;
    }

    public sealed class TilePurchasedEvent : RunEvent
    {
        public TilePurchasedEvent(Coord coord, TerrainType terrain, int cost)
        {
            Coord = coord;
            Terrain = terrain;
            Cost = cost;
        }

        public Coord Coord { get; }

        public TerrainType Terrain { get; }

        public int Cost { get; }

        public override string Kind => "tile_purchased";
    }

    public sealed class BuildingPlacedEvent : RunEvent
    {
        public BuildingPlacedEvent(Coord coord, string buildingId, bool queued, int readyOnDay)
        {
            Coord = coord;
            BuildingId = buildingId;
            Queued = queued;
            ReadyOnDay = readyOnDay;
        }

        public Coord Coord { get; }

        public string BuildingId { get; }

        /// <summary>Verdadeiro quando a raca atrasa a obra e o edificio ainda nao esta em pe.</summary>
        public bool Queued { get; }

        public int ReadyOnDay { get; }

        public override string Kind => "building_placed";
    }

    public sealed class CardPlayedEvent : RunEvent
    {
        public CardPlayedEvent(string cardId, Coord? target, List<EffectResult> results)
        {
            CardId = cardId;
            Target = target;
            Results = results ?? new List<EffectResult>();
        }

        public string CardId { get; }

        public Coord? Target { get; }

        public List<EffectResult> Results { get; }

        public override string Kind => "card_played";
    }

    public sealed class ProductionCollectedEvent : RunEvent
    {
        public ProductionCollectedEvent(int gold, List<ProductionBreakdown> breakdowns)
        {
            Gold = gold;
            Breakdowns = breakdowns ?? new List<ProductionBreakdown>();
        }

        public int Gold { get; }

        public List<ProductionBreakdown> Breakdowns { get; }

        public override string Kind => "production_collected";
    }

    public sealed class ThreatAnnouncedEvent : RunEvent
    {
        public ThreatAnnouncedEvent(ScheduledThreat threat)
        {
            Threat = threat;
        }

        public ScheduledThreat Threat { get; }

        public override string Kind => "threat_announced";
    }

    public sealed class AttackResolvedEvent : RunEvent
    {
        public AttackResolvedEvent(AttackReport report)
        {
            Report = report;
        }

        public AttackReport Report { get; }

        public override string Kind => "attack_resolved";
    }

    public sealed class DayEventDrawnEvent : RunEvent
    {
        public DayEventDrawnEvent(EventDefinition definition)
        {
            Definition = definition;
        }

        public EventDefinition Definition { get; }

        public override string Kind => "event_drawn";
    }

    public sealed class DayEventResolvedEvent : RunEvent
    {
        public DayEventResolvedEvent(EventOutcome outcome)
        {
            Outcome = outcome;
        }

        public EventOutcome Outcome { get; }

        public override string Kind => "event_resolved";
    }

    public sealed class HandDiscardedEvent : RunEvent
    {
        public HandDiscardedEvent(int discarded, int retained)
        {
            Discarded = discarded;
            Retained = retained;
        }

        public int Discarded { get; }

        public int Retained { get; }

        public override string Kind => "hand_discarded";
    }

    public sealed class MilestoneReachedEvent : RunEvent
    {
        public MilestoneReachedEvent(string milestoneId, string label)
        {
            MilestoneId = milestoneId;
            Label = label;
        }

        public string MilestoneId { get; }

        public string Label { get; }

        public override string Kind => "milestone_reached";
    }

    public sealed class RunCollapsedEvent : RunEvent
    {
        public RunCollapsedEvent(CollapseReason reason, RunScore score)
        {
            Reason = reason;
            Score = score;
        }

        public CollapseReason Reason { get; }

        public RunScore Score { get; }

        public override string Kind => "run_collapsed";
    }
}

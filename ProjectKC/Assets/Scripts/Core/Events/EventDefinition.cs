using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    public enum EventClass
    {
        Positive = 0,
        Neutral = 1,
        Negative = 2
    }

    /// <summary>Condicao de elegibilidade de um evento.</summary>
    public abstract class EventCondition
    {
        public abstract bool IsSatisfied(RunState run);

        public abstract string Describe();
    }

    public sealed class MinimumDayCondition : EventCondition
    {
        private readonly int _day;

        public MinimumDayCondition(int day)
        {
            _day = day;
        }

        public override bool IsSatisfied(RunState run) => run.Day >= _day;

        public override string Describe() => "dia >= " + _day;
    }

    public sealed class OwnsTerrainCondition : EventCondition
    {
        private readonly TerrainType _terrain;

        public OwnsTerrainCondition(TerrainType terrain)
        {
            _terrain = terrain;
        }

        public override bool IsSatisfied(RunState run)
        {
            foreach (Tile tile in run.Grid.OwnedTiles())
            {
                if (tile.Terrain == _terrain)
                {
                    return true;
                }
            }

            return false;
        }

        public override string Describe() => "possui " + _terrain;
    }

    public sealed class OwnsBuildingCondition : EventCondition
    {
        private readonly string _buildingId;

        public OwnsBuildingCondition(string buildingId)
        {
            _buildingId = buildingId;
        }

        public override bool IsSatisfied(RunState run)
        {
            foreach (Tile tile in run.Grid.OwnedTiles())
            {
                if (!tile.Destroyed && tile.HasBuilding && tile.Building.Id == _buildingId)
                {
                    return true;
                }
            }

            return false;
        }

        public override string Describe() => "possui edificio " + _buildingId;
    }

    public sealed class MinimumTilesCondition : EventCondition
    {
        private readonly int _tiles;

        public MinimumTilesCondition(int tiles)
        {
            _tiles = tiles;
        }

        public override bool IsSatisfied(RunState run) => run.Grid.OwnedCount >= _tiles;

        public override string Describe() => "territorio >= " + _tiles;
    }

    /// <summary>
    /// Uma opcao de escolha. Um evento sem escolha tem exatamente uma opcao, o que
    /// deixa a resolucao uniforme: sempre se aplica os efeitos de uma opcao.
    /// </summary>
    public sealed class EventOption
    {
        public EventOption(string label, IReadOnlyList<IEffect> effects, bool isOffer = false)
        {
            Label = label;
            Effects = effects ?? new List<IEffect>();
            IsOffer = isOffer;
        }

        public string Label { get; }

        public IReadOnlyList<IEffect> Effects { get; }

        /// <summary>
        /// Opcao paga: o custo em ouro e preco, nao dano. Fica isenta do teto de perda
        /// de ouro da classe porque o jogador aceitou o preco ao escolher; o orcamento
        /// existe para limitar surpresa, e uma escolha nao e surpresa.
        /// So vale em evento que ofereca alternativa sem custo.
        /// </summary>
        public bool IsOffer { get; }
    }

    /// <summary>
    /// Definicao pura de um evento de fim de dia. A classe e parte do dado, e nao
    /// deduzida dos efeitos: a UI precisa mostrar ao jogador se e bom, neutro ou
    /// ruim antes de ele confirmar (spec day-events).
    /// </summary>
    public sealed class EventDefinition
    {
        public EventDefinition(
            string id,
            string displayName,
            EventClass eventClass,
            IReadOnlyList<EventOption> options,
            IReadOnlyList<EventCondition> conditions = null,
            double weight = 1.0,
            int cooldownDays = 5,
            string flavorText = null)
        {
            Id = id;
            DisplayName = displayName;
            Class = eventClass;
            Options = options ?? new List<EventOption>();
            Conditions = conditions ?? new List<EventCondition>();
            Weight = weight;
            CooldownDays = cooldownDays;
            FlavorText = flavorText ?? string.Empty;
        }

        /// <summary>Atalho para o caso mais comum: um evento sem escolha.</summary>
        public static EventDefinition Simple(
            string id,
            string displayName,
            EventClass eventClass,
            IReadOnlyList<IEffect> effects,
            IReadOnlyList<EventCondition> conditions = null,
            double weight = 1.0,
            int cooldownDays = 5,
            string flavorText = null)
        {
            return new EventDefinition(
                id,
                displayName,
                eventClass,
                new List<EventOption> { new EventOption("Continuar", effects) },
                conditions,
                weight,
                cooldownDays,
                flavorText);
        }

        public string Id { get; }

        public string DisplayName { get; }

        public EventClass Class { get; }

        public IReadOnlyList<EventOption> Options { get; }

        public IReadOnlyList<EventCondition> Conditions { get; }

        public double Weight { get; }

        /// <summary>Dias em que o evento fica indisponivel depois de sair.</summary>
        public int CooldownDays { get; }

        public string FlavorText { get; }

        public bool HasChoice => Options.Count > 1;

        /// <summary>Existe ao menos uma opcao paga.</summary>
        public bool HasOffer
        {
            get
            {
                for (int i = 0; i < Options.Count; i++)
                {
                    if (Options[i].IsOffer)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Existe ao menos uma saida sem custo, ou seja, da para recusar.</summary>
        public bool HasDeclineOption
        {
            get
            {
                for (int i = 0; i < Options.Count; i++)
                {
                    if (!Options[i].IsOffer)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsEligible(RunState run)
        {
            if (!run.Race.AllowsEvent(this))
            {
                return false;
            }

            for (int i = 0; i < Conditions.Count; i++)
            {
                if (!Conditions[i].IsSatisfied(run))
                {
                    return false;
                }
            }

            return true;
        }

        public override string ToString() => Id + " (" + Class + ")";
    }
}

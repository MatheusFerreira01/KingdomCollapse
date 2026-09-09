using System;
using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// De onde partiu um efeito. O Core precisa saber porque a mesma lista de efeitos
    /// e usada por cartas e por eventos (design D3), mas so eventos ficam sujeitos ao
    /// orcamento de severidade (design D10).
    /// </summary>
    public enum EffectSource
    {
        Card = 0,
        Event = 1,
        Threat = 2,
        System = 3
    }

    public sealed class EffectResult
    {
        private EffectResult(bool applied, string description, bool clamped)
        {
            Applied = applied;
            Description = description;
            Clamped = clamped;
        }

        public bool Applied { get; }

        /// <summary>Texto curto do que aconteceu. Vai para o log e para a UI.</summary>
        public string Description { get; }

        /// <summary>Verdadeiro quando o orcamento de severidade recortou o efeito.</summary>
        public bool Clamped { get; }

        public static EffectResult Ok(string description, bool clamped = false)
        {
            return new EffectResult(true, description, clamped);
        }

        public static EffectResult NoOp(string reason)
        {
            return new EffectResult(false, reason, false);
        }

        public override string ToString() => (Applied ? "ok: " : "no-op: ") + Description;
    }

    /// <summary>
    /// Contexto de aplicacao. Carrega o alvo escolhido e o orcamento de severidade
    /// vigente; um efeito nunca decide sozinho quanto pode custar.
    /// </summary>
    public sealed class EffectContext
    {
        public EffectContext(RunState run, EffectSource source, Coord? target = null, SeverityBudget budget = null)
        {
            Run = run;
            Source = source;
            Target = target;
            Budget = budget ?? SeverityBudget.Unlimited;
        }

        public RunState Run { get; }

        public EffectSource Source { get; }

        public Coord? Target { get; }

        public SeverityBudget Budget { get; }

        public bool HasTarget => Target.HasValue;

        /// <summary>
        /// Quanto ja foi gasto do orcamento nesta resolucao. Vive no contexto, e nao
        /// no efeito, porque o teto vale para o evento inteiro: dois efeitos de
        /// destruicao na mesma carta de evento nao podem somar duas celulas.
        /// </summary>
        public int TilesDestroyedSoFar { get; private set; }

        public int CardsRemovedSoFar { get; private set; }

        public void NoteTileDestroyed() => TilesDestroyedSoFar++;

        public void NoteCardRemoved() => CardsRemovedSoFar++;
    }

    public interface IEffect
    {
        /// <summary>Identificador do tipo de efeito. Usado por log e por validacao de catalogo.</summary>
        string Kind { get; }

        EffectResult Apply(EffectContext context);
    }

    /// <summary>
    /// Efeitos que impoem perda ao jogador declaram quanto custam, para que a
    /// verificacao de catalogo (tarefa 7.7) consiga reprovar um evento fora do
    /// orcamento sem precisar simular a run.
    /// </summary>
    public interface ISeverityDeclaring
    {
        SeverityClaim DeclareSeverity();
    }

    /// <summary>Custo maximo que um efeito pode impor, em termos comparaveis ao orcamento.</summary>
    public readonly struct SeverityClaim
    {
        public SeverityClaim(
            double goldLossFraction = 0,
            double baseDamageFraction = 0,
            int tilesDestroyed = 0,
            bool destroysBuiltTile = false,
            int cardsRemoved = 0,
            bool scalesWithDay = false)
        {
            GoldLossFraction = goldLossFraction;
            BaseDamageFraction = baseDamageFraction;
            TilesDestroyed = tilesDestroyed;
            DestroysBuiltTile = destroysBuiltTile;
            CardsRemoved = cardsRemoved;
            ScalesWithDay = scalesWithDay;
        }

        public double GoldLossFraction { get; }

        public double BaseDamageFraction { get; }

        public int TilesDestroyed { get; }

        public bool DestroysBuiltTile { get; }

        public int CardsRemoved { get; }

        public bool ScalesWithDay { get; }

        public static readonly SeverityClaim None = new SeverityClaim();
    }

    /// <summary>
    /// Teto de perda por classe de evento (design D10). Cartas usam Unlimited: o
    /// jogador escolheu joga-las, entao nao ha surpresa a limitar.
    /// </summary>
    public sealed class SeverityBudget
    {
        public SeverityBudget(
            double maxGoldLossFraction,
            double maxBaseDamageFraction,
            int maxTilesDestroyed,
            bool canDestroyBuiltTile,
            int maxCardsRemoved,
            bool allowsLethalDamage = false)
        {
            MaxGoldLossFraction = maxGoldLossFraction;
            MaxBaseDamageFraction = maxBaseDamageFraction;
            MaxTilesDestroyed = maxTilesDestroyed;
            CanDestroyBuiltTile = canDestroyBuiltTile;
            MaxCardsRemoved = maxCardsRemoved;
            AllowsLethalDamage = allowsLethalDamage;
        }

        /// <summary>
        /// Se este orcamento pode levar a integridade a zero. Falso para eventos, que
        /// nunca encerram a run; verdadeiro para ameacas, que existem para isso.
        /// </summary>
        public bool AllowsLethalDamage { get; }

        public double MaxGoldLossFraction { get; }

        public double MaxBaseDamageFraction { get; }

        public int MaxTilesDestroyed { get; }

        public bool CanDestroyBuiltTile { get; }

        public int MaxCardsRemoved { get; }

        /// <summary>Sem teto. Usado por cartas, ameacas e efeitos de sistema.</summary>
        public static readonly SeverityBudget Unlimited =
            new SeverityBudget(1.0, 1.0, int.MaxValue, true, int.MaxValue, allowsLethalDamage: true);

        /// <summary>
        /// Orcamento de evento negativo: no maximo 30% do ouro, 15% da integridade,
        /// uma celula vazia e uma carta. Nunca uma celula construida.
        /// </summary>
        public static readonly SeverityBudget NegativeEvent =
            new SeverityBudget(0.30, 0.15, 1, false, 1);

        /// <summary>Eventos neutros nao impoem perda liquida.</summary>
        public static readonly SeverityBudget NeutralEvent =
            new SeverityBudget(0.0, 0.0, 0, false, 0);

        /// <summary>Eventos positivos tambem nao tiram nada do jogador.</summary>
        public static readonly SeverityBudget PositiveEvent =
            new SeverityBudget(0.0, 0.0, 0, false, 0);

        /// <summary>
        /// Mesmo orcamento, sem teto de ouro. Usado por opcao paga: o preco foi
        /// aceito, mas dano e destruicao continuam limitados.
        /// </summary>
        public SeverityBudget AsOffer()
        {
            return new SeverityBudget(
                1.0,
                MaxBaseDamageFraction,
                MaxTilesDestroyed,
                CanDestroyBuiltTile,
                MaxCardsRemoved,
                AllowsLethalDamage);
        }

        public static SeverityBudget ForClass(EventClass eventClass)
        {
            switch (eventClass)
            {
                case EventClass.Negative:
                    return NegativeEvent;
                case EventClass.Neutral:
                    return NeutralEvent;
                case EventClass.Positive:
                    return PositiveEvent;
                default:
                    return NeutralEvent;
            }
        }

        /// <summary>Recorta uma perda de ouro para caber na fracao permitida.</summary>
        public int ClampGoldLoss(int requested, int currentGold)
        {
            int allowed = (int)Math.Floor(currentGold * MaxGoldLossFraction);
            return Math.Max(0, Math.Min(requested, Math.Min(allowed, currentGold)));
        }

        /// <summary>
        /// Recorta dano a base. Alem da fracao, nunca deixa a integridade chegar a
        /// zero: evento nao encerra run (spec day-events).
        /// </summary>
        public int ClampBaseDamage(int requested, int currentIntegrity, int maxIntegrity)
        {
            int byFraction = (int)Math.Floor(maxIntegrity * MaxBaseDamageFraction);
            int ceiling = AllowsLethalDamage ? currentIntegrity : Math.Max(0, currentIntegrity - 1);
            return Math.Max(0, Math.Min(requested, Math.Min(byFraction, ceiling)));
        }

        public bool AllowsTileDestruction(int alreadyDestroyed, bool tileHasBuilding)
        {
            if (alreadyDestroyed >= MaxTilesDestroyed)
            {
                return false;
            }

            return !tileHasBuilding || CanDestroyBuiltTile;
        }

        public bool AllowsCardRemoval(int alreadyRemoved)
        {
            return alreadyRemoved < MaxCardsRemoved;
        }
    }

    /// <summary>Aplica listas de efeitos e junta os resultados.</summary>
    public static class EffectRunner
    {
        public static List<EffectResult> ApplyAll(
            IReadOnlyList<IEffect> effects,
            RunState run,
            EffectSource source,
            Coord? target = null,
            SeverityBudget budget = null)
        {
            List<EffectResult> results = new List<EffectResult>();
            if (effects == null)
            {
                return results;
            }

            EffectContext context = new EffectContext(run, source, target, budget);
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] == null)
                {
                    continue;
                }

                results.Add(effects[i].Apply(context));
            }

            return results;
        }
    }
}

using System;
using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    public static class ResourceEffectKinds
    {
        public const string GainResource = "gain_resource";
        public const string LoseResource = "lose_resource";
        public const string Recruit = "recruit";
        public const string SetStance = "set_stance";
    }

    /// <summary>
    /// Concede um recurso. Substitui o efeito de ouro, que passa a ser um caso
    /// particular deste: um efeito por recurso multiplicaria por cinco o vocabulario
    /// sem acrescentar nada.
    /// </summary>
    public sealed class GainResourceEffect : IEffect
    {
        private readonly ResourceKind _kind;
        private readonly double _amount;
        private readonly GoldScaling _scaling;

        public GainResourceEffect(ResourceKind kind, double amount, GoldScaling scaling = GoldScaling.Flat)
        {
            _kind = kind;
            _amount = amount;
            _scaling = scaling;
        }

        public string Kind => ResourceEffectKinds.GainResource;

        public string Describe()
        {
            return "+" + RulesText.Amount(_amount, _scaling, ResourceKinds.DisplayName(_kind));
        }

        public ResourceKind Resource => _kind;

        public EffectResult Apply(EffectContext context)
        {
            int amount = ResolveAmount(context.Run);
            if (amount <= 0)
            {
                return EffectResult.NoOp("nada a ganhar");
            }

            context.Run.Add(_kind, amount);
            return EffectResult.Ok("+" + amount + " " + ResourceKinds.DisplayName(_kind));
        }

        /// <summary>Quanto este efeito daria agora, sem aplicar. Para a interface mostrar antes da escolha.</summary>
        public int Preview(RunState run) => Math.Max(0, ResolveAmount(run));

        private int ResolveAmount(RunState run)
        {
            switch (_scaling)
            {
                case GoldScaling.PerOwnedTile:
                    return (int)Math.Round(_amount * run.Grid.OwnedCount, MidpointRounding.AwayFromZero);
                case GoldScaling.FractionOfCurrent:
                    return (int)Math.Floor(run[_kind] * _amount);
                default:
                    return (int)Math.Round(_amount, MidpointRounding.AwayFromZero);
            }
        }
    }

    /// <summary>
    /// Retira um recurso, sujeito ao orcamento de severidade. O teto e por recurso:
    /// perder 30% do ouro e um reves, perder 30% da populacao e outra coisa.
    /// </summary>
    public sealed class LoseResourceEffect : IEffect, ISeverityDeclaring
    {
        private readonly ResourceKind _kind;
        private readonly double _amount;
        private readonly GoldScaling _scaling;

        public LoseResourceEffect(
            ResourceKind kind, double amount, GoldScaling scaling = GoldScaling.FractionOfCurrent)
        {
            _kind = kind;
            _amount = amount;
            _scaling = scaling;
        }

        public string Kind => ResourceEffectKinds.LoseResource;

        public string Describe()
        {
            return "-" + RulesText.Amount(_amount, _scaling, ResourceKinds.DisplayName(_kind));
        }

        public ResourceKind Resource => _kind;

        /// <summary>
        /// Quanto este efeito tiraria agora, sem aplicar. Nunca mais do que existe;
        /// o orcamento de severidade ainda pode reduzir na hora de aplicar.
        /// </summary>
        public int Preview(RunState run)
        {
            int current = run[_kind];
            int requested = _scaling == GoldScaling.FractionOfCurrent
                ? (int)Math.Ceiling(current * _amount)
                : (int)Math.Round(_amount, MidpointRounding.AwayFromZero);
            return Math.Max(0, Math.Min(requested, current));
        }

        public EffectResult Apply(EffectContext context)
        {
            RunState run = context.Run;
            int current = run[_kind];

            int requested = _scaling == GoldScaling.FractionOfCurrent
                ? (int)Math.Ceiling(current * _amount)
                : (int)Math.Round(_amount, MidpointRounding.AwayFromZero);

            int allowed = context.Budget.ClampResourceLoss(_kind, requested, current);
            if (allowed <= 0)
            {
                return EffectResult.NoOp("nada a perder de " + ResourceKinds.DisplayName(_kind));
            }

            int removed = run.Remove(_kind, allowed);
            return EffectResult.Ok(
                "-" + removed + " " + ResourceKinds.DisplayName(_kind), clamped: allowed < requested);
        }

        public SeverityClaim DeclareSeverity()
        {
            double fraction = _scaling == GoldScaling.FractionOfCurrent ? _amount : 1.0;
            bool scales = _scaling == GoldScaling.PerOwnedTile;

            if (_kind == ResourceKind.Population)
            {
                return new SeverityClaim(populationLossFraction: fraction, scalesWithDay: scales);
            }

            return new SeverityClaim(goldLossFraction: fraction, scalesWithDay: scales);
        }
    }

    /// <summary>Traz gente nova, respeitando o teto de populacao do reino.</summary>
    public sealed class RecruitEffect : IEffect
    {
        private readonly int _amount;
        private readonly bool _ignoreCapacity;

        public RecruitEffect(int amount, bool ignoreCapacity = false)
        {
            _amount = amount;
            _ignoreCapacity = ignoreCapacity;
        }

        public string Kind => ResourceEffectKinds.Recruit;

        public string Describe()
        {
            return "+" + _amount + " de populacao" + (_ignoreCapacity ? ", mesmo acima do teto" : " (ate o teto)");
        }

        public EffectResult Apply(EffectContext context)
        {
            if (_amount <= 0)
            {
                return EffectResult.NoOp("ninguem a recrutar");
            }

            RunState run = context.Run;
            int amount = _amount;

            if (!_ignoreCapacity)
            {
                // Recrutar alem do teto seria criar gente sem onde morar, e a
                // populacao voltaria a cair no proximo crescimento.
                int room = run.PopulationCapacity() - run[ResourceKind.Population];
                amount = Math.Min(amount, Math.Max(0, room));
            }

            if (amount <= 0)
            {
                return EffectResult.NoOp("sem espaco para mais gente");
            }

            run.Add(ResourceKind.Population, amount);
            return EffectResult.Ok("+" + amount + " de populacao");
        }
    }

    /// <summary>
    /// Muda a postura de trabalho do reino: quem recebe gente primeiro. E o efeito
    /// que realoca trabalhadores entre producao e guarnicao (spec card-system).
    /// </summary>
    public sealed class SetStanceEffect : IEffect
    {
        private readonly WorkerStance _stance;

        public SetStanceEffect(WorkerStance stance)
        {
            _stance = stance;
        }

        public string Kind => ResourceEffectKinds.SetStance;

        public string Describe()
        {
            switch (_stance)
            {
                case WorkerStance.DefenseFirst:
                    return "Gente vai primeiro para as muralhas";
                case WorkerStance.ProductionFirst:
                    return "Gente vai primeiro para os campos";
                default:
                    return "Reparte a gente entre campos e muralhas";
            }
        }

        public EffectResult Apply(EffectContext context)
        {
            if (context.Run.Stance == _stance)
            {
                return EffectResult.NoOp("o reino ja trabalha assim");
            }

            context.Run.Stance = _stance;

            switch (_stance)
            {
                case WorkerStance.DefenseFirst:
                    return EffectResult.Ok("o reino guarnece as muralhas");
                case WorkerStance.ProductionFirst:
                    return EffectResult.Ok("o reino volta aos campos");
                default:
                    return EffectResult.Ok("o reino reparte o trabalho");
            }
        }
    }
}

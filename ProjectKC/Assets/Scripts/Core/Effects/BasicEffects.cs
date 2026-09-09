using System;
using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>Nomes dos tipos de efeito. Usados em log, catalogo e validacao.</summary>
    public static class EffectKinds
    {
        public const string GainGold = "gain_gold";
        public const string LoseGold = "lose_gold";
        public const string BuildOn = "build_on";
        public const string DestroyTile = "destroy_tile";
        public const string RepairBase = "repair_base";
        public const string DamageBase = "damage_base";
        public const string AddDefense = "add_defense";
        public const string DrawCards = "draw_cards";
        public const string RevealTile = "reveal_tile";
        public const string AddCardToDeck = "add_card_to_deck";
        public const string GrantTile = "grant_tile";
        public const string ModifyProduction = "modify_production";
        public const string RemoveCard = "remove_card";
        public const string SetTerrain = "set_terrain";
        public const string DisableTile = "disable_tile";
        public const string RepairTile = "repair_tile";
    }

    /// <summary>Como um valor de ouro e calculado.</summary>
    public enum GoldScaling
    {
        /// <summary>Valor fixo.</summary>
        Flat = 0,

        /// <summary>Valor por celula possuida. Recompensa quem expandiu.</summary>
        PerOwnedTile = 1,

        /// <summary>Fracao do ouro atual. Mantem o custo relativo constante ao longo da run.</summary>
        FractionOfCurrent = 2
    }

    public sealed class GainGoldEffect : IEffect
    {
        private readonly double _amount;
        private readonly GoldScaling _scaling;

        public GainGoldEffect(double amount, GoldScaling scaling = GoldScaling.Flat)
        {
            _amount = amount;
            _scaling = scaling;
        }

        public string Kind => EffectKinds.GainGold;

        public EffectResult Apply(EffectContext context)
        {
            int gold = ResolveAmount(context.Run);
            if (gold <= 0)
            {
                return EffectResult.NoOp("sem ouro a ganhar");
            }

            context.Run.AddGold(gold);
            return EffectResult.Ok("+" + gold + " ouro");
        }

        private int ResolveAmount(RunState run)
        {
            switch (_scaling)
            {
                case GoldScaling.PerOwnedTile:
                    return (int)Math.Round(_amount * run.Grid.OwnedCount, MidpointRounding.AwayFromZero);
                case GoldScaling.FractionOfCurrent:
                    return (int)Math.Floor(run.Gold * _amount);
                default:
                    return (int)Math.Round(_amount, MidpointRounding.AwayFromZero);
            }
        }
    }

    public sealed class LoseGoldEffect : IEffect, ISeverityDeclaring
    {
        private readonly double _amount;
        private readonly GoldScaling _scaling;

        public LoseGoldEffect(double amount, GoldScaling scaling = GoldScaling.FractionOfCurrent)
        {
            _amount = amount;
            _scaling = scaling;
        }

        public string Kind => EffectKinds.LoseGold;

        public EffectResult Apply(EffectContext context)
        {
            RunState run = context.Run;
            int requested = _scaling == GoldScaling.FractionOfCurrent
                ? (int)Math.Ceiling(run.Gold * _amount)
                : (int)Math.Round(_amount, MidpointRounding.AwayFromZero);

            int allowed = context.Budget.ClampGoldLoss(requested, run.Gold);
            if (allowed <= 0)
            {
                return EffectResult.NoOp("nada a perder");
            }

            int removed = run.RemoveGold(allowed);
            return EffectResult.Ok("-" + removed + " ouro", clamped: allowed < requested);
        }

        public SeverityClaim DeclareSeverity()
        {
            // Um valor fixo nao pode ser comparado ao orcamento sem conhecer o saldo,
            // entao so a forma fracionaria e declarada como segura.
            double fraction = _scaling == GoldScaling.FractionOfCurrent ? _amount : 1.0;
            return new SeverityClaim(goldLossFraction: fraction, scalesWithDay: _scaling == GoldScaling.PerOwnedTile);
        }
    }

    public sealed class BuildOnTileEffect : IEffect
    {
        private readonly BuildingDefinition _building;
        private readonly bool _free;

        public BuildOnTileEffect(BuildingDefinition building, bool free = true)
        {
            _building = building;
            _free = free;
        }

        public string Kind => EffectKinds.BuildOn;

        public EffectResult Apply(EffectContext context)
        {
            if (!context.HasTarget)
            {
                return EffectResult.NoOp("sem alvo");
            }

            RunState run = context.Run;
            if (!_free && !run.CanAfford(_building.GoldCost))
            {
                return EffectResult.NoOp("ouro insuficiente");
            }

            GridResult result = run.Grid.Build(context.Target.Value, _building);
            if (!result.Ok)
            {
                return EffectResult.NoOp("construcao recusada: " + result.Rejection);
            }

            if (!_free)
            {
                run.RemoveGold(_building.GoldCost);
            }

            run.Stats.BuildingsBuilt++;
            return EffectResult.Ok(_building.DisplayName + " construida em " + context.Target.Value);
        }
    }

    public sealed class DestroyTileEffect : IEffect, ISeverityDeclaring
    {
        public string Kind => EffectKinds.DestroyTile;

        public EffectResult Apply(EffectContext context)
        {
            RunState run = context.Run;
            Coord? target = context.Target ?? PickDestroyableTile(run, context.Budget);

            if (!target.HasValue)
            {
                return EffectResult.NoOp("nenhuma celula elegivel para arrasar");
            }

            Tile tile = run.Grid.TileAt(target.Value);
            if (!context.Budget.AllowsTileDestruction(context.TilesDestroyedSoFar, tile.HasBuilding))
            {
                // Orcamento de evento nao permite arrasar celula construida: o efeito
                // e recortado inteiro, nunca aplicado pela metade.
                return EffectResult.NoOp("orcamento de severidade impede arrasar esta celula");
            }

            GridResult result = run.Grid.Destroy(target.Value);
            if (!result.Ok)
            {
                return EffectResult.NoOp("celula nao arrasavel: " + result.Rejection);
            }

            context.NoteTileDestroyed();
            return EffectResult.Ok("celula " + target.Value + " arrasada");
        }

        /// <summary>Escolhe uma celula vazia da borda quando o efeito nao tem alvo.</summary>
        private static Coord? PickDestroyableTile(RunState run, SeverityBudget budget)
        {
            List<Tile> border = run.Grid.BorderTiles();
            for (int i = 0; i < border.Count; i++)
            {
                Tile tile = border[i];
                if (tile.Destroyed || tile.Coord == run.Grid.HallCoord)
                {
                    continue;
                }

                if (tile.HasBuilding && !budget.CanDestroyBuiltTile)
                {
                    continue;
                }

                return tile.Coord;
            }

            return null;
        }

        public SeverityClaim DeclareSeverity()
        {
            return new SeverityClaim(tilesDestroyed: 1, destroysBuiltTile: false);
        }
    }

    public sealed class RepairBaseEffect : IEffect
    {
        private readonly double _amount;
        private readonly bool _asFraction;

        public RepairBaseEffect(double amount, bool asFraction = false)
        {
            _amount = amount;
            _asFraction = asFraction;
        }

        public string Kind => EffectKinds.RepairBase;

        public EffectResult Apply(EffectContext context)
        {
            RunState run = context.Run;
            int amount = _asFraction
                ? (int)Math.Round(run.MaxIntegrity * _amount, MidpointRounding.AwayFromZero)
                : (int)Math.Round(_amount, MidpointRounding.AwayFromZero);

            if (amount <= 0 || run.Integrity >= run.MaxIntegrity)
            {
                return EffectResult.NoOp("integridade ja cheia");
            }

            int before = run.Integrity;
            run.Heal(amount);
            return EffectResult.Ok("+" + (run.Integrity - before) + " integridade");
        }
    }

    public sealed class DamageBaseEffect : IEffect, ISeverityDeclaring
    {
        private readonly double _amount;
        private readonly bool _asFraction;

        public DamageBaseEffect(double amount, bool asFraction = false)
        {
            _amount = amount;
            _asFraction = asFraction;
        }

        public string Kind => EffectKinds.DamageBase;

        public EffectResult Apply(EffectContext context)
        {
            RunState run = context.Run;
            int requested = _asFraction
                ? (int)Math.Ceiling(run.MaxIntegrity * _amount)
                : (int)Math.Round(_amount, MidpointRounding.AwayFromZero);

            int allowed = context.Budget.ClampBaseDamage(requested, run.Integrity, run.MaxIntegrity);
            if (allowed <= 0)
            {
                return EffectResult.NoOp("dano recortado a zero");
            }

            int applied = run.Damage(allowed);
            return EffectResult.Ok("-" + applied + " integridade", clamped: allowed < requested);
        }

        public SeverityClaim DeclareSeverity()
        {
            double fraction = _asFraction ? _amount : 1.0;
            return new SeverityClaim(baseDamageFraction: fraction);
        }
    }

    public sealed class AddDefenseEffect : IEffect
    {
        private readonly int _amount;

        public AddDefenseEffect(int amount)
        {
            _amount = amount;
        }

        public string Kind => EffectKinds.AddDefense;

        public EffectResult Apply(EffectContext context)
        {
            if (_amount <= 0)
            {
                return EffectResult.NoOp("sem defesa a somar");
            }

            context.Run.AddPendingDefense(_amount);
            return EffectResult.Ok("+" + _amount + " defesa ate o proximo ataque");
        }
    }

    public sealed class DrawCardsEffect : IEffect
    {
        private readonly int _count;

        public DrawCardsEffect(int count)
        {
            _count = count;
        }

        public string Kind => EffectKinds.DrawCards;

        public EffectResult Apply(EffectContext context)
        {
            if (_count <= 0)
            {
                return EffectResult.NoOp("nada a comprar");
            }

            int drawn = context.Run.Deck.Draw(_count, context.Run.Random.Channel(RandomChannel.Cards));
            return drawn == 0
                ? EffectResult.NoOp("deck e descarte vazios")
                : EffectResult.Ok("comprou " + drawn + " carta(s)");
        }
    }

    public sealed class RevealTileEffect : IEffect
    {
        private readonly int _radius;

        public RevealTileEffect(int radius = 0)
        {
            _radius = radius;
        }

        public string Kind => EffectKinds.RevealTile;

        public EffectResult Apply(EffectContext context)
        {
            RunState run = context.Run;

            if (context.HasTarget && _radius <= 0)
            {
                run.Grid.Reveal(context.Target.Value);
                return EffectResult.Ok("revelou " + context.Target.Value);
            }

            // Sem alvo, revela a fronteira: informacao util sem exigir escolha.
            List<Coord> frontier = run.Grid.PurchasableCoords(run.Rules);
            int limit = _radius > 0 ? Math.Min(_radius, frontier.Count) : frontier.Count;
            for (int i = 0; i < limit; i++)
            {
                run.Grid.Reveal(frontier[i]);
            }

            return limit == 0
                ? EffectResult.NoOp("nada a revelar")
                : EffectResult.Ok("revelou " + limit + " celula(s)");
        }
    }

    public sealed class AddCardToDeckEffect : IEffect
    {
        private readonly CardDefinition _card;

        public AddCardToDeckEffect(CardDefinition card)
        {
            _card = card;
        }

        public string Kind => EffectKinds.AddCardToDeck;

        public EffectResult Apply(EffectContext context)
        {
            if (_card == null)
            {
                return EffectResult.NoOp("carta nula");
            }

            // Consulta a raca, e nao so a lista da carta: a proibicao pode vir de
            // qualquer um dos dois lados, e recompensa e um caminho de entrada tanto
            // quanto o deck inicial (spec races).
            if (!context.Run.Race.AllowsCard(_card))
            {
                return EffectResult.NoOp("carta proibida para a raca");
            }

            context.Run.Deck.AddToDiscard(_card);
            return EffectResult.Ok("ganhou a carta " + _card.DisplayName);
        }
    }

    public sealed class RemoveCardEffect : IEffect, ISeverityDeclaring
    {
        public string Kind => EffectKinds.RemoveCard;

        public EffectResult Apply(EffectContext context)
        {
            if (!context.Budget.AllowsCardRemoval(context.CardsRemovedSoFar))
            {
                return EffectResult.NoOp("orcamento de severidade impede remover outra carta");
            }

            CardDefinition removed = context.Run.Deck.RemoveRandomCard(
                context.Run.Random.Channel(RandomChannel.Cards));

            if (removed == null)
            {
                return EffectResult.NoOp("nenhuma carta a remover");
            }

            context.NoteCardRemoved();
            return EffectResult.Ok("perdeu a carta " + removed.DisplayName);
        }

        public SeverityClaim DeclareSeverity() => new SeverityClaim(cardsRemoved: 1);
    }

    public sealed class GrantTileEffect : IEffect
    {
        public string Kind => EffectKinds.GrantTile;

        public EffectResult Apply(EffectContext context)
        {
            RunState run = context.Run;
            Coord? target = context.Target;

            if (!target.HasValue)
            {
                List<Coord> candidates = run.Grid.PurchasableCoords(run.Rules);
                if (candidates.Count == 0)
                {
                    return EffectResult.NoOp("nenhuma celula disponivel");
                }

                int index = run.Random.Channel(RandomChannel.Events).NextInt(0, candidates.Count);
                target = candidates[index];
            }

            GridResult result = run.Grid.Grant(target.Value, run.Rules);
            if (!result.Ok)
            {
                return EffectResult.NoOp("celula nao concedivel: " + result.Rejection);
            }

            run.Stats.PeakTilesOwned = Math.Max(run.Stats.PeakTilesOwned, run.Grid.OwnedCount);
            return EffectResult.Ok("ganhou a celula " + target.Value);
        }
    }

    public sealed class ModifyProductionEffect : IEffect
    {
        private readonly string _key;
        private readonly double _value;
        private readonly int _days;

        public ModifyProductionEffect(double value, int days, bool multiplier = false)
        {
            _key = multiplier ? ModifierKeys.ProductionMultiplier : ModifierKeys.ProductionFlat;
            _value = value;
            _days = days;
        }

        public string Kind => EffectKinds.ModifyProduction;

        public EffectResult Apply(EffectContext context)
        {
            if (_days <= 0 || _value == 0)
            {
                return EffectResult.NoOp("modificador vazio");
            }

            context.Run.AddModifier(_key, _value, _days);
            return EffectResult.Ok("producao alterada por " + _days + " dia(s)");
        }
    }

    public sealed class DisableTileEffect : IEffect, ISeverityDeclaring
    {
        private readonly int _days;

        public DisableTileEffect(int days = 1)
        {
            _days = days;
        }

        public string Kind => EffectKinds.DisableTile;

        public EffectResult Apply(EffectContext context)
        {
            RunState run = context.Run;
            Coord? target = context.Target;

            if (!target.HasValue)
            {
                foreach (Tile tile in run.Grid.OwnedTilesOrdered())
                {
                    if (tile.HasBuilding && !tile.Destroyed && tile.Coord != run.Grid.HallCoord)
                    {
                        target = tile.Coord;
                        break;
                    }
                }
            }

            if (!target.HasValue)
            {
                return EffectResult.NoOp("nenhum edificio a parar");
            }

            run.AddModifier(ModifierKeys.TileDisabled, 1, _days, target.Value);
            return EffectResult.Ok("celula " + target.Value + " parada por " + _days + " dia(s)");
        }

        /// <summary>
        /// Parar um edificio custa producao, nao patrimonio: nao consome cota de
        /// destruicao nem de ouro, entao nao pesa no orcamento.
        /// </summary>
        public SeverityClaim DeclareSeverity() => SeverityClaim.None;
    }

    public sealed class SetTerrainEffect : IEffect
    {
        private readonly TerrainType _from;
        private readonly TerrainType _to;
        private readonly bool _requireFrom;

        public SetTerrainEffect(TerrainType to, TerrainType from = TerrainType.Plain, bool requireFrom = true)
        {
            _to = to;
            _from = from;
            _requireFrom = requireFrom;
        }

        public string Kind => EffectKinds.SetTerrain;

        public EffectResult Apply(EffectContext context)
        {
            RunState run = context.Run;
            Coord? target = context.Target;

            if (!target.HasValue)
            {
                foreach (Tile tile in run.Grid.OwnedTilesOrdered())
                {
                    if (tile.Coord == run.Grid.HallCoord)
                    {
                        continue;
                    }

                    if (!_requireFrom || tile.Terrain == _from)
                    {
                        target = tile.Coord;
                        break;
                    }
                }
            }

            if (!target.HasValue)
            {
                return EffectResult.NoOp("nenhuma celula com o terreno de origem");
            }

            Tile chosen = run.Grid.TileAt(target.Value);
            if (_requireFrom && chosen.Terrain != _from)
            {
                return EffectResult.NoOp("terreno de origem nao confere");
            }

            // Trocar o terreno sob um edificio incompativel derruba o edificio: o
            // requisito de terreno vale sempre, nao so no momento da construcao.
            bool demolished = false;
            if (chosen.HasBuilding && !chosen.Building.CanBuildOn(_to))
            {
                run.Grid.Demolish(target.Value);
                demolished = true;
            }

            run.Grid.SetTerrain(target.Value, _to);
            return EffectResult.Ok(
                "terreno de " + target.Value + " virou " + _to + (demolished ? " (edificio perdido)" : string.Empty));
        }
    }

    public sealed class RepairTileEffect : IEffect
    {
        public string Kind => EffectKinds.RepairTile;

        public EffectResult Apply(EffectContext context)
        {
            RunState run = context.Run;
            Coord? target = context.Target;

            if (!target.HasValue)
            {
                foreach (Tile tile in run.Grid.OwnedTilesOrdered())
                {
                    if (tile.Destroyed)
                    {
                        target = tile.Coord;
                        break;
                    }
                }
            }

            if (!target.HasValue)
            {
                return EffectResult.NoOp("nenhuma celula arrasada");
            }

            GridResult result = run.Grid.Repair(target.Value);
            return result.Ok
                ? EffectResult.Ok("celula " + target.Value + " reparada")
                : EffectResult.NoOp("reparo recusado: " + result.Rejection);
        }
    }
}

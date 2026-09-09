using System;
using System.Collections.Generic;
using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
    /// <summary>
    /// Tipos de efeito disponiveis no Inspector. Espelha EffectKinds do Core; um
    /// enum e nao uma classe polimorfica porque o Unity nao serializa polimorfismo
    /// sem SerializeReference, e um enum com campos mantem a autoria simples.
    /// </summary>
    public enum EffectEntryKind
    {
        GainGold = 0,
        LoseGold = 1,
        BuildOn = 2,
        DestroyTile = 3,
        RepairBase = 4,
        DamageBase = 5,
        AddDefense = 6,
        DrawCards = 7,
        RevealTile = 8,
        AddCardToDeck = 9,
        GrantTile = 10,
        ModifyProduction = 11,
        RemoveCard = 12,
        SetTerrain = 13,
        DisableTile = 14,
        RepairTile = 15
    }

    /// <summary>
    /// Um efeito autorado no Inspector. Nem todo campo vale para todo tipo; a
    /// documentacao de cada campo diz quem o usa, e ToEffect ignora o resto.
    /// </summary>
    [Serializable]
    public sealed class EffectEntry
    {
        [Tooltip("Que efeito este item aplica.")]
        public EffectEntryKind Kind = EffectEntryKind.GainGold;

        [Tooltip("Valor principal. Ouro, dano, defesa, cartas ou dias, conforme o tipo.")]
        public float Amount = 1f;

        [Tooltip("GainGold/LoseGold: como o valor e calculado.")]
        public GoldScaling Scaling = GoldScaling.Flat;

        [Tooltip("RepairBase/DamageBase: tratar Amount como fracao da integridade maxima.")]
        public bool AsFraction;

        [Tooltip("ModifyProduction/DisableTile: por quantos dias o efeito dura.")]
        public int Days = 1;

        [Tooltip("ModifyProduction: multiplicar em vez de somar.")]
        public bool AsMultiplier;

        [Tooltip("BuildOn: qual edificio erguer.")]
        public BuildingAsset Building;

        [Tooltip("BuildOn: se o edificio sai de graca ou cobra o custo normal.")]
        public bool FreeBuild = true;

        [Tooltip("AddCardToDeck: qual carta conceder.")]
        public CardAsset Card;

        [Tooltip("SetTerrain: terreno resultante.")]
        public TerrainType TargetTerrain = TerrainType.Plain;

        [Tooltip("SetTerrain: terreno exigido na celula de origem.")]
        public TerrainType RequiredTerrain = TerrainType.Plain;

        [Tooltip("SetTerrain: exigir o terreno de origem, ou aceitar qualquer um.")]
        public bool RequireSourceTerrain = true;

        [Tooltip("RevealTile: quantas celulas da fronteira revelar. Zero revela todas.")]
        public int RevealCount;

        /// <summary>Converte para o efeito puro do Core. Retorna null quando falta dado obrigatorio.</summary>
        public IEffect ToEffect()
        {
            switch (Kind)
            {
                case EffectEntryKind.GainGold:
                    return new GainGoldEffect(Amount, Scaling);
                case EffectEntryKind.LoseGold:
                    return new LoseGoldEffect(Amount, Scaling);
                case EffectEntryKind.BuildOn:
                    return Building == null ? null : new BuildOnTileEffect(Building.ToDefinition(), FreeBuild);
                case EffectEntryKind.DestroyTile:
                    return new DestroyTileEffect();
                case EffectEntryKind.RepairBase:
                    return new RepairBaseEffect(Amount, AsFraction);
                case EffectEntryKind.DamageBase:
                    return new DamageBaseEffect(Amount, AsFraction);
                case EffectEntryKind.AddDefense:
                    return new AddDefenseEffect(Mathf.RoundToInt(Amount));
                case EffectEntryKind.DrawCards:
                    return new DrawCardsEffect(Mathf.RoundToInt(Amount));
                case EffectEntryKind.RevealTile:
                    return new RevealTileEffect(RevealCount);
                case EffectEntryKind.AddCardToDeck:
                    return Card == null ? null : new AddCardToDeckEffect(Card.ToDefinition());
                case EffectEntryKind.GrantTile:
                    return new GrantTileEffect();
                case EffectEntryKind.ModifyProduction:
                    return new ModifyProductionEffect(Amount, Days, AsMultiplier);
                case EffectEntryKind.RemoveCard:
                    return new RemoveCardEffect();
                case EffectEntryKind.SetTerrain:
                    return new SetTerrainEffect(TargetTerrain, RequiredTerrain, RequireSourceTerrain);
                case EffectEntryKind.DisableTile:
                    return new DisableTileEffect(Days);
                case EffectEntryKind.RepairTile:
                    return new RepairTileEffect();
                default:
                    return null;
            }
        }

        /// <summary>Converte uma lista, descartando entradas incompletas.</summary>
        public static List<IEffect> ToEffects(IList<EffectEntry> entries)
        {
            List<IEffect> effects = new List<IEffect>();
            if (entries == null)
            {
                return effects;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                IEffect effect = entries[i]?.ToEffect();
                if (effect != null)
                {
                    effects.Add(effect);
                }
            }

            return effects;
        }
    }
}

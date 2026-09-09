using System;
using System.Collections.Generic;
using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
    /// <summary>
    /// Curvas de balanceamento expostas ao Inspector. Ficam num asset separado para
    /// que ajustar ritmo nao exija mexer em conteudo nem recompilar.
    /// </summary>
    [CreateAssetMenu(menuName = "Kingdom Collapse/Curvas de Balanceamento", fileName = "Balance")]
    public sealed class BalanceAsset : ScriptableObject
    {
        [Header("Custo de expansao")]
        [SerializeField] private int _tileBaseCost = 14;
        [SerializeField] private float _tileCostGrowth = 1.05f;
        [SerializeField] private int _tileCostFlatStep = 2;

        [Tooltip("Custo de reparar celula arrasada. Barato de proposito: ataque e reves, nao amputacao.")]
        [SerializeField] private int _repairCost = 8;

        [Header("Forca da ameaca")]
        [SerializeField] private float _threatBaseForce = 3f;
        [SerializeField] private float _threatPerDay = 1f;
        [SerializeField] private float _threatDayExponent = 1.25f;

        [Header("Agenda da ameaca")]
        [SerializeField] private int _firstThreatDay = 4;
        [SerializeField] private int _threatIntervalDays = 4;

        [Tooltip("Antecedencia do anuncio. O minimo de 2 dias e garantido pelo Core.")]
        [SerializeField] private int _threatLeadDays = 3;

        [Header("Mistura de eventos")]
        [SerializeField] private float _positiveWeight = 0.40f;
        [SerializeField] private float _neutralWeight = 0.32f;
        [SerializeField] private float _negativeWeight = 0.28f;

        public int FirstThreatDay => _firstThreatDay;

        public int ThreatIntervalDays => _threatIntervalDays;

        public int ThreatLeadDays => _threatLeadDays;

        public TileCostCurve ToTileCostCurve()
        {
            return new TileCostCurve(_tileBaseCost, _tileCostGrowth, _tileCostFlatStep, _repairCost);
        }

        public ThreatCurve ToThreatCurve()
        {
            // Sem termo por celula: o limite de expansao e economico, nao uma
            // penalidade aplicada ao tamanho do territorio.
            return new ThreatCurve(_threatBaseForce, _threatPerDay, _threatDayExponent);
        }

        public EventClassWeights ToEventWeights()
        {
            return new EventClassWeights(_positiveWeight, _neutralWeight, _negativeWeight);
        }
    }
}

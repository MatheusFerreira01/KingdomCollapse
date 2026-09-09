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
        [SerializeField] private float _threatPerDay = 0.4f;
        [SerializeField] private float _threatDayExponent = 1.1f;

        [Tooltip("Quanto cada celula alem da primeira soma a forca. E o custo em pressao de expandir (design D6).")]
        [SerializeField] private float _threatPerTile = 0.8f;

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
            return new ThreatCurve(_threatBaseForce, _threatPerDay, _threatDayExponent, _threatPerTile);
        }

        public EventClassWeights ToEventWeights()
        {
            return new EventClassWeights(_positiveWeight, _neutralWeight, _negativeWeight);
        }
    }

    /// <summary>
    /// Todo o conteudo do jogo reunido num asset. E a unica ponte entre os assets do
    /// Editor e o ContentCatalog puro que o Core consome.
    /// </summary>
    [CreateAssetMenu(menuName = "Kingdom Collapse/Base de Conteudo", fileName = "GameDatabase")]
    public sealed class GameDatabase : ScriptableObject
    {
        [Header("Conteudo")]
        [SerializeField] private List<BuildingAsset> _buildings = new List<BuildingAsset>();
        [SerializeField] private List<CardAsset> _cards = new List<CardAsset>();
        [SerializeField] private List<EventAsset> _events = new List<EventAsset>();
        [SerializeField] private List<RaceAsset> _races = new List<RaceAsset>();
        [SerializeField] private List<MetaNodeAsset> _metaNodes = new List<MetaNodeAsset>();

        [Header("Desbloqueado desde o inicio")]
        [Tooltip("O que aparece numa run sem nenhum no de meta comprado.")]
        [SerializeField] private List<BuildingAsset> _startingBuildings = new List<BuildingAsset>();

        [SerializeField] private List<CardAsset> _startingCards = new List<CardAsset>();
        [SerializeField] private List<EventAsset> _startingEvents = new List<EventAsset>();
        [SerializeField] private List<RaceAsset> _startingRaces = new List<RaceAsset>();

        [Header("Balanceamento")]
        [SerializeField] private BalanceAsset _balance;

        public BalanceAsset Balance => _balance;

        public IReadOnlyList<RaceAsset> Races => _races;

        public ContentCatalog BuildCatalog()
        {
            ContentCatalog catalog = new ContentCatalog();

            HashSet<string> startingBuildings = IdsOf(_startingBuildings);
            for (int i = 0; i < _buildings.Count; i++)
            {
                if (_buildings[i] == null)
                {
                    continue;
                }

                catalog.AddBuilding(
                    _buildings[i].ToDefinition(), startingBuildings.Contains(_buildings[i].Id));
            }

            HashSet<string> startingCards = IdsOf(_startingCards);
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i] == null)
                {
                    continue;
                }

                catalog.AddCard(_cards[i].ToDefinition(), startingCards.Contains(_cards[i].Id));
            }

            HashSet<string> startingEvents = IdsOf(_startingEvents);
            for (int i = 0; i < _events.Count; i++)
            {
                if (_events[i] == null)
                {
                    continue;
                }

                catalog.AddEvent(_events[i].ToDefinition(), startingEvents.Contains(_events[i].Id));
            }

            HashSet<string> startingRaces = IdsOf(_startingRaces);
            for (int i = 0; i < _races.Count; i++)
            {
                if (_races[i] == null)
                {
                    continue;
                }

                catalog.AddRace(_races[i].ToDefinition(), startingRaces.Contains(_races[i].Id));
            }

            return catalog;
        }

        public MetaTree BuildMetaTree()
        {
            List<MetaNodeDefinition> nodes = new List<MetaNodeDefinition>();
            for (int i = 0; i < _metaNodes.Count; i++)
            {
                if (_metaNodes[i] != null)
                {
                    nodes.Add(_metaNodes[i].ToDefinition());
                }
            }

            return new MetaTree(nodes);
        }

        public List<EventDefinition> BuildEventDefinitions()
        {
            List<EventDefinition> definitions = new List<EventDefinition>();
            for (int i = 0; i < _events.Count; i++)
            {
                if (_events[i] != null)
                {
                    definitions.Add(_events[i].ToDefinition());
                }
            }

            return definitions;
        }

        /// <summary>Monta o RunSetup ja com as curvas do asset de balanceamento.</summary>
        public RunSetup CreateSetup(string raceId, int seed)
        {
            RunSetup setup = new RunSetup(raceId, seed);

            if (_balance == null)
            {
                return setup;
            }

            setup.CostCurve = _balance.ToTileCostCurve();
            setup.ThreatCurve = _balance.ToThreatCurve();
            setup.EventWeights = _balance.ToEventWeights();
            setup.FirstThreatDay = _balance.FirstThreatDay;
            setup.ThreatIntervalDays = _balance.ThreatIntervalDays;
            setup.ThreatLeadDays = _balance.ThreatLeadDays;
            return setup;
        }

        private static HashSet<string> IdsOf<T>(List<T> assets) where T : ContentAsset
        {
            HashSet<string> ids = new HashSet<string>();
            for (int i = 0; i < assets.Count; i++)
            {
                if (assets[i] != null)
                {
                    ids.Add(assets[i].Id);
                }
            }

            return ids;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Roda a validacao de catalogo dentro do Editor e reclama no Console. A
        /// mesma regra e verificada por teste; aqui e so o retorno imediato para
        /// quem esta autorando.
        /// </summary>
        [ContextMenu("Validar catalogo")]
        public void ValidateCatalog()
        {
            List<CatalogViolation> violations = new List<CatalogViolation>();
            violations.AddRange(EventCatalogValidator.Validate(BuildEventDefinitions()));
            violations.AddRange(MetaCatalogValidator.Validate(BuildMetaTree()));

            if (violations.Count == 0)
            {
                Debug.Log("Catalogo valido: nenhum evento ou no fora do orcamento.", this);
                return;
            }

            for (int i = 0; i < violations.Count; i++)
            {
                Debug.LogError(violations[i].ToString(), this);
            }
        }
#endif
    }
}

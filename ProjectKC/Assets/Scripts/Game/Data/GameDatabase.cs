using System.Collections.Generic;
using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
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

        [Tooltip("Climas do jogo. Nao passam por desbloqueio: todo dia tem um.")]
        [SerializeField] private List<WeatherAsset> _weather = new List<WeatherAsset>();

        [Tooltip("Todos os rivais do jogo. Sem niveis, esta lista vira a run, na ordem de entrada. " +
                 "Vazio cai na ameaca anonima.")]
        [SerializeField] private List<RivalAsset> _rivals = new List<RivalAsset>();

        [Tooltip("A escada de dificuldade, do nivel 1 para cima. A posicao na lista e o numero do nivel.")]
        [SerializeField] private List<DifficultyLevelAsset> _levels = new List<DifficultyLevelAsset>();

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

            for (int i = 0; i < _weather.Count; i++)
            {
                if (_weather[i] != null)
                {
                    catalog.AddWeather(_weather[i].ToDefinition());
                }
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

            List<RivalDefinition> rivals = BuildRivalDefinitions();
            for (int i = 0; i < rivals.Count; i++)
            {
                catalog.AddRival(rivals[i]);
            }

            return catalog;
        }

        /// <summary>Rivais na ordem da lista, que e a ordem em que declaram guerra.</summary>
        public List<RivalDefinition> BuildRivalDefinitions()
        {
            List<RivalDefinition> definitions = new List<RivalDefinition>();
            for (int i = 0; i < _rivals.Count; i++)
            {
                if (_rivals[i] != null)
                {
                    definitions.Add(_rivals[i].ToDefinition());
                }
            }

            return definitions;
        }

        /// <summary>
        /// Todas as regras de catalogo de uma vez. O Editor e o gerador de conteudo
        /// chamam isto, para que uma regra nova nao precise ser lembrada em dois lugares.
        /// </summary>
        public List<CatalogViolation> CollectViolations()
        {
            List<RivalDefinition> rivals = BuildRivalDefinitions();

            List<CatalogViolation> violations = new List<CatalogViolation>();
            violations.AddRange(EventCatalogValidator.Validate(BuildEventDefinitions()));
            violations.AddRange(MetaCatalogValidator.Validate(BuildMetaTree()));
            violations.AddRange(WeatherCatalogValidator.Validate(BuildCatalog().Weather));
            violations.AddRange(RivalCatalogValidator.Validate(rivals));

            // Com escada, a ordem de entrada e a de cada nivel; sem ela, a lista de
            // rivais e a propria run, e e ela que precisa estar em ordem.
            if (_levels.Count > 0)
            {
                violations.AddRange(DifficultyLadderValidator.Validate(BuildLadder()));
            }
            else
            {
                violations.AddRange(RivalCatalogValidator.ValidateEntryOrder(name, rivals));
            }

            return violations;
        }

        /// <summary>A escada, com o numero de cada nivel dado pela posicao na lista.</summary>
        public DifficultyLadder BuildLadder()
        {
            List<DifficultyLevel> levels = new List<DifficultyLevel>();
            int rank = 1;
            for (int i = 0; i < _levels.Count; i++)
            {
                if (_levels[i] != null)
                {
                    levels.Add(_levels[i].ToDefinition(rank));
                    rank++;
                }
            }

            return new DifficultyLadder(levels);
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

        /// <summary>
        /// Monta o RunSetup ja com as curvas do asset de balanceamento e o nivel
        /// pedido. Sem escada autorada, a lista de rivais vira a run inteira.
        /// </summary>
        public RunSetup CreateSetup(string raceId, int seed, int levelRank = 1)
        {
            RunSetup setup = new RunSetup(raceId, seed);
            setup.Level = BuildLadder().LevelOfRank(levelRank);
            if (setup.Level == null)
            {
                setup.Rivals = BuildRivalDefinitions();
            }

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
            List<CatalogViolation> violations = CollectViolations();

            if (violations.Count == 0)
            {
                Debug.Log("Catalogo valido: nenhum conteudo fora do orcamento.", this);
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

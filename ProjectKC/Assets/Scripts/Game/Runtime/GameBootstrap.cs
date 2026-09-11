using System.Collections.Generic;
using KingdomCollapse.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KingdomCollapse.Game
{
    /// <summary>
    /// Ponto de entrada da fatia vertical. Monta camera, luz, view e entrada em
    /// runtime, para que a cena nao precise de nenhum prefab autorado ainda.
    ///
    /// O HUD daqui e IMGUI de proposito: e andaime para provar o loop antes de
    /// existir UI de verdade. As telas definitivas (arrastar-para-alvo, Colapso,
    /// selecao de raca, arvore de meta) sao tarefas proprias e substituem isto.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Conteudo")]
        [SerializeField] private GameDatabase _database;

        [SerializeField] private string _raceId = "humans";

        [Tooltip("Zero sorteia uma semente nova a cada partida.")]
        [SerializeField] private int _seed;

        [Header("Cena")]
        [SerializeField] private bool _createLighting = true;

        [Header("HUD")]
        [Tooltip("Opcional: sem icones atribuidos, o painel usa o texto atual.")]
        [SerializeField] private HudIconSet _icons;

        [Tooltip("Opcional: sem textura atribuida, o terreno usa a cor chapada atual.")]
        [SerializeField] private TerrainVisualSet _terrainVisuals;

        [Tooltip("Opcional: sem icone atribuido para o clima do dia, o HUD usa o nome em texto.")]
        [SerializeField] private WeatherIconSet _weatherIcons;

        private RunBundle _bundle;
        private Dictionary<string, Texture2D> _cardArt = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, RenderTexture> _buildingPreviews = new Dictionary<string, RenderTexture>();
        private Dictionary<string, GameObject> _buildingPrefabsCache = new Dictionary<string, GameObject>();
        private Camera _previewCamera;
        private GridView _gridView;
        private IsometricCameraRig _cameraRig;
        private Camera _camera;

        private readonly List<string> _log = new List<string>();
        private string _lastRejection;
        private Vector2 _logScroll;

        private FloatingNumbers _numbers;
        private readonly DayStaging _staging = new DayStaging();

        /// <summary>Estilo com quebra de linha para a descricao da carta. So pode ser
        /// criado dentro de OnGUI, onde GUI.skin ja existe.</summary>
        private GUIStyle _cardRulesStyle;

        // Abas do painel esquerdo, abrem e fecham independente para deixar so o que
        // interessa visivel de cada vez (pedido do playtest).
        private bool _showResources = true;
        private bool _showTerritory = true;
        private bool _showThreat = true;

        // Comeca fechado: o log flutuante e sob demanda (Enter abre, X fecha), nao
        // uma barra permanente (spec proposal, item "Log deixa de ser barra fixa").
        private bool _logOpen;

        private Texture2D _translucentTexture;
        private GUIStyle _tooltipStyle;

        /// <summary>Terreno sob o mouse agora, para o tooltip (spec terrain-art). Nulo
        /// fora do tabuleiro, sobre HUD, ou sobre fantasma nao revelado.</summary>
        private TerrainType? _hoveredTerrain;

        private Vector2 _hoverScreenPos;

        // Clima e cenario reativo (spec weather-scenery)
        private WeatherDefinition _currentWeather;
        private WeatherDefinition _tomorrowWeather;
        private Light _sunLight;
        private ParticleSystem _weatherParticles;
        private string _weatherParticleKind = string.Empty;
        private Rect _weatherIconRect;

        // Recalculadas a cada OnGUI, e usadas no frame seguinte para saber se o
        // clique foi sobre HUD. O atraso de um frame e o mesmo que o codigo ja tinha
        // com PanelWidth/LogHeight fixos.
        private Rect _leftPanelRect;
        private Rect _handStripRect;
        private Rect _logRect;
        private Rect _buildPopupRect;
        private Rect _headerRect;

        /// <summary>Recurso que a ultima recusa apontou, para destacar no painel.</summary>
        private ResourceShortage _lastShortage = ResourceShortage.None;

        // Meta-progressao (task 10.4): perfil persistido em disco, e a escada de
        // dificuldade lida do GameDatabase. Escada vazia (nenhum nivel autorado
        // ainda, tarefa 9.5) preserva o comportamento antigo: a run comeca direto.
        private FileProfileStore _profileStore;
        private MetaProfile _metaProfile;
        private List<DifficultyLevel> _difficultyLevels;
        private string _currentLevelId;
        private bool _awaitingDifficultyChoice;

        private void Start()
        {
            if (_database == null)
            {
                Debug.LogError("GameBootstrap sem GameDatabase: arraste o asset no Inspector.", this);
                enabled = false;
                return;
            }

            _profileStore = new FileProfileStore(
                System.IO.Path.Combine(Application.persistentDataPath, "meta_profile.json"));
            _metaProfile = _profileStore.Load().Profile;
            _difficultyLevels = _database.DifficultyLevels();

            if (_difficultyLevels.Count > 0)
            {
                _awaitingDifficultyChoice = true;
                return;
            }

            StartRun(_seed == 0 ? Random.Range(1, int.MaxValue) : _seed, null);
        }

        private void StartRun(int seed, DifficultyLevel level)
        {
            _awaitingDifficultyChoice = false;
            _currentLevelId = level?.Id;

            ContentCatalog catalog = _database.BuildCatalog();
            _cardArt = _database.BuildCardArt();
            _buildingPrefabsCache = _database.BuildBuildingPrefabs();
            BuildBuildingPreviews(_buildingPrefabsCache);

            if (!catalog.Races.ContainsKey(_raceId))
            {
                Debug.LogError("Raca '" + _raceId + "' nao existe no GameDatabase.", this);
                enabled = false;
                return;
            }

            RunSetup setup = _database.CreateSetup(_raceId, seed, level);
            MetaTree tree = _database.BuildMetaTree();

            try
            {
                _bundle = RunBuilder.Build(setup, catalog, tree, _metaProfile);
            }
            catch (System.Exception error)
            {
                Debug.LogError("Nao consegui montar a run: " + error.Message, this);
                enabled = false;
                return;
            }

            BuildScene();

            _bundle.Engine.StartRun();
            _gridView.Initialize(_bundle.Run, _terrainVisuals, _buildingPrefabsCache);
            _cameraRig.Frame(_gridView.WorldBounds(), instant: true);

            _log.Clear();
            Log("Run iniciada. Semente " + seed + ", raca " + _bundle.Run.Race.DisplayName + ".");
            ConsumeEvents();

            // O clima do dia 1 chega dentro do ConsumeEvents acima; sincroniza de
            // novo para as particulas de buff/debuff de celula (SyncWeatherEffect)
            // nascerem sem esperar a primeira selecao do jogador.
            _gridView.Sync();
        }

        private void BuildScene()
        {
            if (_camera == null)
            {
                _camera = Camera.main;

                if (_camera == null)
                {
                    GameObject cameraObject = new GameObject("Camera Isometrica");
                    cameraObject.tag = "MainCamera";
                    _camera = cameraObject.AddComponent<Camera>();
                }

                _cameraRig = _camera.GetComponent<IsometricCameraRig>();
                if (_cameraRig == null)
                {
                    _cameraRig = _camera.gameObject.AddComponent<IsometricCameraRig>();
                }

                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(0.09f, 0.10f, 0.13f);
            }

            if (_gridView == null)
            {
                GameObject viewObject = new GameObject("Grid View");
                _gridView = viewObject.AddComponent<GridView>();
                _numbers = viewObject.AddComponent<FloatingNumbers>();
            }

            if (_sunLight == null)
            {
                _sunLight = FindAnyObjectByType<Light>();
            }

            if (_createLighting && _sunLight == null)
            {
                GameObject lightObject = new GameObject("Sol");
                _sunLight = lightObject.AddComponent<Light>();
                _sunLight.type = LightType.Directional;
                _sunLight.intensity = 1.1f;
                _sunLight.shadows = LightShadows.Soft;
                lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }

        /// <summary>
        /// Renderiza um retrato 3D de cada predio com prefab atribuido, uma vez so
        /// no inicio da run: o popup de construcao usa a textura pronta em vez de
        /// texto puro. Longe do tabuleiro, sem layer dedicada — nao precisa mexer em
        /// Project Settings, so posicao no mundo que a camera principal nunca ve.
        /// </summary>
        private void BuildBuildingPreviews(Dictionary<string, GameObject> prefabs)
        {
            foreach (KeyValuePair<string, RenderTexture> old in _buildingPreviews)
            {
                if (old.Value != null)
                {
                    Destroy(old.Value);
                }
            }

            _buildingPreviews.Clear();

            foreach (KeyValuePair<string, GameObject> pair in prefabs)
            {
                if (pair.Value != null)
                {
                    _buildingPreviews[pair.Key] = RenderBuildingPreview(pair.Value);
                }
            }
        }

        private static readonly Vector3 PreviewSpot = new Vector3(9000f, 9000f, 9000f);

        private void EnsurePreviewCamera()
        {
            if (_previewCamera != null)
            {
                return;
            }

            GameObject cameraObject = new GameObject("Camera de Retrato");
            cameraObject.transform.position = PreviewSpot + new Vector3(0f, 1.6f, -2.4f);
            cameraObject.transform.LookAt(PreviewSpot);

            _previewCamera = cameraObject.AddComponent<Camera>();
            _previewCamera.enabled = false;
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _previewCamera.fieldOfView = 25f;
            _previewCamera.nearClipPlane = 0.1f;
            _previewCamera.farClipPlane = 30f;

            GameObject lightObject = new GameObject("Luz de Retrato");
            lightObject.transform.SetParent(cameraObject.transform, false);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            lightObject.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
        }

        /// <summary>Uma renderizacao so, guardada como textura: o predio nao muda de
        /// aparencia depois de construido, entao nao precisa redesenhar por quadro.</summary>
        private RenderTexture RenderBuildingPreview(GameObject prefab)
        {
            EnsurePreviewCamera();

            GameObject instance = Instantiate(prefab, PreviewSpot, Quaternion.Euler(0f, 35f, 0f));

            foreach (Collider collider in instance.GetComponentsInChildren<Collider>())
            {
                Destroy(collider);
            }

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            Bounds bounds = new Bounds(PreviewSpot, Vector3.one * 0.5f);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (i == 0)
                {
                    bounds = renderers[i].bounds;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            // Enquadra pelo tamanho real do modelo: torre alta e moinho largo cabem
            // igual sem eu ter que ajustar distancia por predio.
            float radius = Mathf.Max(bounds.extents.magnitude, 0.3f);
            Vector3 direction = new Vector3(1f, 0.7f, -1f).normalized;
            _previewCamera.transform.position = bounds.center + direction * radius * 2.6f;
            _previewCamera.transform.LookAt(bounds.center);

            RenderTexture texture = new RenderTexture(128, 128, 16, RenderTextureFormat.ARGB32)
            {
                hideFlags = HideFlags.DontSave
            };

            _previewCamera.targetTexture = texture;
            _previewCamera.Render();
            _previewCamera.targetTexture = null;

            Destroy(instance);
            return texture;
        }

        private void Update()
        {
            if (_bundle == null)
            {
                return;
            }

            _staging.Tick(Time.deltaTime);
            HandleClick();
            HandleLogToggle();
            HandleHover();
        }

        /// <summary>Le o terreno sob o mouse a cada quadro, sem exigir clique nem
        /// selecao (spec terrain-art — "Requirement: Descricao do terreno ao passar
        /// o mouse").</summary>
        private void HandleHover()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                _hoveredTerrain = null;
                return;
            }

            Vector2 position = mouse.position.ReadValue();
            _hoverScreenPos = position;

            if (IsPointerOverHud(position))
            {
                _hoveredTerrain = null;
                return;
            }

            Ray ray = _camera.ScreenPointToRay(position);
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f))
            {
                _hoveredTerrain = null;
                return;
            }

            TileView view = hit.collider.GetComponent<TileView>();
            if (view == null)
            {
                _hoveredTerrain = null;
                return;
            }

            Tile tile = _bundle.Run.Grid.TileAt(view.Coord);

            // Fantasma nao revelado nao entrega o tipo de terreno de graca — mesma
            // regra que ja vale para a cor (GridView.ColorFor).
            _hoveredTerrain = (!view.IsGhost || tile.Revealed) ? tile.Terrain : (TerrainType?)null;
        }

        /// <summary>Enter abre e fecha o log flutuante (spec proposal — "aberta com
        /// Enter e fechada pelo X"; o X fica dentro do painel, em DrawLogPanel).</summary>
        private void HandleLogToggle()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                _logOpen = !_logOpen;
            }
        }

        private void HandleClick()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            // Clique sobre a area do HUD nao deve atravessar para o mundo.
            if (IsPointerOverHud(mouse.position.ReadValue()))
            {
                return;
            }

            Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f))
            {
                _gridView.Select(null);
                return;
            }

            TileView tile = hit.collider.GetComponent<TileView>();
            if (tile == null)
            {
                _gridView.Select(null);
                return;
            }

            // Celula fantasma agora so seleciona: comprar vira uma opcao do popup de
            // construcao, e nao mais uma acao escondida atras do clique. E o que
            // deixa uma carta com alvo em celula compravel (ex.: doar terreno)
            // receber Selected como alvo valido.
            _gridView.Select(tile.Coord);
        }

        private void TryBuy(Coord coord)
        {
            int cost = _bundle.Run.Grid.NextTileCost(_bundle.Run.Rules);
            CommandResult result = _bundle.Engine.BuyTile(coord);

            if (!result.Ok)
            {
                Reject(result);
                return;
            }

            ClearRejection();
            Log("Comprou " + coord + " por " + cost + " de ouro.");
            AfterStateChanged();
        }

        private void TryBuild(BuildingDefinition building, Coord coord)
        {
            CommandResult result = _bundle.Engine.BuildOn(coord, building);

            if (!result.Ok)
            {
                Reject(result);
                return;
            }

            ClearRejection();
            Log("Construiu " + building.DisplayName + " em " + coord + ".");
            AfterStateChanged();
        }

        private void TryPlayCard(CardDefinition card)
        {
            Coord? target = null;

            if (card.NeedsTarget)
            {
                // Andaime: usa a celula selecionada como alvo. O arrastar-para-alvo
                // chega com a UI de verdade.
                if (!_gridView.Selected.HasValue)
                {
                    _lastRejection = "Selecione uma celula antes de jogar esta carta.";
                    return;
                }

                target = _gridView.Selected;
            }

            CommandResult result = _bundle.Engine.PlayCard(card, target);

            if (!result.Ok)
            {
                Reject(result);
                return;
            }

            ClearRejection();
            Log("Jogou " + card.DisplayName + ".");
            AfterStateChanged();
        }

        private void EndDay()
        {
            int day = _bundle.Run.Day;
            CommandResult result = _bundle.Engine.EndDay();

            if (!result.Ok)
            {
                Reject(result);
                return;
            }

            ClearRejection();
            Log("--- fim do dia " + day + " ---");
            AfterStateChanged();
        }

        private void Reject(CommandResult result)
        {
            _lastRejection = Explain(result);
            _lastShortage = result.Shortage;
        }

        private void ClearRejection()
        {
            _lastRejection = null;
            _lastShortage = ResourceShortage.None;
        }

        private void AfterStateChanged()
        {
            ConsumeEvents();
            _gridView.Sync();
            _cameraRig.Frame(_gridView.WorldBounds());
        }

        /// <summary>
        /// Traduz os eventos de dominio para o log. E o mesmo canal que a UI final vai
        /// consumir para encenar a resolucao do dia.
        /// </summary>
        private void ConsumeEvents()
        {
            List<RunEvent> events = _bundle.Engine.DrainEvents();

            // A encenacao le os mesmos eventos que o log: a ordem apresentada e a
            // ordem resolvida porque as duas leituras vem da mesma fonte.
            List<DayStep> steps = DayPresentation.Build(events);
            if (steps.Count > 0)
            {
                _staging.Begin(steps);
            }

            for (int i = 0; i < events.Count; i++)
            {
                switch (events[i])
                {
                    case ProductionCollectedEvent production:
                        if (!production.Produced.IsEmpty)
                        {
                            Log("Producao: " + production.Produced);
                        }

                        // O numero sobe onde foi gerado. O contador diz que mudou;
                        // isto diz de onde veio.
                        for (int b = 0; b < production.Breakdowns.Count; b++)
                        {
                            ProductionBreakdown breakdown = production.Breakdowns[b];
                            if (!breakdown.IsEmpty && _numbers != null)
                            {
                                _numbers.SpawnBreakdown(_gridView.AnchorFor(breakdown.Coord), breakdown);
                            }
                        }

                        break;

                    case FoodResolvedEvent food:
                        Log(food.Famine
                            ? "FOME: -" + food.Starved + " de populacao."
                            : "Consumo: -" + food.Upkeep + " de comida.");
                        break;

                    case FamineWarningEvent warning:
                        Log("AVISO: a comida prevista nao cobre o consumo de amanha (faltam " +
                            warning.Deficit + ").");
                        break;

                    case PopulationGrewEvent growth:
                        Log("Populacao cresce para " + growth.Total + " de " + growth.Capacity + ".");
                        break;

                    case PlunderCollectedEvent plunder:
                        Log("Saque: " + plunder.Plunder);
                        break;

                    case WeatherChangedEvent weather:
                        if (weather.Today != null)
                        {
                            Log("Clima: " + weather.Today.DisplayName +
                                (weather.Tomorrow != null ? " | amanha: " + weather.Tomorrow.DisplayName : ""));
                        }

                        _currentWeather = weather.Today;
                        _tomorrowWeather = weather.Tomorrow;
                        _gridView.SetWeather(weather.Today);
                        ApplyWeatherVisuals(weather.Today);
                        break;

                    case ThreatAnnouncedEvent announced:
                        Log("AMEACA: " + announced.Threat.Kind + " no dia " + announced.Threat.ArrivalDay +
                            ", forca prevista " + announced.Threat.Force + ".");
                        break;

                    case ThreatWeatheredEvent weathered:
                        if (weathered.Effect.DelayDays != 0 && _numbers != null)
                        {
                            // Verde e positivo quando adia; a UI tambem aceita
                            // negativo (vermelho) se um dia existir efeito que
                            // antecipe — hoje o Core so adia (spec hud-icons).
                            int days = weathered.Effect.DelayDays;
                            Color color = days > 0 ? new Color(0.45f, 0.85f, 0.45f) : new Color(0.90f, 0.35f, 0.35f);
                            string text = (days > 0 ? "+" : string.Empty) + days;
                            _numbers.SpawnScreen(HeaderThreatAnchor(), text, color);
                        }

                        Log("CLIMA sobre ameaca: " + weathered.Effect.WeatherId +
                            (weathered.Effect.DelayDays != 0 ? ", adiada " + weathered.Effect.DelayDays + " dia(s)" : string.Empty) +
                            (weathered.Effect.ForceDelta != 0 ? ", forca " + (weathered.Effect.ForceDelta > 0 ? "+" : string.Empty) + weathered.Effect.ForceDelta : string.Empty));
                        break;

                    case AttackResolvedEvent attack:
                        Log("ATAQUE " + attack.Report.Kind + ": forca " + attack.Report.Force +
                            " vs defesa " + attack.Report.Defense + " => " +
                            attack.Report.TilesDestroyed.Count + " celula(s), " +
                            attack.Report.BaseDamage + " de dano.");
                        break;

                    case DayEventResolvedEvent dayEvent:
                        Log("EVENTO [" + dayEvent.Outcome.Definition.Class + "] " +
                            dayEvent.Outcome.Definition.DisplayName + ": " + Summarize(dayEvent.Outcome));
                        break;

                    case MilestoneReachedEvent milestone:
                        Log("MARCO: " + milestone.Label);
                        break;

                    case RunCollapsedEvent collapsed:
                        Log("=== COLAPSO (" + collapsed.Reason + ") === " +
                            collapsed.Score.TotalPoints + " pts, " +
                            collapsed.Score.MetaCurrency + " moedas de meta.");
                        OnRunEnded(collapsed.Score);
                        break;

                    case RivalDeclaredEvent rivalDeclared:
                        Log("RIVAL: " + rivalDeclared.Rival.DisplayName + " declara guerra.");
                        break;

                    case RivalDefeatedEvent rivalDefeated:
                        Log("RIVAL DERROTADO: " + rivalDefeated.Rival.DisplayName + ".");
                        break;

                    case RunVictoryEvent victory:
                        Log("=== VITORIA === " + victory.Score.TotalPoints + " pts, " +
                            victory.Score.MetaCurrency + " moedas de meta.");
                        OnRunEnded(victory.Score);
                        break;
                }
            }
        }

        private static string Summarize(EventOutcome outcome)
        {
            if (outcome.Results.Count == 0)
            {
                return "sem efeito";
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < outcome.Results.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append("; ");
                }

                builder.Append(outcome.Results[i].Description);
            }

            return builder.ToString();
        }

        private static string Explain(CommandResult result)
        {
            switch (result.Rejection)
            {
                case CommandRejection.NotEnoughResources:
                    return "Faltam " + result.Shortage.Missing + " de " +
                           ResourceKinds.DisplayName(result.Shortage.Kind) + ".";
                case CommandRejection.WeatherBlocked:
                    return "O clima de hoje impede esta acao.";
                case CommandRejection.NotEnoughGold:
                    return "Ouro insuficiente.";
                case CommandRejection.NotEnoughEnergy:
                    return "Energia insuficiente.";
                case CommandRejection.WrongPhase:
                    return "So da para agir no Planejamento.";
                case CommandRejection.RunOver:
                    return "A run acabou.";
                case CommandRejection.TargetRequired:
                    return "Esta carta precisa de um alvo.";
                case CommandRejection.InvalidTarget:
                    return "Alvo invalido.";
                case CommandRejection.CardNotInHand:
                    return "Carta nao esta na mao.";
                case CommandRejection.GridRefused:
                    return ExplainGrid(result.GridRejection);
                default:
                    return "Acao recusada.";
            }
        }

        private static string ExplainGrid(GridRejection rejection)
        {
            switch (rejection)
            {
                case GridRejection.NotAdjacent:
                    return "So da para comprar celulas vizinhas ao territorio.";
                case GridRejection.BeyondRaceRadius:
                    return "Longe demais do Salao para esta raca.";
                case GridRejection.TerrainNotAllowed:
                    return "Terreno incompativel com este edificio.";
                case GridRejection.TileOccupied:
                    return "A celula ja tem edificio.";
                case GridRejection.TileDestroyed:
                    return "Celula arrasada: precisa ser reparada antes.";
                case GridRejection.NotOwned:
                    return "Celula nao pertence ao reino.";
                default:
                    return "Acao recusada pelo grid.";
            }
        }

        private void Log(string line)
        {
            _log.Add(line);
            if (_log.Count > 200)
            {
                _log.RemoveAt(0);
            }

            _logScroll.y = float.MaxValue;
        }

        // --- HUD provisorio (IMGUI) ---

        private const float PanelWidth = 300f;
        private const float HandCardWidth = 160f;
        private const float HandStripHeight = 150f;

        private static readonly Color ShortageHighlight = new Color(1f, 0.55f, 0.35f);

        private bool IsPointerOverHud(Vector2 screenPosition)
        {
            // A posicao do mouse vem com Y de baixo para cima; o HUD e ancorado no topo.
            Vector2 guiPoint = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            return _leftPanelRect.Contains(guiPoint)
                || _logRect.Contains(guiPoint)
                || _handStripRect.Contains(guiPoint)
                || _buildPopupRect.Contains(guiPoint)
                || _headerRect.Contains(guiPoint);
        }

        private void OnGUI()
        {
            if (_awaitingDifficultyChoice)
            {
                DrawDifficultySelect();
                return;
            }

            if (_bundle == null)
            {
                return;
            }

            if (_cardRulesStyle == null)
            {
                _cardRulesStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    fontSize = 11,
                    fontStyle = FontStyle.Italic
                };
            }

            RunState run = _bundle.Run;

            DrawLeftPanel(run);
            DrawHeader(run);
            DrawHandStrip(run);
            DrawLogPanel();
            DrawBuildPopup(run);

            DrawStaging();
            DrawGhostPrices(run);

            if (_hoveredTerrain.HasValue)
            {
                DrawTooltip(TerrainVisuals.DescriptionFor(_hoveredTerrain.Value), _hoverScreenPos);
            }

            Vector2 guiMouse = new Vector2(_hoverScreenPos.x, Screen.height - _hoverScreenPos.y);
            if (_currentWeather != null && _weatherIconRect.Contains(guiMouse))
            {
                DrawTooltip(WeatherTooltipText(), _hoverScreenPos);
            }
        }

        /// <summary>Painel de gestao, com abas que abrem e fecham (Recursos,
        /// Territorio, Ameaca, Selecao). Comprar/construir mora aqui, dentro da aba
        /// Selecao.</summary>
        private void DrawLeftPanel(RunState run)
        {
            // Para 20px acima da faixa de mao: sem isso as duas se sobrepoem quando o
            // painel tem muita coisa aberta (abas todas expandidas empurram o botao
            // "Fim do Dia" para baixo da area clicavel da mao).
            _leftPanelRect = new Rect(10, 10, PanelWidth, Screen.height - HandStripHeight - 30f);
            GUILayout.BeginArea(_leftPanelRect, GUI.skin.box);

            GUILayout.Label("Dia " + run.Day + "   |   " + run.Phase);

            // A recusa fica no topo, sempre visivel, mesmo com abas fechadas: e o
            // unico jeito do jogador ver o motivo sem abrir a aba certa.
            if (!string.IsNullOrEmpty(_lastRejection))
            {
                GUILayout.Label(">> " + _lastRejection);
            }

            _showResources = Foldout("Recursos", _showResources);
            if (_showResources)
            {
                DrawResources(run);
                DrawStatusLine(run);
                DrawWorkforce(run);
            }

            _showTerritory = Foldout("Territorio", _showTerritory);
            if (_showTerritory)
            {
                GUILayout.Label("Celulas " + run.Grid.OwnedCount + " (de pe: " + run.StandingTileCount() + ")");

                int nextCost = run.Grid.NextTileCost(run.Rules);
                GUILayout.Label("Proxima celula: " + nextCost + " ouro" +
                                (run.CanAfford(nextCost) ? string.Empty : "  (falta " + (nextCost - run.Gold) + ")"));
            }

            _showThreat = Foldout("Relogio de ameaca", _showThreat);
            if (_showThreat)
            {
                DrawThreatClock(run);
            }

            GUILayout.FlexibleSpace();

            if (run.IsOver)
            {
                DrawCollapse();
            }
            else
            {
                if (GUILayout.Button("Fim do Dia", GUILayout.Height(32)))
                {
                    EndDay();
                }

                // Acelerar e pular sao requisitos, nao conveniencias: a encenacao
                // irrita na decima repeticao, e a run tem dezenas de dias.
                if (GUILayout.Button("Encenacao: " + DayStaging.Label(_staging.Speed)))
                {
                    _staging.Speed = DayStaging.Cycle(_staging.Speed);
                }
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// Dia e relogio de ameaca, sempre visiveis no topo, entre o painel e o log
        /// (spec hud-icons — "Requirement: Relogio de ameaca no cabecalho").
        /// </summary>
        private void DrawHeader(RunState run)
        {
            const float height = 36f;
            float left = _leftPanelRect.xMax + 10f;
            _headerRect = new Rect(left, 10, Mathf.Max(80f, Screen.width - left - 10f), height);

            GUILayout.BeginArea(_headerRect, GUI.skin.box);
            GUILayout.BeginHorizontal();

            GUILayout.Label("Dia " + run.Day);
            GUILayout.Space(12);

            ScheduledThreat nearest = NearestThreat(run);
            if (nearest == null)
            {
                GUILayout.Label("sem ameaca anunciada");
            }
            else if (_icons != null && _icons.Threat != null)
            {
                GUILayout.Label(_icons.Threat, GUILayout.Width(20), GUILayout.Height(20));
                GUILayout.Label(nearest.DaysUntil(run.Day) + " dia(s)");
            }
            else
            {
                GUILayout.Label("ameaca em " + nearest.DaysUntil(run.Day) + " dia(s)");
            }

            if (run.Campaign != null && run.Campaign.HasRoster && !run.Campaign.AllDefeated
                && run.Campaign.Current != null)
            {
                GUILayout.Space(12);
                GUILayout.Label(run.Campaign.Current.DisplayName + "  " +
                    run.Campaign.RepelsAchieved + "/" + run.Campaign.RepelsNeeded);
            }

            GUILayout.FlexibleSpace();
            DrawWeatherHeader();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private static ScheduledThreat NearestThreat(RunState run)
        {
            IReadOnlyList<ScheduledThreat> pending = run.Threats.Pending;
            ScheduledThreat nearest = null;

            for (int i = 0; i < pending.Count; i++)
            {
                if (nearest == null || pending[i].ArrivalDay < nearest.ArrivalDay)
                {
                    nearest = pending[i];
                }
            }

            return nearest;
        }

        /// <summary>Ponto de tela onde o delta do relogio de ameaca nasce, perto do
        /// icone de ameaca no cabecalho.</summary>
        private Vector2 HeaderThreatAnchor()
        {
            return new Vector2(_headerRect.x + 90f, _headerRect.y + _headerRect.height * 0.5f);
        }

        /// <summary>Textura 1x1 translucida, reaproveitada pelo log flutuante e pelo
        /// tooltip: os dois pedem "sem fundo solido" (design — HUD estendido, nao
        /// migrado para uGUI).</summary>
        private Texture2D TranslucentTexture()
        {
            if (_translucentTexture == null)
            {
                _translucentTexture = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
                _translucentTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
                _translucentTexture.Apply();
            }

            return _translucentTexture;
        }

        /// <summary>Tooltip generico perto do mouse, usado pelo terreno (spec
        /// terrain-art) e pelo clima (spec weather-scenery).</summary>
        private void DrawTooltip(string text, Vector2 screenPos)
        {
            if (_tooltipStyle == null)
            {
                _tooltipStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12 };
            }

            const float width = 260f;
            float height = _tooltipStyle.CalcHeight(new GUIContent(text), width - 16f) + 14f;

            float guiY = Screen.height - screenPos.y;
            float x = screenPos.x + 16f;
            float y = guiY + 16f;

            if (x + width > Screen.width)
            {
                x = Screen.width - width - 6f;
            }

            if (y + height > Screen.height)
            {
                y = Screen.height - height - 6f;
            }

            Rect rect = new Rect(x, y, width, height);
            GUI.DrawTexture(rect, TranslucentTexture());
            GUI.Label(new Rect(rect.x + 8f, rect.y + 7f, rect.width - 16f, rect.height - 14f), text, _tooltipStyle);
        }

        /// <summary>
        /// Classifica o clima pelo Id do asset, para saber qual luz/particula usar.
        /// Amarrado aos ids autorados hoje (weather_sun, weather_rain,
        /// weather_drought); clima fora dessa lista fica no visual neutro.
        /// </summary>
        private static string WeatherKind(string weatherId)
        {
            if (weatherId == "weather_sun")
            {
                return "sun";
            }

            if (weatherId == "weather_rain")
            {
                return "rain";
            }

            if (weatherId == "weather_drought")
            {
                return "drought";
            }

            return "other";
        }

        /// <summary>Luz da cena muda de cor/intensidade por clima (spec
        /// weather-scenery).</summary>
        private void UpdateWeatherLighting()
        {
            if (_sunLight == null)
            {
                return;
            }

            switch (WeatherKind(_currentWeather?.Id))
            {
                case "sun":
                    _sunLight.color = new Color(1f, 0.92f, 0.65f);
                    _sunLight.intensity = 1.6f;
                    break;
                case "rain":
                    _sunLight.color = new Color(0.65f, 0.70f, 0.78f);
                    _sunLight.intensity = 0.75f;
                    break;
                case "drought":
                    _sunLight.color = new Color(1f, 0.85f, 0.55f);
                    _sunLight.intensity = 1.1f;
                    break;
                default:
                    _sunLight.color = Color.white;
                    _sunLight.intensity = 1.1f;
                    break;
            }
        }

        /// <summary>
        /// Troca a particula de cenario do clima (chuva caindo, vento seco, brilho de
        /// sol), criada por codigo sem prefab (design — mesmo padrao de BuildScene).
        /// So reconstroi quando o "tipo" de clima muda, nao a cada dia.
        /// </summary>
        private void ApplyWeatherVisuals(WeatherDefinition today)
        {
            UpdateWeatherLighting();

            string kind = WeatherKind(today?.Id);
            if (kind == _weatherParticleKind)
            {
                return;
            }

            _weatherParticleKind = kind;

            if (_weatherParticles != null)
            {
                Destroy(_weatherParticles.gameObject);
                _weatherParticles = null;
            }

            if (kind == "other" || _gridView == null)
            {
                return;
            }

            GameObject host = new GameObject("Clima FX");
            host.transform.SetParent(transform, false);
            _weatherParticles = host.AddComponent<ParticleSystem>();

            Bounds bounds = _gridView.WorldBounds();
            host.transform.position = bounds.center + Vector3.up * (bounds.extents.y + 3f);

            ParticleSystem.MainModule main = _weatherParticles.main;
            ParticleSystem.EmissionModule emission = _weatherParticles.emission;
            ParticleSystem.ShapeModule shape = _weatherParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(bounds.size.x + 2f, 0.2f, bounds.size.z + 2f);

            switch (kind)
            {
                case "rain":
                    main.startLifetime = 1.2f;
                    main.startSpeed = 6f;
                    main.startSize = 0.04f;
                    main.startColor = new Color(0.6f, 0.75f, 0.95f, 0.6f);
                    main.gravityModifier = 1.2f;
                    emission.rateOverTime = 200f;
                    break;
                case "sun":
                    main.startLifetime = 2.5f;
                    main.startSpeed = 0.3f;
                    main.startSize = 0.08f;
                    main.startColor = new Color(1f, 0.92f, 0.5f, 0.8f);
                    main.gravityModifier = -0.05f;
                    emission.rateOverTime = 6f;
                    break;
                case "drought":
                    main.startLifetime = 2f;
                    main.startSpeed = 1.2f;
                    main.startSize = 0.06f;
                    main.startColor = new Color(0.75f, 0.65f, 0.45f, 0.5f);
                    main.gravityModifier = 0f;
                    emission.rateOverTime = 40f;

                    ParticleSystem.VelocityOverLifetimeModule velocity = _weatherParticles.velocityOverLifetime;
                    velocity.enabled = true;
                    velocity.x = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
                    break;
            }
        }

        /// <summary>Icone de clima no cabecalho, na ponta direita (spec
        /// weather-scenery). O hover é lido em OnGUI, fora do BeginArea do
        /// cabecalho, contra o retangulo absoluto guardado aqui.</summary>
        private void DrawWeatherHeader()
        {
            if (_currentWeather == null)
            {
                return;
            }

            Texture2D icon = _weatherIcons != null ? _weatherIcons.IconFor(_currentWeather.Id) : null;

            if (icon != null)
            {
                GUILayout.Label(icon, GUILayout.Width(24), GUILayout.Height(24));
            }
            else
            {
                GUILayout.Label(_currentWeather.DisplayName, GUILayout.ExpandWidth(false));
            }

            if (Event.current.type == EventType.Repaint)
            {
                Rect local = GUILayoutUtility.GetLastRect();
                _weatherIconRect = new Rect(_headerRect.x + local.x, _headerRect.y + local.y, local.width, local.height);
            }
        }

        private string WeatherTooltipText()
        {
            if (_currentWeather == null)
            {
                return string.Empty;
            }

            string text = _currentWeather.DisplayName + ": " +
                (string.IsNullOrEmpty(_currentWeather.FlavorText) ? "sem efeito descrito." : _currentWeather.FlavorText);

            if (_tomorrowWeather != null)
            {
                text += "\n-> amanha: " + _tomorrowWeather.DisplayName;

                if (_tomorrowWeather.FoodUpkeepMultiplier > 1.0)
                {
                    text += " (consumo de comida sobe)";
                }
                else if (_tomorrowWeather.FoodUpkeepMultiplier < 1.0)
                {
                    text += " (consumo de comida cai)";
                }
            }

            return text;
        }

        /// <summary>Cabecalho clicavel que abre/fecha uma secao, com linha
        /// separadora — as secoes ficam distinguiveis a olho, e o jogador so ve o
        /// que interessa no momento.</summary>
        private static bool Foldout(string title, bool open)
        {
            GUILayout.Space(6);
            if (GUILayout.Button((open ? "- " : "+ ") + title))
            {
                open = !open;
            }

            if (open)
            {
                GUILayout.Box(string.Empty, GUILayout.Height(1), GUILayout.ExpandWidth(true));
            }

            return open;
        }

        /// <summary>Desenha o custo sobre cada celula compravel, projetado na tela.</summary>
        private void DrawGhostPrices(RunState run)
        {
            if (run.IsOver)
            {
                return;
            }

            int cost = run.Grid.NextTileCost(run.Rules);
            bool affordable = run.CanAfford(cost);
            string label = cost + (affordable ? string.Empty : " x");

            List<KeyValuePair<Coord, Vector3>> anchors = _gridView.GhostAnchors();

            for (int i = 0; i < anchors.Count; i++)
            {
                Vector3 screen = _camera.WorldToScreenPoint(anchors[i].Value);
                if (screen.z <= 0f)
                {
                    continue;
                }

                Rect rect = new Rect(screen.x - 28f, Screen.height - screen.y - 12f, 56f, 22f);
                GUI.Label(rect, label);
            }
        }

        /// <summary>
        /// Os cinco recursos, com a variacao prevista do dia. O recurso que a ultima
        /// recusa apontou aparece marcado: a mensagem diz o que falta, e a marca diz
        /// onde olhar (spec game-feel).
        /// </summary>
        private void DrawResources(RunState run)
        {
            ResourceAmounts predicted = run.CollectDailyProductionByResource();
            int upkeep = run.DailyFoodUpkeep();

            for (int i = 0; i < ResourceKinds.All.Length; i++)
            {
                ResourceKind kind = ResourceKinds.All[i];
                int delta = predicted[kind];

                if (kind == ResourceKind.Food)
                {
                    delta -= upkeep;
                }

                if (kind == ResourceKind.Population)
                {
                    continue;
                }

                string deltaText = delta != 0 ? "  (" + (delta > 0 ? "+" : string.Empty) + delta + "/dia)" : string.Empty;
                bool missing = _lastShortage.Any && _lastShortage.Kind == kind;
                Texture2D icon = _icons != null ? _icons.For(kind) : null;

                GUILayout.BeginHorizontal();

                if (icon != null)
                {
                    // Recurso em falta continua destacavel mesmo com icone (spec
                    // hud-icons): tinge o texto em vez de depender do prefixo ">>".
                    Color previous = GUI.color;
                    if (missing)
                    {
                        GUI.color = ShortageHighlight;
                    }

                    GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));
                    GUILayout.Label(run[kind] + deltaText);
                    GUI.color = previous;
                }
                else
                {
                    string line = ResourceKinds.DisplayName(kind).PadRight(9) + run[kind] + deltaText;
                    GUILayout.Label(missing ? ">> " + line + "  <<" : "   " + line);
                }

                GUILayout.EndHorizontal();
            }
        }

        /// <summary>Energia, integridade e defesa: icone e valor quando atribuido,
        /// texto quando nao (spec hud-icons).</summary>
        private void DrawStatusLine(RunState run)
        {
            GUILayout.BeginHorizontal();

            if (_icons != null && _icons.Energy != null)
            {
                GUILayout.Label(_icons.Energy, GUILayout.Width(20), GUILayout.Height(20));
                GUILayout.Label(run.Energy + "/" + run.EnergyPerDay, GUILayout.ExpandWidth(false));
            }
            else
            {
                GUILayout.Label("Energia " + run.Energy + "/" + run.EnergyPerDay, GUILayout.ExpandWidth(false));
            }

            GUILayout.Space(10);
            GUILayout.Label("Integridade " + run.Integrity + "/" + run.MaxIntegrity, GUILayout.ExpandWidth(false));

            GUILayout.Space(10);

            if (_icons != null && _icons.Defense != null)
            {
                GUILayout.Label(_icons.Defense, GUILayout.Width(20), GUILayout.Height(20));
                GUILayout.Label(run.TotalDefense().ToString(), GUILayout.ExpandWidth(false));
            }
            else
            {
                GUILayout.Label("Defesa " + run.TotalDefense(), GUILayout.ExpandWidth(false));
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawWorkforce(RunState run)
        {
            WorkerAllocation allocation = run.Allocation();
            int capacity = run.PopulationCapacity();
            int idlePopulation = Mathf.Max(0, run[ResourceKind.Population] - allocation.Assigned);

            string detail = "  (" + allocation.Assigned + " trabalhando";
            if (idlePopulation > 0)
            {
                detail += ", " + idlePopulation + " ociosa";
            }

            if (allocation.IdleBuildings > 0)
            {
                detail += ", " + allocation.IdleBuildings + " edif. parado(s)";
            }

            detail += ")";

            bool missing = _lastShortage.Any && _lastShortage.Kind == ResourceKind.Population;
            Texture2D icon = _icons != null ? _icons.For(ResourceKind.Population) : null;

            GUILayout.BeginHorizontal();

            if (icon != null)
            {
                Color previous = GUI.color;
                if (missing)
                {
                    GUI.color = ShortageHighlight;
                }

                GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));
                GUILayout.Label(run[ResourceKind.Population] + "/" + capacity + detail);
                GUI.color = previous;
            }
            else
            {
                string line = "populacao ".PadRight(9) + run[ResourceKind.Population] + "/" + capacity + detail;
                GUILayout.Label(missing ? ">> " + line + "  <<" : "   " + line);
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Postura:", GUILayout.Width(60));
            if (GUILayout.Button(StanceLabel(run.Stance)))
            {
                run.Stance = NextStance(run.Stance);
                _gridView.Sync();
            }

            GUILayout.EndHorizontal();
        }

        private static WorkerStance NextStance(WorkerStance stance)
        {
            switch (stance)
            {
                case WorkerStance.Balanced:
                    return WorkerStance.ProductionFirst;
                case WorkerStance.ProductionFirst:
                    return WorkerStance.DefenseFirst;
                default:
                    return WorkerStance.Balanced;
            }
        }

        private static string StanceLabel(WorkerStance stance)
        {
            switch (stance)
            {
                case WorkerStance.ProductionFirst:
                    return "campos primeiro";
                case WorkerStance.DefenseFirst:
                    return "muralhas primeiro";
                default:
                    return "equilibrada";
            }
        }

        /// <summary>A cena do dia, com o passo atual em destaque.</summary>
        private void DrawStaging()
        {
            if (!_staging.IsPlaying && _staging.Current == null)
            {
                return;
            }

            DayStep step = _staging.Current;
            if (step == null)
            {
                return;
            }

            float width = 460f;
            float height = step.IsHighlight ? 70f : 46f;
            Rect rect = new Rect((Screen.width - width) * 0.5f, 60f, width, height);

            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label(step.IsHighlight ? "*** " + step.Headline + " ***" : step.Headline);

            if (GUILayout.Button("pular"))
            {
                _staging.SkipRest();
            }

            GUILayout.EndArea();
        }

        private void DrawThreatClock(RunState run)
        {
            IReadOnlyList<ScheduledThreat> pending = run.Threats.Pending;
            if (pending.Count == 0)
            {
                GUILayout.Label("nada anunciado");
                return;
            }

            for (int i = 0; i < pending.Count; i++)
            {
                ScheduledThreat threat = pending[i];
                GUILayout.Label(threat.Kind + " em " + threat.DaysUntil(run.Day) +
                                " dia(s), forca " + threat.Force);
            }
        }

        /// <summary>
        /// Menu central de construcao: abre ao selecionar uma celula, fecha ao
        /// selecionar outra ou ao desselecionar (spec building-placement). Substitui
        /// a lista de botoes que vivia dentro do painel esquerdo.
        /// </summary>
        private void DrawBuildPopup(RunState run)
        {
            if (!_gridView.Selected.HasValue)
            {
                _buildPopupRect = new Rect(0, 0, 0, 0);
                return;
            }

            Coord coord = _gridView.Selected.Value;
            Tile tile = run.Grid.TileAt(coord);

            const float width = 360f;

            if (tile.HasBuilding)
            {
                ProductionBreakdown breakdown = ProductionCalculator.ForTile(run.Grid, tile, run.Rules);
                float height = 90f + breakdown.Lines.Count * 20f;
                BeginPopup(width, height);

                GUILayout.Label("Celula " + coord + "   |   " + tile.Terrain +
                                (tile.Destroyed ? " (arrasada)" : string.Empty));
                GUILayout.Label(tile.Building.DisplayName + " => " + breakdown.Total + " ouro/dia");

                for (int i = 0; i < breakdown.Lines.Count; i++)
                {
                    GUILayout.Label("   " + breakdown.Lines[i]);
                }

                if (tile.Coord != run.Grid.HallCoord && GUILayout.Button("Demolir"))
                {
                    _bundle.Engine.Demolish(coord);
                    AfterStateChanged();
                }

                GUILayout.EndArea();
                return;
            }

            if (!tile.Owned)
            {
                int cost = run.Grid.NextTileCost(run.Rules);
                BeginPopup(width, 110f);

                GUILayout.Label("Celula " + coord + "   |   " +
                                (tile.Revealed ? tile.Terrain.ToString() : "desconhecida"));

                GUI.enabled = run.CanAfford(cost);
                if (GUILayout.Button("Comprar (" + cost + " ouro)", GUILayout.Height(30)))
                {
                    TryBuy(coord);
                }

                GUI.enabled = true;
                GUILayout.EndArea();
                return;
            }

            List<BuildingDefinition> buildings = _bundle.AvailableBuildings();
            float popupHeight = 60f + buildings.Count * 44f;
            BeginPopup(width, popupHeight);

            GUILayout.Label("Celula " + coord + "   |   " + tile.Terrain);

            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingDefinition building = buildings[i];
                if (building.Id == RunBuilder.HallBuildingId)
                {
                    continue;
                }

                bool compatible = building.CanBuildOn(tile.Terrain);
                string label = building.DisplayName + " (" + building.GoldCost + " ouro, +" +
                               building.BaseGoldProduction + "/dia)" +
                               (compatible ? string.Empty : "  [incompativel]");

                GUILayout.BeginHorizontal();

                // Retrato 3D quando o predio tem prefab atribuido (renderizado uma
                // vez em BuildBuildingPreviews); sem prefab, so o botao com texto.
                if (_buildingPreviews.TryGetValue(building.Id, out RenderTexture preview) && preview != null)
                {
                    GUILayout.Label(preview, GUILayout.Width(40), GUILayout.Height(40));
                }

                // Fundo amarelo indica compativel; sem cor indica incompativel (spec
                // building-placement). GUI.backgroundColor tinge o skin do botao sem
                // precisar de uma textura propria.
                GUI.backgroundColor = compatible ? TerrainVisuals.PurchasableColor : Color.white;
                GUI.enabled = compatible && run.CanAfford(building.GoldCost);

                if (GUILayout.Button(label, GUILayout.Height(40)))
                {
                    TryBuild(building, coord);
                }

                GUI.enabled = true;
                GUI.backgroundColor = Color.white;

                GUILayout.EndHorizontal();
            }

            GUILayout.EndArea();
        }

        /// <summary>Abre a area do popup central, acima do tabuleiro, e registra o
        /// retangulo para o clique nao atravessar para o mundo.</summary>
        private void BeginPopup(float width, float height)
        {
            _buildPopupRect = new Rect((Screen.width - width) * 0.5f, 140f, width, height);
            GUILayout.BeginArea(_buildPopupRect, GUI.skin.box);
        }

        /// <summary>
        /// A mao vive na propria faixa, centralizada embaixo da tela, separada do
        /// painel de gestao: e a area de decisao do turno, cartas grandes o
        /// suficiente para ler a descricao sem apertar os olhos.
        /// </summary>
        private void DrawHandStrip(RunState run)
        {
            List<CardDefinition> hand = new List<CardDefinition>(run.Deck.Hand);

            float width = Mathf.Min(Mathf.Max(hand.Count, 1) * HandCardWidth + 20f, Screen.width - 40f);
            _handStripRect = new Rect((Screen.width - width) * 0.5f, Screen.height - HandStripHeight - 10f,
                width, HandStripHeight);

            GUILayout.BeginArea(_handStripRect, GUI.skin.box);
            GUILayout.Label("Mao (" + hand.Count + ")");

            if (hand.Count == 0)
            {
                GUILayout.Label("sem cartas");
                GUILayout.EndArea();
                return;
            }

            GUILayout.BeginHorizontal();

            for (int i = 0; i < hand.Count; i++)
            {
                CardDefinition card = hand[i];
                GUI.enabled = run.Energy >= card.EnergyCost;

                GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(HandCardWidth - 10f));

                // Nem toda carta vai ter arte propria nesta leva; sem ela a carta
                // continua jogavel, so com o layout de texto (spec proposal, item 8).
                if (_cardArt.TryGetValue(card.Id, out Texture2D art) && art != null)
                {
                    GUILayout.Label(art, GUILayout.Height(64));
                }

                if (GUILayout.Button(card.DisplayName + "  [" + card.EnergyCost + "]"))
                {
                    TryPlayCard(card);
                }

                // Descricao vem do asset (RulesText); sem isso o jogador so via nome e
                // custo, e tinha que adivinhar o efeito jogando a carta.
                if (!string.IsNullOrEmpty(card.RulesText))
                {
                    GUILayout.Label(card.RulesText, _cardRulesStyle);
                }

                GUILayout.EndVertical();

                GUI.enabled = true;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawCollapse()
        {
            RunScore score = _bundle.Engine.FinalScore;
            bool victory = _bundle.Run.Outcome == RunOutcome.Victory;

            GUILayout.Label(victory
                ? "=== VITORIA ==="
                : "=== COLAPSO: " + _bundle.Run.Collapse + " ===");

            if (score != null)
            {
                for (int i = 0; i < score.Lines.Count; i++)
                {
                    GUILayout.Label(score.Lines[i].ToString());
                }

                GUILayout.Label("Total " + score.TotalPoints + " pts  =>  " +
                                score.MetaCurrency + " moedas de meta");
            }

            if (victory && !string.IsNullOrEmpty(_currentLevelId))
            {
                DifficultyLevel next = NextLevelAfter(_currentLevelId);
                if (next != null)
                {
                    GUILayout.Label("Nivel seguinte desbloqueado: " + next.DisplayName);
                }
                else
                {
                    GUILayout.Label("Ultimo nivel da escada vencido.");
                }
            }

            if (GUILayout.Button("Nova run", GUILayout.Height(30)))
            {
                if (_difficultyLevels.Count > 0)
                {
                    _awaitingDifficultyChoice = true;
                }
                else
                {
                    StartRun(Random.Range(1, int.MaxValue), null);
                }
            }
        }

        private DifficultyLevel NextLevelAfter(string levelId)
        {
            for (int i = 0; i < _difficultyLevels.Count - 1; i++)
            {
                if (_difficultyLevels[i].Id == levelId)
                {
                    return _difficultyLevels[i + 1];
                }
            }

            return null;
        }

        /// <summary>
        /// Grava o resultado da run no perfil (task 10.4): vitoria desbloqueia o
        /// proximo nivel da escada, colapso nao desbloqueia nada (spec
        /// difficulty-ladder). Salva em disco na hora — sem essa run nao ha um
        /// "fim de sessao" melhor para gravar.
        /// </summary>
        private void OnRunEnded(RunScore score)
        {
            if (_metaProfile == null || score == null)
            {
                return;
            }

            _metaProfile.RecordRun(score, _bundle.Run.Day);

            if (_bundle.Run.Outcome == RunOutcome.Victory && !string.IsNullOrEmpty(_currentLevelId))
            {
                _metaProfile.UnlockNextDifficulty(_currentLevelId, _difficultyLevels);
            }

            _profileStore?.Save(_metaProfile);
        }

        /// <summary>Tela antes da run comecar, quando ha ao menos um nivel de
        /// dificuldade autorado (task 10.4). O primeiro nivel esta sempre liberado; os
        /// demais exigem ter vencido o anterior nesta maquina.</summary>
        private void DrawDifficultySelect()
        {
            Rect area = new Rect(Screen.width * 0.5f - 200f, Screen.height * 0.5f - 200f, 400f, 400f);
            GUILayout.BeginArea(area, GUI.skin.box);

            GUILayout.Label("=== ESCOLHA A DIFICULDADE ===");

            for (int i = 0; i < _difficultyLevels.Count; i++)
            {
                DifficultyLevel level = _difficultyLevels[i];
                bool unlocked = _metaProfile.IsDifficultyUnlocked(level.Id, _difficultyLevels);

                GUILayout.BeginVertical(GUI.skin.box);
                GUI.enabled = unlocked;

                if (GUILayout.Button(level.DisplayName + (unlocked ? string.Empty : "  (bloqueado)")))
                {
                    StartRun(_seed == 0 ? Random.Range(1, int.MaxValue) : _seed, level);
                }

                GUI.enabled = true;

                for (int h = 0; h < level.Hardenings.Count; h++)
                {
                    GUILayout.Label("- " + level.Hardenings[h]);
                }

                GUILayout.EndVertical();
            }

            GUILayout.EndArea();
        }

        /// <summary>Caixa flutuante ao lado da mao, semi-transparente e sem fundo
        /// solido: Enter abre (HandleLogToggle), X fecha. Fica escondida quando
        /// fechada, em vez de reservar espaco fixo na tela.</summary>
        private void DrawLogPanel()
        {
            if (!_logOpen)
            {
                _logRect = new Rect(0, 0, 0, 0);
                return;
            }

            const float width = 320f;
            float x = _handStripRect.xMax + 10f;
            if (x + width > Screen.width - 10f)
            {
                x = Screen.width - width - 10f;
            }

            _logRect = new Rect(x, _handStripRect.y, width, HandStripHeight);

            GUI.DrawTexture(_logRect, TranslucentTexture());
            GUILayout.BeginArea(_logRect);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Log");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("X", GUILayout.Width(24)))
            {
                _logOpen = false;
            }

            GUILayout.EndHorizontal();

            _logScroll = GUILayout.BeginScrollView(_logScroll);

            for (int i = 0; i < _log.Count; i++)
            {
                GUILayout.Label(_log[i]);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}

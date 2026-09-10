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

        [Tooltip("Nivel de dificuldade da run. Limitado ao mais alto que o perfil ja liberou. " +
                 "Andaime ate existir a tela de selecao.")]
        [Min(1)]
        [SerializeField] private int _levelRank = 1;

        [Header("Cena")]
        [SerializeField] private bool _createLighting = true;

        private RunBundle _bundle;
        private GridView _gridView;
        private IsometricCameraRig _cameraRig;
        private Camera _camera;

        private readonly List<string> _log = new List<string>();
        private string _lastRejection;
        private Vector2 _logScroll;

        private FloatingNumbers _numbers;
        private readonly DayStaging _staging = new DayStaging();

        /// <summary>Recurso que a ultima recusa apontou, para destacar no painel.</summary>
        private ResourceShortage _lastShortage = ResourceShortage.None;

        private FileProfileStore _profileStore;
        private MetaProfile _profile;
        private DifficultyLadder _ladder;

        /// <summary>Nivel que a run encerrada acabou de liberar, para nomear na tela de fim.</summary>
        private DifficultyLevel _justUnlocked;

        private void Start()
        {
            if (_database == null)
            {
                Debug.LogError("GameBootstrap sem GameDatabase: arraste o asset no Inspector.", this);
                enabled = false;
                return;
            }

            LoadProfile();
            StartRun(_seed == 0 ? Random.Range(1, int.MaxValue) : _seed);
        }

        /// <summary>
        /// O perfil guarda moeda de meta e niveis liberados entre sessoes. Save
        /// ilegivel nunca impede o jogo de abrir: o Core preserva o arquivo e devolve
        /// um perfil novo.
        /// </summary>
        private void LoadProfile()
        {
            _profileStore = new FileProfileStore(
                System.IO.Path.Combine(Application.persistentDataPath, "profile.json"));
            ProfileLoadResult result = _profileStore.Load();
            _profile = result.Profile;

            if (result.Status == ProfileLoadStatus.Corrupt)
            {
                Debug.LogWarning(result.Message + (result.BackupPath != null ? " Copia em " + result.BackupPath : ""));
            }
        }

        /// <summary>Nivel pedido no Inspector, limitado ao que o perfil ja liberou e ao que existe.</summary>
        private int ResolveLevelRank()
        {
            int rank = Mathf.Clamp(_levelRank, 1, _profile.HighestUnlockedLevel);
            return _ladder.LevelOfRank(rank) != null ? rank : 1;
        }

        private void RecordRunEnd(RunScore score)
        {
            int unlocked = _profile.RecordRun(score, _bundle.Run.Stats.DaysSurvived);
            _justUnlocked = unlocked > 0 ? _ladder.LevelOfRank(unlocked) : null;

            try
            {
                _profileStore.Save(_profile);
            }
            catch (System.Exception error)
            {
                Debug.LogError("Nao consegui salvar o perfil: " + error.Message, this);
            }

            if (_justUnlocked != null)
            {
                Log("DESBLOQUEADO: " + _justUnlocked.DisplayName + ".");
            }
        }

        private void StartRun(int seed)
        {
            ContentCatalog catalog = _database.BuildCatalog();

            if (!catalog.Races.ContainsKey(_raceId))
            {
                Debug.LogError("Raca '" + _raceId + "' nao existe no GameDatabase.", this);
                enabled = false;
                return;
            }

            _ladder = _database.BuildLadder();
            _justUnlocked = null;
            RunSetup setup = _database.CreateSetup(_raceId, seed, ResolveLevelRank());

            try
            {
                _bundle = RunBuilder.Build(setup, catalog);
            }
            catch (System.Exception error)
            {
                Debug.LogError("Nao consegui montar a run: " + error.Message, this);
                enabled = false;
                return;
            }

            BuildScene();

            _bundle.Engine.StartRun();
            _gridView.Initialize(_bundle.Run);
            _cameraRig.Frame(_gridView.WorldBounds(), instant: true);

            _log.Clear();
            Log("Run iniciada. Semente " + seed + ", raca " + _bundle.Run.Race.DisplayName +
                (_bundle.Run.Level != null ? ", " + _bundle.Run.Level.DisplayName : string.Empty) + ".");
            ConsumeEvents();
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

            if (_createLighting && FindAnyObjectByType<Light>() == null)
            {
                GameObject lightObject = new GameObject("Sol");
                Light sun = lightObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.1f;
                sun.shadows = LightShadows.Soft;
                lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }

        private void Update()
        {
            if (_bundle == null)
            {
                return;
            }

            _staging.Tick(Time.deltaTime);
            HandleClick();
        }

        private void HandleClick()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            // Com um momento na tela, o dia esta parado: nada no tabuleiro responde.
            if (_staging.IsAwaitingAcknowledge)
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

            if (tile.IsGhost)
            {
                TryBuy(tile.Coord);
                return;
            }

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

                        break;

                    case ThreatAnnouncedEvent announced:
                        Log("AMEACA: " + announced.Threat.Kind + " no dia " + announced.Threat.ArrivalDay +
                            ", forca prevista " + announced.Threat.Force + ".");
                        break;

                    case AttackResolvedEvent attack:
                        Log("ATAQUE " + attack.Report.Kind + ": forca " + attack.Report.Force +
                            " vs defesa " + attack.Report.Defense + " => " +
                            attack.Report.TilesDestroyed.Count + " celula(s), " +
                            attack.Report.BaseDamage + " de dano" +
                            (attack.Report.ResourcesLost.IsEmpty ? string.Empty : ", levou " + attack.Report.ResourcesLost) +
                            (attack.Report.Repelled ? " (repelido)." : "."));
                        break;

                    case RivalDeclaredWarEvent declared:
                        Log("GUERRA: " + declared.Rival.DisplayName + " (" + declared.Rival.Identity.DisplayName +
                            ") — " + declared.Rival.Identity.Description + " Repelir " +
                            declared.Rival.AttacksToDefeat + " ataques para derrota-lo.");
                        break;

                    case RivalDefeatedEvent defeated:
                        Log("DERROTADO: " + defeated.Rival.DisplayName + ".");
                        break;

                    case DayEventResolvedEvent dayEvent:
                        Log("EVENTO [" + dayEvent.Outcome.Definition.Class + "] " +
                            dayEvent.Outcome.Definition.DisplayName + ": " + Summarize(dayEvent.Outcome));
                        break;

                    case MilestoneReachedEvent milestone:
                        Log("MARCO: " + milestone.Label);
                        break;

                    case RunWonEvent won:
                        Log("=== VITORIA === " + won.Score.TotalPoints + " pts, " +
                            won.Score.MetaCurrency + " moedas de meta.");
                        RecordRunEnd(won.Score);
                        break;

                    case RunCollapsedEvent collapsed:
                        Log("=== COLAPSO (" + collapsed.Reason + ") === " +
                            collapsed.Score.TotalPoints + " pts, " +
                            collapsed.Score.MetaCurrency + " moedas de meta.");
                        RecordRunEnd(collapsed.Score);
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
        //
        // Andaime, mas organizado em regioes fixas: barra de estado no topo, painel de
        // consulta com rolagem a esquerda, mao de cartas embaixo e acoes a direita.
        // Tudo e desenhado numa resolucao virtual e escalado para a tela real, para
        // que nada corte num monitor menor. A UI definitiva substitui isto inteiro.

        private const float ReferenceWidth = 1600f;
        private const float ReferenceHeight = 900f;
        private const float TopBarHeight = 66f;
        private const float SidePanelWidth = 340f;
        private const float HandHeight = 190f;
        private const float ActionsWidth = 190f;
        private const float CardWidth = 190f;
        private const float LogWidth = 460f;
        private const float Margin = 8f;

        private float _uiScale = 1f;
        private Vector2 _sideScroll;
        private Vector2 _handScroll;
        private bool _showLog;
        private bool _openRivals = true;
        private bool _openClock = true;
        private bool _openKingdom = true;
        private bool _openCell = true;

        private GUIStyle _wrapStyle;
        private GUIStyle _boldStyle;
        private GUIStyle _sectionStyle;
        private static GUIStyle _momentTitle;

        private float VirtualWidth => Screen.width / _uiScale;

        private float VirtualHeight => Screen.height / _uiScale;

        private Rect TopBarRect => new Rect(0f, 0f, VirtualWidth, TopBarHeight);

        private Rect SideRect => new Rect(
            Margin, TopBarHeight + Margin, SidePanelWidth, VirtualHeight - TopBarHeight - HandHeight - 3f * Margin);

        private Rect HandRect => new Rect(
            Margin, VirtualHeight - HandHeight - Margin, VirtualWidth - ActionsWidth - 3f * Margin, HandHeight);

        private Rect ActionsRect => new Rect(
            VirtualWidth - ActionsWidth - Margin, VirtualHeight - HandHeight - Margin, ActionsWidth, HandHeight);

        private Rect LogRect => new Rect(
            VirtualWidth - LogWidth - Margin, TopBarHeight + Margin, LogWidth,
            VirtualHeight - TopBarHeight - HandHeight - 3f * Margin);

        private void UpdateUiScale()
        {
            _uiScale = Mathf.Clamp(Mathf.Min(Screen.width / ReferenceWidth, Screen.height / ReferenceHeight), 0.6f, 2f);
        }

        private bool IsPointerOverHud(Vector2 screenPosition)
        {
            // O mouse vem em pixels com Y de baixo para cima; o HUD vive na resolucao
            // virtual com Y de cima para baixo.
            Vector2 point = new Vector2(screenPosition.x / _uiScale, (Screen.height - screenPosition.y) / _uiScale);

            return TopBarRect.Contains(point) ||
                   SideRect.Contains(point) ||
                   HandRect.Contains(point) ||
                   ActionsRect.Contains(point) ||
                   (_showLog && LogRect.Contains(point)) ||
                   (_bundle != null && _bundle.Run.IsOver);
        }

        private void EnsureStyles()
        {
            if (_wrapStyle != null)
            {
                return;
            }

            _wrapStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12 };
            _boldStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            _sectionStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
        }

        private void OnGUI()
        {
            if (_bundle == null)
            {
                return;
            }

            EnsureStyles();
            UpdateUiScale();

            Matrix4x4 screenMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(_uiScale, _uiScale, 1f));

            RunState run = _bundle.Run;

            // Um momento para o dia: o HUD continua visivel, mas nada nele responde
            // ate o jogador reconhecer o que aconteceu.
            bool blocked = _staging.IsAwaitingAcknowledge;

            DrawGhostPrices(run);

            GUI.enabled = !blocked;
            DrawTopBar(run);
            DrawSidePanel(run);
            DrawHand(run);
            DrawActions(run);

            if (_showLog)
            {
                DrawLog();
            }

            DrawRejection();

            if (run.IsOver && !blocked)
            {
                DrawRunEnd();
            }

            GUI.enabled = true;
            DrawStaging();

            GUI.matrix = screenMatrix;
        }

        // --- Barra de estado ---

        /// <summary>
        /// Tudo que muda a cada acao, numa linha que nunca rola: recursos com a
        /// variacao prevista do dia, gente, energia, integridade e defesa. O recurso
        /// que a ultima recusa apontou aparece marcado (spec game-feel).
        /// </summary>
        private void DrawTopBar(RunState run)
        {
            GUILayout.BeginArea(TopBarRect, GUI.skin.box);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Dia " + run.Day, _boldStyle, GUILayout.Width(70));
            if (run.Level != null)
            {
                GUILayout.Label(run.Level.DisplayName, GUILayout.Width(140));
            }

            GUILayout.Label(PhaseLabel(run.Phase), GUILayout.Width(120));
            GUILayout.FlexibleSpace();

            if (run.TodayWeather != null)
            {
                string tomorrow = run.Weather.Tomorrow != null ? run.Weather.Tomorrow.DisplayName : "?";
                GUILayout.Label("Clima hoje: " + run.TodayWeather.DisplayName + "     amanha: " + tomorrow);
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            ResourceAmounts predicted = run.CollectDailyProductionByResource();
            int upkeep = run.DailyFoodUpkeep();

            for (int i = 0; i < ResourceKinds.All.Length; i++)
            {
                ResourceKind kind = ResourceKinds.All[i];
                if (kind == ResourceKind.Population)
                {
                    continue;
                }

                int delta = predicted[kind] - (kind == ResourceKind.Food ? upkeep : 0);
                string chip = ResourceKinds.DisplayName(kind) + " " + run[kind] +
                              (delta != 0 ? " (" + (delta > 0 ? "+" : string.Empty) + delta + ")" : string.Empty);
                GUILayout.Label(Marked(chip, kind), GUILayout.Width(150));
            }

            WorkerAllocation allocation = run.Allocation();
            string people = "gente " + run[ResourceKind.Population] + "/" + run.PopulationCapacity() +
                            " (" + allocation.Assigned + " trab." +
                            (allocation.IdleBuildings > 0 ? ", " + allocation.IdleBuildings + " parado(s)" : string.Empty) + ")";
            GUILayout.Label(Marked(people, ResourceKind.Population), GUILayout.Width(210));

            GUILayout.FlexibleSpace();
            GUILayout.Label("energia " + run.Energy + "/" + run.EnergyPerDay, GUILayout.Width(90));
            GUILayout.Label("integridade " + run.Integrity + "/" + run.MaxIntegrity, GUILayout.Width(120));
            GUILayout.Label("defesa " + run.TotalDefense(), GUILayout.Width(80));
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private string Marked(string text, ResourceKind kind)
        {
            bool missing = _lastShortage.Any && _lastShortage.Kind == kind;
            return missing ? ">> " + text + " <<" : text;
        }

        private static string PhaseLabel(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.Planning:
                    return "Planejamento";
                case DayPhase.Resolution:
                    return "Resolucao";
                case DayPhase.Collapsed:
                    return "Colapso";
                case DayPhase.Won:
                    return "Vitoria";
                default:
                    return phase.ToString();
            }
        }

        // --- Painel de consulta ---

        private void DrawSidePanel(RunState run)
        {
            GUILayout.BeginArea(SideRect, GUI.skin.box);
            _sideScroll = GUILayout.BeginScrollView(_sideScroll);

            if (Section("Rivais em guerra", ref _openRivals))
            {
                DrawRivals(run);
            }

            if (Section("Relogio de ameaca", ref _openClock))
            {
                DrawThreatClock(run);
            }

            if (Section("Reino", ref _openKingdom))
            {
                DrawKingdom(run);
            }

            if (Section("Celula selecionada", ref _openCell))
            {
                DrawSelection(run);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>Cabecalho de secao recolhivel. Devolve se a secao esta aberta.</summary>
        private bool Section(string title, ref bool open)
        {
            GUILayout.Space(4);
            if (GUILayout.Button((open ? "[-] " : "[+] ") + title, _sectionStyle))
            {
                open = !open;
            }

            return open;
        }

        private void DrawRivals(RunState run)
        {
            ThreatClock clock = run.Threats;
            if (!clock.HasRivals)
            {
                GUILayout.Label("Nenhum rival no conteudo: a ameaca e anonima.", _wrapStyle);
                return;
            }

            List<RivalCampaign> active = clock.ActiveCampaigns();
            for (int i = 0; i < active.Count; i++)
            {
                RivalCampaign campaign = active[i];
                GUILayout.Label(campaign.Rival.DisplayName + " (" + campaign.Rival.Identity.DisplayName + ")", _boldStyle);
                GUILayout.Label("repelidos " + campaign.AttacksRepelled + " de " + campaign.Rival.AttacksToDefeat +
                                (campaign.AttacksSuffered > 0 ? "   |   sofridos " + campaign.AttacksSuffered : string.Empty));
                GUILayout.Label(campaign.Rival.Identity.Description, _wrapStyle);
                GUILayout.Space(4);
            }

            if (clock.NextRivalToEnter != null)
            {
                GUILayout.Label("Proximo a entrar: " + clock.NextRivalToEnter.DisplayName +
                                " (" + clock.NextRivalToEnter.Identity.DisplayName + "), dia " + clock.NextEntryDay, _wrapStyle);
            }

            GUILayout.Label("Derrotados: " + run.Stats.RivalsDefeated + " de " + clock.Rivals.Count);
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
                int days = threat.DaysUntil(run.Day);
                string when = days <= 0 ? "HOJE" : days == 1 ? "amanha" : "em " + days + " dias";
                string origin = threat.Rival != null ? threat.Rival.DisplayName : threat.Kind.ToString();
                GUILayout.Label(when + ": " + origin + " — forca " + threat.Force +
                                "  (sua defesa: " + run.TotalDefense() + ")", _wrapStyle);
            }
        }

        private void DrawKingdom(RunState run)
        {
            GUILayout.Label("Celulas " + run.Grid.OwnedCount + " (de pe: " + run.StandingTileCount() + ")");

            int nextCost = run.Grid.NextTileCost(run.Rules);
            GUILayout.Label("Proxima celula: " + nextCost + " ouro" +
                            (run.CanAfford(nextCost) ? string.Empty : "  (falta " + (nextCost - run.Gold) + ")"));

            GUILayout.BeginHorizontal();
            GUILayout.Label("Postura:", GUILayout.Width(62));
            if (GUILayout.Button(StanceLabel(run.Stance)))
            {
                run.Stance = NextStance(run.Stance);
                _gridView.Sync();
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("Postura decide quem recebe gente primeiro: campos produzem, muralhas defendem.", _wrapStyle);
        }

        private void DrawSelection(RunState run)
        {
            if (!_gridView.Selected.HasValue)
            {
                GUILayout.Label("Clique numa celula sua para ver e construir. " +
                                "Clique num bloco amarelo para comprar.", _wrapStyle);
                return;
            }

            Coord coord = _gridView.Selected.Value;
            Tile tile = run.Grid.TileAt(coord);

            GUILayout.Label(coord + " — " + tile.Terrain + (tile.Destroyed ? " (arrasada)" : string.Empty), _boldStyle);

            if (tile.HasBuilding)
            {
                ProductionBreakdown breakdown = ProductionCalculator.ForTile(run.Grid, tile, run.Rules);
                GUILayout.Label(tile.Building.DisplayName + ": " +
                                (breakdown.IsEmpty ? "nao produz" : breakdown.Totals + " por dia"), _wrapStyle);

                for (int i = 0; i < breakdown.Lines.Count; i++)
                {
                    GUILayout.Label("   " + breakdown.Lines[i], _wrapStyle);
                }

                if (tile.Coord != run.Grid.HallCoord && GUILayout.Button("Demolir"))
                {
                    _bundle.Engine.Demolish(coord);
                    AfterStateChanged();
                }

                return;
            }

            if (tile.Destroyed || !tile.Owned)
            {
                return;
            }

            List<BuildingDefinition> buildings = _bundle.AvailableBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingDefinition building = buildings[i];
                if (building.Id == RunBuilder.HallBuildingId || !building.CanBuildOn(tile.Terrain))
                {
                    continue;
                }

                ResourceAmounts cost = _bundle.Engine.BuildCostOf(building);
                bool wasEnabled = GUI.enabled;
                GUI.enabled = wasEnabled && run.CanAfford(cost, out _);

                if (GUILayout.Button(building.DisplayName + "  —  " + CostText(cost)))
                {
                    TryBuild(building, coord);
                }

                GUI.enabled = wasEnabled;
                GUILayout.Label(BuildingSummary(building), _wrapStyle);
            }
        }

        private static string CostText(ResourceAmounts cost)
        {
            List<string> parts = new List<string>();
            foreach (ResourceKind kind in cost.NonZero())
            {
                parts.Add(cost[kind] + " " + ResourceKinds.DisplayName(kind));
            }

            return parts.Count == 0 ? "de graca" : string.Join(", ", parts);
        }

        private static string BuildingSummary(BuildingDefinition building)
        {
            List<string> parts = new List<string>();
            if (!building.Production.IsEmpty)
            {
                parts.Add("produz " + building.Production + "/dia");
            }

            if (building.Defense > 0)
            {
                parts.Add("+" + building.Defense + " defesa");
            }

            if (building.PopulationCapacity > 0)
            {
                parts.Add("abriga " + building.PopulationCapacity);
            }

            if (building.WorkersRequired > 0)
            {
                parts.Add("exige " + building.WorkersRequired + " de gente");
            }

            return "   " + string.Join(", ", parts);
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

        // --- Mao e acoes ---

        /// <summary>
        /// A mao como cartas lado a lado, cada uma com o texto de regra tirado dos
        /// proprios efeitos. Rola na horizontal quando a mao nao cabe.
        /// </summary>
        private void DrawHand(RunState run)
        {
            GUILayout.BeginArea(HandRect, GUI.skin.box);
            GUILayout.Label("Mao (" + run.Deck.Hand.Count + ")   —   energia " + run.Energy + "/" + run.EnergyPerDay,
                _boldStyle);

            if (run.Deck.Hand.Count == 0)
            {
                GUILayout.Label("sem cartas");
                GUILayout.EndArea();
                return;
            }

            _handScroll = GUILayout.BeginScrollView(_handScroll, true, false);
            GUILayout.BeginHorizontal();

            List<CardDefinition> hand = new List<CardDefinition>(run.Deck.Hand);
            for (int i = 0; i < hand.Count; i++)
            {
                DrawCard(run, hand[i]);
            }

            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawCard(RunState run, CardDefinition card)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(CardWidth), GUILayout.Height(HandHeight - 58f));

            GUILayout.BeginHorizontal();
            GUILayout.Label(card.DisplayName, _boldStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label("[" + card.EnergyCost + "]", _boldStyle);
            GUILayout.EndHorizontal();

            GUILayout.Label(CardText.RulesFor(card), _wrapStyle);
            GUILayout.FlexibleSpace();

            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && run.Energy >= card.EnergyCost && !run.IsOver;
            string label = card.NeedsTarget
                ? (_gridView.Selected.HasValue ? "Jogar na celula" : "Selecione o alvo")
                : "Jogar";
            if (GUILayout.Button(label))
            {
                TryPlayCard(card);
            }

            GUI.enabled = wasEnabled;
            GUILayout.EndVertical();
        }

        private void DrawActions(RunState run)
        {
            GUILayout.BeginArea(ActionsRect, GUI.skin.box);

            if (!run.IsOver && GUILayout.Button("Fim do Dia", GUILayout.Height(56)))
            {
                EndDay();
            }

            // Acelerar e pular sao requisitos, nao conveniencias: a encenacao irrita
            // na decima repeticao, e a run tem dezenas de dias.
            if (GUILayout.Button("Encenacao: " + DayStaging.Label(_staging.Speed)))
            {
                _staging.Speed = DayStaging.Cycle(_staging.Speed);
            }

            if (GUILayout.Button(_showLog ? "Esconder log" : "Mostrar log"))
            {
                _showLog = !_showLog;
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// A recusa aparece logo abaixo da barra de estado, perto do contador que ela
        /// aponta. No rodape ela passava despercebida e o clique parecia nao fazer nada.
        /// </summary>
        private void DrawRejection()
        {
            if (string.IsNullOrEmpty(_lastRejection))
            {
                return;
            }

            float width = 520f;
            GUI.Box(new Rect((VirtualWidth - width) * 0.5f, TopBarHeight + 4f, width, 28f), ">> " + _lastRejection);
        }

        // --- Mundo ---

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

                float x = screen.x / _uiScale;
                float y = (Screen.height - screen.y) / _uiScale;
                GUI.Label(new Rect(x - 28f, y - 12f, 56f, 22f), label);
            }
        }

        // --- Encenacao e momentos ---

        /// <summary>A cena do dia, com o passo atual em destaque.</summary>
        private void DrawStaging()
        {
            DayStep step = _staging.Current;
            if (step == null)
            {
                return;
            }

            if (step.IsMoment)
            {
                DrawMoment(step);
                return;
            }

            float width = 480f;
            float height = step.IsHighlight ? 72f : 50f;
            Rect rect = new Rect((VirtualWidth - width) * 0.5f, TopBarHeight + 40f, width, height);

            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label(step.IsHighlight ? "*** " + step.Headline + " ***" : step.Headline, _boldStyle);

            if (GUILayout.Button("pular"))
            {
                _staging.SkipRest();
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// Derrota de rival e fim de run: um quadro no centro da tela que so sai com
        /// "Continuar". Andaime como o resto do HUD, mas ja com a regra que importa —
        /// o dia para ate o jogador ver o que conquistou ou perdeu.
        /// </summary>
        private void DrawMoment(DayStep step)
        {
            List<string> lines = new List<string>();
            string title;

            switch (step.Source)
            {
                case RivalDefeatedEvent defeated:
                    title = "RIVAL DERROTADO";
                    lines.Add(defeated.Rival.DisplayName + " (" + defeated.Rival.Identity.DisplayName + ")");
                    lines.Add(defeated.Campaign.AttacksRepelled + " ataques repelidos" +
                              (defeated.Campaign.AttacksSuffered > 0
                                  ? ", " + defeated.Campaign.AttacksSuffered + " sofridos"
                                  : string.Empty));

                    // Contagem de derrotados, e nao a lista de quem ainda luta: a lista
                    // ja pode conter o rival que so entra no dia seguinte, e o momento
                    // precisa vir antes da proxima declaracao de guerra.
                    lines.Add("Rivais derrotados: " + _bundle.Run.Stats.RivalsDefeated + " de " +
                              _bundle.Run.Threats.Rivals.Count);
                    break;

                case RunWonEvent won:
                    title = "VITORIA";
                    lines.Add("Todos os rivais cairam em " + _bundle.Run.Day + " dias.");
                    lines.Add(won.Score.TotalPoints + " pts  =>  " + won.Score.MetaCurrency + " moedas de meta");
                    AddUnlockLines(lines);
                    break;

                case RunCollapsedEvent collapsed:
                    title = "COLAPSO";
                    lines.Add(collapsed.Reason == CollapseReason.TerritoryLost
                        ? "Nenhuma celula ficou de pe."
                        : "A base caiu.");
                    lines.Add(collapsed.Score.TotalPoints + " pts  =>  " + collapsed.Score.MetaCurrency + " moedas de meta");
                    break;

                default:
                    title = step.Headline;
                    break;
            }

            float width = 540f;
            float height = 150f + lines.Count * 24f;
            Rect rect = new Rect((VirtualWidth - width) * 0.5f, (VirtualHeight - height) * 0.35f, width, height);

            GUILayout.BeginArea(rect, GUI.skin.window);
            GUILayout.Space(8);
            GUILayout.Label("*** " + title + " ***", MomentTitleStyle());
            GUILayout.Space(8);

            for (int i = 0; i < lines.Count; i++)
            {
                GUILayout.Label(lines[i], _wrapStyle);
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Continuar", GUILayout.Height(34)))
            {
                _staging.Acknowledge();
            }

            GUILayout.EndArea();
        }

        private void AddUnlockLines(List<string> lines)
        {
            if (_justUnlocked == null)
            {
                return;
            }

            lines.Add(string.Empty);
            lines.Add("DESBLOQUEADO: " + _justUnlocked.DisplayName);
            if (!string.IsNullOrEmpty(_justUnlocked.Hardening))
            {
                lines.Add(_justUnlocked.Hardening);
            }
        }

        private static GUIStyle MomentTitleStyle()
        {
            if (_momentTitle == null)
            {
                _momentTitle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            return _momentTitle;
        }

        /// <summary>Tela de fim, no centro, com o placar discriminado e o que foi desbloqueado.</summary>
        private void DrawRunEnd()
        {
            RunScore score = _bundle.Engine.FinalScore;
            float width = 500f;
            float height = 150f + (score != null ? score.Lines.Count * 22f : 0f) + (_justUnlocked != null ? 70f : 0f);
            Rect rect = new Rect((VirtualWidth - width) * 0.5f, TopBarHeight + 60f, width, height);

            GUILayout.BeginArea(rect, GUI.skin.window);
            GUILayout.Label(_bundle.Run.Outcome == RunOutcome.Victory
                ? "*** VITORIA ***"
                : "*** COLAPSO: " + _bundle.Run.Collapse + " ***", MomentTitleStyle());

            if (score != null)
            {
                for (int i = 0; i < score.Lines.Count; i++)
                {
                    GUILayout.Label(score.Lines[i].ToString());
                }

                GUILayout.Label("Total " + score.TotalPoints + " pts  =>  " + score.MetaCurrency + " moedas de meta",
                    _boldStyle);
            }

            // O desbloqueio fica na tela de fim tambem, e nao so no momento: e aqui
            // que o jogador decide a proxima run (spec game-feel).
            List<string> unlock = new List<string>();
            AddUnlockLines(unlock);
            for (int i = 0; i < unlock.Count; i++)
            {
                GUILayout.Label(unlock[i], _wrapStyle);
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Nova run", GUILayout.Height(34)))
            {
                StartRun(Random.Range(1, int.MaxValue));
            }

            GUILayout.EndArea();
        }

        private void DrawLog()
        {
            GUILayout.BeginArea(LogRect, GUI.skin.box);
            _logScroll = GUILayout.BeginScrollView(_logScroll);

            for (int i = 0; i < _log.Count; i++)
            {
                GUILayout.Label(_log[i], _wrapStyle);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}

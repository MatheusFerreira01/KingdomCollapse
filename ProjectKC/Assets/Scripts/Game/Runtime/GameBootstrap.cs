using System.Collections.Generic;
using KingdomCollapse.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KingdomCollapse.Game
{
    /// <summary>De onde vem o conteudo do jogo.</summary>
    public enum ContentSource
    {
        /// <summary>O conteudo escrito em codigo no Core (StarterContent). E o jogo como ele e balanceado.</summary>
        BuiltIn = 0,

        /// <summary>O GameDatabase autorado no Inspector. Para experimentar conteudo proprio.</summary>
        Database = 1
    }

    /// <summary>
    /// Ponto de entrada do jogo. Monta camera, luz e view em runtime e conduz o fluxo
    /// inteiro: menu principal, selecao de nivel, arvore do reino, a run e o fim dela.
    ///
    /// A interface e IMGUI desenhada por codigo (ver UiTheme): nao depende de nenhum
    /// prefab nem de montagem na cena, entao o jogo abre com a cena vazia mais este
    /// componente. Esta classe esta dividida em tres arquivos:
    ///   GameBootstrap.cs          fluxo, entrada e traducao dos eventos do Core
    ///   GameBootstrap.Hud.cs      a tela da run
    ///   GameBootstrap.Screens.cs  menus, arvore, pausa e fim de run
    /// </summary>
    public sealed partial class GameBootstrap : MonoBehaviour
    {
        [Header("Conteudo")]
        [Tooltip("BuiltIn usa o conteudo do Core (StarterContent), que e o que o simulador balanceia. " +
                 "Database usa o asset abaixo.")]
        [SerializeField] private ContentSource _contentSource = ContentSource.BuiltIn;

        [SerializeField] private GameDatabase _database;

        [SerializeField] private string _raceId = "humans";

        [Tooltip("Zero sorteia uma semente nova a cada partida.")]
        [SerializeField] private int _seed;

        [Tooltip("So vale com 'Pular Menu': nivel em que a run de teste comeca.")]
        [Min(1)]
        [SerializeField] private int _levelRank = 1;

        [Header("Desenvolvimento")]
        [Tooltip("Abre direto numa run, sem passar pelo menu. Para iterar mais rapido no Editor.")]
        [SerializeField] private bool _skipMenu;

        [Header("Cena")]
        [SerializeField] private bool _createLighting = true;

        private enum GameScreen
        {
            MainMenu,
            LevelSelect,
            MetaTree,
            HowToPlay,
            Playing
        }

        private GameScreen _screen = GameScreen.MainMenu;

        /// <summary>Para onde "Voltar" leva quando a tela foi aberta de dentro de outra.</summary>
        private GameScreen _returnTo = GameScreen.MainMenu;

        /// <summary>Passo do tutorial em curso; -1 quando nao ha tutorial.</summary>
        private int _tutorialStep = -1;

        private bool _paused;
        private bool _confirmAbandon;

        // --- Conteudo e perfil ---

        private ContentCatalog _catalog;
        private DifficultyLadder _ladder;
        private MetaTree _tree;
        private FileProfileStore _profileStore;
        private MetaProfile _profile;

        // --- Run ---

        private RunBundle _bundle;
        private int _runSeed;
        private GridView _gridView;
        private IsometricCameraRig _cameraRig;
        private Camera _camera;
        private FloatingNumbers _numbers;
        private readonly DayStaging _staging = new DayStaging();

        /// <summary>Reino de enfeite atras do menu: uma run jogada pelo bot por alguns dias.</summary>
        private RunBundle _showcase;

        /// <summary>Nivel que a run encerrada acabou de liberar, para nomear na tela de fim.</summary>
        private DifficultyLevel _justUnlocked;

        private int _coinsEarned;

        // --- Estado de interface ---

        private readonly List<string> _log = new List<string>();
        private Vector2 _logScroll;
        private bool _showLog;

        /// <summary>Carta esperando o jogador escolher o alvo no tabuleiro.</summary>
        private CardDefinition _targetingCard;

        private string _toast;
        private Color _toastColor;
        private float _toastUntil;

        /// <summary>Recurso que a ultima recusa apontou, para piscar no painel.</summary>
        private ResourceKind _flashKind;
        private float _flashUntil;

        private void Start()
        {
            if (_contentSource == ContentSource.Database && _database == null)
            {
                Debug.LogWarning("GameBootstrap em modo Database sem GameDatabase: usando o conteudo embutido.", this);
                _contentSource = ContentSource.BuiltIn;
            }

            LoadContent();
            LoadProfile();
            BuildScene();

            if (_skipMenu)
            {
                StartRun(Mathf.Clamp(_levelRank, 1, Mathf.Max(1, _profile.HighestUnlockedLevel)));
            }
            else
            {
                ShowMainMenu();
            }
        }

        // --- Conteudo ---

        private void LoadContent()
        {
            if (_contentSource == ContentSource.Database)
            {
                _catalog = _database.BuildCatalog();
                _ladder = _database.BuildLadder();
                _tree = _database.BuildMetaTree();
            }
            else
            {
                _catalog = StarterContent.BuildCatalog();
                _ladder = StarterContent.BuildLadder();
                _tree = StarterContent.BuildMetaTree();
                _raceId = StarterContent.HumansId;

                List<CatalogViolation> violations = StarterContent.Validate();
                for (int i = 0; i < violations.Count; i++)
                {
                    Debug.LogError("Conteudo embutido invalido: " + violations[i], this);
                }
            }

            if (!_catalog.Races.ContainsKey(_raceId))
            {
                Debug.LogError("Raca '" + _raceId + "' nao existe no conteudo.", this);
            }
        }

        private RunSetup CreateSetup(int seed, int levelRank)
        {
            return _contentSource == ContentSource.Database
                ? _database.CreateSetup(_raceId, seed, levelRank)
                : StarterContent.CreateSetup(seed, levelRank, _raceId);
        }

        // --- Perfil ---

        /// <summary>
        /// O perfil guarda moeda de meta, nos comprados e niveis liberados entre
        /// sessoes. Save ilegivel nunca impede o jogo de abrir: o Core preserva o
        /// arquivo e devolve um perfil novo.
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

        private void SaveProfile()
        {
            try
            {
                _profileStore.Save(_profile);
            }
            catch (System.Exception error)
            {
                Debug.LogError("Nao consegui salvar o perfil: " + error.Message, this);
                Toast("Não consegui salvar o progresso.", UiTheme.Bad);
            }
        }

        private void RecordRunEnd(RunScore score)
        {
            _coinsEarned = score.MetaCurrency;
            int unlocked = _profile.RecordRun(score, _bundle.Run.Stats.DaysSurvived);
            _justUnlocked = unlocked > 0 ? _ladder.LevelOfRank(unlocked) : null;
            SaveProfile();
        }

        // --- Fluxo ---

        private void ShowMainMenu()
        {
            _screen = GameScreen.MainMenu;
            _paused = false;
            _confirmAbandon = false;
            _targetingCard = null;
            _bundle = null;
            _staging.Begin(null);
            if (_numbers != null)
            {
                _numbers.Clear();
            }

            BuildShowcase();
        }

        /// <summary>
        /// Monta o reino que aparece atras do menu. E uma run de verdade, jogada pelo
        /// bot por alguns dias: mostra o jogo em vez de uma tela vazia, e custa uma
        /// fracao de segundo.
        /// </summary>
        private void BuildShowcase()
        {
            try
            {
                // O reino do menu e da raca escolhida por ultimo: e a vitrine dela.
                string race = _contentSource == ContentSource.BuiltIn && System.Array.IndexOf(StarterContent.RaceIds, _raceId) >= 0
                    ? _raceId
                    : StarterContent.HumansId;
                BuildingVisuals.SetRace(race);
                ContentCatalog catalog = StarterContent.BuildCatalog();
                _showcase = RunBuilder.Build(
                    StarterContent.CreateSetup(20260 + _profile.RunsPlayed, 1, race), catalog);
                _showcase.Engine.StartRun();

                StewardPolicy bot = new StewardPolicy();
                for (int day = 0; day < 24 && !_showcase.Run.IsOver; day++)
                {
                    bot.PlanDay(_showcase.Engine, _showcase);
                    _showcase.Engine.EndDay();
                }

                _showcase.Engine.DrainEvents();
                _gridView.Initialize(_showcase.Run);
                ApplyCameraInsets(instant: true);
                _cameraRig.Frame(_gridView.WorldBounds(), instant: true);
            }
            catch (System.Exception error)
            {
                // O enfeite nunca pode impedir o menu de abrir.
                Debug.LogWarning("Nao consegui montar o reino do menu: " + error.Message, this);
                _showcase = null;
                _gridView.Clear();
            }
        }

        private void StartRun(int levelRank)
        {
            _runSeed = _seed == 0 ? Random.Range(1, int.MaxValue) : _seed;
            _justUnlocked = null;
            _coinsEarned = 0;
            _paused = false;
            _confirmAbandon = false;
            _targetingCard = null;
            _armedGhost = null;
            _showcase = null;
            _selectionScroll = Vector2.zero;

            int rank = _ladder.LevelOfRank(levelRank) != null ? levelRank : 1;

            try
            {
                _bundle = RunBuilder.Build(CreateSetup(_runSeed, rank), _catalog, _tree, _profile);
            }
            catch (System.Exception error)
            {
                Debug.LogError("Nao consegui montar a run: " + error.Message, this);
                Toast("Não consegui montar a run. Veja o Console.", UiTheme.Bad);
                ShowMainMenu();
                return;
            }

            _screen = GameScreen.Playing;
            BuildingVisuals.SetRace(_raceId);
            _numbers.Clear();
            _staging.Begin(null);

            _bundle.Engine.StartRun();
            _gridView.Initialize(_bundle.Run);
            ApplyCameraInsets(instant: true);
            _cameraRig.Frame(_gridView.WorldBounds(), instant: true);

            // A primeira run do perfil e guiada. Novo Jogo zera o perfil e cai aqui tambem.
            _tutorialStep = _profile.RunsPlayed == 0 ? 0 : -1;

            _log.Clear();
            Log("Run iniciada" + (_bundle.Run.Level != null ? " — " + _bundle.Run.Level.DisplayName : string.Empty) +
                ". Semente " + _runSeed + ".");
            ConsumeEvents();
        }

        private void BuildScene()
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
            _camera.backgroundColor = new Color(0.10f, 0.115f, 0.14f);

            GameObject viewObject = new GameObject("Grid View");
            _gridView = viewObject.AddComponent<GridView>();
            _numbers = viewObject.AddComponent<FloatingNumbers>();
            TerrainVisuals.CreateGround(viewObject.transform);
            UiSound.Init(gameObject);

            if (_createLighting && FindAnyObjectByType<Light>() == null)
            {
                GameObject lightObject = new GameObject("Sol");
                Light sun = lightObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.15f;
                sun.shadows = LightShadows.Soft;
                lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }

        /// <summary>
        /// Diz a camera quanto da tela o HUD cobre, para ela enquadrar o reino no
        /// espaco que sobra.
        /// </summary>
        private void ApplyCameraInsets(bool instant = false)
        {
            if (_cameraRig == null)
            {
                return;
            }

            float width = Mathf.Max(1f, Screen.width / Mathf.Max(0.01f, UiTheme.Scale));
            float height = Mathf.Max(1f, Screen.height / Mathf.Max(0.01f, UiTheme.Scale));

            if (_screen == GameScreen.Playing)
            {
                _cameraRig.SetInsets(
                    (SidePanelWidth + 2f * Margin) / width,
                    (SidePanelWidth + 2f * Margin) / width,
                    TopBarHeight / height,
                    (BottomHeight + Margin) / height,
                    instant);
            }
            else
            {
                // No menu, o painel fica a esquerda e o reino ocupa a direita.
                _cameraRig.SetInsets(0.38f, 0.02f, 0.05f, 0.05f, instant);
            }
        }

        // --- Entrada ---

        private bool RunIsLive => _screen == GameScreen.Playing && _bundle != null;

        /// <summary>O evento com escolha so aparece depois que a encenacao do dia terminou.</summary>
        private bool EventChoiceVisible =>
            RunIsLive && _bundle.Engine.IsAwaitingEventChoice && !_staging.IsPlaying;

        private bool RunEndVisible => RunIsLive && _bundle.Run.IsOver && !_staging.IsPlaying;

        /// <summary>Algo no centro da tela exige resposta: o tabuleiro e o HUD ficam parados.</summary>
        private bool ModalOpen =>
            _paused || (RunIsLive && (_staging.IsAwaitingAcknowledge || EventChoiceVisible || RunEndVisible));

        private void Update()
        {
            ApplyCameraInsets();

            if (!RunIsLive)
            {
                return;
            }

            if (!_paused)
            {
                _staging.Tick(Time.deltaTime);
            }

            // O desfecho nao precisa de um "Continuar" proprio: a tela de fim de run
            // ja e o momento, e ela aparece assim que a encenacao termina.
            if (_staging.Current != null && _staging.Current.Kind == DayStepKind.Outcome)
            {
                _staging.Acknowledge();
            }

            HandleKeyboard();
            HandlePointer();
        }

        private void HandleKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (_targetingCard != null)
                {
                    CancelTargeting();
                }
                else if (_paused)
                {
                    _paused = false;
                    _confirmAbandon = false;
                }
                else if (_gridView.Selected.HasValue && !ModalOpen)
                {
                    _gridView.Select(null);
                }
                else if (!RunEndVisible)
                {
                    _paused = true;
                }

                return;
            }

            if (ModalOpen)
            {
                // Enter e espaco confirmam o quadro que esta na tela.
                if ((keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame) &&
                    _staging.IsAwaitingAcknowledge && !_paused)
                {
                    _staging.Acknowledge();
                }

                return;
            }

            if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
            {
                if (_staging.IsPlaying)
                {
                    _staging.SkipRest();
                }
                else
                {
                    EndDay();
                }

                return;
            }

            if (keyboard.lKey.wasPressedThisFrame)
            {
                _showLog = !_showLog;
            }

            Key[] digits =
            {
                Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
                Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
            };

            for (int i = 0; i < digits.Length; i++)
            {
                if (keyboard[digits[i]].wasPressedThisFrame && i < _bundle.Run.Deck.Hand.Count)
                {
                    OnCardClicked(_bundle.Run.Deck.Hand[i]);
                    return;
                }
            }
        }

        private void HandlePointer()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 position = mouse.position.ReadValue();
            bool blocked = ModalOpen || IsPointerOverHud(position);

            TileView tile = blocked ? null : TileUnder(position);
            _gridView.SetHover(tile != null ? tile.Coord : (Coord?)null);
            _hoveredTile = tile;

            if (mouse.rightButton.wasPressedThisFrame && !ModalOpen)
            {
                if (_targetingCard != null)
                {
                    CancelTargeting();
                }
                else
                {
                    _gridView.Select(null);
                }

                return;
            }

            if (!mouse.leftButton.wasPressedThisFrame || blocked)
            {
                return;
            }

            if (_targetingCard != null)
            {
                if (tile == null)
                {
                    return;
                }

                if (!_gridView.IsHighlighted(tile.Coord))
                {
                    Toast("Essa célula não serve de alvo para " + _targetingCard.DisplayName + ".", UiTheme.Warn);
                    return;
                }

                CardDefinition card = _targetingCard;
                CancelTargeting();
                PlayCard(card, tile.Coord);
                return;
            }

            if (tile == null)
            {
                Disarm();
                _gridView.Select(null);
                return;
            }

            if (tile.IsGhost)
            {
                // Dois cliques: o primeiro marca, o segundo compra. Um clique so
                // gastava ouro por engano ao tentar selecionar outra coisa.
                if (_armedGhost.HasValue && _armedGhost.Value == tile.Coord)
                {
                    Disarm();
                    TryBuy(tile.Coord);
                    return;
                }

                _armedGhost = tile.Coord;
                _gridView.Select(null);
                _gridView.SetHighlights(new[] { tile.Coord }, TerrainVisuals.PurchasableColor);
                UiSound.Play(Sfx.Click);
                Toast("Clique de novo para comprar por " + _bundle.Run.Grid.NextTileCost(_bundle.Run.Rules) + " de ouro.", UiTheme.Gold);
                return;
            }

            Disarm();
            _gridView.Select(tile.Coord);
            _selectionScroll = Vector2.zero;
        }

        private TileView _hoveredTile;

        /// <summary>Celula compravel que ja recebeu o primeiro clique.</summary>
        private Coord? _armedGhost;

        private void Disarm()
        {
            if (_armedGhost.HasValue)
            {
                _armedGhost = null;
                _gridView.SetHighlights(null);
            }
        }

        private TileView TileUnder(Vector2 screenPosition)
        {
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            return Physics.Raycast(ray, out RaycastHit hit, 500f) ? hit.collider.GetComponent<TileView>() : null;
        }

        // --- Comandos ---

        private void TryBuy(Coord coord)
        {
            if (_bundle.Run.IsOver)
            {
                return;
            }

            int cost = _bundle.Run.Grid.NextTileCost(_bundle.Run.Rules);
            CommandResult result = _bundle.Engine.BuyTile(coord);

            if (!result.Ok)
            {
                if (result.Rejection == CommandRejection.NotEnoughGold)
                {
                    Flash(ResourceKind.Gold);
                    Toast("Faltam " + (cost - _bundle.Run.Gold) + " de ouro para esta célula (custa " + cost + ").", UiTheme.Bad);
                }
                else
                {
                    Reject(result);
                }

                return;
            }

            Tile tile = _bundle.Run.Grid.TileAt(coord);
            UiSound.PlayTerrain(tile.Terrain);
            Log("Comprou " + UiText.Terrain(tile.Terrain) + " por " + cost + " de ouro.");
            _numbers.Spawn(_gridView.AnchorFor(coord), "-" + cost + " ouro", UiTheme.ResourceColor(ResourceKind.Gold));
            AfterStateChanged();
            _gridView.Select(coord);
        }

        private void TryBuild(BuildingDefinition building, Coord coord)
        {
            CommandResult result = _bundle.Engine.BuildOn(coord, building);

            if (!result.Ok)
            {
                Reject(result);
                return;
            }

            UiSound.PlayBuild(building.Id);
            Log("Construiu " + building.DisplayName + ".");
            AfterStateChanged();
        }

        private void TryRepair(Coord coord)
        {
            CommandResult result = _bundle.Engine.RepairTile(coord);

            if (!result.Ok)
            {
                if (result.Rejection == CommandRejection.NotEnoughGold)
                {
                    Flash(ResourceKind.Gold);
                    Toast("Ouro insuficiente para o reparo.", UiTheme.Bad);
                }
                else
                {
                    Reject(result);
                }

                return;
            }

            Log("Reparou uma célula arrasada.");
            AfterStateChanged();
        }

        private void TryDemolish(Coord coord)
        {
            CommandResult result = _bundle.Engine.Demolish(coord);
            if (!result.Ok)
            {
                Reject(result);
                return;
            }

            Log("Demoliu um edifício.");
            AfterStateChanged();
        }

        /// <summary>
        /// Clique numa carta. Sem alvo, joga; com alvo, entra no modo de mira e o
        /// tabuleiro acende as celulas que servem.
        /// </summary>
        private void OnCardClicked(CardDefinition card)
        {
            RunState run = _bundle.Run;
            if (run.IsOver || ModalOpen)
            {
                return;
            }

            Disarm();

            if (_targetingCard == card)
            {
                CancelTargeting();
                return;
            }

            if (run.Energy < card.EnergyCost)
            {
                Toast("Energia insuficiente: " + card.DisplayName + " custa " + card.EnergyCost + ".", UiTheme.Bad);
                return;
            }

            if (!card.NeedsTarget)
            {
                CancelTargeting();
                PlayCard(card, null);
                return;
            }

            List<Coord> targets = TargetRules.ValidTargets(run.Grid, card.Target, run.Rules);
            if (targets.Count == 0)
            {
                Toast("Nenhum alvo válido agora: " + UiText.Polish(CardText.TargetText(card.Target)) + ".", UiTheme.Warn);
                return;
            }

            // Se a celula selecionada ja serve, a carta vai direto nela.
            if (_gridView.Selected.HasValue && targets.Contains(_gridView.Selected.Value))
            {
                PlayCard(card, _gridView.Selected.Value);
                return;
            }

            _targetingCard = card;
            _gridView.Select(null);
            _gridView.SetHighlights(targets);
        }

        private void CancelTargeting()
        {
            _targetingCard = null;
            _gridView.SetHighlights(null);
        }

        private void PlayCard(CardDefinition card, Coord? target)
        {
            CommandResult result = _bundle.Engine.PlayCard(card, target);

            if (!result.Ok)
            {
                Reject(result);
                return;
            }

            UiSound.Play(Sfx.Card);
            AfterStateChanged();
        }

        private void EndDay()
        {
            if (_bundle.Run.IsOver || ModalOpen)
            {
                return;
            }

            CancelTargeting();
            int day = _bundle.Run.Day;
            CommandResult result = _bundle.Engine.BeginEndDay();

            if (!result.Ok)
            {
                Reject(result);
                return;
            }

            Disarm();
            UiSound.Play(Sfx.DayEnd);
            Log(UiTheme.Tint("— fim do dia " + day + " —", UiTheme.InkFaint));
            AfterStateChanged();
        }

        private void ChooseEventOption(int index)
        {
            CommandResult result = _bundle.Engine.ResolvePendingEvent(index);
            if (!result.Ok)
            {
                Reject(result);
                return;
            }

            AfterStateChanged();
        }

        private void Reject(CommandResult result)
        {
            if (result.Shortage.Any)
            {
                Flash(result.Shortage.Kind);
            }

            UiSound.Play(Sfx.Deny);
            Toast(Explain(result), UiTheme.Bad);
        }

        private void Flash(ResourceKind kind)
        {
            _flashKind = kind;
            _flashUntil = Time.unscaledTime + 1.4f;
        }

        private void Toast(string message, Color color)
        {
            _toast = message;
            _toastColor = color;
            _toastUntil = Time.unscaledTime + 3.2f;
        }

        private void AfterStateChanged()
        {
            ConsumeEvents();
            _gridView.Sync();
            _cameraRig.Frame(_gridView.WorldBounds());
        }

        // --- Eventos do Core ---

        /// <summary>
        /// Traduz os eventos de dominio em encenacao, numeros no tabuleiro e registro.
        /// A ordem apresentada e a ordem resolvida porque tudo sai da mesma lista.
        /// </summary>
        private void ConsumeEvents()
        {
            List<RunEvent> events = _bundle.Engine.DrainEvents();

            List<DayStep> steps = DayPresentation.Build(events);
            if (steps.Count > 0)
            {
                _staging.Begin(steps);
            }

            Vector3 hall = _gridView.AnchorFor(_bundle.Run.Grid.HallCoord) + Vector3.up * 0.5f;

            for (int i = 0; i < events.Count; i++)
            {
                switch (events[i])
                {
                    case CardPlayedEvent played:
                        Log("Carta " + NameOfCard(played.CardId) + ": " + Summarize(played.Results));
                        break;

                    case ProductionCollectedEvent production:
                        if (!production.Produced.IsEmpty)
                        {
                            Log("Produção: " + UiText.Amounts(production.Produced, true));
                        }

                        // O numero sobe onde foi gerado. O contador diz que mudou;
                        // isto diz de onde veio.
                        for (int b = 0; b < production.Breakdowns.Count; b++)
                        {
                            ProductionBreakdown breakdown = production.Breakdowns[b];
                            if (!breakdown.IsEmpty)
                            {
                                _numbers.SpawnBreakdown(_gridView.AnchorFor(breakdown.Coord), breakdown);
                            }
                        }

                        break;

                    case FoodResolvedEvent food:
                        if (food.Famine)
                        {
                            Log(UiTheme.Tint("FOME: " + food.Starved + " de gente perdida.", UiTheme.Bad));
                            _numbers.Spawn(hall, "FOME  -" + food.Starved + " gente", UiTheme.Bad);
                        }
                        else
                        {
                            Log("Consumo: " + UiTheme.Tint("-" + food.Upkeep + " Comida", UiTheme.ResourceColor(ResourceKind.Food)));
                        }

                        break;

                    case FamineWarningEvent warning:
                        Log(UiTheme.Tint("Aviso: a comida não cobre o consumo de amanhã (faltam " +
                                         warning.Deficit + ").", UiTheme.Warn));
                        break;

                    case PopulationGrewEvent growth:
                        Log("Chegou gente: " + growth.Total + " de " + growth.Capacity + ".");
                        _numbers.Spawn(hall, "+" + growth.Amount + " gente", UiTheme.ResourceColor(ResourceKind.Population));
                        break;

                    case PlunderCollectedEvent plunder:
                        Log("Saque: " + UiText.Amounts(plunder.Plunder, true));
                        break;

                    case WeatherChangedEvent weather:
                        if (weather.Today != null)
                        {
                            Log("Clima: " + weather.Today.DisplayName +
                                (weather.Tomorrow != null ? " (amanhã: " + weather.Tomorrow.DisplayName + ")" : ""));
                        }

                        break;

                    case ThreatAnnouncedEvent announced:
                        Log(UiTheme.Tint("Ataque anunciado: " + ThreatOrigin(announced.Threat) + ", dia " +
                                         announced.Threat.ArrivalDay + ", força " + announced.Threat.Force + ".", UiTheme.Warn));
                        break;

                    case AttackResolvedEvent attack:
                        LogAttack(attack.Report, hall);
                        break;

                    case RivalDeclaredWarEvent declared:
                        Log(UiTheme.Tint("GUERRA: " + declared.Rival.DisplayName + " (" +
                                         declared.Rival.Identity.DisplayName + ").", UiTheme.Bad));
                        break;

                    case RivalDefeatedEvent defeated:
                        Log(UiTheme.Tint("DERROTADO: " + defeated.Rival.DisplayName + ".", UiTheme.Good));
                        break;

                    case DayEventDrawnEvent drawn:
                        if (drawn.Definition != null)
                        {
                            UiSound.PlayEvent(drawn.Definition.Class,
                                drawn.Definition.Id.StartsWith(StarterContent.EncounterPrefix));
                        }

                        break;

                    case DayEventResolvedEvent dayEvent:
                        Log("Evento — " + dayEvent.Outcome.Definition.DisplayName + ": " +
                            Summarize(dayEvent.Outcome.Results));
                        break;

                    case MilestoneReachedEvent milestone:
                        Log(UiTheme.Tint("Marco: " + UiText.Polish(milestone.Label), UiTheme.Gold));
                        break;

                    case RunWonEvent won:
                        UiSound.Play(Sfx.Victory);
                        RecordRunEnd(won.Score);
                        Log(UiTheme.Tint("VITÓRIA — " + won.Score.TotalPoints + " pontos.", UiTheme.Gold));
                        break;

                    case RunCollapsedEvent collapsed:
                        UiSound.Play(Sfx.Collapse);
                        RecordRunEnd(collapsed.Score);
                        Log(UiTheme.Tint("COLAPSO — " + collapsed.Score.TotalPoints + " pontos.", UiTheme.Bad));
                        break;
                }
            }
        }

        private void LogAttack(AttackReport report, Vector3 hall)
        {
            string line = "Ataque: força " + report.Force + " contra defesa " + report.Defense;

            UiSound.Play(report.Repelled ? Sfx.Repel : Sfx.Breach);
            if (report.Repelled)
            {
                line += " — repelido";
                _numbers.Spawn(hall, "REPELIDO", UiTheme.Good);
            }
            else
            {
                _numbers.Spawn(hall, "DEFESA ROMPIDA", UiTheme.Bad);
            }

            if (report.TilesDestroyed.Count > 0)
            {
                line += ", " + report.TilesDestroyed.Count + " célula(s) arrasada(s)";
                for (int i = 0; i < report.TilesDestroyed.Count; i++)
                {
                    _numbers.Spawn(_gridView.AnchorFor(report.TilesDestroyed[i]), "ARRASADA", UiTheme.Bad);
                }
            }

            if (report.BaseDamage > 0)
            {
                line += ", " + report.BaseDamage + " de dano na base";
                _numbers.Spawn(hall + Vector3.up * 0.35f, "-" + report.BaseDamage + " integridade", UiTheme.Bad);
            }

            if (!report.ResourcesLost.IsEmpty)
            {
                line += ", levaram " + LostText(report.ResourcesLost);
                _numbers.Spawn(hall + Vector3.up * 0.7f, "levaram " + LostPlain(report.ResourcesLost), UiTheme.Warn);
            }

            Log(UiTheme.Tint(line + ".", report.Repelled && report.ResourcesLost.IsEmpty ? UiTheme.Good : UiTheme.Warn));
        }

        private static string LostText(ResourceAmounts lost)
        {
            List<string> parts = new List<string>();
            foreach (ResourceKind kind in lost.NonZero())
            {
                parts.Add(Mathf.Abs(lost[kind]) + " " + UiText.Resource(kind));
            }

            return string.Join(", ", parts);
        }

        private static string LostPlain(ResourceAmounts lost) => LostText(lost).ToLowerInvariant();

        private string NameOfCard(string cardId)
        {
            return _catalog.Cards.TryGetValue(cardId, out CardDefinition card) ? card.DisplayName : cardId;
        }

        private static string ThreatOrigin(ScheduledThreat threat)
        {
            return threat.Rival != null ? threat.Rival.DisplayName : "Horda";
        }

        private static string Summarize(IReadOnlyList<EffectResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return "sem efeito";
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < results.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append("; ");
                }

                builder.Append(UiText.Polish(results[i].Description));
            }

            return builder.ToString();
        }

        private static string Explain(CommandResult result)
        {
            switch (result.Rejection)
            {
                case CommandRejection.NotEnoughResources:
                    return "Faltam " + result.Shortage.Missing + " de " +
                           UiText.Resource(result.Shortage.Kind).ToLowerInvariant() + ".";
                case CommandRejection.WeatherBlocked:
                    return "O clima de hoje impede esta ação.";
                case CommandRejection.NotEnoughGold:
                    return "Ouro insuficiente.";
                case CommandRejection.NotEnoughEnergy:
                    return "Energia insuficiente.";
                case CommandRejection.WrongPhase:
                    return "Só dá para agir no Planejamento.";
                case CommandRejection.RunOver:
                    return "A run acabou.";
                case CommandRejection.TargetRequired:
                    return "Esta carta precisa de um alvo.";
                case CommandRejection.InvalidTarget:
                    return "Alvo inválido.";
                case CommandRejection.CardNotInHand:
                    return "A carta não está na mão.";
                case CommandRejection.GridRefused:
                    return ExplainGrid(result.GridRejection);
                default:
                    return "Ação recusada.";
            }
        }

        private static string ExplainGrid(GridRejection rejection)
        {
            switch (rejection)
            {
                case GridRejection.NotAdjacent:
                    return "Só dá para comprar células vizinhas ao território.";
                case GridRejection.BeyondRaceRadius:
                    return "Longe demais do Salão para esta raça.";
                case GridRejection.TerrainNotAllowed:
                    return "Terreno incompatível com este edifício.";
                case GridRejection.TileOccupied:
                    return "A célula já tem edifício.";
                case GridRejection.TileDestroyed:
                    return "Célula arrasada: precisa ser reparada antes.";
                case GridRejection.NotOwned:
                    return "A célula não pertence ao reino.";
                default:
                    return "Ação recusada.";
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
    }
}

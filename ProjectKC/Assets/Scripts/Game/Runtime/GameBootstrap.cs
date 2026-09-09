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

        private RunBundle _bundle;
        private GridView _gridView;
        private IsometricCameraRig _cameraRig;
        private Camera _camera;

        private readonly List<string> _log = new List<string>();
        private string _lastRejection;
        private Vector2 _logScroll;

        private void Start()
        {
            if (_database == null)
            {
                Debug.LogError("GameBootstrap sem GameDatabase: arraste o asset no Inspector.", this);
                enabled = false;
                return;
            }

            StartRun(_seed == 0 ? Random.Range(1, int.MaxValue) : _seed);
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

            RunSetup setup = _database.CreateSetup(_raceId, seed);

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
            Log("Run iniciada. Semente " + seed + ", raca " + _bundle.Run.Race.DisplayName + ".");
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

            HandleClick();
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
                _lastRejection = Explain(result);
                return;
            }

            _lastRejection = null;
            Log("Comprou " + coord + " por " + cost + " de ouro.");
            AfterStateChanged();
        }

        private void TryBuild(BuildingDefinition building, Coord coord)
        {
            CommandResult result = _bundle.Engine.BuildOn(coord, building);

            if (!result.Ok)
            {
                _lastRejection = Explain(result);
                return;
            }

            _lastRejection = null;
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
                _lastRejection = Explain(result);
                return;
            }

            _lastRejection = null;
            Log("Jogou " + card.DisplayName + ".");
            AfterStateChanged();
        }

        private void EndDay()
        {
            int day = _bundle.Run.Day;
            CommandResult result = _bundle.Engine.EndDay();

            if (!result.Ok)
            {
                _lastRejection = Explain(result);
                return;
            }

            _lastRejection = null;
            Log("--- fim do dia " + day + " ---");
            AfterStateChanged();
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

            for (int i = 0; i < events.Count; i++)
            {
                switch (events[i])
                {
                    case ProductionCollectedEvent production:
                        if (production.Gold > 0)
                        {
                            Log("Producao: +" + production.Gold + " ouro.");
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

        private const float PanelWidth = 340f;
        private const float LogHeight = 190f;

        private bool IsPointerOverHud(Vector2 screenPosition)
        {
            // A posicao do mouse vem com Y de baixo para cima; o HUD e ancorado no topo.
            float guiY = Screen.height - screenPosition.y;
            bool overPanel = screenPosition.x <= PanelWidth + 20f;
            bool overLog = guiY >= Screen.height - LogHeight - 20f;
            return overPanel || overLog;
        }

        private void OnGUI()
        {
            if (_bundle == null)
            {
                return;
            }

            RunState run = _bundle.Run;

            GUILayout.BeginArea(new Rect(10, 10, PanelWidth, Screen.height - LogHeight - 40f), GUI.skin.box);

            GUILayout.Label("Dia " + run.Day + "   |   " + run.Phase);
            GUILayout.Label("Ouro " + run.Gold + "  (+" + run.CollectDailyProduction() + "/dia)" +
                            "    Energia " + run.Energy + "/" + run.EnergyPerDay);
            GUILayout.Label("Integridade " + run.Integrity + "/" + run.MaxIntegrity +
                            "    Defesa " + run.TotalDefense());
            GUILayout.Label("Celulas " + run.Grid.OwnedCount + " (de pe: " + run.StandingTileCount() + ")");

            int nextCost = run.Grid.NextTileCost(run.Rules);
            GUILayout.Label("Proxima celula: " + nextCost + " ouro" +
                            (run.CanAfford(nextCost) ? string.Empty : "  (falta " + (nextCost - run.Gold) + ")"));

            // A recusa fica no topo, junto do dado que a explica. No rodape ela passava
            // despercebida e o jogador so via o clique "nao fazer nada".
            if (!string.IsNullOrEmpty(_lastRejection))
            {
                GUILayout.Label(">> " + _lastRejection);
            }

            DrawThreatClock(run);
            GUILayout.Space(6);
            DrawSelection(run);
            GUILayout.Space(6);
            DrawHand(run);

            GUILayout.FlexibleSpace();

            if (run.IsOver)
            {
                DrawCollapse();
            }
            else if (GUILayout.Button("Fim do Dia", GUILayout.Height(32)))
            {
                EndDay();
            }

            GUILayout.EndArea();

            DrawGhostPrices(run);
            DrawLog();
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

        private void DrawThreatClock(RunState run)
        {
            GUILayout.Space(6);
            GUILayout.Label("-- Relogio de ameaca --");

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

        private void DrawSelection(RunState run)
        {
            if (!_gridView.Selected.HasValue)
            {
                GUILayout.Label("-- Nenhuma celula selecionada --");
                GUILayout.Label("Clique num bloco amarelo para comprar.");
                return;
            }

            Coord coord = _gridView.Selected.Value;
            Tile tile = run.Grid.TileAt(coord);

            GUILayout.Label("-- Celula " + coord + " --");
            GUILayout.Label("Terreno: " + tile.Terrain + (tile.Destroyed ? " (arrasada)" : string.Empty));

            if (tile.HasBuilding)
            {
                ProductionBreakdown breakdown = ProductionCalculator.ForTile(run.Grid, tile, run.Rules);
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

                string label = building.DisplayName + " (" + building.GoldCost + " ouro, +" +
                               building.BaseGoldProduction + "/dia)";

                GUI.enabled = run.CanAfford(building.GoldCost);
                if (GUILayout.Button(label))
                {
                    TryBuild(building, coord);
                }

                GUI.enabled = true;
            }
        }

        private void DrawHand(RunState run)
        {
            GUILayout.Label("-- Mao (" + run.Deck.Hand.Count + ") --");

            if (run.Deck.Hand.Count == 0)
            {
                GUILayout.Label("sem cartas");
                return;
            }

            List<CardDefinition> hand = new List<CardDefinition>(run.Deck.Hand);
            for (int i = 0; i < hand.Count; i++)
            {
                CardDefinition card = hand[i];
                GUI.enabled = run.Energy >= card.EnergyCost;

                if (GUILayout.Button(card.DisplayName + "  [" + card.EnergyCost + "]"))
                {
                    TryPlayCard(card);
                }

                GUI.enabled = true;
            }
        }

        private void DrawCollapse()
        {
            RunScore score = _bundle.Engine.FinalScore;

            GUILayout.Label("=== COLAPSO: " + _bundle.Run.Collapse + " ===");

            if (score != null)
            {
                for (int i = 0; i < score.Lines.Count; i++)
                {
                    GUILayout.Label(score.Lines[i].ToString());
                }

                GUILayout.Label("Total " + score.TotalPoints + " pts  =>  " +
                                score.MetaCurrency + " moedas de meta");
            }

            if (GUILayout.Button("Nova run", GUILayout.Height(30)))
            {
                StartRun(Random.Range(1, int.MaxValue));
            }
        }

        private void DrawLog()
        {
            GUILayout.BeginArea(new Rect(10, Screen.height - LogHeight - 10, Screen.width - 20, LogHeight), GUI.skin.box);
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

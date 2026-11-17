using System.Collections.Generic;
using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
    /// <summary>
    /// A tela da run. Quatro regioes fixas em volta do tabuleiro:
    ///   topo      o que muda a cada acao: dia, clima, recursos, integridade
    ///   esquerda  o que vem: ataques anunciados, rivais, gente e postura
    ///   direita   o que esta selecionado: celula, edificio, o que da para construir
    ///   embaixo   a mao de cartas, a energia e o botao de encerrar o dia
    /// O centro e do tabuleiro, e a camera enquadra o reino nesse espaco.
    /// </summary>
    public sealed partial class GameBootstrap
    {
        private const float TopBarHeight = 60f;
        private const float SidePanelWidth = 318f;
        private const float BottomHeight = 236f;
        private const float Margin = 10f;
        private const float CardWidth = 164f;
        private const float CardHeight = 204f;

        private const float ThreatRowHeight = 72f;
        private const float ThreatRowStep = ThreatRowHeight + 6f;
        private const float RivalRowStep = 44f;

        private Vector2 _leftScroll;
        private Vector2 _selectionScroll;

        /// <summary>Regioes que o HUD ocupa, em pixels. O clique no mundo nao atravessa.</summary>
        private readonly List<Rect> _hudRects = new List<Rect>();
        private readonly List<Rect> _hudRectsBuilding = new List<Rect>();

        private string _tooltip;
        private string _tooltipBuilding;

        private float W => UiTheme.Width;

        private float H => UiTheme.Height;

        // --- Infraestrutura de desenho ---

        private void OnGUI()
        {
            GUI.depth = 0;
            UiTheme.Begin();

            if (Event.current.type == EventType.Repaint)
            {
                _hudRectsBuilding.Clear();
                _tooltipBuilding = null;
            }

            switch (_screen)
            {
                case GameScreen.Playing:
                    if (_bundle != null)
                    {
                        DrawRunScreen();
                    }

                    break;
                case GameScreen.LevelSelect:
                    DrawLevelSelect();
                    break;
                case GameScreen.MetaTree:
                    DrawMetaTree();
                    break;
                case GameScreen.HowToPlay:
                    DrawHowToPlay();
                    break;
                default:
                    DrawMainMenu();
                    break;
            }

            DrawToast();

            if (Event.current.type == EventType.Repaint)
            {
                _tooltip = _tooltipBuilding;
                DrawTooltip();

                _hudRects.Clear();
                _hudRects.AddRange(_hudRectsBuilding);
            }
        }

        private bool IsPointerOverHud(Vector2 screenPosition)
        {
            // O mouse vem com Y de baixo para cima; o IMGUI trabalha de cima para baixo.
            Vector2 point = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            for (int i = 0; i < _hudRects.Count; i++)
            {
                if (_hudRects[i].Contains(point))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Desenha um painel na resolucao de referencia e o registra como area do HUD.</summary>
        private Rect PanelAt(float x, float y, float width, float height)
        {
            Rect pixels = UiTheme.R(x, y, width, height);
            UiTheme.DrawPanel(pixels);
            Block(pixels);
            return pixels;
        }

        private void Block(Rect pixels)
        {
            if (Event.current.type == EventType.Repaint)
            {
                _hudRectsBuilding.Add(pixels);
            }
        }

        private void Tip(Rect pixels, string text)
        {
            if (!string.IsNullOrEmpty(text) && Event.current.type == EventType.Repaint &&
                pixels.Contains(Event.current.mousePosition))
            {
                _tooltipBuilding = text;
            }
        }

        private static void Txt(float x, float y, float width, float height, string text, GUIStyle style)
        {
            GUI.Label(UiTheme.R(x, y, width, height), text, style);
        }

        private static void Txt(float x, float y, float width, float height, string text, GUIStyle style, Color color)
        {
            UiTheme.LabelColored(UiTheme.R(x, y, width, height), text, style, color);
        }

        /// <summary>Altura que um texto com quebra ocupa numa largura dada, em unidades de referencia.</summary>
        private static float TextHeight(string text, GUIStyle style, float width)
        {
            return Mathf.Ceil(style.CalcHeight(new GUIContent(text), UiTheme.S(width)) / UiTheme.Scale) + 1f;
        }

        private bool Btn(float x, float y, float width, float height, string text, GUIStyle style, string tip = null, bool enabled = true)
        {
            Rect pixels = UiTheme.R(x, y, width, height);
            bool wasEnabled = GUI.enabled;
            Color previous = GUI.color;

            GUI.enabled = wasEnabled && enabled;
            if (!enabled)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.45f);
            }

            bool clicked = GUI.Button(pixels, text, style);

            GUI.color = previous;
            GUI.enabled = wasEnabled;
            Tip(pixels, tip);
            if (clicked && enabled)
            {
                UiSound.Play(Sfx.Click);
            }

            return clicked && enabled;
        }

        private void DrawTooltip()
        {
            if (string.IsNullOrEmpty(_tooltip))
            {
                return;
            }

            GUIContent content = new GUIContent(_tooltip);
            float maxWidth = UiTheme.S(340f);
            Vector2 size = UiTheme.Tooltip.CalcSize(content);
            float width = Mathf.Min(maxWidth, size.x + UiTheme.S(8f));
            float height = UiTheme.Tooltip.CalcHeight(content, width) + UiTheme.S(2f);

            Vector2 mouse = Event.current.mousePosition;
            float x = mouse.x + UiTheme.S(18f);
            float y = mouse.y + UiTheme.S(20f);

            if (x + width > Screen.width - 4f)
            {
                x = mouse.x - width - UiTheme.S(12f);
            }

            if (y + height > Screen.height - 4f)
            {
                y = mouse.y - height - UiTheme.S(12f);
            }

            GUI.Label(new Rect(Mathf.Max(4f, x), Mathf.Max(4f, y), width, height), content, UiTheme.Tooltip);
        }

        private void DrawToast()
        {
            if (string.IsNullOrEmpty(_toast) || Time.unscaledTime > _toastUntil)
            {
                return;
            }

            float remaining = _toastUntil - Time.unscaledTime;
            float alpha = Mathf.Clamp01(remaining / 0.4f);
            float width = Mathf.Min(760f, 60f + _toast.Length * 9.5f);
            float toastY = _screen == GameScreen.Playing ? TopBarHeight + 14f : H - 74f;
            Rect pixels = UiTheme.R((W - width) * 0.5f, toastY, width, 40f);

            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            UiTheme.DrawBox(pixels, UiTheme.Tooltip);
            UiTheme.LabelColored(pixels, _toast, UiTheme.CenterBold, _toastColor);
            GUI.color = previous;
        }

        // --- Tela da run ---

        private void DrawRunScreen()
        {
            RunState run = _bundle.Run;
            bool modal = ModalOpen;

            DrawGhostPrices(run);

            GUI.enabled = !modal;
            DrawTopBar(run);
            DrawLeftPanel(run);
            DrawRightPanel(run);
            DrawBottom(run);

            if (_showLog)
            {
                DrawLog();
            }

            DrawTargetingBanner();
            DrawTutorial(run);
            GUI.enabled = true;

            DrawStaging(run);

            if (_paused)
            {
                DrawPause();
            }
            else if (EventChoiceVisible)
            {
                DrawEventChoice(run);
            }
            else if (RunEndVisible)
            {
                DrawRunEnd(run);
            }
        }

        // --- Topo ---

        private void DrawTopBar(RunState run)
        {
            Rect bar = UiTheme.R(0f, 0f, W, TopBarHeight);
            UiTheme.Fill(bar, new Color(0.055f, 0.062f, 0.085f, 0.97f));
            UiTheme.Fill(UiTheme.R(0f, TopBarHeight - 2f, W, 2f), new Color(0.50f, 0.42f, 0.24f, 0.9f));
            Block(bar);

            // Dia e nivel.
            Txt(10f, 6f, 126f, 30f, "Dia " + run.Day, UiTheme.Title, UiTheme.Gold);
            Txt(0f, 36f, 146f, 18f, (run.Level != null ? run.Level.DisplayName : "Run livre") + " · " + run.Race.DisplayName,
                UiTheme.CenterSmall);

            // Clima.
            float x = 150f;
            if (run.TodayWeather != null)
            {
                WeatherDefinition tomorrow = run.Weather.Tomorrow;
                Txt(x, 8f, 180f, 22f, run.TodayWeather.DisplayName, UiTheme.Bold);
                Txt(x, 31f, 180f, 20f, "amanhã: " + (tomorrow != null ? tomorrow.DisplayName : "?"), UiTheme.Small);
                Tip(UiTheme.R(x, 4f, 180f, 50f),
                    "<b>" + run.TodayWeather.DisplayName + "</b>\n" + WeatherEffects(run.TodayWeather) +
                    (tomorrow != null ? "\n\n<b>Amanhã: " + tomorrow.DisplayName + "</b>\n" + WeatherEffects(tomorrow) : string.Empty));
            }

            // Recursos, com o saldo previsto do dia.
            ResourceAmounts predicted = run.CollectDailyProductionByResource();
            int upkeep = run.DailyFoodUpkeep();
            float chipWidth = 142f;
            float chipsWidth = chipWidth * 5f;
            x = Mathf.Max(340f, (W - chipsWidth) * 0.5f - 40f);

            for (int i = 0; i < ResourceKinds.All.Length; i++)
            {
                ResourceKind kind = ResourceKinds.All[i];
                DrawResourceChip(run, kind, x, chipWidth, predicted, upkeep);
                x += chipWidth;
            }

            // Integridade e defesa.
            float right = W - 16f;
            if (Btn(right - 96f, 12f, 96f, 36f, "Menu", UiTheme.Button, "Pausa, ajuda e saída (Esc)"))
            {
                _paused = true;
            }

            right -= 110f;
            float barWidth = 170f;
            float fraction = run.MaxIntegrity > 0 ? run.Integrity / (float)run.MaxIntegrity : 0f;
            Color color = fraction > 0.6f ? UiTheme.Good : fraction > 0.3f ? UiTheme.Warn : UiTheme.Bad;
            Txt(right - barWidth, 6f, barWidth, 20f, "Base  " + run.Integrity + " / " + run.MaxIntegrity, UiTheme.Bold);
            UiTheme.Bar(UiTheme.R(right - barWidth, 30f, barWidth, 10f), fraction, color);
            Txt(right - barWidth, 41f, barWidth, 16f, "Defesa " + run.TotalDefense() +
                (run.PendingDefense > 0 ? "  (" + run.PendingDefense + " só hoje)" : string.Empty), UiTheme.Small);
            Tip(UiTheme.R(right - barWidth, 4f, barWidth, 54f),
                "<b>Integridade da base</b>\nSe chegar a zero, o reino colapsa.\n\n" +
                "<b>Defesa</b>\nSoma dos edifícios defensivos <i>com gente</i> mais a defesa das cartas, que dura só o dia em que é jogada.");
        }

        private void DrawResourceChip(
            RunState run, ResourceKind kind, float x, float width, ResourceAmounts predicted, int upkeep)
        {
            Color color = UiTheme.ResourceColor(kind);
            bool flashing = Time.unscaledTime < _flashUntil && _flashKind == kind;

            if (flashing)
            {
                float pulse = 0.35f + 0.35f * Mathf.Sin(Time.unscaledTime * 16f);
                UiTheme.Fill(UiTheme.R(x - 4f, 6f, width - 6f, TopBarHeight - 14f), new Color(0.9f, 0.2f, 0.2f, pulse));
            }

            UiTheme.DrawDot(UiTheme.R(x, 13f, 14f, 14f), color);

            string value;
            string detail;
            string tip;

            if (kind == ResourceKind.Population)
            {
                WorkerAllocation allocation = run.Allocation();
                value = run[kind] + " / " + run.PopulationCapacity();
                detail = allocation.IdleBuildings > 0
                    ? UiTheme.Tint(allocation.IdleBuildings + " edif. parado(s)", UiTheme.Bad)
                    : allocation.Unassigned > 0
                        ? UiTheme.Tint(allocation.Unassigned + " livre(s)", UiTheme.Good)
                        : "todos ocupados";
                tip = "<b>Gente</b>\n" + run[kind] + " de " + run.PopulationCapacity() + " (o teto vem do Salão e das Casas).\n" +
                      allocation.Assigned + " trabalhando, " + allocation.Unassigned + " sem posto.\n\n" +
                      "Cada pessoa come 1 de comida por dia. Com comida sobrando e casa vazia, chega gente nova.";
            }
            else
            {
                int delta = predicted[kind] - (kind == ResourceKind.Food ? upkeep : 0);
                value = run[kind].ToString();
                detail = delta == 0
                    ? "±0 hoje"
                    : UiTheme.Tint((delta > 0 ? "+" : string.Empty) + delta + " hoje", delta > 0 ? UiTheme.Good : UiTheme.Bad);
                tip = "<b>" + UiText.Resource(kind) + "</b>\nProdução prevista hoje: +" + predicted[kind] +
                      (kind == ResourceKind.Food ? "\nConsumo da gente: -" + upkeep : string.Empty) +
                      "\n\n" + ResourceHint(kind);
            }

            Txt(x + 20f, 6f, width - 24f, 28f, value, UiTheme.Number, color);
            Txt(x + 20f, 34f, width - 24f, 18f, UiText.Resource(kind) + "  " + detail, UiTheme.Small);
            Tip(UiTheme.R(x - 4f, 4f, width - 4f, TopBarHeight - 8f), tip);
        }

        private static string ResourceHint(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Gold:
                    return "Compra células novas e paga parte das obras.";
                case ResourceKind.Wood:
                    return "Quase toda obra pede madeira. Vem das Serrarias, em floresta.";
                case ResourceKind.Stone:
                    return "Torres e muralhas pedem pedra. Vem das Pedreiras, em rochedo.";
                case ResourceKind.Food:
                    return "Sustenta a gente. Se faltar, parte da população vai embora.";
                default:
                    return string.Empty;
            }
        }

        private static string WeatherEffects(WeatherDefinition weather)
        {
            List<string> parts = new List<string>();

            foreach (KeyValuePair<TerrainType, int> pair in weather.TerrainProduction)
            {
                parts.Add(UiText.Terrain(pair.Key) + " " + (pair.Value > 0 ? "+" : string.Empty) + pair.Value);
            }

            if (weather.ProductionMultiplier != 0)
            {
                parts.Add("Produção " + (weather.ProductionMultiplier > 0 ? "+" : string.Empty) +
                          Mathf.RoundToInt((float)weather.ProductionMultiplier * 100f) + "%");
            }

            if (weather.BuildCostMultiplier != 1.0)
            {
                parts.Add("Obras " + (weather.BuildCostMultiplier > 1.0 ? "+" : string.Empty) +
                          Mathf.RoundToInt((float)(weather.BuildCostMultiplier - 1.0) * 100f) + "% de custo");
            }

            if (weather.EnergyDelta != 0)
            {
                parts.Add("Energia " + (weather.EnergyDelta > 0 ? "+" : string.Empty) + weather.EnergyDelta);
            }

            if (weather.BaseDamage > 0)
            {
                parts.Add("Base -" + weather.BaseDamage);
            }

            if (weather.BlocksPurchase)
            {
                parts.Add("Não dá para comprar células");
            }

            if (weather.ThreatForceDelta != 0)
            {
                parts.Add("Ataques " + (weather.ThreatForceDelta > 0 ? "+" : string.Empty) + weather.ThreatForceDelta + " de força");
            }

            if (weather.ThreatDelayDays > 0)
            {
                parts.Add("Ataques atrasam " + weather.ThreatDelayDays + " dia(s)");
            }

            if (weather.FoodUpkeepMultiplier != 1.0)
            {
                parts.Add("Consumo de comida x" + weather.FoodUpkeepMultiplier.ToString("0.##"));
            }

            string effects = parts.Count == 0 ? "Sem efeito." : string.Join(" · ", parts);
            return string.IsNullOrEmpty(weather.FlavorText) ? effects : "<i>" + weather.FlavorText + "</i>\n" + effects;
        }

        // --- Esquerda: ameacas, rivais, reino ---

        private void DrawLeftPanel(RunState run)
        {
            float height = H - TopBarHeight - BottomHeight - 3f * Margin;
            Rect panel = PanelAt(Margin, TopBarHeight + Margin, SidePanelWidth, height);

            float inner = SidePanelWidth - 40f;
            float content = MeasureLeft(run, inner);

            Rect view = UiTheme.R(Margin + 6f, TopBarHeight + Margin + 6f, SidePanelWidth - 12f, height - 12f);
            Rect contentRect = new Rect(0f, 0f, view.width - UiTheme.S(16f), UiTheme.S(content));
            _leftScroll = GUI.BeginScrollView(view, _leftScroll, contentRect, false, false, GUIStyle.none, UiTheme.Scrollbar);

            float y = 4f;
            y = DrawThreats(run, 8f, y, inner);
            y = DrawRivals(run, 8f, y + 10f, inner);
            DrawKingdom(run, 8f, y + 10f, inner);

            GUI.EndScrollView();
        }

        private float MeasureLeft(RunState run, float inner)
        {
            float threats = run.Threats.Pending.Count == 0 ? 62f : run.Threats.Pending.Count * ThreatRowStep;
            float rivals = run.Threats.HasRivals ? 28f + run.Threats.Rivals.Count * RivalRowStep + 10f : 0f;
            float kingdom = 28f + 22f + (run.Allocation().IdleBuildings > 0 ? 22f : 0f) + 40f + 22f;
            return 4f + 28f + threats + 10f + rivals + kingdom + 6f;
        }

        private float DrawThreats(RunState run, float x, float y, float width)
        {
            Txt(x, y, width, 24f, "PRÓXIMOS ATAQUES", UiTheme.Heading);
            y += 28f;

            List<ScheduledThreat> pending = new List<ScheduledThreat>(run.Threats.Pending);
            pending.Sort((a, b) => a.ArrivalDay.CompareTo(b.ArrivalDay));

            if (pending.Count == 0)
            {
                string calm = run.Threats.NextRivalToEnter != null
                    ? "Nenhum ataque anunciado. " + run.Threats.NextRivalToEnter.DisplayName +
                      " declara guerra no dia " + run.Threats.NextEntryDay + "."
                    : "Nenhum ataque anunciado.";
                Txt(x, y, width, 60f, calm, UiTheme.Dim);
                return y + 62f;
            }

            int structural = run.TotalDefense() - run.PendingDefense;

            for (int i = 0; i < pending.Count; i++)
            {
                ScheduledThreat threat = pending[i];
                RivalIdentity identity = threat.Rival?.Identity;
                int days = threat.DaysUntil(run.Day);
                bool today = days <= 0;

                // No dia do ataque a defesa das cartas conta; antes, so a estrutura.
                int defense = today ? run.TotalDefense() : structural;
                int gap = threat.Force - defense;
                bool holds = gap <= 0;

                Rect row = UiTheme.R(x, y, width, ThreatRowHeight);
                UiTheme.DrawInset(row);
                if (today)
                {
                    UiTheme.Fill(UiTheme.R(x, y, 5f, ThreatRowHeight), holds ? UiTheme.Good : UiTheme.Bad);
                }

                Color badge = today ? UiTheme.Bad : days == 1 ? UiTheme.Warn : UiTheme.InkDim;
                Txt(x + 10f, y + 6f, 62f, 24f, today ? "HOJE" : days + (days == 1 ? " dia" : " dias"), UiTheme.Bold, badge);
                Txt(x + 76f, y + 6f, width - 82f, 22f, ThreatOrigin(threat), UiTheme.Bold);

                string verdict;
                if (identity != null && !identity.IsCounteredByDefense && identity.LostResource.HasValue)
                {
                    int raid = Mathf.RoundToInt((float)(threat.Force * identity.RaidFraction * identity.LossPerForce));
                    string what = UiText.Resource(identity.LostResource.Value).ToLowerInvariant();
                    verdict = holds
                        ? UiTheme.Tint("leva ~" + raid + " de " + what, UiTheme.Warn)
                        : UiTheme.Tint("leva ~" + (raid + gap) + " de " + what + " (faltam " + gap + ")", UiTheme.Bad);
                }
                else
                {
                    verdict = holds
                        ? UiTheme.Tint("defesa segura", UiTheme.Good)
                        : UiTheme.Tint("faltam " + gap + " de defesa", UiTheme.Bad);
                }

                Txt(x + 10f, y + 28f, width - 16f, 20f,
                    "Força <b>" + threat.Force + "</b>  ×  Defesa <b>" + defense + "</b>", UiTheme.Small, UiTheme.Ink);
                Txt(x + 10f, y + 47f, width - 16f, 20f, verdict, UiTheme.Small, UiTheme.Ink);

                Tip(row, ThreatTooltip(threat, identity, today));
                y += ThreatRowStep;
            }

            return y;
        }

        private static string ThreatTooltip(ScheduledThreat threat, RivalIdentity identity, bool today)
        {
            string text = "<b>" + ThreatOrigin(threat) + "</b>";
            if (identity != null)
            {
                text += " — " + identity.DisplayName + "\n" + identity.Description;
            }

            text += "\n\nChega no dia " + threat.ArrivalDay + " com força " + threat.Force +
                    " e resolve quando você encerrar esse dia.";
            text += today
                ? "\n\n<b>É hoje.</b> Cartas de defesa jogadas agora contam."
                : "\n\nA comparação usa só a defesa dos edifícios: carta de defesa vale apenas no dia do ataque.";
            return text;
        }

        private float DrawRivals(RunState run, float x, float y, float width)
        {
            ThreatClock clock = run.Threats;
            if (!clock.HasRivals)
            {
                return y;
            }

            Txt(x, y, width, 24f, "RIVAIS  " + run.Stats.RivalsDefeated + " / " + clock.Rivals.Count, UiTheme.Heading);
            y += 28f;

            for (int i = 0; i < clock.Rivals.Count; i++)
            {
                RivalDefinition rival = clock.Rivals[i];
                RivalCampaign campaign = i < clock.Campaigns.Count ? clock.Campaigns[i] : null;

                string state;
                Color nameColor = UiTheme.Ink;
                if (campaign == null)
                {
                    state = UiTheme.Tint("entra no dia " + clock.EntryDayOf(i), UiTheme.InkDim);
                    nameColor = UiTheme.InkDim;
                }
                else if (campaign.IsDefeated)
                {
                    state = UiTheme.Tint("derrotado", UiTheme.Good);
                    nameColor = UiTheme.InkFaint;
                }
                else
                {
                    state = UiTheme.Tint("em guerra", UiTheme.Bad);
                }

                Txt(x, y, width - 96f, 22f, rival.DisplayName, UiTheme.Bold, nameColor);
                Txt(x, y + 22f, width, 20f, rival.Identity.DisplayName + " · " + state, UiTheme.Small);

                // Um ponto por ataque que falta repelir: o progresso da campanha.
                int repelled = campaign != null ? campaign.AttacksRepelled : 0;
                for (int p = 0; p < rival.AttacksToDefeat; p++)
                {
                    Rect dot = UiTheme.R(x + width - 18f - (rival.AttacksToDefeat - 1 - p) * 20f, y + 5f, 14f, 14f);
                    UiTheme.DrawDot(dot, p < repelled ? UiTheme.Good : new Color(0.30f, 0.31f, 0.36f));
                }

                Tip(UiTheme.R(x, y, width, 44f),
                    "<b>" + rival.DisplayName + "</b> — " + rival.Identity.DisplayName + "\n" + rival.Identity.Description +
                    "\n\nRepila " + rival.AttacksToDefeat + " ataques para derrotá-lo" +
                    (campaign != null ? " (" + campaign.AttacksRepelled + " até agora" +
                                        (campaign.AttacksSuffered > 0 ? ", " + campaign.AttacksSuffered + " sofridos" : string.Empty) + ")" : string.Empty) +
                    ".\nCada ataque vem mais forte que o anterior.");
                y += RivalRowStep;
            }

            return y;
        }

        private void DrawKingdom(RunState run, float x, float y, float width)
        {
            Txt(x, y, width, 24f, "REINO", UiTheme.Heading);
            y += 28f;

            WorkerAllocation allocation = run.Allocation();
            Txt(x, y, width, 20f,
                "Gente: " + allocation.Assigned + " trabalhando, " + allocation.Unassigned + " livre(s)", UiTheme.Label);
            y += 22f;

            if (allocation.IdleBuildings > 0)
            {
                Txt(x, y, width, 20f, allocation.IdleBuildings + " edifício(s) parado(s) por falta de gente",
                    UiTheme.Small, UiTheme.Bad);
                y += 22f;
            }

            float third = (width - 8f) / 3f;
            DrawStanceButton(run, WorkerStance.ProductionFirst, x, y, third, "Campos",
                "<b>Quando faltar gente, quem trabalha primeiro?</b>\nCampos: a produção recebe gente antes das defesas. O padrão em dias calmos.");
            DrawStanceButton(run, WorkerStance.Balanced, x + third + 4f, y, third, "Equilíbrio",
                "<b>Quando faltar gente, quem trabalha primeiro?</b>\nEquilíbrio: gente distribuída na ordem do território, sem prioridade.");
            DrawStanceButton(run, WorkerStance.DefenseFirst, x + 2f * (third + 4f), y, third, "Muralhas",
                "<b>Quando faltar gente, quem trabalha primeiro?</b>\nMuralhas: torres e paliçadas recebem gente antes dos campos. Use no dia do ataque.");
            y += 40f;

            int nextCost = run.Grid.NextTileCost(run.Rules);
            int razed = run.Grid.OwnedCount - run.StandingTileCount();
            Txt(x, y, width, 20f,
                run.Grid.OwnedCount + (run.Grid.OwnedCount == 1 ? " célula" : " células") + (razed > 0 ? " (" + razed + " arrasadas)" : string.Empty) +
                "  ·  próxima: " + UiTheme.Tint(nextCost + " Ouro",
                    run.CanAfford(nextCost) ? UiTheme.ResourceColor(ResourceKind.Gold) : UiTheme.Bad), UiTheme.Label);
        }

        private void DrawStanceButton(RunState run, WorkerStance stance, float x, float y, float width, string label, string tip)
        {
            bool active = run.Stance == stance;
            if (Btn(x, y, width, 32f, label, active ? UiTheme.ButtonFlatOn : UiTheme.ButtonFlat, tip) && !active)
            {
                run.Stance = stance;
                _gridView.Sync();
            }
        }

        // --- Direita: selecao ---

        private void DrawRightPanel(RunState run)
        {
            float height = H - TopBarHeight - BottomHeight - 3f * Margin;
            float px = W - SidePanelWidth - Margin;
            PanelAt(px, TopBarHeight + Margin, SidePanelWidth, height);

            float x = px + 14f;
            float y = TopBarHeight + Margin + 10f;
            float width = SidePanelWidth - 28f;

            if (!_gridView.Selected.HasValue)
            {
                DrawGuide(run, x, y, width);
                return;
            }

            Coord coord = _gridView.Selected.Value;
            Tile tile = run.Grid.TileAt(coord);

            Txt(x, y, width, 26f, UiText.Terrain(tile.Terrain).ToUpperInvariant() +
                                  (tile.Destroyed ? "  — ARRASADA" : string.Empty), UiTheme.Heading);
            y += 28f;

            if (tile.Destroyed)
            {
                int cost = run.Grid.CostCurve.RepairCost;
                Txt(x, y, width, 60f, "O ataque deixou só entulho. Reparar devolve a célula, sem o edifício que havia nela.", UiTheme.Dim);
                y += 66f;
                if (Btn(x, y, width, 40f, "Reparar — " + cost + " Ouro", UiTheme.ButtonPrimary, null, run.CanAfford(cost)))
                {
                    TryRepair(coord);
                }

                return;
            }

            if (tile.HasBuilding)
            {
                DrawBuildingInfo(run, tile, x, y, width);
                return;
            }

            // A dica padrao da floresta fala de Serraria, que nao vale para quem a preserva.
            bool preserved = tile.Terrain == TerrainType.Forest && run.Rules.GetBool(RuleKeys.NoBuildOnForest, false);
            if (!preserved)
            {
                string hint = UiText.TerrainHint(tile.Terrain);
                float hintHeight = TextHeight(hint, UiTheme.Small, width);
                Txt(x, y, width, hintHeight, hint, UiTheme.Small);
                y += hintHeight + 8f;
            }

            DrawBuildMenu(run, tile, x, y, width, TopBarHeight + Margin + height - y - 10f);
        }

        private void DrawGuide(RunState run, float x, float y, float width)
        {
            Txt(x, y, width, 26f, "O QUE FAZER", UiTheme.Heading);
            y += 32f;

            string[] lines =
            {
                "<b>Objetivo:</b> repelir todos os ataques de cada rival. Derrote os " + run.Threats.Rivals.Count + " para vencer.",
                "<b>Clique numa célula sua</b> para construir nela.",
                "<b>Clique numa célula amarela</b> para comprá-la com ouro.",
                "<b>Cartas</b> custam energia, que volta todo dia. Carta de defesa só vale no dia do ataque.",
                "<b>Gente</b> opera os edifícios. Sem comida ela vai embora; sem casa ela não chega.",
                "Quando terminar, <b>encerre o dia</b> (Espaço)."
            };

            for (int i = 0; i < lines.Length; i++)
            {
                float lineHeight = TextHeight(lines[i], UiTheme.Label, width - 16f);
                UiTheme.DrawDot(UiTheme.R(x, y + 6f, 7f, 7f), UiTheme.Gold);
                Txt(x + 16f, y, width - 16f, lineHeight, lines[i], UiTheme.Label);
                y += lineHeight + 9f;
            }

            if (_hoveredTile != null && _hoveredTile.IsGhost)
            {
                Tile tile = run.Grid.TileAt(_hoveredTile.Coord);
                y += 6f;
                UiTheme.DrawInset(UiTheme.R(x - 4f, y, width + 8f, 54f));
                Txt(x + 6f, y + 6f, width, 20f, tile.Revealed ? UiText.Terrain(tile.Terrain) : "Terreno desconhecido", UiTheme.Bold);
                Txt(x + 6f, y + 28f, width, 20f, "Comprar por " + run.Grid.NextTileCost(run.Rules) + " de ouro", UiTheme.Small);
            }
        }

        private void DrawBuildingInfo(RunState run, Tile tile, float x, float y, float width)
        {
            BuildingDefinition building = tile.Building;
            bool staffed = run.Allocation().IsStaffed(tile.Coord);
            bool disabled = run.IsTileDisabled(tile.Coord);

            Txt(x, y, width, 24f, building.DisplayName, UiTheme.CenterBold);
            y += 28f;

            if (disabled)
            {
                Txt(x, y, width, 20f, "Fechado por um evento.", UiTheme.Small, UiTheme.Bad);
                y += 22f;
            }
            else if (!staffed)
            {
                Txt(x, y, width, 38f, "Parado: falta gente. Construa Casas ou mude a postura.", UiTheme.Small, UiTheme.Bad);
                y += 40f;
            }
            else if (building.WorkersRequired > 0)
            {
                Txt(x, y, width, 20f, "Operando com " + building.WorkersRequired + " de gente.", UiTheme.Small, UiTheme.Good);
                y += 22f;
            }

            ProductionBreakdown breakdown = ProductionCalculator.ForTile(run.Grid, tile, run.Rules, staffed);
            if (!breakdown.IsEmpty)
            {
                Txt(x, y, width, 22f, "Rende por dia: " + UiText.Amounts(breakdown.Totals, true), UiTheme.Label);
                y += 24f;

                for (int i = 0; i < breakdown.Lines.Count; i++)
                {
                    ProductionLine line = breakdown.Lines[i];
                    Txt(x + 10f, y, width - 10f, 18f,
                        UiText.Polish(line.Reason) + ": " + UiTheme.Tint(
                            (line.Gold > 0 ? "+" : string.Empty) + line.Gold + " " + UiText.Resource(line.Resource),
                            UiTheme.ResourceColor(line.Resource)), UiTheme.Small);
                    y += 19f;
                }

                y += 4f;
            }

            if (building.Defense > 0)
            {
                Txt(x, y, width, 20f, "Defesa: +" + building.Defense + (staffed ? string.Empty : " (sem gente, não conta)"), UiTheme.Label);
                y += 22f;
            }

            if (building.PopulationCapacity > 0)
            {
                Txt(x, y, width, 20f, "Abriga " + building.PopulationCapacity + " de gente.", UiTheme.Label);
                y += 22f;
            }

            if (tile.Coord != run.Grid.HallCoord)
            {
                y += 10f;
                if (Btn(x, y, width, 34f, "Demolir", UiTheme.ButtonDanger,
                        "Remove o edifício. O que foi gasto na obra não volta."))
                {
                    TryDemolish(tile.Coord);
                }
            }
            else
            {
                y += 6f;
                Txt(x, y, width, 60f, "O coração do reino. Não pode ser demolido, e a run acaba se a integridade da base chegar a zero.", UiTheme.Small);
            }
        }

        private void DrawBuildMenu(RunState run, Tile tile, float x, float y, float width, float available)
        {
            if (tile.Terrain == TerrainType.Forest && run.Rules.GetBool(RuleKeys.NoBuildOnForest, false))
            {
                ResourceAmounts yield = run.Rules.TerrainYield(TerrainType.Forest);
                Txt(x, y, width, 80f,
                    "Floresta preservada. Rende sozinha " + UiText.Amounts(yield, true) +
                    " por dia, sem gente, e não aceita construção.", UiTheme.Label);
                return;
            }

            List<BuildingDefinition> buildings = _bundle.AvailableBuildings();
            List<BuildingDefinition> fitting = new List<BuildingDefinition>();
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i].Id != RunBuilder.HallBuildingId && buildings[i].CanBuildOn(tile.Terrain) &&
                    run.Race.AllowsBuilding(buildings[i]))
                {
                    fitting.Add(buildings[i]);
                }
            }

            if (fitting.Count == 0)
            {
                Txt(x, y, width, 40f, "Nada pode ser construído neste terreno.", UiTheme.Dim);
                return;
            }

            Txt(x, y, width, 22f, "CONSTRUIR", UiTheme.Heading);
            y += 26f;
            available -= 26f;

            const float RowHeight = 62f;
            Rect view = UiTheme.R(x - 4f, y, width + 8f, available);
            Rect content = new Rect(0f, 0f, view.width - UiTheme.S(16f), UiTheme.S(fitting.Count * (RowHeight + 6f)));
            _selectionScroll = GUI.BeginScrollView(view, _selectionScroll, content, false, false, GUIStyle.none, UiTheme.Scrollbar);

            float rowWidth = width - 10f;
            int freeWorkers = run.Allocation().Unassigned;

            for (int i = 0; i < fitting.Count; i++)
            {
                BuildingDefinition building = fitting[i];
                ResourceAmounts cost = _bundle.Engine.BuildCostOf(building);
                bool affordable = run.CanAfford(cost, out _);
                float rowY = i * (RowHeight + 6f);

                Rect row = UiTheme.R(2f, rowY, rowWidth, RowHeight);
                string tip = BuildingTooltip(building, freeWorkers);

                if (Btn(2f, rowY, rowWidth, RowHeight, string.Empty, UiTheme.ButtonLeft, tip, affordable))
                {
                    TryBuild(building, tile.Coord);
                }

                Color previous = GUI.color;
                if (!affordable)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.6f);
                }

                Txt(14f, rowY + 5f, rowWidth - 20f, 20f, building.DisplayName, UiTheme.Bold);
                Txt(14f, rowY + 24f, rowWidth - 20f, 18f, BuildingSummary(building), UiTheme.Small, UiTheme.Ink);
                Txt(14f, rowY + 41f, rowWidth - 20f, 18f, UiText.Cost(cost, run), UiTheme.Small);
                GUI.color = previous;

                if (building.WorkersRequired > freeWorkers && Event.current.type == EventType.Repaint)
                {
                    UiTheme.DrawDot(new Rect(row.xMax - UiTheme.S(20f), row.y + UiTheme.S(8f), UiTheme.S(10f), UiTheme.S(10f)), UiTheme.Warn);
                }
            }

            GUI.EndScrollView();
        }

        private static string BuildingSummary(BuildingDefinition building)
        {
            List<string> parts = new List<string>();
            if (!building.Production.IsEmpty)
            {
                parts.Add(UiText.Amounts(building.Production, true) + "/dia");
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
                parts.Add(building.WorkersRequired + " gente");
            }

            return string.Join(" · ", parts);
        }

        private static string BuildingTooltip(BuildingDefinition building, int freeWorkers)
        {
            string text = "<b>" + building.DisplayName + "</b>\n" + BuildingSummary(building);

            for (int i = 0; i < building.AdjacencyBonuses.Count; i++)
            {
                AdjacencyBonus bonus = building.AdjacencyBonuses[i];
                string what = bonus.Match == AdjacencyMatch.Terrain
                    ? UiText.Terrain(bonus.Terrain).ToLowerInvariant() + " vizinho"
                    : "edifício vizinho do tipo certo";
                text += "\n+" + bonus.GoldPerMatch + " " + UiText.Resource(bonus.Resource) + " por " + what + ".";
            }

            if (building.WorkersRequired > 0)
            {
                text += building.WorkersRequired > freeWorkers
                    ? "\n\n" + UiTheme.Tint("Você tem " + freeWorkers + " de gente livre: ele ficaria parado, ou tiraria gente de outro edifício.", UiTheme.Warn)
                    : "\n\nPrecisa de " + building.WorkersRequired + " de gente para funcionar.";
            }
            else if (building.Defense > 0)
            {
                text += "\n\nDefende sem precisar de gente.";
            }

            return text;
        }

        // --- Embaixo: mao, energia, fim do dia ---

        private void DrawBottom(RunState run)
        {
            float y = H - BottomHeight - Margin;
            Rect strip = UiTheme.R(0f, y - 6f, W, BottomHeight + Margin + 6f);
            GUI.DrawTexture(strip, UiTheme.Vignette);
            Block(strip);

            DrawEnergy(run, Margin + 6f, y + 8f, 150f);
            DrawHand(run, 176f, y + 12f, W - 176f - 262f);
            DrawEndDay(run, W - 250f, y + 8f, 240f);
        }

        private void DrawEnergy(RunState run, float x, float y, float width)
        {
            Txt(x, y, width, 22f, "ENERGIA", UiTheme.Heading);

            int total = Mathf.Max(run.EnergyPerDay, run.Energy);
            for (int i = 0; i < total; i++)
            {
                Rect orb = UiTheme.R(x + i * 30f, y + 30f, 24f, 24f);
                UiTheme.DrawDot(orb, i < run.Energy ? UiTheme.Info : new Color(0.20f, 0.22f, 0.27f));
            }

            Txt(x, y + 60f, width, 20f, run.Energy + " de " + run.EnergyPerDay + " hoje", UiTheme.Small);
            Tip(UiTheme.R(x, y, width, 82f),
                "<b>Energia</b>\nPaga as cartas. Volta cheia todo dia e não acumula: o que sobrar se perde.");

            Txt(x, y + 100f, width, 20f, "Baralho  " + run.Deck.DrawPile.Count, UiTheme.Small);
            Txt(x, y + 120f, width, 20f, "Descarte  " + run.Deck.DiscardPile.Count, UiTheme.Small);
            Tip(UiTheme.R(x, y + 98f, width, 44f),
                "No fim do dia a mão vai para o descarte e você compra uma mão nova. Quando o baralho acaba, o descarte é reembaralhado.");
        }

        private void DrawHand(RunState run, float x, float y, float width)
        {
            IReadOnlyList<CardDefinition> hand = run.Deck.Hand;
            if (hand.Count == 0)
            {
                Txt(x, y + 80f, width, 30f, "Sem cartas na mão hoje.", UiTheme.Center, UiTheme.InkDim);
                return;
            }

            // A mao nunca rola: as cartas estreitam ate caber.
            const float Gap = 10f;
            float cardWidth = Mathf.Min(CardWidth, (width - Gap * (hand.Count - 1)) / hand.Count);
            float total = cardWidth * hand.Count + Gap * (hand.Count - 1);
            float startX = x + (width - total) * 0.5f;

            // Copia: jogar uma carta muda a mao no meio do laco.
            List<CardDefinition> cards = new List<CardDefinition>(hand);
            for (int i = 0; i < cards.Count; i++)
            {
                DrawCard(run, cards[i], i, startX + i * (cardWidth + Gap), y, cardWidth);
            }
        }

        private void DrawCard(RunState run, CardDefinition card, int index, float x, float y, float width)
        {
            bool affordable = run.Energy >= card.EnergyCost && !run.IsOver;
            bool targeting = _targetingCard == card;

            Rect hit = UiTheme.R(x, y - 16f, width, CardHeight + 16f);
            bool hovered = GUI.enabled && hit.Contains(Event.current.mousePosition);
            float lift = targeting ? 20f : hovered && affordable ? 12f : 0f;

            Rect pixels = UiTheme.R(x, y - lift, width, CardHeight);
            UiTheme.DrawBox(pixels, !affordable ? UiTheme.CardOff : (hovered || targeting) ? UiTheme.CardHot : UiTheme.Card);

            Color previous = GUI.color;
            if (!affordable)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.55f);
            }

            // Custo de energia no canto.
            Rect orb = UiTheme.R(x + 8f, y - lift + 8f, 30f, 30f);
            UiTheme.DrawDot(orb, affordable ? UiTheme.Info : new Color(0.35f, 0.36f, 0.40f));
            UiTheme.LabelColored(orb, card.EnergyCost.ToString(), UiTheme.CenterBold, new Color(0.04f, 0.06f, 0.10f));

            // Nome comprido cai para a fonte menor em vez de cortar a segunda linha.
            bool longName = TextHeight(card.DisplayName, UiTheme.Bold, width - 48f) > 26f;
            Txt(x + 42f, y - lift + (longName ? 5f : 8f), width - 48f, longName ? 38f : 34f, card.DisplayName,
                longName ? UiTheme.BoldSmall : UiTheme.Bold, UiTheme.Gold);
            UiTheme.Fill(UiTheme.R(x + 10f, y - lift + 46f, width - 20f, 1f), new Color(0.55f, 0.47f, 0.30f, 0.6f));
            Txt(x + 10f, y - lift + 52f, width - 20f, CardHeight - 84f, UiText.CardRules(card), UiTheme.Small, UiTheme.Ink);

            string footer = targeting
                ? UiTheme.Tint("escolha o alvo", UiTheme.Info)
                : card.NeedsTarget ? "[" + (index + 1) + "]  com alvo" : "[" + (index + 1) + "]  jogar";
            Txt(x + 6f, y - lift + CardHeight - 26f, width - 12f, 20f, footer, UiTheme.CenterSmall);
            GUI.color = previous;

            if (GUI.Button(hit, GUIContent.none, GUIStyle.none))
            {
                OnCardClicked(card);
            }
        }

        private void DrawEndDay(RunState run, float x, float y, float width)
        {
            ScheduledThreat today = null;
            IReadOnlyList<ScheduledThreat> pending = run.Threats.Pending;
            for (int i = 0; i < pending.Count; i++)
            {
                if (pending[i].DaysUntil(run.Day) <= 0 && (today == null || pending[i].Force > today.Force))
                {
                    today = pending[i];
                }
            }

            if (today != null)
            {
                int gap = today.Force - run.TotalDefense();
                bool countered = today.Rival == null || today.Rival.Identity.IsCounteredByDefense;
                Rect warning = UiTheme.R(x, y, width, 58f);
                UiTheme.DrawInset(warning);
                Txt(x + 8f, y + 5f, width - 16f, 22f, "ATAQUE HOJE", UiTheme.CenterBold, gap > 0 ? UiTheme.Bad : UiTheme.Good);
                Txt(x + 8f, y + 29f, width - 16f, 22f,
                    gap > 0
                        ? "faltam " + gap + " de defesa"
                        : countered ? "sua defesa segura" : "defesa segura, mas o saque passa",
                    UiTheme.CenterSmall, UiTheme.Ink);
            }
            else
            {
                int upkeep = run.DailyFoodUpkeep();
                int food = run[ResourceKind.Food] + run.CollectDailyProductionByResource()[ResourceKind.Food];
                if (food < upkeep)
                {
                    Rect warning = UiTheme.R(x, y, width, 58f);
                    UiTheme.DrawInset(warning);
                    Txt(x + 8f, y + 5f, width - 16f, 22f, "FOME HOJE", UiTheme.CenterBold, UiTheme.Bad);
                    Txt(x + 8f, y + 29f, width - 16f, 22f, "faltam " + (upkeep - food) + " de comida", UiTheme.CenterSmall, UiTheme.Ink);
                }
            }

            bool busy = _staging.IsPlaying;
            if (Btn(x, y + 66f, width, 74f, busy ? "Pular  »" : "Encerrar o dia", UiTheme.ButtonPrimary,
                    busy ? "Pula a encenação do que aconteceu (Espaço)" : "Resolve produção, consumo, ataques e evento (Espaço)",
                    !run.IsOver))
            {
                if (busy)
                {
                    _staging.SkipRest();
                }
                else
                {
                    EndDay();
                }
            }

            float half = (width - 6f) * 0.5f;
            if (Btn(x, y + 148f, half, 30f, "Ritmo: " + SpeedLabel(_staging.Speed), UiTheme.ButtonFlat,
                    "Velocidade da encenação do dia: normal, rápido ou sem encenação."))
            {
                _staging.Speed = DayStaging.Cycle(_staging.Speed);
            }

            if (Btn(x + half + 6f, y + 148f, half, 30f, _showLog ? "Fechar registro" : "Registro (L)", UiTheme.ButtonFlat,
                    "Tudo o que aconteceu na run, em ordem."))
            {
                _showLog = !_showLog;
            }
        }

        private static string SpeedLabel(StagingSpeed speed)
        {
            switch (speed)
            {
                case StagingSpeed.Fast:
                    return "rápido";
                case StagingSpeed.Skip:
                    return "direto";
                default:
                    return "normal";
            }
        }

        private void DrawTargetingBanner()
        {
            if (_targetingCard == null)
            {
                return;
            }

            float width = 640f;
            Rect pixels = UiTheme.R((W - width) * 0.5f, H - BottomHeight - Margin - 62f, width, 44f);
            UiTheme.DrawBox(pixels, UiTheme.Tooltip);
            UiTheme.LabelColored(pixels,
                "<b>" + _targetingCard.DisplayName + "</b>: clique numa célula destacada   ·   Esc ou botão direito cancela",
                UiTheme.Center, UiTheme.Info);
        }

        // --- Tutorial da primeira run ---

        private static readonly string[] TutorialTexts =
        {
            "<b>Bem-vindo ao reino.</b> Ele começa só com o Salão. Clique num dos blocos com preço em volta dele e <b>clique de novo para comprar a célula</b> com ouro.",
            "A célula comprada já fica selecionada. <b>Escolha um edifício</b> no painel da direita: Fazenda dá comida, Casas trazem gente, Paliçada defende.",
            "<b>Jogue uma carta</b>: clique nela ou aperte o número. Cartas custam energia (as bolinhas azuis), que volta cheia todo dia.",
            "Olhe <b>Próximos ataques</b>, à esquerda. No dia marcado, sua Defesa precisa alcançar a Força do inimigo. Paliçadas e Torres defendem sempre; carta de defesa só vale no dia em que é jogada.",
            "Quando não houver mais o que fazer, <b>encerre o dia</b> (botão à direita ou Espaço). O reino produz, a gente come e os ataques de hoje chegam.",
            "É isso. Repila todos os ataques de um rival para derrotá-lo, e derrote os três para vencer. As moedas da run compram melhorias na Árvore do Reino. O Como Jogar fica no Menu."
        };

        private bool TutorialStepDone(RunState run)
        {
            switch (_tutorialStep)
            {
                case 0:
                    return run.Grid.OwnedCount >= 2;
                case 1:
                    int built = 0;
                    foreach (Tile tile in run.Grid.OwnedTilesOrdered())
                    {
                        if (tile.HasBuilding)
                        {
                            built++;
                        }
                    }

                    return built >= 2 || run.Day > 1;
                case 2:
                    return run.Energy < run.EnergyPerDay || run.Deck.DiscardPile.Count > 0 || run.Day > 1;
                case 4:
                    return run.Day > 1;
                default:
                    return false;
            }
        }

        private void DrawTutorial(RunState run)
        {
            if (_tutorialStep < 0 || ModalOpen || _staging.IsPlaying)
            {
                return;
            }

            if (Event.current.type == EventType.Repaint && TutorialStepDone(run))
            {
                _tutorialStep++;
            }

            if (_tutorialStep >= TutorialTexts.Length)
            {
                _tutorialStep = -1;
                return;
            }

            bool manual = _tutorialStep == 3 || _tutorialStep == TutorialTexts.Length - 1;
            string text = TutorialTexts[_tutorialStep];

            float width = 640f;
            float textHeight = TextHeight(text, UiTheme.Label, width - 36f);
            float height = textHeight + 70f;
            float x = (W - width) * 0.5f;
            float y = H - BottomHeight - Margin - 70f - height;

            Rect pixels = UiTheme.R(x, y, width, height);
            UiTheme.DrawBox(pixels, UiTheme.CardHot);
            Block(pixels);

            Txt(x + 18f, y + 8f, 300f, 22f, "TUTORIAL  " + (_tutorialStep + 1) + " / " + TutorialTexts.Length, UiTheme.Heading);
            Txt(x + 18f, y + 34f, width - 36f, textHeight, text, UiTheme.Label);

            if (Btn(x + 18f, y + height - 34f, 130f, 26f, "Pular tutorial", UiTheme.ButtonFlat))
            {
                _tutorialStep = -1;
                return;
            }

            if (manual && Btn(x + width - 168f, y + height - 36f, 150f, 30f,
                    _tutorialStep == TutorialTexts.Length - 1 ? "Começar" : "Entendi  »", UiTheme.ButtonFlatOn))
            {
                _tutorialStep = _tutorialStep == TutorialTexts.Length - 1 ? -1 : _tutorialStep + 1;
            }
        }

        private void DrawLog()
        {
            float width = 520f;
            float height = H - TopBarHeight - BottomHeight - 3f * Margin - 60f;
            float x = W - SidePanelWidth - 2f * Margin - width;
            float y = TopBarHeight + Margin;
            PanelAt(x, y, width, height);
            Txt(x + 14f, y + 8f, width - 28f, 24f, "REGISTRO", UiTheme.Heading);

            float total = 0f;
            float inner = width - 44f;
            float[] heights = new float[_log.Count];
            for (int i = 0; i < _log.Count; i++)
            {
                heights[i] = TextHeight(_log[i], UiTheme.Small, inner) + 3f;
                total += heights[i];
            }

            Rect view = UiTheme.R(x + 10f, y + 36f, width - 20f, height - 46f);
            Rect content = new Rect(0f, 0f, view.width - UiTheme.S(18f), UiTheme.S(total));
            _logScroll = GUI.BeginScrollView(view, _logScroll, content, false, true, GUIStyle.none, UiTheme.Scrollbar);

            float lineY = 0f;
            for (int i = 0; i < _log.Count; i++)
            {
                Txt(4f, lineY, inner, heights[i], _log[i], UiTheme.Small, UiTheme.Ink);
                lineY += heights[i];
            }

            GUI.EndScrollView();
        }

        // --- Mundo ---

        /// <summary>Desenha o custo sobre cada celula compravel, projetado na tela.</summary>
        private void DrawGhostPrices(RunState run)
        {
            if (run.IsOver || _targetingCard != null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            int cost = run.Grid.NextTileCost(run.Rules);
            bool affordable = run.CanAfford(cost);
            List<KeyValuePair<Coord, Vector3>> anchors = _gridView.GhostAnchors();
            Color color = affordable ? UiTheme.Gold : new Color(0.80f, 0.45f, 0.40f);

            for (int i = 0; i < anchors.Count; i++)
            {
                Vector3 screen = _camera.WorldToScreenPoint(anchors[i].Value + Vector3.up * 0.1f);
                if (screen.z <= 0f)
                {
                    continue;
                }

                float width = UiTheme.S(34f);
                float height = UiTheme.S(20f);
                Rect pixels = new Rect(screen.x - width * 0.5f, Screen.height - screen.y - height * 0.5f, width, height);
                UiTheme.Fill(pixels, new Color(0f, 0f, 0f, 0.55f));
                UiTheme.LabelColored(pixels, cost.ToString(), UiTheme.CenterBold, color);
            }
        }

        // --- Encenacao do dia ---

        private AttackResolvedEvent _raidShown;

        private void DrawStaging(RunState run)
        {
            DayStep step = _staging.Current;
            if (step == null || _paused)
            {
                return;
            }

            switch (step.Source)
            {
                case RivalDefeatedEvent defeated:
                    DrawRivalDefeated(run, defeated);
                    return;

                case AttackResolvedEvent attack:
                    // A cena no tabuleiro comeca junto com o quadro do ataque.
                    if (!ReferenceEquals(_raidShown, attack))
                    {
                        _raidShown = attack;
                        _gridView.PlayRaid(attack.Report.Repelled, attack.Report.Force);
                    }

                    DrawBattle(attack.Report);
                    return;

                case RunWonEvent _:
                case RunCollapsedEvent _:
                    return;
            }

            string text = StepText(step, out Color color, out string detail);
            float width = 560f;
            float height = string.IsNullOrEmpty(detail) ? 46f : 46f + TextHeight(detail, UiTheme.CenterSmall, width - 30f) + 4f;
            Rect pixels = UiTheme.R((W - width) * 0.5f, TopBarHeight + 70f, width, height);
            UiTheme.DrawBox(pixels, UiTheme.Tooltip);
            Block(pixels);
            UiTheme.LabelColored(UiTheme.R((W - width) * 0.5f, TopBarHeight + 76f, width, 34f), text, UiTheme.CenterBold, color);
            if (!string.IsNullOrEmpty(detail))
            {
                Txt((W - width) * 0.5f + 15f, TopBarHeight + 112f, width - 30f, height - 44f, detail, UiTheme.CenterSmall, UiTheme.Ink);
            }
        }

        private static string StepText(DayStep step, out Color color, out string detail)
        {
            color = UiTheme.Ink;
            detail = null;

            switch (step.Source)
            {
                case ProductionCollectedEvent production:
                    return "Produção   " + UiText.Amounts(production.Produced, true);

                case FoodResolvedEvent food:
                    color = food.Famine ? UiTheme.Bad : UiTheme.Ink;
                    return food.Famine
                        ? "Fome: " + food.Starved + " de gente foi embora"
                        : "A gente come   " + UiTheme.Tint("-" + food.Upkeep + " Comida", UiTheme.ResourceColor(ResourceKind.Food));

                case PopulationGrewEvent growth:
                    color = UiTheme.ResourceColor(ResourceKind.Population);
                    return "Chegou gente: agora " + growth.Total + " de " + growth.Capacity;

                case WeatherChangedEvent weather:
                    detail = WeatherEffects(weather.Today);
                    return "Dia novo — " + weather.Today.DisplayName;

                case ThreatAnnouncedEvent announced:
                    color = UiTheme.Warn;
                    return ThreatOrigin(announced.Threat) + " ataca no dia " + announced.Threat.ArrivalDay +
                           " com força " + announced.Threat.Force;

                case ThreatWeatheredEvent weathered:
                    color = UiTheme.Info;
                    return weathered.Effect.ForceDelta != 0
                        ? "O clima muda o ataque: força " + (weathered.Effect.ForceDelta > 0 ? "+" : string.Empty) + weathered.Effect.ForceDelta
                        : "O clima atrasa o ataque em " + weathered.Effect.DelayDays + " dia(s)";

                case RivalDeclaredWarEvent declared:
                    color = UiTheme.Bad;
                    detail = declared.Rival.Identity.DisplayName + " — " + declared.Rival.Identity.Description +
                             " Repila " + declared.Rival.AttacksToDefeat + " ataques para derrotá-lo.";
                    return declared.Rival.DisplayName + " declara guerra";

                case PlunderCollectedEvent plunder:
                    color = UiTheme.Gold;
                    return "Saque   " + UiText.Amounts(plunder.Plunder, true);

                case DayEventResolvedEvent dayEvent:
                    color = dayEvent.Outcome.Definition.Class == EventClass.Negative ? UiTheme.Warn
                        : dayEvent.Outcome.Definition.Class == EventClass.Positive ? UiTheme.Good : UiTheme.Ink;
                    detail = (string.IsNullOrEmpty(dayEvent.Outcome.Definition.FlavorText)
                                 ? string.Empty
                                 : "<i>" + dayEvent.Outcome.Definition.FlavorText + "</i>\n") +
                             Summarize(dayEvent.Outcome.Results);
                    return dayEvent.Outcome.Definition.DisplayName;

                case MilestoneReachedEvent milestone:
                    color = UiTheme.Gold;
                    return "Marco: " + UiText.Polish(milestone.Label);

                default:
                    return step.Headline;
            }
        }

        /// <summary>
        /// O ataque como cena: forca de um lado, defesa do outro, e o desfecho escrito
        /// grande. E o climax de varios dias, e nao pode ser uma linha de registro.
        /// </summary>
        private void DrawBattle(AttackReport report)
        {
            float width = 600f;
            float height = 196f;
            float x = (W - width) * 0.5f;
            float y = TopBarHeight + 70f;
            UiTheme.DrawBox(UiTheme.R(x, y, width, height), UiTheme.Panel);
            Block(UiTheme.R(x, y, width, height));

            Color verdictColor = report.Repelled ? UiTheme.Good : UiTheme.Bad;
            Txt(x, y + 10f, width, 34f, report.Repelled ? "ATAQUE REPELIDO" : "AS DEFESAS CEDEM", UiTheme.Title, verdictColor);

            float barWidth = width - 200f;
            float scale = Mathf.Max(1f, Mathf.Max(report.Force, report.Defense));

            Txt(x + 24f, y + 56f, 150f, 22f, "Força inimiga", UiTheme.Label);
            UiTheme.Bar(UiTheme.R(x + 150f, y + 58f, barWidth, 18f), report.Force / scale, UiTheme.Bad);
            Txt(x + 150f + barWidth + 8f, y + 54f, 40f, 24f, report.Force.ToString(), UiTheme.Number);

            Txt(x + 24f, y + 88f, 150f, 22f, "Sua defesa", UiTheme.Label);
            UiTheme.Bar(UiTheme.R(x + 150f, y + 90f, barWidth, 18f), report.Defense / scale, UiTheme.Good);
            Txt(x + 150f + barWidth + 8f, y + 86f, 40f, 24f, report.Defense.ToString(), UiTheme.Number);

            List<string> losses = new List<string>();
            if (report.TilesDestroyed.Count > 0)
            {
                losses.Add(report.TilesDestroyed.Count + " célula(s) arrasada(s)");
            }

            if (report.BaseDamage > 0)
            {
                losses.Add(report.BaseDamage + " de dano na base");
            }

            if (!report.ResourcesLost.IsEmpty)
            {
                losses.Add("levaram " + LostText(report.ResourcesLost));
            }

            Txt(x + 20f, y + 124f, width - 40f, 26f,
                losses.Count == 0 ? "Nada foi perdido." : "Perdas: " + string.Join(" · ", losses) + ".",
                UiTheme.Center, losses.Count == 0 ? UiTheme.InkDim : UiTheme.Warn);

            GUI.enabled = true;
            if (Btn(x + width - 130f, y + height - 40f, 114f, 30f, "Seguir  »", UiTheme.ButtonFlat))
            {
                _staging.SkipRest();
            }
        }

        private void DrawRivalDefeated(RunState run, RivalDefeatedEvent defeated)
        {
            DimScreen();
            float width = 560f;
            float height = 250f;
            float x = (W - width) * 0.5f;
            float y = (H - height) * 0.38f;
            UiTheme.DrawBox(UiTheme.R(x, y, width, height), UiTheme.Panel);

            Txt(x, y + 18f, width, 40f, "RIVAL DERROTADO", UiTheme.Title, UiTheme.Gold);
            Txt(x, y + 66f, width, 28f, defeated.Rival.DisplayName, UiTheme.CenterBold);
            Txt(x, y + 96f, width, 24f,
                defeated.Campaign.AttacksRepelled + " ataques repelidos" +
                (defeated.Campaign.AttacksSuffered > 0 ? ", " + defeated.Campaign.AttacksSuffered + " sofridos" : string.Empty),
                UiTheme.CenterSmall);

            // Contagem de derrotados, e nao a lista de quem ainda luta: a lista ja
            // pode conter o rival que so entra no dia seguinte.
            int left = run.Threats.Rivals.Count - run.Stats.RivalsDefeated;
            Txt(x, y + 132f, width, 26f,
                left > 0 ? "Faltam " + left + " para a vitória." : "Era o último.",
                UiTheme.Center, left > 0 ? UiTheme.Ink : UiTheme.Gold);

            if (Btn(x + (width - 220f) * 0.5f, y + height - 66f, 220f, 46f, "Continuar", UiTheme.ButtonPrimary))
            {
                _staging.Acknowledge();
            }
        }

        private void DimScreen()
        {
            Rect all = new Rect(0f, 0f, Screen.width, Screen.height);
            UiTheme.Fill(all, new Color(0f, 0f, 0f, 0.62f));
            Block(all);
        }

        // --- Evento com escolha ---

        private void DrawEventChoice(RunState run)
        {
            EventDefinition definition = _bundle.Engine.PendingDayEvent;
            if (definition == null)
            {
                return;
            }

            DimScreen();

            float width = 680f;
            float optionHeight = 96f;
            float flavorHeight = TextHeight(definition.FlavorText, UiTheme.Center, width - 60f);
            float height = 176f + flavorHeight + definition.Options.Count * (optionHeight + 10f);
            float x = (W - width) * 0.5f;
            float y = Mathf.Max(TopBarHeight + 8f, (H - height) * 0.40f);
            UiTheme.DrawBox(UiTheme.R(x, y, width, height), UiTheme.Panel);

            Txt(x, y + 14f, width, 22f,
                definition.Id.StartsWith(StarterContent.EncounterPrefix) ? "ENCONTRO" : "EVENTO DO DIA",
                UiTheme.CenterSmall, UiTheme.Gold);
            Txt(x, y + 36f, width, 38f, definition.DisplayName, UiTheme.Title);
            Txt(x + 30f, y + 80f, width - 60f, flavorHeight, "<i>" + definition.FlavorText + "</i>", UiTheme.Center, UiTheme.InkDim);

            // O que o reino tem agora. A tela atras esta escurecida, e a escolha
            // quase sempre custa recurso: os numeros precisam estar aqui, nitidos.
            float stripY = y + 90f + flavorHeight;
            UiTheme.DrawInset(UiTheme.R(x + 24f, stripY, width - 48f, 56f));
            Txt(x + 36f, stripY + 4f, 200f, 16f, "VOCÊ TEM", UiTheme.Small, UiTheme.InkDim);
            float chip = (width - 72f) / ResourceKinds.All.Length;
            for (int k = 0; k < ResourceKinds.All.Length; k++)
            {
                ResourceKind kind = ResourceKinds.All[k];
                float cx = x + 36f + k * chip;
                UiTheme.DrawDot(UiTheme.R(cx, stripY + 28f, 14f, 14f), UiTheme.ResourceColor(kind));
                Txt(cx + 20f, stripY + 20f, chip - 24f, 28f,
                    "<b>" + run[kind] + "</b>  <size=" + Mathf.RoundToInt(13 * UiTheme.Scale) + ">" + UiText.Resource(kind) + "</size>",
                    UiTheme.Number, UiTheme.ResourceColor(kind));
            }

            float optionY = stripY + 68f;
            for (int i = 0; i < definition.Options.Count; i++)
            {
                EventOption option = definition.Options[i];
                OptionPreview(run, option, out string costs, out string gains);

                if (Btn(x + 24f, optionY, width - 48f, optionHeight, string.Empty,
                        option.IsOffer ? UiTheme.ButtonPrimary : UiTheme.Button))
                {
                    ChooseEventOption(i);
                    return;
                }

                Txt(x + 40f, optionY + 8f, width - 80f, 24f, option.Label, UiTheme.CenterBold);

                // Gasto em vermelho e ganho em verde, cada um na sua linha.
                Rect costRow = UiTheme.R(x + 60f, optionY + 36f, width - 120f, 24f);
                if (costs != null)
                {
                    UiTheme.Fill(costRow, new Color(0.10f, 0.03f, 0.03f, 0.80f));
                    Txt(x + 60f, optionY + 36f, width - 120f, 24f, "GASTA   " + costs, UiTheme.CenterBold, UiTheme.Bad);
                }
                else
                {
                    Txt(x + 60f, optionY + 36f, width - 120f, 24f, "Não gasta nada", UiTheme.CenterSmall, UiTheme.Ink);
                }

                if (gains != null)
                {
                    Txt(x + 40f, optionY + 64f, width - 80f, 24f, "GANHA   " + gains, UiTheme.CenterBold, UiTheme.Good);
                }
                else if (costs == null)
                {
                    Txt(x + 40f, optionY + 64f, width - 80f, 24f, "Nada acontece.", UiTheme.CenterSmall, UiTheme.InkDim);
                }

                optionY += optionHeight + 10f;
            }
        }

        /// <summary>
        /// Separa o que a opcao tira do que ela da, com os numeros de agora: uma
        /// perda de "30% do ouro" aparece como a quantia exata e o saldo que sobra.
        /// </summary>
        private static void OptionPreview(RunState run, EventOption option, out string costs, out string gains)
        {
            List<string> costParts = new List<string>();
            List<string> gainParts = new List<string>();

            for (int i = 0; i < option.Effects.Count; i++)
            {
                IEffect effect = option.Effects[i];
                switch (effect)
                {
                    case LoseResourceEffect lose:
                        int lost = lose.Preview(run);
                        costParts.Add("-" + lost + " " + UiText.Resource(lose.Resource) +
                                      " (fica " + (run[lose.Resource] - lost) + ")");
                        break;
                    case GainResourceEffect gain:
                        gainParts.Add("+" + gain.Preview(run) + " " + UiText.Resource(gain.Resource));
                        break;
                    case DamageBaseEffect _:
                        costParts.Add(UiText.Polish(effect.Describe()));
                        break;
                    default:
                        string line = effect?.Describe();
                        if (!string.IsNullOrEmpty(line))
                        {
                            gainParts.Add(UiText.Polish(line));
                        }

                        break;
                }
            }

            costs = costParts.Count > 0 ? string.Join("   ·   ", costParts) : null;
            gains = gainParts.Count > 0 ? string.Join("   ·   ", gainParts) : null;
        }

        private static string DescribeEffects(IReadOnlyList<IEffect> effects)
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < effects.Count; i++)
            {
                string line = effects[i]?.Describe();
                if (!string.IsNullOrEmpty(line))
                {
                    parts.Add(UiText.Polish(line));
                }
            }

            return string.Join(" · ", parts);
        }
    }
}

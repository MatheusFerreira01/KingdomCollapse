using System.Collections.Generic;
using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
    /// <summary>
    /// As telas fora do tabuleiro: menu principal, selecao de nivel, arvore do reino,
    /// ajuda, pausa e fim de run.
    /// </summary>
    public sealed partial class GameBootstrap
    {
        private string _selectedNode;
        private int _selectedLevel = 1;
        private bool _confirmReset;

        /// <summary>Aba da arvore: null e o tronco geral; senao, o galho da raca.</summary>
        private string _metaTab;

        // --- Menu principal ---

        private void DrawMainMenu()
        {
            // Escurece a esquerda, onde fica o menu, e deixa o reino aparecer a direita.
            UiTheme.Fill(UiTheme.R(0f, 0f, 560f, H), new Color(0.04f, 0.045f, 0.065f, 0.90f));
            UiTheme.Fill(UiTheme.R(560f, 0f, 3f, H), new Color(0.50f, 0.42f, 0.24f, 0.8f));

            float x = 80f;
            float y = H * 0.14f;

            Txt(x, y, 440f, 70f, "KINGDOM", UiTheme.Huge);
            Txt(x, y + 62f, 440f, 70f, "COLLAPSE", UiTheme.Huge, UiTheme.Ink);
            Txt(x, y + 136f, 440f, 44f,
                "Erga um reino, célula por célula, e segure-o contra os reinos vizinhos.", UiTheme.Center, UiTheme.InkDim);

            y += 210f;
            float width = 320f;
            float left = x + (440f - width) * 0.5f;

            if (Btn(left, y, width, 58f, "Nova Run", UiTheme.ButtonPrimary))
            {
                _selectedLevel = Mathf.Clamp(_profile.HighestUnlockedLevel, 1, Mathf.Max(1, _ladder.Levels.Count));
                _screen = GameScreen.LevelSelect;
            }

            // Novo Jogo: zera o perfil e abre a primeira run com o tutorial.
            y += 70f;
            bool hasProgress = _profile.RunsPlayed > 0 || _profile.Currency > 0 || _profile.HighestUnlockedLevel > 1;
            if (!_confirmReset)
            {
                if (Btn(left, y, width, 48f, "Novo Jogo", UiTheme.Button,
                        hasProgress
                            ? "Recomeça do zero, com o tutorial. Apaga moedas, árvore e níveis liberados."
                            : "Começa pela run guiada, que ensina o básico."))
                {
                    if (hasProgress)
                    {
                        _confirmReset = true;
                    }
                    else
                    {
                        BeginNewGame();
                        return;
                    }
                }
            }
            else
            {
                if (Btn(left, y, width * 0.5f - 4f, 48f, "Apagar tudo", UiTheme.ButtonDanger,
                        "Zera moedas, árvore e níveis liberados. Não tem volta."))
                {
                    BeginNewGame();
                    return;
                }

                if (Btn(left + width * 0.5f + 4f, y, width * 0.5f - 4f, 48f, "Cancelar", UiTheme.Button))
                {
                    _confirmReset = false;
                }
            }

            y += 58f;
            if (Btn(left, y, width, 48f, "Árvore do Reino   " + UiTheme.Tint(_profile.Currency + " moedas", UiTheme.Gold),
                    UiTheme.Button, "Gaste as moedas ganhas nas runs em melhorias permanentes."))
            {
                _returnTo = GameScreen.MainMenu;
                _screen = GameScreen.MetaTree;
            }

            y += 58f;
            if (Btn(left, y, width, 48f, "Como Jogar", UiTheme.Button))
            {
                _returnTo = GameScreen.MainMenu;
                _screen = GameScreen.HowToPlay;
            }

            y += 58f;
            if (Btn(left, y, width, 48f, "Sair", UiTheme.Button))
            {
                Quit();
            }

            // Rodape: o que o perfil ja fez.
            string stats = _profile.RunsPlayed == 0
                ? "Nenhuma run jogada ainda."
                : _profile.RunsPlayed + " run(s)  ·  melhor dia " + _profile.BestDayReached +
                  "  ·  recorde " + _profile.BestScore + " pts  ·  nível " +
                  Mathf.Min(_profile.HighestUnlockedLevel, Mathf.Max(1, _ladder.Levels.Count)) + " liberado";
            Txt(x, H - 86f, 440f, 22f, stats, UiTheme.CenterSmall);
        }

        private void BeginNewGame()
        {
            _profile = MetaProfile.NewProfile();
            SaveProfile();
            _confirmReset = false;
            _metaTab = null;
            _selectedNode = null;
            _raceId = StarterContent.HumansId;
            StartRun(1);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void DrawBackdrop()
        {
            UiTheme.Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.04f, 0.045f, 0.065f, 0.93f));
        }

        // --- Selecao de nivel ---

        private void DrawLevelSelect()
        {
            DrawBackdrop();

            Txt(0f, 46f, W, 50f, "ESCOLHA O NÍVEL", UiTheme.Title, UiTheme.Gold);
            Txt(0f, 96f, W, 26f, "Vencer um nível libera o seguinte, que é mais duro em algo declarado.", UiTheme.Center, UiTheme.InkDim);

            List<LevelChoice> choices = _ladder.Choices(_profile);
            if (choices.Count == 0)
            {
                Txt(0f, 300f, W, 30f, "Este conteúdo não tem níveis: a run usa a lista de rivais direto.", UiTheme.Center);
                if (Btn((W - 300f) * 0.5f, 360f, 300f, 54f, "Começar", UiTheme.ButtonPrimary))
                {
                    StartRun(1);
                }
            }

            float cardWidth = 380f;
            float gap = 24f;
            float total = choices.Count * cardWidth + (choices.Count - 1) * gap;
            float startX = (W - total) * 0.5f;
            float y = 150f;
            float cardHeight = 520f;

            for (int i = 0; i < choices.Count; i++)
            {
                LevelChoice choice = choices[i];
                DifficultyLevel level = choice.Level;
                float x = startX + i * (cardWidth + gap);
                bool selected = _selectedLevel == level.Rank;

                UiTheme.DrawBox(UiTheme.R(x, y, cardWidth, cardHeight),
                    !choice.Unlocked ? UiTheme.CardOff : selected ? UiTheme.CardHot : UiTheme.Card);

                Color previous = GUI.color;
                if (!choice.Unlocked)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.5f);
                }

                Txt(x, y + 18f, cardWidth, 22f, "NÍVEL " + level.Rank, UiTheme.CenterSmall, UiTheme.Gold);
                Txt(x, y + 40f, cardWidth, 40f, level.DisplayName, UiTheme.Title);

                float hardeningHeight = TextHeight(level.Hardening, UiTheme.Center, cardWidth - 48f);
                Txt(x + 24f, y + 92f, cardWidth - 48f, hardeningHeight, level.Hardening, UiTheme.Center, UiTheme.InkDim);

                float rowY = y + 104f + Mathf.Max(66f, hardeningHeight);
                Txt(x + 24f, rowY, cardWidth - 48f, 22f, "RIVAIS", UiTheme.Heading);
                rowY += 28f;

                for (int r = 0; r < level.Rivals.Count; r++)
                {
                    RivalDefinition rival = level.Rivals[r];
                    Txt(x + 24f, rowY, cardWidth - 48f, 22f, rival.DisplayName, UiTheme.Bold);
                    Txt(x + 24f, rowY + 21f, cardWidth - 48f, 20f,
                        rival.Identity.DisplayName + " · " + rival.AttacksToDefeat + " ataques, força " +
                        rival.Campaign.ForceFor(0) + " a " + rival.PeakForce + " · dia " + level.EntryDayAt(r),
                        UiTheme.Small);
                    Tip(UiTheme.R(x + 24f, rowY, cardWidth - 48f, 42f),
                        "<b>" + rival.Identity.DisplayName + "</b>\n" + rival.Identity.Description);
                    rowY += 48f;
                }

                GUI.color = previous;

                if (!choice.Unlocked)
                {
                    DifficultyLevel before = _ladder.LevelOfRank(level.Rank - 1);
                    Txt(x + 20f, y + cardHeight - 62f, cardWidth - 40f, 44f,
                        "Bloqueado: vença " + (before != null ? before.DisplayName : "o nível anterior") + ".",
                        UiTheme.Center, UiTheme.Warn);
                }
                else if (Btn(x + 30f, y + cardHeight - 70f, cardWidth - 60f, 50f,
                             selected ? "Começar" : "Escolher", selected ? UiTheme.ButtonPrimary : UiTheme.Button))
                {
                    if (selected)
                    {
                        StartRun(level.Rank);
                        return;
                    }

                    _selectedLevel = level.Rank;
                }
            }

            DrawRacePicker(y + cardHeight + 16f);

            if (Btn(40f, H - 76f, 180f, 44f, "«  Voltar", UiTheme.Button))
            {
                _screen = GameScreen.MainMenu;
            }
        }

        /// <summary>Com que raca a run comeca. So existe no conteudo embutido.</summary>
        private void DrawRacePicker(float y)
        {
            if (_contentSource != ContentSource.BuiltIn)
            {
                return;
            }

            string[] races = StarterContent.RaceIds;
            const float ButtonWidth = 150f;
            const float Gap = 10f;
            float total = races.Length * ButtonWidth + (races.Length - 1) * Gap;
            float x = (W - total) * 0.5f;

            Txt(x - 110f, y, 100f, 38f, "RAÇA", UiTheme.Heading);
            for (int i = 0; i < races.Length; i++)
            {
                bool active = _raceId == races[i];
                if (Btn(x + i * (ButtonWidth + Gap), y, ButtonWidth, 38f, StarterContent.RaceName(races[i]),
                        active ? UiTheme.ButtonPrimary : UiTheme.Button, StarterContent.RaceSummary(races[i])))
                {
                    _raceId = races[i];
                    BuildShowcase();
                }
            }

            float textWidth = Mathf.Min(1000f, W - 460f);
            Txt((W - textWidth) * 0.5f, y + 42f, textWidth, 62f, StarterContent.RaceSummary(_raceId), UiTheme.Center, UiTheme.InkDim);
        }

        // --- Arvore do reino ---

        private void DrawMetaTree()
        {
            DrawBackdrop();

            Txt(0f, 34f, W, 50f, "ÁRVORE DO REINO", UiTheme.Title, UiTheme.Gold);
            Txt(0f, 82f, W, 26f,
                "Melhorias permanentes, pagas com as moedas das runs. Valem para toda run daqui em diante.",
                UiTheme.Center, UiTheme.InkDim);

            Txt(W - 330f, 40f, 290f, 40f, UiTheme.Tint(_profile.Currency.ToString(), UiTheme.Gold) + " moedas", UiTheme.Title);

            if (_contentSource == ContentSource.BuiltIn)
            {
                string[] races = StarterContent.RaceIds;
                float tabX = 40f;
                if (Btn(tabX, 100f, 110f, 32f, "Geral", _metaTab == null ? UiTheme.ButtonFlatOn : UiTheme.ButtonFlat,
                        "Vale para qualquer raça."))
                {
                    _metaTab = null;
                    _selectedNode = null;
                }

                for (int i = 0; i < races.Length; i++)
                {
                    if (StarterContent.RaceBranch(races[i]).Length == 0)
                    {
                        continue;
                    }

                    tabX += 116f;
                    if (Btn(tabX, 100f, 110f, 32f, StarterContent.RaceName(races[i]),
                            _metaTab == races[i] ? UiTheme.ButtonFlatOn : UiTheme.ButtonFlat,
                            "Galho que só vale em runs de " + StarterContent.RaceName(races[i]) + "."))
                    {
                        _metaTab = races[i];
                        _selectedNode = null;
                    }
                }
            }

            string[][] columns = MetaColumns();
            string[] titles = MetaColumnTitles(columns.Length);

            const float NodeWidth = 250f;
            const float NodeHeight = 96f;
            const float RowGap = 44f;
            float treeWidth = W - 420f;
            float columnWidth = treeWidth / Mathf.Max(1, columns.Length);
            float top = 150f;

            for (int c = 0; c < columns.Length; c++)
            {
                float cx = 40f + c * columnWidth + (columnWidth - NodeWidth) * 0.5f;
                Txt(cx, top - 6f, NodeWidth, 26f, titles[c].ToUpperInvariant(), UiTheme.Heading);

                for (int r = 0; r < columns[c].Length; r++)
                {
                    MetaNodeDefinition node = _tree.Get(columns[c][r]);
                    if (node == null)
                    {
                        continue;
                    }

                    float ny = top + 30f + r * (NodeHeight + RowGap);

                    // Liga cada no ao de baixo: a coluna se le de cima para baixo.
                    if (r > 0)
                    {
                        bool open = _profile.HasNode(columns[c][r - 1]);
                        UiTheme.Fill(UiTheme.R(cx + NodeWidth * 0.5f - 2f, ny - RowGap, 4f, RowGap),
                            open ? UiTheme.Gold : new Color(0.28f, 0.29f, 0.34f));
                    }

                    DrawMetaNode(node, cx, ny, NodeWidth, NodeHeight);
                }
            }

            DrawMetaDetail(W - 370f, 150f, 330f, H - 150f - 100f);

            if (Btn(40f, H - 76f, 180f, 44f, "«  Voltar", UiTheme.Button))
            {
                _screen = _returnTo == GameScreen.Playing && _bundle != null ? GameScreen.Playing : GameScreen.MainMenu;
            }
        }

        /// <summary>
        /// Colunas da arvore. O conteudo embutido declara o desenho; para qualquer
        /// outro, uma coluna por raiz, seguindo os pre-requisitos.
        /// </summary>
        private string[][] MetaColumns()
        {
            if (_contentSource == ContentSource.BuiltIn)
            {
                return _metaTab == null
                    ? StarterContent.MetaTreeColumns
                    : StarterContent.RaceColumns(_metaTab);
            }

            List<string[]> columns = new List<string[]>();
            HashSet<string> placed = new HashSet<string>();
            List<string> ids = new List<string>(_tree.Nodes.Keys);
            ids.Sort(string.CompareOrdinal);

            for (int i = 0; i < ids.Count; i++)
            {
                if (_tree.Get(ids[i]).PrerequisiteIds.Count > 0)
                {
                    continue;
                }

                List<string> chain = new List<string> { ids[i] };
                placed.Add(ids[i]);
                bool grew = true;
                while (grew)
                {
                    grew = false;
                    for (int j = 0; j < ids.Count; j++)
                    {
                        MetaNodeDefinition candidate = _tree.Get(ids[j]);
                        if (placed.Contains(ids[j]) || candidate.PrerequisiteIds.Count == 0 ||
                            candidate.PrerequisiteIds[0] != chain[chain.Count - 1])
                        {
                            continue;
                        }

                        chain.Add(ids[j]);
                        placed.Add(ids[j]);
                        grew = true;
                        break;
                    }
                }

                columns.Add(chain.ToArray());
            }

            return columns.ToArray();
        }

        private string[] MetaColumnTitles(int count)
        {
            if (_contentSource == ContentSource.BuiltIn)
            {
                return _metaTab == null
                    ? StarterContent.MetaTreeColumnTitles
                    : StarterContent.RaceColumnTitles(_metaTab);
            }

            string[] titles = new string[count];
            for (int i = 0; i < count; i++)
            {
                titles[i] = "Ramo " + (i + 1);
            }

            return titles;
        }

        private void DrawMetaNode(MetaNodeDefinition node, float x, float y, float width, float height)
        {
            bool owned = _profile.HasNode(node.Id);
            PurchaseResult check = _tree.CanPurchase(_profile, node.Id);
            bool selected = _selectedNode == node.Id;

            GUIStyle frame = owned ? UiTheme.CardHot : check.Ok ? UiTheme.Card : UiTheme.CardOff;
            Rect pixels = UiTheme.R(x, y, width, height);
            UiTheme.DrawBox(pixels, frame);

            if (selected)
            {
                UiTheme.Fill(UiTheme.R(x - 3f, y + height - 2f, width + 6f, 4f), UiTheme.Info);
            }

            Color nameColor = owned ? UiTheme.Gold : check.Ok ? UiTheme.Ink : UiTheme.InkDim;
            Txt(x + 12f, y + 10f, width - 24f, 26f, node.DisplayName, UiTheme.CenterBold, nameColor);

            string status;
            if (owned)
            {
                status = UiTheme.Tint("adquirido", UiTheme.Good);
            }
            else
            {
                switch (check.Rejection)
                {
                    case PurchaseRejection.MissingPrerequisite:
                        status = UiTheme.Tint("requer " + NodeName(check.MissingPrerequisite), UiTheme.InkDim);
                        break;
                    case PurchaseRejection.LevelLocked:
                        status = UiTheme.Tint("requer liberar o nível " + node.RequiredLevel, UiTheme.InkDim);
                        break;
                    case PurchaseRejection.NotEnoughCurrency:
                        status = UiTheme.Tint(node.Cost + " moedas (faltam " + (node.Cost - _profile.Currency) + ")", UiTheme.Warn);
                        break;
                    default:
                        status = UiTheme.Tint(node.Cost + " moedas", UiTheme.Gold);
                        break;
                }
            }

            Txt(x + 12f, y + 40f, width - 24f, 22f, status, UiTheme.CenterSmall);
            Txt(x + 12f, y + 62f, width - 24f, 26f, ShortEffect(node), UiTheme.CenterSmall, UiTheme.Ink);

            if (GUI.Button(pixels, GUIContent.none, GUIStyle.none))
            {
                _selectedNode = node.Id;
            }
        }

        private string NodeName(string nodeId)
        {
            MetaNodeDefinition node = nodeId != null ? _tree.Get(nodeId) : null;
            return node != null ? node.DisplayName : "outro nó";
        }

        private string ShortEffect(MetaNodeDefinition node)
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < node.Bonuses.Count; i++)
            {
                MetaBonus bonus = node.Bonuses[i];
                parts.Add(bonus.Kind == MetaBonusKind.MaxIntegrity
                    ? "+" + bonus.Amount + " Base"
                    : UiTheme.Tint("+" + bonus.Amount + " " + UiText.Resource(bonus.Resource), UiTheme.ResourceColor(bonus.Resource)));
            }

            for (int i = 0; i < node.Unlocks.Count; i++)
            {
                parts.Add(UnlockName(node.Unlocks[i]));
            }

            return string.Join(" · ", parts);
        }

        private string UnlockName(Unlock unlock)
        {
            switch (unlock.Kind)
            {
                case UnlockKind.Card:
                    return "carta " + (_catalog.Cards.TryGetValue(unlock.ContentId, out CardDefinition card) ? card.DisplayName : unlock.ContentId);
                case UnlockKind.Building:
                    return "edifício " + (_catalog.Buildings.TryGetValue(unlock.ContentId, out BuildingDefinition building)
                        ? building.DisplayName
                        : unlock.ContentId);
                case UnlockKind.Race:
                    return "raça " + unlock.ContentId;
                default:
                    return unlock.ContentId;
            }
        }

        private void DrawMetaDetail(float x, float y, float width, float height)
        {
            UiTheme.DrawPanel(UiTheme.R(x, y, width, height));

            MetaNodeDefinition node = _selectedNode != null ? _tree.Get(_selectedNode) : null;
            if (node == null)
            {
                Txt(x + 20f, y + 20f, width - 40f, 120f,
                    "Clique num nó para ver o que ele faz.\n\nCada coluna se compra de cima para baixo, e o último degrau só abre depois de vencer o primeiro nível.",
                    UiTheme.Dim);
                return;
            }

            float ty = y + 18f;
            Txt(x + 20f, ty, width - 40f, 30f, node.DisplayName, UiTheme.Heading);
            ty += 36f;

            float descriptionHeight = TextHeight(node.Description, UiTheme.Label, width - 40f);
            Txt(x + 20f, ty, width - 40f, descriptionHeight, node.Description, UiTheme.Label);
            ty += descriptionHeight + 14f;

            for (int i = 0; i < node.Bonuses.Count; i++)
            {
                Txt(x + 20f, ty, width - 40f, 22f, "•  " + UiText.Polish(node.Bonuses[i].Describe()), UiTheme.Label, UiTheme.Good);
                ty += 24f;
            }

            for (int i = 0; i < node.Unlocks.Count; i++)
            {
                Unlock unlock = node.Unlocks[i];
                Txt(x + 20f, ty, width - 40f, 22f, "•  Libera " + UnlockName(unlock), UiTheme.Label, UiTheme.Good);
                ty += 24f;

                string detail = UnlockDetail(unlock);
                if (!string.IsNullOrEmpty(detail))
                {
                    float detailHeight = TextHeight(detail, UiTheme.Small, width - 60f);
                    Txt(x + 40f, ty, width - 60f, detailHeight, detail, UiTheme.Small);
                    ty += detailHeight + 6f;
                }
            }

            bool owned = _profile.HasNode(node.Id);
            PurchaseResult check = _tree.CanPurchase(_profile, node.Id);
            float by = y + height - 68f;

            if (owned)
            {
                Txt(x + 20f, by, width - 40f, 46f, "Adquirido", UiTheme.CenterBold, UiTheme.Good);
                return;
            }

            string label = "Comprar por " + node.Cost + " moedas";
            if (Btn(x + 20f, by, width - 40f, 48f, label, UiTheme.ButtonPrimary, PurchaseHint(node, check), check.Ok))
            {
                PurchaseResult result = _tree.Purchase(_profile, node.Id);
                if (result.Ok)
                {
                    SaveProfile();
                    Toast(node.DisplayName + " adquirido.", UiTheme.Good);
                }
            }

            if (!check.Ok)
            {
                Txt(x + 20f, by - 30f, width - 40f, 24f, PurchaseHint(node, check), UiTheme.CenterSmall, UiTheme.Warn);
            }
        }

        private string PurchaseHint(MetaNodeDefinition node, PurchaseResult check)
        {
            switch (check.Rejection)
            {
                case PurchaseRejection.MissingPrerequisite:
                    return "Compre antes: " + NodeName(check.MissingPrerequisite) + ".";
                case PurchaseRejection.LevelLocked:
                    DifficultyLevel before = _ladder.LevelOfRank(node.RequiredLevel - 1);
                    return "Vença " + (before != null ? before.DisplayName : "o nível anterior") + " para liberar.";
                case PurchaseRejection.NotEnoughCurrency:
                    return "Faltam " + (node.Cost - _profile.Currency) + " moedas.";
                default:
                    return null;
            }
        }

        private string UnlockDetail(Unlock unlock)
        {
            if (unlock.Kind == UnlockKind.Card && _catalog.Cards.TryGetValue(unlock.ContentId, out CardDefinition card))
            {
                return "Custa " + card.EnergyCost + " de energia. " + UiText.CardRules(card);
            }

            if (unlock.Kind == UnlockKind.Building &&
                _catalog.Buildings.TryGetValue(unlock.ContentId, out BuildingDefinition building))
            {
                return BuildingSummary(building) + "\nCusto: " + UiText.Cost(building.Cost, null);
            }

            return null;
        }

        // --- Como jogar ---

        private static readonly string[][] HelpSections =
        {
            new[]
            {
                "O objetivo",
                "Cada nível tem reinos rivais. Um rival é derrotado quando você <b>repele</b> todos os ataques da campanha dele. Derrote todos e a run é sua. Se a integridade da base chegar a zero, ou não sobrar célula de pé, o reino colapsa."
            },
            new[]
            {
                "O dia",
                "De dia você planeja: compra células, constrói, joga cartas. Ao <b>encerrar o dia</b> o reino produz, a gente come, os ataques marcados para hoje chegam e pode acontecer um evento. Depois começa o dia seguinte."
            },
            new[]
            {
                "Recursos e gente",
                "<b>Ouro</b> compra terra. <b>Madeira</b> e <b>pedra</b> erguem edifícios. <b>Comida</b> sustenta a gente, e <b>gente</b> opera quase tudo: um edifício sem gente fica parado. Casas aumentam o teto de população; com comida sobrando, chega gente nova."
            },
            new[]
            {
                "Terreno",
                "Planície aceita Fazenda, floresta aceita Serraria, rochedo aceita Pedreira e rio aceita Ancoradouro. Vizinhança importa: Serraria rende mais cercada de floresta, Fazenda rende mais ao lado de rio."
            },
            new[]
            {
                "Ataques",
                "Todo ataque é anunciado com dias de antecedência, com a força que traz. Compare com a sua defesa no painel da esquerda. Cada rival pressiona uma coisa: o de <b>Assalto</b> arrasa a borda, o de <b>Cerco</b> bate direto na base, o de <b>Saque</b> leva comida mesmo repelido."
            },
            new[]
            {
                "Cartas",
                "Você compra uma mão nova todo dia e tem pouca energia para gastar. <b>Carta de defesa vale só no dia em que é jogada</b>: guarde para o dia do ataque."
            },
            new[]
            {
                "Entre runs",
                "Toda run rende moedas, mais ainda se você vencer. Elas compram melhorias permanentes na Árvore do Reino. Vencer um nível libera o seguinte."
            }
        };

        private void DrawHowToPlay()
        {
            DrawBackdrop();
            Txt(0f, 30f, W, 50f, "COMO JOGAR", UiTheme.Title, UiTheme.Gold);

            float width = Mathf.Min(1180f, W - 80f);
            float x = (W - width) * 0.5f;
            float columnWidth = (width - 40f) * 0.5f;
            float[] columnY = { 100f, 100f };

            for (int i = 0; i < HelpSections.Length; i++)
            {
                int column = columnY[0] <= columnY[1] ? 0 : 1;
                float cx = x + column * (columnWidth + 40f);
                float bodyHeight = TextHeight(HelpSections[i][1], UiTheme.Label, columnWidth - 36f);
                float boxHeight = bodyHeight + 52f;

                UiTheme.DrawPanel(UiTheme.R(cx, columnY[column], columnWidth, boxHeight));
                Txt(cx + 18f, columnY[column] + 10f, columnWidth - 36f, 26f, HelpSections[i][0].ToUpperInvariant(), UiTheme.Heading);
                Txt(cx + 18f, columnY[column] + 40f, columnWidth - 36f, bodyHeight, HelpSections[i][1], UiTheme.Label);
                columnY[column] += boxHeight + 12f;
            }

            Txt(x, H - 120f, width, 24f,
                "Atalhos:  Espaço encerra o dia ou pula a encenação  ·  1 a 9 jogam cartas  ·  Esc cancela ou pausa  ·  L abre o registro",
                UiTheme.CenterSmall);

            if (Btn(40f, H - 76f, 180f, 44f, "«  Voltar", UiTheme.Button))
            {
                _screen = _returnTo == GameScreen.Playing && _bundle != null ? GameScreen.Playing : GameScreen.MainMenu;
            }
        }

        // --- Pausa ---

        private void DrawPause()
        {
            DimScreen();

            float width = 380f;
            float height = _confirmAbandon ? 300f : 480f;
            float x = (W - width) * 0.5f;
            float y = (H - height) * 0.42f;
            UiTheme.DrawBox(UiTheme.R(x, y, width, height), UiTheme.Panel);
            Txt(x, y + 16f, width, 40f, _confirmAbandon ? "ABANDONAR A RUN?" : "PAUSA", UiTheme.Title, UiTheme.Gold);

            float bx = x + 40f;
            float bw = width - 80f;
            float by = y + 76f;

            if (_confirmAbandon)
            {
                Txt(bx, by, bw, 66f, "A run termina agora e não rende moedas nem conta para o perfil.", UiTheme.Center, UiTheme.InkDim);
                by += 80f;
                if (Btn(bx, by, bw, 46f, "Abandonar", UiTheme.ButtonDanger))
                {
                    ShowMainMenu();
                    return;
                }

                by += 56f;
                if (Btn(bx, by, bw, 46f, "Voltar", UiTheme.Button))
                {
                    _confirmAbandon = false;
                }

                return;
            }

            if (Btn(bx, by, bw, 50f, "Continuar", UiTheme.ButtonPrimary))
            {
                _paused = false;
            }

            by += 62f;
            if (Btn(bx, by, bw, 44f, "Como Jogar", UiTheme.Button))
            {
                _returnTo = GameScreen.Playing;
                _screen = GameScreen.HowToPlay;
            }

            by += 54f;
            if (Btn(bx, by, bw, 44f, UiSound.Muted ? "Som: desligado" : "Som: ligado", UiTheme.Button))
            {
                UiSound.Muted = !UiSound.Muted;
            }

            by += 54f;
            if (Btn(bx, by, bw, 44f, UiSound.MusicOff ? "Música: desligada" : "Música: ligada", UiTheme.Button))
            {
                UiSound.MusicOff = !UiSound.MusicOff;
            }

            by += 54f;
            if (Btn(bx, by, bw, 44f, "Abandonar a run", UiTheme.Button))
            {
                _confirmAbandon = true;
            }

            by += 54f;
            if (Btn(bx, by, bw, 44f, "Sair do jogo", UiTheme.Button))
            {
                Quit();
            }

            Txt(x, y + height - 34f, width, 20f, "Semente " + _runSeed, UiTheme.CenterSmall);
        }

        // --- Fim de run ---

        private void DrawRunEnd(RunState run)
        {
            DimScreen();

            RunScore score = _bundle.Engine.FinalScore;
            bool victory = run.Outcome == RunOutcome.Victory;
            int lines = score != null ? score.Lines.Count : 0;

            float width = 600f;
            float height = 330f + lines * 24f + (_justUnlocked != null ? 96f : 0f);
            float x = (W - width) * 0.5f;
            float y = Mathf.Max(20f, (H - height) * 0.5f);
            UiTheme.DrawBox(UiTheme.R(x, y, width, height), UiTheme.Panel);

            Txt(x, y + 16f, width, 64f, victory ? "VITÓRIA" : "COLAPSO", UiTheme.Huge, victory ? UiTheme.Gold : UiTheme.Bad);

            string subtitle = victory
                ? "Todos os rivais caíram em " + run.Day + " dias."
                : run.Collapse == CollapseReason.TerritoryLost
                    ? "Não sobrou célula de pé. Dia " + run.Day + "."
                    : "A base caiu no dia " + run.Day + ".";
            Txt(x, y + 84f, width, 26f, subtitle, UiTheme.Center, UiTheme.InkDim);

            float ty = y + 124f;
            if (score != null)
            {
                for (int i = 0; i < score.Lines.Count; i++)
                {
                    ScoreLine line = score.Lines[i];
                    Txt(x + 60f, ty, width - 220f, 22f, UiText.Polish(ScoreLabel(line.Label)) + "  ×" + line.Quantity, UiTheme.Label);
                    Txt(x + width - 180f, ty, 120f, 22f, line.Points.ToString(), UiTheme.Right);
                    ty += 24f;
                }

                UiTheme.Fill(UiTheme.R(x + 60f, ty + 4f, width - 120f, 1f), new Color(0.5f, 0.42f, 0.24f));
                ty += 12f;
                Txt(x + 60f, ty, width - 220f, 26f, "Total", UiTheme.Bold);
                Txt(x + width - 180f, ty, 120f, 26f, score.TotalPoints + " pts", UiTheme.Right, UiTheme.Gold);
                ty += 36f;

                Txt(x, ty, width, 30f,
                    "+" + _coinsEarned + " moedas   (" + _profile.Currency + " no total)", UiTheme.CenterBold, UiTheme.Gold);
                ty += 40f;
            }

            // O desbloqueio fica na tela de fim: e aqui que o jogador decide a proxima run.
            if (_justUnlocked != null)
            {
                UiTheme.DrawInset(UiTheme.R(x + 40f, ty, width - 80f, 84f));
                Txt(x + 40f, ty + 8f, width - 80f, 24f, "NÍVEL LIBERADO: " + _justUnlocked.DisplayName.ToUpperInvariant(),
                    UiTheme.CenterBold, UiTheme.Good);
                Txt(x + 56f, ty + 34f, width - 112f, 46f, _justUnlocked.Hardening, UiTheme.CenterSmall, UiTheme.Ink);
                ty += 96f;
            }

            float third = (width - 100f) / 3f;
            float by = y + height - 66f;
            if (Btn(x + 40f, by, third, 46f, "Jogar de novo", UiTheme.ButtonPrimary))
            {
                StartRun(run.LevelRank);
                return;
            }

            if (Btn(x + 50f + third, by, third, 46f, "Árvore do Reino", UiTheme.Button))
            {
                // A run acabou: voltar da arvore leva ao menu, nao a uma run encerrada.
                ShowMainMenu();
                _returnTo = GameScreen.MainMenu;
                _screen = GameScreen.MetaTree;
                return;
            }

            if (Btn(x + 60f + 2f * third, by, third, 46f, "Menu", UiTheme.Button))
            {
                ShowMainMenu();
            }
        }

        private static string ScoreLabel(string label)
        {
            switch (label)
            {
                case "Celulas de pe":
                    return "Células de pé";
                case "Edificios erguidos":
                    return "Edifícios erguidos";
                case "Marcos alcancados":
                    return "Marcos alcançados";
                case "Vitoria":
                    return "Vitória";
                default:
                    return label.StartsWith("Nivel ") ? "Nível " + label.Substring(6) : label;
            }
        }
    }
}

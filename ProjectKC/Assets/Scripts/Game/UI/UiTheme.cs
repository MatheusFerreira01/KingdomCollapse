using System.Collections.Generic;
using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
    /// <summary>
    /// Aparencia do HUD: cores, texturas geradas em codigo e estilos de IMGUI.
    ///
    /// Tudo e desenhado numa resolucao de referencia (1600x900) e multiplicado por
    /// <see cref="Scale"/> na hora de virar pixel. O texto e renderizado ja no
    /// tamanho final, e nao esticado por matriz, que e o que mantem as letras
    /// nitidas em qualquer monitor.
    /// </summary>
    public static class UiTheme
    {
        public const float ReferenceWidth = 1600f;
        public const float ReferenceHeight = 900f;

        // --- Paleta ---

        public static readonly Color Ink = new Color(0.93f, 0.91f, 0.85f);
        public static readonly Color InkDim = new Color(0.66f, 0.66f, 0.64f);
        public static readonly Color InkFaint = new Color(0.46f, 0.47f, 0.49f);
        public static readonly Color Gold = new Color(0.96f, 0.78f, 0.33f);
        public static readonly Color Good = new Color(0.52f, 0.82f, 0.46f);
        public static readonly Color Bad = new Color(0.93f, 0.40f, 0.36f);
        public static readonly Color Warn = new Color(0.96f, 0.66f, 0.28f);
        public static readonly Color Info = new Color(0.48f, 0.72f, 0.95f);

        private static readonly Color PanelFill = new Color(0.085f, 0.095f, 0.125f, 0.94f);
        private static readonly Color PanelEdge = new Color(0.30f, 0.31f, 0.36f, 1f);
        private static readonly Color InsetFill = new Color(0.14f, 0.15f, 0.19f, 0.96f);
        private static readonly Color InsetEdge = new Color(0.24f, 0.25f, 0.30f, 1f);

        public static float Scale { get; private set; } = 1f;

        /// <summary>Largura da tela na resolucao de referencia.</summary>
        public static float Width => Screen.width / Scale;

        public static float Height => Screen.height / Scale;

        // --- Estilos ---

        public static GUIStyle Label;
        public static GUIStyle Small;
        public static GUIStyle Dim;
        public static GUIStyle Bold;
        public static GUIStyle BoldSmall;
        public static GUIStyle Heading;
        public static GUIStyle Title;
        public static GUIStyle Huge;
        public static GUIStyle Center;
        public static GUIStyle CenterBold;
        public static GUIStyle CenterSmall;
        public static GUIStyle Right;
        public static GUIStyle Number;
        public static GUIStyle Button;
        public static GUIStyle ButtonPrimary;
        public static GUIStyle ButtonDanger;
        public static GUIStyle ButtonFlat;
        public static GUIStyle ButtonFlatOn;
        public static GUIStyle ButtonLeft;
        public static GUIStyle Panel;
        public static GUIStyle Inset;
        public static GUIStyle Card;
        public static GUIStyle CardHot;
        public static GUIStyle CardOff;
        public static GUIStyle Tooltip;
        public static GUIStyle Scrollbar;

        public static Texture2D White;
        public static Texture2D Dot;
        public static Texture2D Vignette;

        private static float _builtScale = -1f;
        private static Font _font;
        private static readonly List<Texture2D> Owned = new List<Texture2D>();

        /// <summary>Recalcula a escala e refaz os estilos quando ela muda. Chamar dentro de OnGUI.</summary>
        public static void Begin()
        {
            float scale = Mathf.Min(Screen.width / ReferenceWidth, Screen.height / ReferenceHeight);
            Scale = Mathf.Clamp(scale, 0.55f, 3f);

            if (Label != null && White != null && Mathf.Approximately(_builtScale, Scale))
            {
                return;
            }

            Build();
            _builtScale = Scale;
        }

        public static float S(float value) => Mathf.Round(value * Scale);

        public static Rect R(float x, float y, float width, float height)
        {
            return new Rect(S(x), S(y), S(width), S(height));
        }

        /// <summary>Converte um retangulo de referencia em pixels.</summary>
        public static Rect R(Rect reference)
        {
            return R(reference.x, reference.y, reference.width, reference.height);
        }

        // --- Construcao ---

        private static void Build()
        {
            for (int i = 0; i < Owned.Count; i++)
            {
                if (Owned[i] != null)
                {
                    Object.Destroy(Owned[i]);
                }
            }

            Owned.Clear();

            if (_font == null)
            {
                // Fonte do sistema quando existir; a fonte padrao do Unity e a rede.
                try
                {
                    _font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Verdana", "Arial" }, 16);
                }
                catch (System.Exception)
                {
                    _font = null;
                }
            }

            White = Solid(Color.white);
            Dot = Circle(32);
            Vignette = VerticalGradient(new Color(0.05f, 0.06f, 0.085f, 0.88f), new Color(0.03f, 0.035f, 0.05f, 0.98f));

            Label = Text(15, Ink, FontStyle.Normal, TextAnchor.UpperLeft, true);
            Small = Text(13, InkDim, FontStyle.Normal, TextAnchor.UpperLeft, true);
            Dim = Text(15, InkDim, FontStyle.Normal, TextAnchor.UpperLeft, true);
            Bold = Text(15, Ink, FontStyle.Bold, TextAnchor.UpperLeft, true);
            BoldSmall = Text(13, Ink, FontStyle.Bold, TextAnchor.UpperLeft, true);
            Heading = Text(18, Gold, FontStyle.Bold, TextAnchor.MiddleLeft, false);
            Title = Text(30, Ink, FontStyle.Bold, TextAnchor.MiddleCenter, false);
            Huge = Text(64, Gold, FontStyle.Bold, TextAnchor.MiddleCenter, false);
            Center = Text(15, Ink, FontStyle.Normal, TextAnchor.MiddleCenter, true);
            CenterBold = Text(16, Ink, FontStyle.Bold, TextAnchor.MiddleCenter, true);
            CenterSmall = Text(13, InkDim, FontStyle.Normal, TextAnchor.MiddleCenter, true);
            Right = Text(15, Ink, FontStyle.Normal, TextAnchor.MiddleRight, false);
            Number = Text(19, Ink, FontStyle.Bold, TextAnchor.MiddleLeft, false);

            Panel = Boxed(Rounded(PanelFill, PanelEdge, 10), 10);
            Inset = Boxed(Rounded(InsetFill, InsetEdge, 8), 8);

            Button = ButtonStyle(
                new Color(0.20f, 0.22f, 0.28f), new Color(0.27f, 0.30f, 0.38f), new Color(0.15f, 0.16f, 0.21f),
                new Color(0.40f, 0.42f, 0.50f), Ink, 15, TextAnchor.MiddleCenter);
            ButtonLeft = ButtonStyle(
                new Color(0.20f, 0.22f, 0.28f), new Color(0.27f, 0.30f, 0.38f), new Color(0.15f, 0.16f, 0.21f),
                new Color(0.40f, 0.42f, 0.50f), Ink, 15, TextAnchor.MiddleLeft);
            ButtonPrimary = ButtonStyle(
                new Color(0.62f, 0.44f, 0.12f), new Color(0.76f, 0.56f, 0.18f), new Color(0.48f, 0.33f, 0.08f),
                new Color(0.98f, 0.82f, 0.40f), new Color(1f, 0.97f, 0.88f), 18, TextAnchor.MiddleCenter);
            ButtonPrimary.fontStyle = FontStyle.Bold;
            ButtonDanger = ButtonStyle(
                new Color(0.42f, 0.16f, 0.15f), new Color(0.56f, 0.22f, 0.20f), new Color(0.32f, 0.11f, 0.10f),
                new Color(0.80f, 0.40f, 0.36f), Ink, 15, TextAnchor.MiddleCenter);
            ButtonFlat = ButtonStyle(
                new Color(0.13f, 0.14f, 0.18f, 0.9f), new Color(0.22f, 0.24f, 0.30f), new Color(0.10f, 0.11f, 0.14f),
                new Color(0.26f, 0.27f, 0.32f), InkDim, 13, TextAnchor.MiddleCenter);

            ButtonFlatOn = ButtonStyle(
                new Color(0.62f, 0.44f, 0.12f), new Color(0.76f, 0.56f, 0.18f), new Color(0.48f, 0.33f, 0.08f),
                new Color(0.98f, 0.82f, 0.40f), new Color(1f, 0.97f, 0.88f), 13, TextAnchor.MiddleCenter);
            ButtonFlatOn.fontStyle = FontStyle.Bold;
            ButtonFlat.padding = Pad(4, 4);
            ButtonFlatOn.padding = Pad(4, 4);

            Card = Boxed(Rounded(new Color(0.17f, 0.16f, 0.14f, 0.98f), new Color(0.55f, 0.47f, 0.30f), 10, 2), 10);
            CardHot = Boxed(Rounded(new Color(0.24f, 0.21f, 0.15f, 1f), new Color(0.98f, 0.82f, 0.40f), 10, 3), 10);
            CardOff = Boxed(Rounded(new Color(0.12f, 0.12f, 0.13f, 0.96f), new Color(0.28f, 0.28f, 0.30f), 10, 2), 10);

            BuildScrollbar();

            Tooltip = Boxed(Rounded(new Color(0.04f, 0.05f, 0.07f, 0.98f), new Color(0.62f, 0.54f, 0.34f), 6), 6);
            Tooltip.normal.textColor = Ink;
            Tooltip.wordWrap = true;
            Tooltip.fontSize = Mathf.RoundToInt(14 * Scale);
            Tooltip.font = _font;
            Tooltip.padding = Pad(10, 8);
            Tooltip.richText = true;
        }

        /// <summary>
        /// Barra de rolagem fina no tom do HUD. O IMGUI procura o polegar e os botoes
        /// pelo nome do estilo dentro da skin, entao eles sao registrados nela.
        /// </summary>
        private static void BuildScrollbar()
        {
            int width = Mathf.Max(4, Mathf.RoundToInt(8 * Scale));

            Scrollbar = new GUIStyle { name = "kcscroll", fixedWidth = width };
            Scrollbar.normal.background = Solid(new Color(0f, 0f, 0f, 0.25f));
            Scrollbar.margin = new RectOffset(2, 0, 0, 0);

            GUIStyle thumb = new GUIStyle { name = "kcscrollthumb", fixedWidth = width };
            thumb.normal.background = Solid(new Color(0.50f, 0.46f, 0.36f, 0.9f));
            thumb.hover.background = Solid(new Color(0.75f, 0.66f, 0.42f, 1f));
            thumb.active.background = thumb.hover.background;

            GUIStyle up = new GUIStyle { name = "kcscrollupbutton" };
            GUIStyle down = new GUIStyle { name = "kcscrolldownbutton" };

            GUISkin skin = GUI.skin;
            List<GUIStyle> custom = new List<GUIStyle>();
            GUIStyle[] existing = skin.customStyles ?? new GUIStyle[0];
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null && !existing[i].name.StartsWith("kcscroll"))
                {
                    custom.Add(existing[i]);
                }
            }

            custom.Add(Scrollbar);
            custom.Add(thumb);
            custom.Add(up);
            custom.Add(down);
            skin.customStyles = custom.ToArray();
        }

        private static RectOffset Pad(int horizontal, int vertical)
        {
            int h = Mathf.RoundToInt(horizontal * Scale);
            int v = Mathf.RoundToInt(vertical * Scale);
            return new RectOffset(h, h, v, v);
        }

        private static GUIStyle Text(int size, Color color, FontStyle fontStyle, TextAnchor anchor, bool wrap)
        {
            GUIStyle style = new GUIStyle
            {
                font = _font,
                fontSize = Mathf.Max(8, Mathf.RoundToInt(size * Scale)),
                fontStyle = fontStyle,
                alignment = anchor,
                wordWrap = wrap,
                richText = true,
                clipping = TextClipping.Clip
            };
            style.normal.textColor = color;
            return style;
        }

        private static GUIStyle Boxed(Texture2D texture, int border)
        {
            GUIStyle style = new GUIStyle();
            style.normal.background = texture;
            style.border = new RectOffset(border + 2, border + 2, border + 2, border + 2);
            return style;
        }

        private static GUIStyle ButtonStyle(
            Color normal, Color hover, Color pressed, Color edge, Color text, int size, TextAnchor anchor)
        {
            GUIStyle style = Boxed(Rounded(normal, edge, 7), 7);
            style.hover.background = Rounded(hover, Color.Lerp(edge, Color.white, 0.25f), 7);
            style.active.background = Rounded(pressed, edge, 7);
            style.focused.background = style.normal.background;
            style.normal.textColor = text;
            style.hover.textColor = Color.white;
            style.active.textColor = text;
            style.focused.textColor = text;
            style.font = _font;
            style.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * Scale));
            style.alignment = anchor;
            style.padding = Pad(12, 4);
            style.richText = true;
            style.wordWrap = false;
            style.clipping = TextClipping.Clip;
            return style;
        }

        // --- Texturas ---

        private static Texture2D Track(Texture2D texture)
        {
            texture.hideFlags = HideFlags.HideAndDontSave;
            Owned.Add(texture);
            return texture;
        }

        private static Texture2D Solid(Color color)
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();
            return Track(texture);
        }

        private static Texture2D VerticalGradient(Color top, Color bottom)
        {
            const int Size = 64;
            Texture2D texture = new Texture2D(1, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < Size; y++)
            {
                texture.SetPixel(0, y, Color.Lerp(bottom, top, y / (float)(Size - 1)));
            }

            texture.Apply();
            return Track(texture);
        }

        private static Texture2D Circle(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                    float alpha = Mathf.Clamp01(radius - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Track(texture);
        }

        /// <summary>Retangulo de cantos arredondados com borda, proprio para 9-slice.</summary>
        private static Texture2D Rounded(Color fill, Color edge, int radius, int edgeWidth = 1)
        {
            int size = radius * 2 + 8;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distancia ate a borda do retangulo arredondado, em pixels.
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float inside = radius - distance;

                    float alpha = Mathf.Clamp01(inside + 0.5f);
                    float edgeBlend = Mathf.Clamp01(edgeWidth + 0.5f - inside);
                    Color color = Color.Lerp(fill, edge, edgeBlend);
                    color.a *= alpha;
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return Track(texture);
        }

        // --- Desenho ---

        public static void Fill(Rect pixels, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(pixels, White);
            GUI.color = previous;
        }

        public static void DrawDot(Rect pixels, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(pixels, Dot);
            GUI.color = previous;
        }

        public static void DrawPanel(Rect pixels)
        {
            if (Event.current.type == EventType.Repaint)
            {
                Panel.Draw(pixels, false, false, false, false);
            }
        }

        public static void DrawInset(Rect pixels)
        {
            if (Event.current.type == EventType.Repaint)
            {
                Inset.Draw(pixels, false, false, false, false);
            }
        }

        public static void DrawBox(Rect pixels, GUIStyle style)
        {
            if (Event.current.type == EventType.Repaint)
            {
                style.Draw(pixels, false, false, false, false);
            }
        }

        /// <summary>Barra de progresso: fundo escuro, preenchimento na cor dada.</summary>
        public static void Bar(Rect pixels, float fraction, Color color)
        {
            Fill(pixels, new Color(0f, 0f, 0f, 0.55f));
            Rect inner = new Rect(pixels.x + 1f, pixels.y + 1f, (pixels.width - 2f) * Mathf.Clamp01(fraction), pixels.height - 2f);
            Fill(inner, color);
        }

        public static void LabelColored(Rect pixels, string text, GUIStyle style, Color color)
        {
            Color previous = style.normal.textColor;
            style.normal.textColor = color;
            GUI.Label(pixels, text, style);
            style.normal.textColor = previous;
        }

        public static Color ResourceColor(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Gold:
                    return new Color(0.98f, 0.80f, 0.28f);
                case ResourceKind.Wood:
                    return new Color(0.72f, 0.50f, 0.28f);
                case ResourceKind.Stone:
                    return new Color(0.72f, 0.75f, 0.80f);
                case ResourceKind.Food:
                    return new Color(0.56f, 0.82f, 0.36f);
                case ResourceKind.Population:
                    return new Color(0.80f, 0.66f, 0.95f);
                default:
                    return Color.white;
            }
        }

        public static string Hex(Color color)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        public static string Tint(string text, Color color)
        {
            return "<color=" + Hex(color) + ">" + text + "</color>";
        }
    }
}

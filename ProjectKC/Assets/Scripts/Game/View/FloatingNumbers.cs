using System.Collections.Generic;
using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
    /// <summary>
    /// Um número que sobe e some sobre a célula que o gerou.
    /// </summary>
    public sealed class FloatingNumber
    {
        public FloatingNumber(Vector3 world, string text, Color color, float lifetime)
        {
            World = world;
            Text = text;
            Color = color;
            Lifetime = Mathf.Max(0.1f, lifetime);
        }

        public Vector3 World { get; }

        public string Text { get; }

        public Color Color { get; }

        public float Lifetime { get; }

        public float Age { get; private set; }

        public bool Expired => Age >= Lifetime;

        /// <summary>Zero a um. Usado para subir e desvanecer.</summary>
        public float Progress => Mathf.Clamp01(Age / Lifetime);

        public void Tick(float delta) => Age += delta;
    }

    /// <summary>
    /// Mostra a mudança onde ela aconteceu. O contador no painel diz que o número
    /// mudou; o número sobre a célula diz **de onde** ele veio, que é a informação
    /// que ensina o jogador a ler o próprio reino (spec game-feel).
    /// </summary>
    public sealed class FloatingNumbers : MonoBehaviour
    {
        private readonly List<FloatingNumber> _active = new List<FloatingNumber>();

        [SerializeField] private float _lifetime = 1.6f;
        [SerializeField] private float _riseDistance = 1.1f;

        public IReadOnlyList<FloatingNumber> Active => _active;

        public static Color ColorFor(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Gold:
                    return new Color(0.95f, 0.80f, 0.30f);
                case ResourceKind.Wood:
                    return new Color(0.78f, 0.56f, 0.32f);
                case ResourceKind.Stone:
                    return new Color(0.70f, 0.72f, 0.76f);
                case ResourceKind.Food:
                    return new Color(0.55f, 0.80f, 0.38f);
                case ResourceKind.Population:
                    return new Color(0.85f, 0.72f, 0.95f);
                default:
                    return Color.white;
            }
        }

        public static string Symbol(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Gold:
                    return "ouro";
                case ResourceKind.Wood:
                    return "madeira";
                case ResourceKind.Stone:
                    return "pedra";
                case ResourceKind.Food:
                    return "comida";
                case ResourceKind.Population:
                    return "gente";
                default:
                    return string.Empty;
            }
        }

        public void Spawn(Vector3 world, ResourceKind kind, int amount)
        {
            if (amount == 0)
            {
                return;
            }

            string text = (amount > 0 ? "+" : string.Empty) + amount + " " + Symbol(kind);
            _active.Add(new FloatingNumber(world, text, ColorFor(kind), _lifetime));
        }

        public void Spawn(Vector3 world, string text, Color color)
        {
            _active.Add(new FloatingNumber(world, text, color, _lifetime));
        }

        /// <summary>
        /// Um número por recurso da célula, escalonado no tempo para que valores
        /// diferentes não se sobreponham e virem borrão.
        /// </summary>
        public void SpawnBreakdown(Vector3 world, ProductionBreakdown breakdown)
        {
            if (breakdown == null)
            {
                return;
            }

            int offset = 0;
            foreach (ResourceKind kind in breakdown.Totals.NonZero())
            {
                Vector3 stacked = world + Vector3.up * (offset * 0.28f);
                Spawn(stacked, kind, breakdown.Totals[kind]);
                offset++;
            }
        }

        public void Clear() => _active.Clear();

        private void Update()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                _active[i].Tick(Time.deltaTime);
                if (_active[i].Expired)
                {
                    _active.RemoveAt(i);
                }
            }
        }

        private void OnGUI()
        {
            // Atras dos paineis do HUD: numero do tabuleiro nao cobre botao.
            GUI.depth = 10;

            if (_active.Count == 0 || Event.current.type != EventType.Repaint)
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            UiTheme.Begin();

            Color previous = GUI.color;
            float width = UiTheme.S(220f);
            float height = UiTheme.S(26f);

            for (int i = 0; i < _active.Count; i++)
            {
                FloatingNumber number = _active[i];
                Vector3 world = number.World + Vector3.up * (_riseDistance * number.Progress);
                Vector3 screen = camera.WorldToScreenPoint(world);
                if (screen.z <= 0f)
                {
                    continue;
                }

                // Some so no fim: o numero precisa ficar legivel enquanto sobe.
                float alpha = Mathf.Clamp01((1f - number.Progress) * 2.5f);
                Rect rect = new Rect(screen.x - width * 0.5f, Screen.height - screen.y - height * 0.5f, width, height);

                GUI.color = new Color(0f, 0f, 0f, alpha * 0.85f);
                GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), number.Text, ShadowStyle());

                Color color = number.Color;
                color.a = alpha;
                GUI.color = Color.white;
                UiTheme.LabelColored(rect, number.Text, UiTheme.CenterBold, color);
            }

            GUI.color = previous;
        }

        private static GUIStyle _shadow;
        private static int _shadowSize;

        /// <summary>Mesma fonte do numero, em preto, sem rich text: a sombra nao herda cor de tag.</summary>
        private static GUIStyle ShadowStyle()
        {
            if (_shadow == null || _shadowSize != UiTheme.CenterBold.fontSize)
            {
                _shadow = new GUIStyle(UiTheme.CenterBold) { richText = false };
                _shadow.normal.textColor = Color.white;
                _shadowSize = UiTheme.CenterBold.fontSize;
            }

            return _shadow;
        }
    }
}

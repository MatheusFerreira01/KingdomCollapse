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

        [SerializeField] private float _lifetime = 1.1f;
        [SerializeField] private float _riseDistance = 0.9f;

        public IReadOnlyList<FloatingNumber> Active => _active;

        public static Color ColorFor(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Gold:
                    return new Color(0.95f, 0.80f, 0.30f);
                case ResourceKind.Wood:
                    return new Color(0.55f, 0.40f, 0.24f);
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
                    return "mad";
                case ResourceKind.Stone:
                    return "ped";
                case ResourceKind.Food:
                    return "com";
                case ResourceKind.Population:
                    return "pop";
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
            if (_active.Count == 0)
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            Color previous = GUI.color;

            for (int i = 0; i < _active.Count; i++)
            {
                FloatingNumber number = _active[i];
                Vector3 world = number.World + Vector3.up * (_riseDistance * number.Progress);
                Vector3 screen = camera.WorldToScreenPoint(world);

                if (screen.z <= 0f)
                {
                    continue;
                }

                Color color = number.Color;
                color.a = 1f - number.Progress;
                GUI.color = color;

                GUI.Label(new Rect(screen.x - 40f, Screen.height - screen.y - 12f, 80f, 22f), number.Text);
            }

            GUI.color = previous;
        }
    }
}

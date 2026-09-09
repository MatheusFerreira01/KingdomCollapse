using System.Collections.Generic;
using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
    /// <summary>Quanto tempo a encenação leva.</summary>
    public enum StagingSpeed
    {
        Normal = 0,
        Fast = 1,

        /// <summary>Sem encenação: o resultado aparece de uma vez.</summary>
        Skip = 2
    }

    /// <summary>
    /// Toca a resolução do dia passo a passo. Não decide nada: recebe os passos que o
    /// núcleo já resolveu e apenas controla o tempo entre eles.
    ///
    /// Acelerar e pular são requisitos, não conveniências: uma encenação que não pode
    /// ser pulada vira irritação na décima repetição, e o jogo tem trinta dias.
    /// </summary>
    public sealed class DayStaging
    {
        private readonly List<DayStep> _pending = new List<DayStep>();
        private float _timer;

        public DayStaging(StagingSpeed speed = StagingSpeed.Normal)
        {
            Speed = speed;
        }

        /// <summary>Preferência do jogador. Persiste entre dias.</summary>
        public StagingSpeed Speed { get; set; }

        public DayStep Current { get; private set; }

        public bool IsPlaying => Current != null || _pending.Count > 0;

        /// <summary>Passos já mostrados neste dia, para consulta depois da cena.</summary>
        public List<DayStep> Shown { get; } = new List<DayStep>();

        public void Begin(List<DayStep> steps)
        {
            _pending.Clear();
            Shown.Clear();
            Current = null;
            _timer = 0f;

            if (steps == null)
            {
                return;
            }

            if (Speed == StagingSpeed.Skip)
            {
                // Pular não pode perder informação: os passos continuam consultáveis,
                // só não são exibidos um a um.
                Shown.AddRange(steps);
                return;
            }

            _pending.AddRange(steps);
            Advance();
        }

        /// <summary>Corta a encenação em andamento, preservando o que faltava mostrar.</summary>
        public void SkipRest()
        {
            if (Current != null)
            {
                Current = null;
            }

            Shown.AddRange(_pending);
            _pending.Clear();
        }

        public void Tick(float delta)
        {
            if (Current == null)
            {
                return;
            }

            _timer -= delta;
            if (_timer <= 0f)
            {
                Advance();
            }
        }

        private void Advance()
        {
            if (Current != null)
            {
                Shown.Add(Current);
            }

            if (_pending.Count == 0)
            {
                Current = null;
                return;
            }

            Current = _pending[0];
            _pending.RemoveAt(0);
            _timer = DurationFor(Current);
        }

        private float DurationFor(DayStep step)
        {
            float baseTime = step.IsHighlight ? 1.15f : 0.55f;
            return Speed == StagingSpeed.Fast ? baseTime * 0.4f : baseTime;
        }

        public static StagingSpeed Cycle(StagingSpeed speed)
        {
            switch (speed)
            {
                case StagingSpeed.Normal:
                    return StagingSpeed.Fast;
                case StagingSpeed.Fast:
                    return StagingSpeed.Skip;
                default:
                    return StagingSpeed.Normal;
            }
        }

        public static string Label(StagingSpeed speed)
        {
            switch (speed)
            {
                case StagingSpeed.Fast:
                    return "rapido";
                case StagingSpeed.Skip:
                    return "sem encenacao";
                default:
                    return "normal";
            }
        }
    }
}

using System.Collections.Generic;
using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
    public enum Sfx
    {
        Click,
        Buy,
        Build,
        Card,
        Deny,
        DayEnd,
        Repel,
        Breach,
        Victory,
        Collapse
    }

    /// <summary>
    /// Todo o audio do jogo, gerado em codigo: efeitos e a musica ambiente. Nao ha
    /// arquivo de audio no projeto. Cada som e montado somando notas (senoides com
    /// decaimento) e rajadas de ruido filtrado, que e o que da madeira, pedra,
    /// folha e agua sem precisar de amostra gravada.
    /// </summary>
    public static class UiSound
    {
        private const int Rate = 44100;
        private const int MusicRate = 22050;
        private const string MutedKey = "kc_muted";
        private const string MusicKey = "kc_music_off";

        private static AudioSource _source;
        private static AudioSource _music;
        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();

        public static bool Muted
        {
            get => PlayerPrefs.GetInt(MutedKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(MutedKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static bool MusicOff
        {
            get => PlayerPrefs.GetInt(MusicKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(MusicKey, value ? 1 : 0);
                PlayerPrefs.Save();
                ApplyMusic();
            }
        }

        public static void Init(GameObject host)
        {
            AudioSource[] existing = host.GetComponents<AudioSource>();
            _source = existing.Length > 0 ? existing[0] : host.AddComponent<AudioSource>();
            _music = existing.Length > 1 ? existing[1] : host.AddComponent<AudioSource>();

            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _music.playOnAwake = false;
            _music.spatialBlend = 0f;
            _music.loop = true;
            _music.volume = 0.10f;
            Clips.Clear();

            if (Object.FindAnyObjectByType<AudioListener>() == null && Camera.main != null)
            {
                Camera.main.gameObject.AddComponent<AudioListener>();
            }

            ApplyMusic();
        }

        private static void ApplyMusic()
        {
            if (_music == null)
            {
                return;
            }

            if (MusicOff)
            {
                _music.Stop();
                return;
            }

            if (_music.clip == null)
            {
                _music.clip = BuildMusic();
            }

            if (!_music.isPlaying)
            {
                _music.Play();
            }
        }

        // --- Disparo ---

        public static void Play(Sfx sfx) => PlayKey("sfx_" + sfx, () => BuildSfx(sfx), 0.26f);

        /// <summary>Som da obra, pelo material do edificio.</summary>
        public static void PlayBuild(string buildingId)
        {
            switch (buildingId)
            {
                case StarterContent.Quarry:
                case StarterContent.Wall:
                case StarterContent.Watchtower:
                case StarterContent.Barracks:
                    PlayKey("build_stone", BuildStone, 0.32f);
                    break;
                case StarterContent.Farm:
                    PlayKey("build_field", BuildField, 0.32f);
                    break;
                case StarterContent.Docks:
                    PlayKey("build_docks", BuildDocks, 0.32f);
                    break;
                default:
                    PlayKey("build_wood", BuildWood, 0.32f);
                    break;
            }
        }

        /// <summary>Som da terra comprada: cada terreno soa como o que e.</summary>
        public static void PlayTerrain(TerrainType terrain)
        {
            PlayKey("terrain_" + terrain, () => BuildTerrain(terrain), 0.32f);
        }

        /// <summary>Som de quando um evento do dia aparece.</summary>
        public static void PlayEvent(EventClass eventClass, bool encounter)
        {
            string key = encounter ? "event_encounter" : "event_" + eventClass;
            PlayKey(key, () => BuildEvent(eventClass, encounter), 0.30f);
        }

        private static void PlayKey(string key, System.Func<float[]> build, float volume)
        {
            if (_source == null || Muted)
            {
                return;
            }

            if (!Clips.TryGetValue(key, out AudioClip clip) || clip == null)
            {
                float[] data = build();
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = Mathf.Clamp(data[i], -1f, 1f);
                }

                clip = AudioClip.Create("kc_" + key, data.Length, 1, Rate, false);
                clip.SetData(data, 0);
                clip.hideFlags = HideFlags.DontSave;
                Clips[key] = clip;
            }

            _source.PlayOneShot(clip, volume);
        }

        // --- Sintese ---

        private static float[] Buffer(float seconds) => new float[Mathf.RoundToInt(seconds * Rate)];

        /// <summary>Nota com decaimento exponencial; glide desliza a frequencia ate o fim.</summary>
        private static void Tone(float[] data, float at, float frequency, float seconds, float volume, float decay,
            float glideTo = 0f)
        {
            int start = Mathf.RoundToInt(at * Rate);
            int length = Mathf.RoundToInt(seconds * Rate);
            float phase = 0f;

            for (int i = 0; i < length && start + i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float f = glideTo > 0f ? Mathf.Lerp(frequency, glideTo, i / (float)length) : frequency;
                phase += 2f * Mathf.PI * f / Rate;
                float envelope = Mathf.Exp(-t * decay) * Mathf.Clamp01(i / 80f);
                data[start + i] += (Mathf.Sin(phase) + 0.3f * Mathf.Sin(2f * phase)) * envelope * volume;
            }
        }

        /// <summary>Ruido filtrado. smooth perto de 1 fica grave e macio; perto de 0, seco e claro.</summary>
        private static void Noise(float[] data, float at, float seconds, float volume, float decay, float smooth, int seed)
        {
            int start = Mathf.RoundToInt(at * Rate);
            int length = Mathf.RoundToInt(seconds * Rate);
            System.Random random = new System.Random(seed);
            float last = 0f;

            for (int i = 0; i < length && start + i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                last = last * smooth + white * (1f - smooth);
                float envelope = Mathf.Exp(-t * decay) * Mathf.Clamp01(i / 40f);
                data[start + i] += last * envelope * volume * (1f + smooth * 2.5f);
            }
        }

        private static float[] BuildSfx(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Click:
                    return Sequence(0.05f, 0.30f, 880f);
                case Sfx.Buy:
                    return Sequence(0.08f, 0.45f, 523f, 784f);
                case Sfx.Build:
                    return BuildWood();
                case Sfx.Card:
                    return Sequence(0.06f, 0.40f, 659f, 988f);
                case Sfx.Deny:
                    return Sequence(0.12f, 0.45f, 185f, 156f);
                case Sfx.DayEnd:
                    return Sequence(0.16f, 0.35f, 440f, 330f);
                case Sfx.Repel:
                    return Sequence(0.12f, 0.55f, 523f, 659f, 784f);
                case Sfx.Breach:
                    return Sequence(0.16f, 0.60f, 220f, 165f, 110f);
                case Sfx.Victory:
                    return Sequence(0.18f, 0.55f, 523f, 659f, 784f, 1047f, 1319f);
                default:
                    return Sequence(0.26f, 0.60f, 196f, 147f, 110f, 82f);
            }
        }

        private static float[] Sequence(float noteSeconds, float volume, params float[] frequencies)
        {
            float[] data = Buffer(noteSeconds * frequencies.Length + 0.25f);
            for (int n = 0; n < frequencies.Length; n++)
            {
                Tone(data, n * noteSeconds, frequencies[n], noteSeconds + 0.22f, volume * 0.6f, 16f);
            }

            return data;
        }

        // Madeira: tres marteladas secas, a ultima mais funda.
        private static float[] BuildWood()
        {
            float[] data = Buffer(0.62f);
            for (int i = 0; i < 3; i++)
            {
                float at = i * 0.15f;
                Noise(data, at, 0.07f, 0.55f, 55f, 0.35f, 11 + i);
                Tone(data, at, i == 2 ? 150f : 210f, 0.12f, 0.45f, 38f);
            }

            return data;
        }

        // Pedra: dois toques de cinzel e o bloco assentando.
        private static float[] BuildStone()
        {
            float[] data = Buffer(0.75f);
            for (int i = 0; i < 2; i++)
            {
                float at = i * 0.16f;
                Tone(data, at, 1650f + i * 240f, 0.10f, 0.30f, 55f);
                Tone(data, at, 2480f, 0.07f, 0.16f, 70f);
                Noise(data, at, 0.04f, 0.30f, 90f, 0.10f, 21 + i);
            }

            Tone(data, 0.36f, 82f, 0.32f, 0.70f, 14f);
            Noise(data, 0.36f, 0.22f, 0.35f, 20f, 0.92f, 23);
            return data;
        }

        // Campo: a enxada na terra e a palha.
        private static float[] BuildField()
        {
            float[] data = Buffer(0.70f);
            Noise(data, 0f, 0.20f, 0.40f, 14f, 0.80f, 31);
            Tone(data, 0.02f, 130f, 0.12f, 0.45f, 30f);
            Noise(data, 0.24f, 0.20f, 0.40f, 14f, 0.80f, 32);
            Tone(data, 0.26f, 120f, 0.12f, 0.45f, 30f);
            Noise(data, 0.44f, 0.24f, 0.25f, 12f, 0.55f, 33);
            return data;
        }

        // Ancoradouro: madeira molhada e agua batendo.
        private static float[] BuildDocks()
        {
            float[] data = Buffer(0.80f);
            Noise(data, 0f, 0.07f, 0.50f, 55f, 0.40f, 41);
            Tone(data, 0f, 170f, 0.14f, 0.45f, 30f);
            Noise(data, 0.18f, 0.07f, 0.50f, 55f, 0.40f, 42);
            Tone(data, 0.18f, 150f, 0.14f, 0.45f, 30f);
            Tone(data, 0.36f, 520f, 0.16f, 0.30f, 16f, 300f);
            Noise(data, 0.36f, 0.40f, 0.28f, 7f, 0.93f, 43);
            return data;
        }

        private static float[] BuildTerrain(TerrainType terrain)
        {
            float[] data = Buffer(0.9f);

            switch (terrain)
            {
                case TerrainType.Forest:
                    // Folhagem, e dois troncos estalando.
                    Noise(data, 0f, 0.60f, 0.40f, 5f, 0.45f, 51);
                    Noise(data, 0.12f, 0.40f, 0.25f, 8f, 0.20f, 52);
                    Tone(data, 0.20f, 196f, 0.14f, 0.35f, 26f);
                    Tone(data, 0.42f, 165f, 0.16f, 0.35f, 22f);
                    break;

                case TerrainType.Mine:
                    // Pedra rolando e um veio que tine.
                    Noise(data, 0f, 0.45f, 0.45f, 9f, 0.94f, 61);
                    Tone(data, 0.10f, 1870f, 0.14f, 0.28f, 40f);
                    Tone(data, 0.26f, 2350f, 0.16f, 0.24f, 34f);
                    Tone(data, 0.10f, 74f, 0.40f, 0.50f, 9f);
                    break;

                case TerrainType.River:
                    // Tres gotas e a corrente por baixo.
                    Noise(data, 0f, 0.80f, 0.22f, 3f, 0.90f, 71);
                    Tone(data, 0.02f, 420f, 0.14f, 0.40f, 18f, 760f);
                    Tone(data, 0.20f, 520f, 0.14f, 0.36f, 18f, 900f);
                    Tone(data, 0.40f, 360f, 0.18f, 0.36f, 14f, 640f);
                    break;

                case TerrainType.Ruin:
                    // Oco e antigo: uma quinta grave que demora a morrer.
                    Tone(data, 0f, 98f, 0.85f, 0.55f, 3.2f);
                    Tone(data, 0.08f, 147f, 0.75f, 0.35f, 3.6f);
                    Tone(data, 0.30f, 233f, 0.50f, 0.16f, 5f);
                    Noise(data, 0f, 0.30f, 0.20f, 12f, 0.70f, 81);
                    break;

                default:
                    // Planicie: vento baixo no capim e uma nota aberta.
                    Noise(data, 0f, 0.55f, 0.30f, 5f, 0.75f, 91);
                    Tone(data, 0.05f, 392f, 0.40f, 0.32f, 7f);
                    Tone(data, 0.18f, 587f, 0.36f, 0.24f, 8f);
                    break;
            }

            return data;
        }

        private static float[] BuildEvent(EventClass eventClass, bool encounter)
        {
            float[] data = Buffer(1.3f);

            if (encounter)
            {
                // Alguem chegou: tres notas menores, devagar, com o ar em volta.
                Noise(data, 0f, 1.0f, 0.12f, 2.5f, 0.92f, 101);
                Tone(data, 0f, 330f, 0.60f, 0.38f, 4.5f);
                Tone(data, 0.28f, 392f, 0.60f, 0.36f, 4.5f);
                Tone(data, 0.56f, 466f, 0.70f, 0.36f, 3.6f);
                Tone(data, 0f, 110f, 1.10f, 0.30f, 2.6f);
                return data;
            }

            switch (eventClass)
            {
                case EventClass.Positive:
                    Tone(data, 0f, 659f, 0.50f, 0.40f, 6f);
                    Tone(data, 0.12f, 880f, 0.55f, 0.36f, 5.5f);
                    Tone(data, 0.24f, 1319f, 0.50f, 0.20f, 6f);
                    break;
                case EventClass.Negative:
                    Tone(data, 0f, 233f, 0.70f, 0.45f, 4.5f);
                    Tone(data, 0f, 220f, 0.70f, 0.40f, 4.5f);
                    Tone(data, 0.22f, 110f, 0.80f, 0.45f, 3.6f);
                    Noise(data, 0f, 0.35f, 0.25f, 9f, 0.90f, 111);
                    break;
                default:
                    Tone(data, 0f, 523f, 0.45f, 0.38f, 6.5f);
                    Tone(data, 0.16f, 392f, 0.50f, 0.34f, 6f);
                    break;
            }

            return data;
        }

        // --- Musica ambiente ---

        /// <summary>
        /// Um laco de 32 segundos: quatro acordes lentos em camadas de senoide, um
        /// baixo que respira e uma melodia pentatonica esparsa. Tudo escrito com
        /// indice circular, entao o fim emenda no comeco sem estalo.
        /// </summary>
        private static AudioClip BuildMusic()
        {
            const float ChordSeconds = 4f;
            float[][] chords =
            {
                new[] { 220.00f, 261.63f, 329.63f },   // la menor
                new[] { 174.61f, 220.00f, 261.63f },   // fa
                new[] { 130.81f, 196.00f, 261.63f },   // do
                new[] { 196.00f, 246.94f, 293.66f },   // sol
                new[] { 220.00f, 261.63f, 329.63f },
                new[] { 146.83f, 220.00f, 293.66f },   // re menor
                new[] { 174.61f, 220.00f, 261.63f },
                new[] { 164.81f, 207.65f, 246.94f }    // mi
            };

            float[] scale = { 440f, 523.25f, 587.33f, 659.25f, 783.99f, 880f };
            int total = Mathf.RoundToInt(chords.Length * ChordSeconds * MusicRate);
            int chordSamples = Mathf.RoundToInt(ChordSeconds * MusicRate);
            float[] data = new float[total];

            for (int c = 0; c < chords.Length; c++)
            {
                int start = c * chordSamples;
                int length = chordSamples + chordSamples / 2;

                for (int i = 0; i < length; i++)
                {
                    float t = i / (float)MusicRate;
                    // Entra devagar e sai por cima do acorde seguinte.
                    float envelope = Mathf.Clamp01(t / 1.4f) * Mathf.Clamp01((length - i) / (MusicRate * 2f));
                    float sample = 0f;
                    for (int n = 0; n < chords[c].Length; n++)
                    {
                        float f = chords[c][n];
                        sample += Mathf.Sin(2f * Mathf.PI * f * t) * 0.20f;
                        sample += Mathf.Sin(2f * Mathf.PI * f * 2.003f * t) * 0.05f;
                    }

                    sample += Mathf.Sin(2f * Mathf.PI * chords[c][0] * 0.5f * t) * 0.26f;
                    data[(start + i) % total] += sample * envelope * 0.5f;
                }
            }

            // Melodia: poucas notas, escolhidas por uma semente fixa.
            System.Random random = new System.Random(2026);
            float cursor = 1f;
            float end = chords.Length * ChordSeconds;
            while (cursor < end)
            {
                float f = scale[random.Next(scale.Length)];
                int start = Mathf.RoundToInt(cursor * MusicRate);
                int length = Mathf.RoundToInt(1.8f * MusicRate);
                for (int i = 0; i < length; i++)
                {
                    float t = i / (float)MusicRate;
                    float envelope = Mathf.Exp(-t * 2.4f) * Mathf.Clamp01(i / 200f);
                    data[(start + i) % total] += (Mathf.Sin(2f * Mathf.PI * f * t) + 0.25f * Mathf.Sin(4f * Mathf.PI * f * t)) *
                                                 envelope * 0.16f;
                }

                cursor += 1f + random.Next(0, 4) * 0.5f;
            }

            for (int i = 0; i < total; i++)
            {
                data[i] = Mathf.Clamp(data[i], -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("kc_music", total, 1, MusicRate, false);
            clip.SetData(data, 0);
            clip.hideFlags = HideFlags.DontSave;
            return clip;
        }
    }
}

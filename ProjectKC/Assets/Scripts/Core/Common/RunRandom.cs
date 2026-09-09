using System;
using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Canais independentes de aleatoriedade dentro de uma run (design D5). Canais
    /// separados garantem que jogar uma carta a mais nao desloque a sequencia de
    /// eventos futuros: cada sistema consome do seu proprio gerador.
    /// </summary>
    public enum RandomChannel
    {
        Terrain = 0,
        Events = 1,
        Threats = 2,
        Cards = 3,
        Content = 4
    }

    public interface IRandomSource
    {
        /// <summary>Inteiro em [minInclusive, maxExclusive).</summary>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>Double em [0,1).</summary>
        double NextDouble();
    }

    /// <summary>
    /// xorshift128 com semente explicita. Nao usa System.Random nem UnityEngine.Random
    /// porque ambos tem implementacao dependente de plataforma ou de versao: uma run
    /// reportada como bug precisa reproduzir identica em qualquer maquina.
    /// </summary>
    public sealed class XorShiftRandom : IRandomSource
    {
        private uint _x, _y, _z, _w;

        public XorShiftRandom(int seed)
        {
            unchecked
            {
                uint s = (uint)seed;
                // Sementes pequenas ou zeradas degeneram o xorshift; espalha primeiro.
                _x = Scramble(s == 0 ? 0x9E3779B9u : s);
                _y = Scramble(_x ^ 0x85EBCA6Bu);
                _z = Scramble(_y ^ 0xC2B2AE35u);
                _w = Scramble(_z ^ 0x27D4EB2Fu);
            }
        }

        private static uint Scramble(uint v)
        {
            unchecked
            {
                v ^= v >> 16;
                v *= 0x7FEB352Du;
                v ^= v >> 15;
                v *= 0x846CA68Bu;
                v ^= v >> 16;
                return v == 0 ? 0x9E3779B9u : v;
            }
        }

        private uint NextUInt()
        {
            unchecked
            {
                uint t = _x ^ (_x << 11);
                _x = _y;
                _y = _z;
                _z = _w;
                _w = _w ^ (_w >> 19) ^ t ^ (t >> 8);
                return _w;
            }
        }

        public double NextDouble()
        {
            // 53 bits de mantissa a partir de dois saques de 32 bits.
            ulong hi = NextUInt() >> 5;
            ulong lo = NextUInt() >> 6;
            return ((hi << 26) + lo) / (double)(1UL << 53);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }

            long range = (long)maxExclusive - minInclusive;
            return (int)(minInclusive + (long)(NextDouble() * range));
        }
    }

    /// <summary>
    /// Aleatoriedade de uma run: uma semente, varios canais derivados dela.
    /// </summary>
    public sealed class RunRandom
    {
        private readonly Dictionary<RandomChannel, XorShiftRandom> _channels =
            new Dictionary<RandomChannel, XorShiftRandom>();

        public RunRandom(int seed)
        {
            Seed = seed;
            foreach (RandomChannel channel in Enum.GetValues(typeof(RandomChannel)))
            {
                unchecked
                {
                    _channels[channel] = new XorShiftRandom(seed ^ (int)((uint)channel * 0x9E3779B9u));
                }
            }
        }

        public int Seed { get; }

        public IRandomSource Channel(RandomChannel channel) => _channels[channel];

        /// <summary>
        /// Valor estavel para uma coordenada, independente da ordem em que as celulas
        /// foram compradas. E o que permite a mesma semente gerar o mesmo mapa mesmo
        /// que o jogador expanda em outra direcao.
        /// </summary>
        public double StableValueAt(RandomChannel channel, Coord coord)
        {
            unchecked
            {
                uint h = (uint)Seed;
                h ^= (uint)((int)channel + 1) * 0x85EBCA6Bu;
                h = Mix(h ^ (uint)coord.X * 0xC2B2AE35u);
                h = Mix(h ^ (uint)coord.Y * 0x27D4EB2Fu);
                return (h >> 8) / (double)(1u << 24);
            }
        }

        private static uint Mix(uint v)
        {
            unchecked
            {
                v ^= v >> 16;
                v *= 0x7FEB352Du;
                v ^= v >> 15;
                v *= 0x846CA68Bu;
                v ^= v >> 16;
                return v;
            }
        }

        /// <summary>Escolhe um item por peso. Retorna -1 se nao houver peso positivo.</summary>
        public static int PickWeighted(IRandomSource source, IReadOnlyList<double> weights)
        {
            double total = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] > 0)
                {
                    total += weights[i];
                }
            }

            if (total <= 0)
            {
                return -1;
            }

            double roll = source.NextDouble() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0)
                {
                    continue;
                }

                roll -= weights[i];
                if (roll < 0)
                {
                    return i;
                }
            }

            for (int i = weights.Count - 1; i >= 0; i--)
            {
                if (weights[i] > 0)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Embaralhamento de Fisher-Yates no lugar.</summary>
        public static void Shuffle<T>(IRandomSource source, IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = source.NextInt(0, i + 1);
                T tmp = items[i];
                items[i] = items[j];
                items[j] = tmp;
            }
        }
    }
}

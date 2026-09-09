using System;
using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Posicao inteira no grid do reino. O territorio cresce em qualquer direcao a
    /// partir do Salao, entao nao existe origem nem limite pre-definido.
    /// </summary>
    public readonly struct Coord : IEquatable<Coord>
    {
        public readonly int X;
        public readonly int Y;

        public Coord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static readonly Coord Zero = new Coord(0, 0);

        /// <summary>Vizinhos ortogonais, na ordem norte, leste, sul, oeste.</summary>
        public IEnumerable<Coord> Neighbors()
        {
            yield return new Coord(X, Y + 1);
            yield return new Coord(X + 1, Y);
            yield return new Coord(X, Y - 1);
            yield return new Coord(X - 1, Y);
        }

        public bool IsAdjacentTo(Coord other)
        {
            int dx = Math.Abs(X - other.X);
            int dy = Math.Abs(Y - other.Y);
            return dx + dy == 1;
        }

        /// <summary>Distancia de Manhattan. Usada pelo limite de raio dos Anoes.</summary>
        public int ManhattanDistanceTo(Coord other)
        {
            return Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
        }

        public bool Equals(Coord other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is Coord other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }

        public static bool operator ==(Coord a, Coord b) => a.Equals(b);

        public static bool operator !=(Coord a, Coord b) => !a.Equals(b);

        public override string ToString() => "(" + X + "," + Y + ")";
    }
}

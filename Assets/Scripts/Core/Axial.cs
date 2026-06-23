using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Axial hex coordinate (q, r). Pure value type, engine-agnostic.
    /// Cube form is derived as (x = q, z = r, y = -q - r) for distance math.
    /// </summary>
    public readonly struct Axial : IEquatable<Axial>
    {
        public readonly int Q;
        public readonly int R;

        public Axial(int q, int r)
        {
            Q = q;
            R = r;
        }

        /// <summary>The six axial step directions around a hex.</summary>
        public static readonly Axial[] Directions =
        {
            new Axial(1, 0), new Axial(1, -1), new Axial(0, -1),
            new Axial(-1, 0), new Axial(-1, 1), new Axial(0, 1)
        };

        public static Axial operator +(Axial a, Axial b) => new Axial(a.Q + b.Q, a.R + b.R);
        public static Axial operator -(Axial a, Axial b) => new Axial(a.Q - b.Q, a.R - b.R);

        public IEnumerable<Axial> Neighbors()
        {
            foreach (var dir in Directions)
                yield return this + dir;
        }

        /// <summary>Hex distance via the cube-coordinate formula.</summary>
        public static int Distance(Axial a, Axial b)
        {
            int dq = a.Q - b.Q;
            int dr = a.R - b.R;
            return (Math.Abs(dq) + Math.Abs(dq + dr) + Math.Abs(dr)) / 2;
        }

        public bool Equals(Axial other) => Q == other.Q && R == other.R;
        public override bool Equals(object obj) => obj is Axial other && Equals(other);
        public override int GetHashCode() => unchecked((Q * 397) ^ R);

        public static bool operator ==(Axial a, Axial b) => a.Equals(b);
        public static bool operator !=(Axial a, Axial b) => !a.Equals(b);

        public override string ToString() => $"({Q},{R})";
    }
}

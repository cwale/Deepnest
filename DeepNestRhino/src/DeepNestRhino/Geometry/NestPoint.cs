using System;

namespace DeepNestRhino.Geometry
{
    /// <summary>
    /// A simple 2D point used throughout the nesting engine.
    /// Maps to the {x, y} objects used in the original JS code.
    /// </summary>
    public struct NestPoint : IEquatable<NestPoint>
    {
        public double X;
        public double Y;

        /// <summary>
        /// True if this point lies exactly on a segment of the original polygon
        /// (used for line merge detection).
        /// </summary>
        public bool Exact;

        /// <summary>
        /// Marked flag used during NFP traversal to avoid revisiting points.
        /// </summary>
        public bool Marked;

        public NestPoint(double x, double y)
        {
            X = x;
            Y = y;
            Exact = false;
            Marked = false;
        }

        public NestPoint(double x, double y, bool exact)
        {
            X = x;
            Y = y;
            Exact = exact;
            Marked = false;
        }

        public bool Equals(NestPoint other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is NestPoint other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public override string ToString()
        {
            return $"({X:F6}, {Y:F6})";
        }

        public static bool operator ==(NestPoint left, NestPoint right) => left.Equals(right);
        public static bool operator !=(NestPoint left, NestPoint right) => !left.Equals(right);
    }
}

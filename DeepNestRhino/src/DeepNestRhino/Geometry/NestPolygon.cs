using System;
using System.Collections.Generic;

namespace DeepNestRhino.Geometry
{
    /// <summary>
    /// A polygon with metadata, used throughout the nesting engine.
    /// In the original JS code, polygons are arrays with attached properties
    /// (e.g. polygon.id, polygon.source, polygon.children). This class
    /// models that pattern properly in C#.
    /// </summary>
    public class NestPolygon
    {
        /// <summary>
        /// The ordered vertices of the polygon (no closing duplicate).
        /// </summary>
        public List<NestPoint> Points { get; set; } = new List<NestPoint>();

        /// <summary>
        /// Child polygons (holes for outer polygons, or nested parts).
        /// Odd-depth children are holes, even-depth children are parts.
        /// </summary>
        public List<NestPolygon> Children { get; set; }

        /// <summary>
        /// Unique instance ID — distinguishes cloned duplicates of the same part.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Index into the original parts list — identifies which unique part design this is.
        /// </summary>
        public int Source { get; set; } = -1;

        /// <summary>
        /// Current rotation angle in degrees.
        /// </summary>
        public double Rotation { get; set; }

        /// <summary>
        /// Placement offset X (set after placement).
        /// </summary>
        public double OffsetX { get; set; }

        /// <summary>
        /// Placement offset Y (set after placement).
        /// </summary>
        public double OffsetY { get; set; }

        /// <summary>
        /// Number of points in the polygon.
        /// </summary>
        public int Length => Points.Count;

        /// <summary>
        /// Access a point by index.
        /// </summary>
        public NestPoint this[int index]
        {
            get => Points[index];
            set => Points[index] = value;
        }

        /// <summary>
        /// Create a deep clone of this polygon and its children.
        /// </summary>
        public NestPolygon DeepClone()
        {
            var clone = new NestPolygon
            {
                Id = Id,
                Source = Source,
                Rotation = Rotation,
                OffsetX = OffsetX,
                OffsetY = OffsetY,
            };

            foreach (var p in Points)
            {
                clone.Points.Add(new NestPoint(p.X, p.Y, p.Exact));
            }

            if (Children != null && Children.Count > 0)
            {
                clone.Children = new List<NestPolygon>();
                foreach (var child in Children)
                {
                    clone.Children.Add(child.DeepClone());
                }
            }

            return clone;
        }

        /// <summary>
        /// Replace all points in this polygon with points from another list.
        /// Preserves metadata. Equivalent to the JS pattern:
        /// Array.prototype.splice.apply(t, [0, t.length].concat(newPoints))
        /// </summary>
        public void ReplacePoints(List<NestPoint> newPoints)
        {
            Points.Clear();
            Points.AddRange(newPoints);
        }
    }
}

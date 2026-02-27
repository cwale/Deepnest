using System.Collections.Generic;
using DeepNestRhino.Geometry;

namespace DeepNestRhino.Algorithm
{
    /// <summary>
    /// A single individual in the GA population.
    /// Represents one possible ordering and rotation of parts.
    /// Maps to the population member objects in deepnest.js (line 1340).
    /// </summary>
    public class Individual
    {
        /// <summary>
        /// Ordered list of parts to place (the insertion order).
        /// </summary>
        public List<NestPolygon> Placement { get; set; }

        /// <summary>
        /// Rotation angle for each part (parallel to Placement list).
        /// </summary>
        public List<double> Rotation { get; set; }

        /// <summary>
        /// Fitness score after evaluation. Null if not yet evaluated.
        /// Lower is better.
        /// </summary>
        public double? Fitness { get; set; }

        /// <summary>
        /// True if this individual is currently being evaluated.
        /// </summary>
        public bool Processing { get; set; }
    }
}

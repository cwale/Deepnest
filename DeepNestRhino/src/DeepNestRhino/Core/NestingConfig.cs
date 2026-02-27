namespace DeepNestRhino.Core
{
    /// <summary>
    /// Configuration for the nesting engine.
    /// Maps to the config object in deepnest.js (lines 20-33).
    /// </summary>
    public class NestingConfig
    {
        /// <summary>
        /// Scale factor for Clipper integer conversion. Default 10,000,000.
        /// </summary>
        public double ClipperScale { get; set; } = 10000000;

        /// <summary>
        /// Tolerance for curve-to-polyline approximation.
        /// </summary>
        public double CurveTolerance { get; set; } = 0.3;

        /// <summary>
        /// Spacing between parts (half applied to each side).
        /// </summary>
        public double Spacing { get; set; }

        /// <summary>
        /// Number of discrete rotation angles to try. 4 = 0/90/180/270.
        /// </summary>
        public int Rotations { get; set; } = 4;

        /// <summary>
        /// GA population size.
        /// </summary>
        public int PopulationSize { get; set; } = 10;

        /// <summary>
        /// GA mutation rate as a percentage (0-100).
        /// </summary>
        public int MutationRate { get; set; } = 10;

        /// <summary>
        /// Maximum parallel threads for NFP computation.
        /// </summary>
        public int Threads { get; set; } = 4;

        /// <summary>
        /// Placement strategy: "gravity", "box", or "convexhull".
        /// </summary>
        public string PlacementType { get; set; } = "gravity";

        /// <summary>
        /// Whether to optimize for shared cut lines between parts.
        /// </summary>
        public bool MergeLines { get; set; } = true;

        /// <summary>
        /// Weight for material-vs-time optimization (0 = pure material, 1 = pure time).
        /// </summary>
        public double TimeRatio { get; set; } = 0.5;

        /// <summary>
        /// Pixels per unit for SVG scaling (used in original; kept for compatibility).
        /// </summary>
        public double Scale { get; set; } = 72;

        /// <summary>
        /// Whether to use convex hull simplification for parts.
        /// </summary>
        public bool Simplify { get; set; }
    }
}

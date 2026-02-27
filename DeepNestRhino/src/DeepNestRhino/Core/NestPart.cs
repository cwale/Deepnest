using System;
using DeepNestRhino.Geometry;

namespace DeepNestRhino.Core
{
    /// <summary>
    /// Represents a part to be nested, with its polygon tree and metadata.
    /// Maps to the part objects in deepnest.js.
    /// </summary>
    public class NestPart
    {
        /// <summary>
        /// The polygon tree for this part. The root polygon is the outer boundary;
        /// children are holes, children of holes are nested parts, etc.
        /// </summary>
        public NestPolygon PolygonTree { get; set; }

        /// <summary>
        /// Number of copies to nest.
        /// </summary>
        public int Quantity { get; set; } = 1;

        /// <summary>
        /// True if this part represents a sheet/bin rather than a part to place.
        /// </summary>
        public bool IsSheet { get; set; }

        /// <summary>
        /// Reference back to the original Rhino document object for placement.
        /// </summary>
        public Guid RhinoObjectId { get; set; }

        /// <summary>
        /// Bounding box area (width * height), used for sorting.
        /// </summary>
        public double Area { get; set; }
    }
}

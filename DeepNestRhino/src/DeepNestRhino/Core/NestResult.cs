using System.Collections.Generic;

namespace DeepNestRhino.Core
{
    /// <summary>
    /// Result of a nesting evaluation (one individual's placement).
    /// Maps to the placement result returned from background.js placeParts().
    /// </summary>
    public class NestResult
    {
        /// <summary>
        /// Fitness score (lower is better).
        /// </summary>
        public double Fitness { get; set; }

        /// <summary>
        /// Total sheet area used.
        /// </summary>
        public double Area { get; set; }

        /// <summary>
        /// Total length of merged lines.
        /// </summary>
        public double MergedLength { get; set; }

        /// <summary>
        /// Per-sheet placement data.
        /// </summary>
        public List<SheetPlacement> Placements { get; set; } = new List<SheetPlacement>();

        /// <summary>
        /// Index of the GA individual that produced this result.
        /// </summary>
        public int Index { get; set; }
    }

    /// <summary>
    /// Placement data for a single sheet.
    /// </summary>
    public class SheetPlacement
    {
        /// <summary>
        /// Source index of the sheet part.
        /// </summary>
        public int SheetSource { get; set; }

        /// <summary>
        /// Unique sheet instance ID.
        /// </summary>
        public int SheetId { get; set; }

        /// <summary>
        /// Parts placed on this sheet.
        /// </summary>
        public List<PartPlacement> SheetPlacements { get; set; } = new List<PartPlacement>();
    }

    /// <summary>
    /// Placement of a single part on a sheet.
    /// Maps to the placement objects in background.js (x, y, id, source, rotation).
    /// </summary>
    public class PartPlacement
    {
        public double X { get; set; }
        public double Y { get; set; }
        public int Id { get; set; }
        public int Source { get; set; }
        public double Rotation { get; set; }
        public double MergedLength { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using Rhino.Display;
using Rhino.Geometry;
using DeepNestRhino.Conversion;
using DeepNestRhino.Core;

namespace DeepNestRhino.UI
{
    /// <summary>
    /// Custom DisplayConduit for live viewport preview of nesting results.
    /// Draws sheet outlines, placed parts, and holes with distinct colors.
    /// Updated via thread-safe reference swap when a new best result is found.
    /// </summary>
    public class NestingDisplayConduit : DisplayConduit
    {
        private static NestingDisplayConduit _instance;
        public static NestingDisplayConduit Instance => _instance;

        // Thread-safe snapshot of drawable geometry
        private volatile DrawableSnapshot _snapshot;

        private static readonly Color SheetColor = Color.FromArgb(80, 120, 200);
        private static readonly Color PartColor = Color.FromArgb(60, 200, 80);
        private static readonly Color HoleColor = Color.FromArgb(200, 60, 60);

        public NestingDisplayConduit()
        {
            _instance = this;
        }

        /// <summary>
        /// Update the display with a new nesting result.
        /// Called from the engine when a new best placement is found.
        /// Thread-safe via Interlocked.Exchange.
        /// </summary>
        public void UpdateResult(NestResult result, List<NestPart> allParts)
        {
            if (result?.Placements == null)
                return;

            var snapshot = new DrawableSnapshot();

            // Build sheet polylines
            foreach (var part in allParts)
            {
                if (!part.IsSheet) continue;
                var curve = RhinoGeometryConverter.NestPolygonToRhinoCurve(part.PolygonTree);
                if (curve != null)
                    snapshot.Sheets.Add(curve);
            }

            // Build placed part polylines
            foreach (var sheetPlacement in result.Placements)
            {
                foreach (var partPlacement in sheetPlacement.SheetPlacements)
                {
                    // Find the original part by source
                    NestPart originalPart = null;
                    foreach (var part in allParts)
                    {
                        if (!part.IsSheet && part.PolygonTree.Source == partPlacement.Source)
                        {
                            originalPart = part;
                            break;
                        }
                    }

                    if (originalPart == null)
                        continue;

                    // Build the placed polygon outline
                    var placedPolygon = Geometry.GeometryUtil.RotatePolygon(
                        originalPart.PolygonTree, partPlacement.Rotation);

                    // Translate points
                    var translatedPoints = new List<Geometry.NestPoint>();
                    foreach (var pt in placedPolygon.Points)
                    {
                        translatedPoints.Add(new Geometry.NestPoint(
                            pt.X + partPlacement.X, pt.Y + partPlacement.Y));
                    }
                    placedPolygon.ReplacePoints(translatedPoints);

                    var partCurve = RhinoGeometryConverter.NestPolygonToRhinoCurve(placedPolygon);
                    if (partCurve != null)
                        snapshot.Parts.Add(partCurve);

                    // Draw holes
                    if (originalPart.PolygonTree.Children != null)
                    {
                        foreach (var child in originalPart.PolygonTree.Children)
                        {
                            var rotatedChild = Geometry.GeometryUtil.RotatePolygon(
                                child, partPlacement.Rotation);

                            var translatedChildPoints = new List<Geometry.NestPoint>();
                            foreach (var pt in rotatedChild.Points)
                            {
                                translatedChildPoints.Add(new Geometry.NestPoint(
                                    pt.X + partPlacement.X, pt.Y + partPlacement.Y));
                            }
                            rotatedChild.ReplacePoints(translatedChildPoints);

                            var holeCurve = RhinoGeometryConverter.NestPolygonToRhinoCurve(rotatedChild);
                            if (holeCurve != null)
                                snapshot.Holes.Add(holeCurve);
                        }
                    }
                }
            }

            Interlocked.Exchange(ref _snapshot, snapshot);
        }

        protected override void DrawOverlay(DrawEventArgs e)
        {
            var snap = _snapshot;
            if (snap == null) return;

            // Draw sheets
            foreach (var sheet in snap.Sheets)
            {
                e.Display.DrawCurve(sheet, SheetColor, 2);
            }

            // Draw placed parts
            foreach (var part in snap.Parts)
            {
                e.Display.DrawCurve(part, PartColor, 2);
            }

            // Draw holes
            foreach (var hole in snap.Holes)
            {
                e.Display.DrawCurve(hole, HoleColor, 1);
            }
        }

        private class DrawableSnapshot
        {
            public List<PolylineCurve> Sheets { get; } = new();
            public List<PolylineCurve> Parts { get; } = new();
            public List<PolylineCurve> Holes { get; } = new();
        }
    }
}

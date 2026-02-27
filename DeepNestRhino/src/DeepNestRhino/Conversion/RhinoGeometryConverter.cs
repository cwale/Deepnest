using System;
using System.Collections.Generic;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using DeepNestRhino.Core;
using DeepNestRhino.Geometry;

namespace DeepNestRhino.Conversion
{
    /// <summary>
    /// Converts between Rhino geometry (Curve, Point3d) and internal nesting types
    /// (NestPolygon, NestPoint). Replaces the SVG parser from the original Deepnest.
    /// </summary>
    public static class RhinoGeometryConverter
    {
        /// <summary>
        /// Convert a Rhino closed curve to a NestPolygon.
        /// Uses Curve.ToPolyline() for tessellation, handling all curve types natively.
        /// </summary>
        public static NestPolygon CurveToNestPolygon(Curve curve, double tolerance)
        {
            if (curve == null)
                throw new ArgumentNullException(nameof(curve));

            if (!curve.IsClosed)
                throw new ArgumentException("Curve must be closed for nesting.", nameof(curve));

            // Tessellate using RhinoCommon — handles lines, arcs, NURBS, polycurves, etc.
            var polyline = curve.ToPolyline(
                tolerance,       // tolerance
                tolerance * 0.1, // angleTolerance
                0,               // minimumLength
                0                // maximumLength (0 = no limit)
            );

            if (polyline == null || !polyline.IsValid)
                return null;

            var poly = polyline.ToPolyline();
            if (poly == null || poly.Count < 3)
                return null;

            var polygon = new NestPolygon();

            // Extract points, skip the closing duplicate (last == first for closed polylines)
            int count = poly.IsClosed ? poly.Count - 1 : poly.Count;
            for (int i = 0; i < count; i++)
            {
                // Rhino is Y-up, nesting engine expects Y-down (SVG convention).
                // We negate Y at the conversion boundary.
                polygon.Points.Add(new NestPoint(poly[i].X, -poly[i].Y));
            }

            // Ensure CCW winding (negative area in our convention = outer boundary)
            double area = GeometryUtil.PolygonArea(polygon.Points);
            if (area > 0)
            {
                polygon.Points.Reverse();
            }

            return polygon;
        }

        /// <summary>
        /// Convert multiple selected Rhino curves into NestPart objects.
        /// Handles parent/child (part/hole) detection via PolygonTreeBuilder.
        /// </summary>
        public static List<NestPart> CurvesToNestParts(
            IEnumerable<ObjRef> objRefs, double tolerance, double clipperScale)
        {
            var polygons = new List<NestPolygon>();
            var objectIds = new List<Guid>();

            int source = 0;
            foreach (var objRef in objRefs)
            {
                var curve = objRef.Curve();
                if (curve == null || !curve.IsClosed)
                    continue;

                var polygon = CurveToNestPolygon(curve, tolerance);
                if (polygon == null || polygon.Points.Count < 3)
                    continue;

                double polyArea = Math.Abs(GeometryUtil.PolygonArea(polygon.Points));
                if (polyArea < tolerance * tolerance)
                    continue;

                polygon.Source = source;
                polygons.Add(polygon);
                objectIds.Add(objRef.ObjectId);
                source++;
            }

            // Build parent/child tree (detect holes)
            var roots = PolygonTreeBuilder.BuildTree(polygons, clipperScale);

            // Create NestPart objects from root polygons
            var parts = new List<NestPart>();
            for (int i = 0; i < roots.Count; i++)
            {
                var root = roots[i];
                var bounds = GeometryUtil.GetPolygonBounds(root.Points);

                var part = new NestPart
                {
                    PolygonTree = root,
                    Quantity = 1,
                    IsSheet = false,
                    Area = bounds.HasValue ? bounds.Value.Width * bounds.Value.Height : 0
                };

                // Map back to the Rhino object ID using the source index
                if (root.Source >= 0 && root.Source < objectIds.Count)
                    part.RhinoObjectId = objectIds[root.Source];

                parts.Add(part);
            }

            return parts;
        }

        /// <summary>
        /// Create a rectangular sheet NestPart from dimensions.
        /// </summary>
        public static NestPart CreateRectangleSheet(double width, double height)
        {
            var polygon = new NestPolygon();
            // CCW rectangle (negative area in our convention)
            polygon.Points.Add(new NestPoint(0, 0));
            polygon.Points.Add(new NestPoint(width, 0));
            polygon.Points.Add(new NestPoint(width, height));
            polygon.Points.Add(new NestPoint(0, height));

            double area = GeometryUtil.PolygonArea(polygon.Points);
            if (area > 0)
                polygon.Points.Reverse();

            return new NestPart
            {
                PolygonTree = polygon,
                Quantity = 1,
                IsSheet = true,
                Area = width * height
            };
        }

        /// <summary>
        /// Convert a Rhino closed curve to a sheet NestPart.
        /// </summary>
        public static NestPart CurveToSheet(Curve curve, double tolerance)
        {
            var polygon = CurveToNestPolygon(curve, tolerance);
            if (polygon == null)
                return null;

            var bounds = GeometryUtil.GetPolygonBounds(polygon.Points);
            return new NestPart
            {
                PolygonTree = polygon,
                Quantity = 1,
                IsSheet = true,
                Area = bounds.HasValue ? bounds.Value.Width * bounds.Value.Height : 0
            };
        }

        /// <summary>
        /// Convert a NestPoint back to a Rhino Point3d (Y-flip at boundary).
        /// </summary>
        public static Point3d NestPointToRhinoPoint(NestPoint pt)
        {
            return new Point3d(pt.X, -pt.Y, 0);
        }

        /// <summary>
        /// Convert a NestPolygon back to a Rhino Polyline curve.
        /// </summary>
        public static PolylineCurve NestPolygonToRhinoCurve(NestPolygon polygon)
        {
            var pts = new List<Point3d>();
            foreach (var p in polygon.Points)
            {
                pts.Add(NestPointToRhinoPoint(p));
            }
            // Close the polyline
            if (pts.Count > 0)
                pts.Add(pts[0]);

            return new PolylineCurve(pts);
        }

        /// <summary>
        /// Apply a placement result to the Rhino document by creating transformed copies.
        /// </summary>
        public static void ApplyPlacement(
            RhinoDoc doc,
            NestResult result,
            List<NestPart> allParts,
            List<NestPolygon> sheets)
        {
            if (result?.Placements == null || result.Placements.Count == 0)
                return;

            // Create or find the results layer
            int layerIndex = GetOrCreateLayer(doc, "DeepNest Results");

            for (int s = 0; s < result.Placements.Count; s++)
            {
                var sheetPlacement = result.Placements[s];

                foreach (var partPlacement in sheetPlacement.SheetPlacements)
                {
                    // Find the original part by matching source
                    NestPart originalPart = null;
                    foreach (var part in allParts)
                    {
                        if (!part.IsSheet && part.PolygonTree.Source == partPlacement.Source)
                        {
                            originalPart = part;
                            break;
                        }
                    }

                    if (originalPart == null || originalPart.RhinoObjectId == Guid.Empty)
                        continue;

                    // Get the original Rhino object
                    var rhinoObj = doc.Objects.FindId(originalPart.RhinoObjectId);
                    if (rhinoObj == null)
                        continue;

                    var geom = rhinoObj.Geometry.Duplicate();

                    // Build transform: rotate about origin, then translate.
                    // Note: Y-flip is applied (negate Y for placement coordinates).
                    var xform = Transform.Identity;
                    if (Math.Abs(partPlacement.Rotation) > GeometryUtil.TOL)
                    {
                        xform = Transform.Rotation(
                            partPlacement.Rotation * Math.PI / 180.0,
                            Vector3d.ZAxis,
                            Point3d.Origin);
                    }

                    // Translate: X stays the same, Y is negated back to Rhino Y-up
                    var translate = Transform.Translation(
                        partPlacement.X, -partPlacement.Y, 0);

                    geom.Transform(xform * translate);

                    var attributes = new ObjectAttributes
                    {
                        LayerIndex = layerIndex,
                        Name = $"Nested_{partPlacement.Source}_{partPlacement.Id}"
                    };

                    doc.Objects.Add(geom, attributes);
                }
            }

            doc.Views.Redraw();
        }

        private static int GetOrCreateLayer(RhinoDoc doc, string layerName)
        {
            int index = doc.Layers.FindByFullPath(layerName, -1);
            if (index >= 0)
                return index;

            var layer = new Layer
            {
                Name = layerName,
                Color = System.Drawing.Color.FromArgb(0, 180, 0)
            };
            return doc.Layers.Add(layer);
        }
    }
}

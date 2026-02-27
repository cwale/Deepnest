using System;
using System.Collections.Generic;
using Clipper2Lib;

namespace DeepNestRhino.Geometry
{
    /// <summary>
    /// Polygon manipulation utilities using Clipper2.
    /// Port of cleanPolygon, polygonOffset, simplifyPolygon, and offsetTree from deepnest.js.
    /// </summary>
    public static class PolygonHelper
    {
        /// <summary>
        /// Remove self-intersections and clean up a polygon.
        /// Port of deepnest.js cleanPolygon() (lines 1180-1216).
        /// </summary>
        public static List<NestPoint> CleanPolygon(List<NestPoint> polygon, double clipperScale, double curveTolerance)
        {
            var p = ClipperHelper.ToPath64(polygon, clipperScale);

            // Remove self-intersections, find largest polygon
            var simple = Clipper.SimplifyPaths(new Paths64 { p }, 0, false);

            if (simple == null || simple.Count == 0)
                return null;

            var biggest = simple[0];
            double biggestArea = Math.Abs(Clipper.Area(biggest));
            for (int i = 1; i < simple.Count; i++)
            {
                double area = Math.Abs(Clipper.Area(simple[i]));
                if (area > biggestArea)
                {
                    biggest = simple[i];
                    biggestArea = area;
                }
            }

            // Clean up singularities, coincident points
            double cleanDist = 0.01 * curveTolerance * clipperScale;
            var clean = Clipper.RamerDouglasPeucker(biggest, cleanDist);

            if (clean == null || clean.Count == 0)
                return null;

            var cleaned = ClipperHelper.FromPath64(clean, clipperScale);

            // Remove duplicate endpoints
            if (cleaned.Count >= 2)
            {
                var start = cleaned[0];
                var end = cleaned[cleaned.Count - 1];
                if (GeometryUtil.AlmostEqual(start.X, end.X) && GeometryUtil.AlmostEqual(start.Y, end.Y))
                    cleaned.RemoveAt(cleaned.Count - 1);
            }

            return cleaned;
        }

        /// <summary>
        /// Offset a polygon by the given amount. Positive = expand, negative = contract.
        /// Returns an array of polygons (offsetting can split a polygon).
        /// Port of deepnest.js polygonOffset() (lines 1157-1177).
        /// </summary>
        public static List<List<NestPoint>> PolygonOffset(List<NestPoint> polygon, double offset, double clipperScale, double curveTolerance)
        {
            if (GeometryUtil.AlmostEqual(offset, 0))
                return new List<List<NestPoint>> { polygon };

            var p = ClipperHelper.ToPath64(polygon, clipperScale);

            double miterLimit = 4;
            var co = new ClipperOffset(miterLimit, curveTolerance * clipperScale);
            co.AddPath(p, JoinType.Miter, EndType.Polygon);

            var newpaths = new Paths64();
            co.Execute(offset * clipperScale, newpaths);

            var result = new List<List<NestPoint>>();
            for (int i = 0; i < newpaths.Count; i++)
            {
                result.Add(ClipperHelper.FromPath64(newpaths[i], clipperScale));
            }

            return result;
        }

        /// <summary>
        /// Recursively offset a polygon tree (parts get expanded, sheets/holes get contracted).
        /// Port of deepnest.js offsetTree() (lines 972-1005).
        /// </summary>
        public static void OffsetTree(NestPolygon t, double offset, double clipperScale, double curveTolerance, bool inside = false)
        {
            List<NestPoint> simple = t.Points;

            var offsetPaths = new List<List<NestPoint>> { simple };
            if (offset > 0)
            {
                offsetPaths = PolygonOffset(simple, offset, clipperScale, curveTolerance);
            }

            if (offsetPaths.Count > 0)
            {
                t.ReplacePoints(offsetPaths[0]);
            }

            if (t.Children != null && t.Children.Count > 0)
            {
                for (int i = 0; i < t.Children.Count; i++)
                {
                    OffsetTree(t.Children[i], -offset, clipperScale, curveTolerance, !inside);
                }
            }
        }
    }
}

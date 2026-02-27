using System;
using System.Collections.Generic;
using Clipper2Lib;

namespace DeepNestRhino.Geometry
{
    /// <summary>
    /// Helper for converting between NestPolygon/NestPoint types and Clipper2 types.
    /// Abstracts the Clipper2 API and provides the same operations as the original
    /// ClipperLib usage in deepnest.js.
    /// </summary>
    public static class ClipperHelper
    {
        public const double DefaultScale = 10000000;

        public static Path64 ToPath64(List<NestPoint> polygon, double scale = DefaultScale)
        {
            var path = new Path64(polygon.Count);
            for (int i = 0; i < polygon.Count; i++)
            {
                path.Add(new Point64((long)(polygon[i].X * scale), (long)(polygon[i].Y * scale)));
            }
            return path;
        }

        public static List<NestPoint> FromPath64(Path64 path, double scale = DefaultScale)
        {
            var result = new List<NestPoint>(path.Count);
            for (int i = 0; i < path.Count; i++)
            {
                result.Add(new NestPoint(path[i].X / scale, path[i].Y / scale));
            }
            return result;
        }

        public static PathD ToPathD(List<NestPoint> polygon)
        {
            var path = new PathD(polygon.Count);
            for (int i = 0; i < polygon.Count; i++)
            {
                path.Add(new PointD(polygon[i].X, polygon[i].Y));
            }
            return path;
        }

        public static List<NestPoint> FromPathD(PathD path)
        {
            var result = new List<NestPoint>(path.Count);
            for (int i = 0; i < path.Count; i++)
            {
                result.Add(new NestPoint(path[i].x, path[i].y));
            }
            return result;
        }

        /// <summary>
        /// Boolean union of multiple polygons.
        /// </summary>
        public static Paths64 Union(Paths64 subjects, double scale = DefaultScale)
        {
            var clipper = new Clipper64();
            clipper.AddSubject(subjects);
            var result = new Paths64();
            clipper.Execute(ClipType.Union, FillRule.NonZero, result);
            return result;
        }

        /// <summary>
        /// Boolean difference: subject minus clip.
        /// </summary>
        public static Paths64 Difference(Paths64 subjects, Paths64 clips,
            FillRule subjectFill = FillRule.EvenOdd, FillRule clipFill = FillRule.NonZero)
        {
            var clipper = new Clipper64();
            clipper.AddSubject(subjects);
            clipper.AddClip(clips);
            var result = new Paths64();
            clipper.Execute(ClipType.Difference, subjectFill, result);
            return result;
        }

        /// <summary>
        /// Convert an NFP (with possible children) to Clipper paths for boolean operations.
        /// Maps nfpToClipperCoordinates() from background.js.
        /// </summary>
        public static Paths64 NfpToClipperPaths(NestPolygon nfp, double scale = DefaultScale)
        {
            var paths = new Paths64();

            // Children first (holes in the NFP)
            if (nfp.Children != null && nfp.Children.Count > 0)
            {
                foreach (var child in nfp.Children)
                {
                    var childPoints = child.Points;
                    if (GeometryUtil.PolygonArea(childPoints) < 0)
                    {
                        childPoints = new List<NestPoint>(childPoints);
                        childPoints.Reverse();
                    }
                    paths.Add(ToPath64(childPoints, scale));
                }
            }

            // Outer NFP (ensure negative area = CW in Clipper convention)
            var outerPoints = nfp.Points;
            if (GeometryUtil.PolygonArea(outerPoints) > 0)
            {
                outerPoints = new List<NestPoint>(outerPoints);
                outerPoints.Reverse();
            }
            paths.Add(ToPath64(outerPoints, scale));

            return paths;
        }

        /// <summary>
        /// Convert inner NFPs (array of NFPs) to Clipper paths.
        /// Maps innerNfpToClipperCoordinates() from background.js.
        /// </summary>
        public static Paths64 InnerNfpToClipperPaths(List<NestPolygon> nfps, double scale = DefaultScale)
        {
            var paths = new Paths64();
            foreach (var nfp in nfps)
            {
                var nfpPaths = NfpToClipperPaths(nfp, scale);
                paths.AddRange(nfpPaths);
            }
            return paths;
        }
    }
}

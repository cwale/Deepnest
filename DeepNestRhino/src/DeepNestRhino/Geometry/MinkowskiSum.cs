using System;
using System.Collections.Generic;
using Clipper2Lib;

namespace DeepNestRhino.Geometry
{
    /// <summary>
    /// Minkowski sum computation using Clipper2.
    /// Port of the Clipper-based NFP computation in background.js (lines 173-249, 650-684).
    /// Replaces the native C++ addon (minkowski.cc) which used Boost.Polygon.
    /// </summary>
    public static class MinkowskiSumComputer
    {
        /// <summary>
        /// Compute the outer No-Fit Polygon of A and B using Minkowski sum.
        /// The NFP is the Minkowski sum of A with the negation of B, shifted by B[0].
        /// </summary>
        public static NestPolygon ComputeOuterNfp(List<NestPoint> A, List<NestPoint> B, double clipperScale = ClipperHelper.DefaultScale)
        {
            // Convert to Clipper coordinates
            var Ac = ClipperHelper.ToPath64(A, clipperScale);
            var Bc = ClipperHelper.ToPath64(B, clipperScale);

            // Negate B for NFP (Minkowski difference)
            for (int i = 0; i < Bc.Count; i++)
            {
                Bc[i] = new Point64(-Bc[i].X, -Bc[i].Y);
            }

            // Compute Minkowski sum
            var solution = Clipper.MinkowskiSum(Ac, Bc, true);

            if (solution == null || solution.Count == 0)
                return null;

            // Find the largest polygon by area
            List<NestPoint> clipperNfp = null;
            double largestArea = double.MinValue;

            for (int i = 0; i < solution.Count; i++)
            {
                var n = ClipperHelper.FromPath64(solution[i], clipperScale);
                double sarea = -GeometryUtil.PolygonArea(n);
                if (sarea > largestArea)
                {
                    clipperNfp = n;
                    largestArea = sarea;
                }
            }

            if (clipperNfp == null)
                return null;

            // Shift by B[0] reference point
            for (int i = 0; i < clipperNfp.Count; i++)
            {
                clipperNfp[i] = new NestPoint(clipperNfp[i].X + B[0].X, clipperNfp[i].Y + B[0].Y);
            }

            var result = new NestPolygon { Points = clipperNfp };
            return result;
        }

        /// <summary>
        /// Compute the outer NFP with hole handling.
        /// For polygons with holes (children), compute the outer NFP of the frame
        /// and then process holes separately.
        /// Maps getOuterNfp() from background.js (lines 620-710).
        /// </summary>
        public static NestPolygon ComputeOuterNfpWithHoles(NestPolygon A, NestPolygon B, double clipperScale = ClipperHelper.DefaultScale)
        {
            NestPolygon nfp;

            if (A.Children != null && A.Children.Count > 0)
            {
                // Use the orbiting algorithm for polygons with holes
                var nfpList = NfpAlgorithm.ComputeNfp(A.Points, B.Points, false, false);
                if (nfpList == null || nfpList.Count == 0)
                    return null;

                nfp = new NestPolygon { Points = nfpList[nfpList.Count - 1] };

                // Process holes: compute inner NFPs for each hole in A that's large enough for B
                var rotatedB = GeometryUtil.RotatePolygon(B, B.Rotation);
                var bbounds = GeometryUtil.GetPolygonBounds(rotatedB.Points);

                if (bbounds != null)
                {
                    var innerNfps = new List<NestPolygon>();
                    foreach (var hole in A.Children)
                    {
                        var cbounds = GeometryUtil.GetPolygonBounds(hole.Points);
                        if (cbounds != null && cbounds.Value.Width > bbounds.Value.Width
                            && cbounds.Value.Height > bbounds.Value.Height)
                        {
                            var inner = ComputeInnerNfp(hole.Points, rotatedB.Points, clipperScale);
                            if (inner != null)
                                innerNfps.AddRange(inner);
                        }
                    }

                    if (innerNfps.Count > 0)
                    {
                        nfp.Children = innerNfps;
                    }
                }
            }
            else
            {
                // Simple polygon: use Clipper MinkowskiSum
                nfp = ComputeOuterNfp(A.Points, B.Points, clipperScale);
            }

            return nfp;
        }

        /// <summary>
        /// Compute inner NFP (where B can be placed inside A).
        /// Uses the frame approach from background.js getInnerNfp() (lines 734-801).
        /// </summary>
        public static List<NestPolygon> ComputeInnerNfp(List<NestPoint> A, List<NestPoint> B,
            double clipperScale = ClipperHelper.DefaultScale)
        {
            // Create a frame around A (expanded by 10%)
            var frame = CreateFrame(A);

            // Compute outer NFP of frame vs B
            // The frame has A as a child (hole), so use orbiting algorithm
            var frameNfpList = NfpAlgorithm.ComputeNfp(frame.Points, B, true, true);

            if (frameNfpList == null || frameNfpList.Count == 0)
                return null;

            // The inner NFP results are the NFP loops
            var result = new List<NestPolygon>();
            foreach (var nfpPoints in frameNfpList)
            {
                result.Add(new NestPolygon { Points = nfpPoints });
            }

            return result;
        }

        /// <summary>
        /// Create a frame rectangle around polygon A, with A as a child (hole).
        /// Maps getFrame() from background.js (lines 712-732).
        /// </summary>
        public static NestPolygon CreateFrame(List<NestPoint> A)
        {
            var bounds = GeometryUtil.GetPolygonBounds(A);
            if (bounds == null)
                return null;

            var b = bounds.Value;

            // Expand bounds by 10%
            double w = b.Width * 1.1;
            double h = b.Height * 1.1;
            double x = b.X - 0.5 * (w - b.Width);
            double y = b.Y - 0.5 * (h - b.Height);

            var frame = new NestPolygon
            {
                Points = new List<NestPoint>
                {
                    new NestPoint(x, y),
                    new NestPoint(x + w, y),
                    new NestPoint(x + w, y + h),
                    new NestPoint(x, y + h)
                },
                Children = new List<NestPolygon>
                {
                    new NestPolygon { Points = new List<NestPoint>(A) }
                }
            };

            return frame;
        }
    }
}

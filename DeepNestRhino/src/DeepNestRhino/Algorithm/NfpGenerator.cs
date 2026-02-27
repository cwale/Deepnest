using System.Collections.Generic;
using DeepNestRhino.Core;
using DeepNestRhino.Geometry;

namespace DeepNestRhino.Algorithm
{
    /// <summary>
    /// Dispatches NFP computation and manages the cache.
    /// Maps getOuterNfp() and getInnerNfp() from background.js (lines 620-801).
    /// </summary>
    public class NfpGenerator
    {
        private readonly NfpCache _cache;
        private readonly NestingConfig _config;

        public NfpGenerator(NfpCache cache, NestingConfig config)
        {
            _cache = cache;
            _config = config;
        }

        /// <summary>
        /// Get outer NFP for placing part B adjacent to part A.
        /// Checks cache first, computes and stores if not found.
        /// </summary>
        public NestPolygon GetOuterNfp(NestPolygon A, NestPolygon B, bool inside = false)
        {
            // Check cache
            if (!inside && A.Source >= 0 && B.Source >= 0)
            {
                var cached = _cache.FindOuter(A.Source, B.Source, A.Rotation, B.Rotation);
                if (cached != null)
                    return cached;
            }

            NestPolygon nfp;

            if (inside || (A.Children != null && A.Children.Count > 0))
            {
                // Use orbiting algorithm for complex polygons
                nfp = MinkowskiSumComputer.ComputeOuterNfpWithHoles(A, B, _config.ClipperScale);
            }
            else
            {
                // Simple polygon: use Clipper MinkowskiSum
                nfp = MinkowskiSumComputer.ComputeOuterNfp(A.Points, B.Points, _config.ClipperScale);
            }

            if (nfp == null)
                return null;

            // Store in cache
            if (!inside && A.Source >= 0 && B.Source >= 0)
            {
                _cache.InsertOuter(A.Source, B.Source, A.Rotation, B.Rotation, nfp);
            }

            return nfp;
        }

        /// <summary>
        /// Get inner NFP for placing part B inside sheet A.
        /// Uses the frame approach: creates a frame around A, computes outer NFP of frame vs B,
        /// then uses the children (inner boundaries) as the inner NFP.
        /// Maps getInnerNfp() from background.js (lines 734-801).
        /// </summary>
        public List<NestPolygon> GetInnerNfp(NestPolygon sheet, NestPolygon part)
        {
            // Check cache
            if (sheet.Source >= 0 && part.Source >= 0)
            {
                var cached = _cache.FindInner(sheet.Source, part.Source, 0, part.Rotation);
                if (cached != null)
                    return cached;
            }

            // Use rectangle shortcut if applicable
            if (GeometryUtil.IsRectangle(sheet.Points))
            {
                var rectNfp = GeometryUtil.NoFitPolygonRectangle(sheet.Points, part.Points);
                if (rectNfp != null)
                {
                    var result = new List<NestPolygon>();
                    foreach (var pts in rectNfp)
                        result.Add(new NestPolygon { Points = pts });

                    if (sheet.Source >= 0 && part.Source >= 0)
                        _cache.InsertInner(sheet.Source, part.Source, 0, part.Rotation, result);

                    return result;
                }
            }

            // Frame approach
            var innerNfps = MinkowskiSumComputer.ComputeInnerNfp(sheet.Points, part.Points, _config.ClipperScale);

            if (innerNfps == null || innerNfps.Count == 0)
                return null;

            // Handle holes in sheet
            if (sheet.Children != null && sheet.Children.Count > 0)
            {
                var holeNfps = new List<NestPolygon>();
                foreach (var hole in sheet.Children)
                {
                    var hnfp = GetOuterNfp(hole, part);
                    if (hnfp != null)
                        holeNfps.Add(hnfp);
                }

                if (holeNfps.Count > 0)
                {
                    // Subtract hole NFPs from inner NFPs using Clipper
                    var clipperInner = ClipperHelper.InnerNfpToClipperPaths(innerNfps, _config.ClipperScale);
                    var clipperHoles = ClipperHelper.InnerNfpToClipperPaths(holeNfps, _config.ClipperScale);

                    var finalNfp = ClipperHelper.Difference(clipperInner, clipperHoles,
                        Clipper2Lib.FillRule.NonZero, Clipper2Lib.FillRule.NonZero);

                    if (finalNfp.Count == 0)
                        return null;

                    innerNfps = new List<NestPolygon>();
                    foreach (var path in finalNfp)
                    {
                        innerNfps.Add(new NestPolygon
                        {
                            Points = ClipperHelper.FromPath64(path, _config.ClipperScale)
                        });
                    }
                }
            }

            // Cache
            if (sheet.Source >= 0 && part.Source >= 0)
                _cache.InsertInner(sheet.Source, part.Source, 0, part.Rotation, innerNfps);

            return innerNfps;
        }
    }
}

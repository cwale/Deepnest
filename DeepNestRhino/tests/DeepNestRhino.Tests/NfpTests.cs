using System;
using System.Collections.Generic;
using Xunit;
using DeepNestRhino.Geometry;

namespace DeepNestRhino.Tests
{
    public class NfpTests
    {
        private static List<NestPoint> MakeSquare(double size, double x = 0, double y = 0)
        {
            return new List<NestPoint>
            {
                new NestPoint(x, y),
                new NestPoint(x + size, y),
                new NestPoint(x + size, y + size),
                new NestPoint(x, y + size)
            };
        }

        private static List<NestPoint> MakeTriangle(double size, double x = 0, double y = 0)
        {
            return new List<NestPoint>
            {
                new NestPoint(x, y),
                new NestPoint(x + size, y),
                new NestPoint(x + size / 2, y + size)
            };
        }

        #region MinkowskiSum

        [Fact]
        public void MinkowskiSum_TwoSquares_ProducesValidNfp()
        {
            var A = MakeSquare(10);
            var B = MakeSquare(5);

            var nfp = MinkowskiSumComputer.ComputeOuterNfp(A, B);

            Assert.NotNull(nfp);
            Assert.True(nfp.Points.Count >= 4, "NFP of two squares should have at least 4 points");

            // The NFP bounding box should be approximately 15x15
            // (10 + 5 = 15 in each dimension, minus overlap)
            var bounds = GeometryUtil.GetPolygonBounds(nfp.Points);
            Assert.NotNull(bounds);
        }

        [Fact]
        public void MinkowskiSum_SquareAndTriangle_ProducesValidNfp()
        {
            var A = MakeSquare(10);
            var B = MakeTriangle(5);

            var nfp = MinkowskiSumComputer.ComputeOuterNfp(A, B);

            Assert.NotNull(nfp);
            Assert.True(nfp.Points.Count >= 3, "NFP should have at least 3 points");
        }

        #endregion

        #region Frame Creation

        [Fact]
        public void CreateFrame_HasCorrectStructure()
        {
            var poly = MakeSquare(10);
            var frame = MinkowskiSumComputer.CreateFrame(poly);

            Assert.NotNull(frame);
            Assert.Equal(4, frame.Points.Count); // Frame is a rectangle
            Assert.NotNull(frame.Children);
            Assert.Single(frame.Children); // Original polygon is the hole

            // Frame should be larger than the original
            var frameBounds = GeometryUtil.GetPolygonBounds(frame.Points);
            var polyBounds = GeometryUtil.GetPolygonBounds(poly);
            Assert.True(frameBounds.Value.Width > polyBounds.Value.Width);
            Assert.True(frameBounds.Value.Height > polyBounds.Value.Height);
        }

        #endregion

        #region ClipperHelper

        [Fact]
        public void ClipperHelper_ToPath64_RoundTrip()
        {
            var points = MakeSquare(10);
            double scale = ClipperHelper.DefaultScale;

            var path = ClipperHelper.ToPath64(points, scale);
            var result = ClipperHelper.FromPath64(path, scale);

            Assert.Equal(points.Count, result.Count);
            for (int i = 0; i < points.Count; i++)
            {
                Assert.True(GeometryUtil.AlmostEqual(points[i].X, result[i].X, 1e-3));
                Assert.True(GeometryUtil.AlmostEqual(points[i].Y, result[i].Y, 1e-3));
            }
        }

        [Fact]
        public void ClipperHelper_ToPathD_RoundTrip()
        {
            var points = MakeSquare(10);

            var path = ClipperHelper.ToPathD(points);
            var result = ClipperHelper.FromPathD(path);

            Assert.Equal(points.Count, result.Count);
            for (int i = 0; i < points.Count; i++)
            {
                Assert.True(GeometryUtil.AlmostEqual(points[i].X, result[i].X, 1e-9));
                Assert.True(GeometryUtil.AlmostEqual(points[i].Y, result[i].Y, 1e-9));
            }
        }

        [Fact]
        public void ClipperHelper_Union_TwoOverlappingSquares()
        {
            var sq1 = ClipperHelper.ToPath64(MakeSquare(10), ClipperHelper.DefaultScale);
            var sq2 = ClipperHelper.ToPath64(MakeSquare(10, 5, 0), ClipperHelper.DefaultScale);

            var paths = new Clipper2Lib.Paths64 { sq1, sq2 };
            var result = ClipperHelper.Union(paths);

            Assert.NotNull(result);
            Assert.True(result.Count >= 1, "Union should produce at least one polygon");
        }

        #endregion

        #region PolygonHelper

        [Fact]
        public void CleanPolygon_RemovesSelfIntersections()
        {
            // Simple square should survive cleaning
            var square = MakeSquare(10);
            var cleaned = PolygonHelper.CleanPolygon(square, ClipperHelper.DefaultScale, 0.3);

            Assert.NotNull(cleaned);
            Assert.True(cleaned.Count >= 3, "Cleaned polygon should have at least 3 points");
        }

        [Fact]
        public void PolygonOffset_PositiveExpands()
        {
            var square = MakeSquare(10);
            var offsetResult = PolygonHelper.PolygonOffset(
                square, 1.0, ClipperHelper.DefaultScale, 0.3);

            Assert.NotNull(offsetResult);
            Assert.True(offsetResult.Count >= 1);

            // Expanded polygon should have larger area
            double originalArea = Math.Abs(GeometryUtil.PolygonArea(square));
            double offsetArea = Math.Abs(GeometryUtil.PolygonArea(offsetResult[0]));
            Assert.True(offsetArea > originalArea, "Positive offset should expand the polygon");
        }

        [Fact]
        public void PolygonOffset_NegativeContracts()
        {
            var square = MakeSquare(20);
            var offsetResult = PolygonHelper.PolygonOffset(
                square, -2.0, ClipperHelper.DefaultScale, 0.3);

            Assert.NotNull(offsetResult);
            Assert.True(offsetResult.Count >= 1);

            double originalArea = Math.Abs(GeometryUtil.PolygonArea(square));
            double offsetArea = Math.Abs(GeometryUtil.PolygonArea(offsetResult[0]));
            Assert.True(offsetArea < originalArea, "Negative offset should contract the polygon");
        }

        [Fact]
        public void PolygonOffset_ZeroOffset_ReturnsSame()
        {
            var square = MakeSquare(10);
            var offsetResult = PolygonHelper.PolygonOffset(
                square, 0, ClipperHelper.DefaultScale, 0.3);

            Assert.NotNull(offsetResult);
            Assert.Single(offsetResult);
            Assert.Equal(square.Count, offsetResult[0].Count);
        }

        #endregion

        #region PolygonTreeBuilder

        [Fact]
        public void PolygonTreeBuilder_DetectsHole()
        {
            // Outer square
            var outer = new NestPolygon { Source = 0 };
            outer.Points.AddRange(MakeSquare(20));

            // Inner square (hole)
            var inner = new NestPolygon { Source = 1 };
            inner.Points.AddRange(MakeSquare(5, 5, 5));

            var polygons = new List<NestPolygon> { outer, inner };
            var roots = Conversion.PolygonTreeBuilder.BuildTree(polygons);

            Assert.Single(roots); // Only outer should be a root
            Assert.NotNull(roots[0].Children);
            Assert.Single(roots[0].Children); // Inner is a child
        }

        [Fact]
        public void PolygonTreeBuilder_SeparatePolygons_BothRoots()
        {
            var poly1 = new NestPolygon { Source = 0 };
            poly1.Points.AddRange(MakeSquare(10));

            var poly2 = new NestPolygon { Source = 1 };
            poly2.Points.AddRange(MakeSquare(10, 50, 0)); // Far away

            var polygons = new List<NestPolygon> { poly1, poly2 };
            var roots = Conversion.PolygonTreeBuilder.BuildTree(polygons);

            Assert.Equal(2, roots.Count); // Both should be roots
        }

        #endregion
    }
}

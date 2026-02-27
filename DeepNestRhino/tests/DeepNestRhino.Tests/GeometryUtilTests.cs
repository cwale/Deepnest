using System;
using System.Collections.Generic;
using Xunit;
using DeepNestRhino.Geometry;

namespace DeepNestRhino.Tests
{
    public class GeometryUtilTests
    {
        #region AlmostEqual

        [Fact]
        public void AlmostEqual_SameValues_ReturnsTrue()
        {
            Assert.True(GeometryUtil.AlmostEqual(1.0, 1.0));
        }

        [Fact]
        public void AlmostEqual_VeryCloseValues_ReturnsTrue()
        {
            Assert.True(GeometryUtil.AlmostEqual(1.0, 1.0 + 1e-10));
        }

        [Fact]
        public void AlmostEqual_DifferentValues_ReturnsFalse()
        {
            Assert.False(GeometryUtil.AlmostEqual(1.0, 2.0));
        }

        [Fact]
        public void AlmostEqual_CustomTolerance()
        {
            Assert.True(GeometryUtil.AlmostEqual(1.0, 1.05, 0.1));
            Assert.False(GeometryUtil.AlmostEqual(1.0, 1.2, 0.1));
        }

        #endregion

        #region PolygonArea

        [Fact]
        public void PolygonArea_UnitSquare_CCW_NegativeArea()
        {
            // CCW winding: (0,0) -> (1,0) -> (1,1) -> (0,1)
            var square = new List<NestPoint>
            {
                new NestPoint(0, 0),
                new NestPoint(1, 0),
                new NestPoint(1, 1),
                new NestPoint(0, 1)
            };

            double area = GeometryUtil.PolygonArea(square);
            Assert.True(area < 0, "CCW polygon should have negative area");
            Assert.True(GeometryUtil.AlmostEqual(Math.Abs(area), 1.0, 1e-6));
        }

        [Fact]
        public void PolygonArea_UnitSquare_CW_PositiveArea()
        {
            // CW winding: (0,0) -> (0,1) -> (1,1) -> (1,0)
            var square = new List<NestPoint>
            {
                new NestPoint(0, 0),
                new NestPoint(0, 1),
                new NestPoint(1, 1),
                new NestPoint(1, 0)
            };

            double area = GeometryUtil.PolygonArea(square);
            Assert.True(area > 0, "CW polygon should have positive area");
            Assert.True(GeometryUtil.AlmostEqual(Math.Abs(area), 1.0, 1e-6));
        }

        [Fact]
        public void PolygonArea_Triangle()
        {
            var triangle = new List<NestPoint>
            {
                new NestPoint(0, 0),
                new NestPoint(4, 0),
                new NestPoint(0, 3)
            };

            double area = Math.Abs(GeometryUtil.PolygonArea(triangle));
            Assert.True(GeometryUtil.AlmostEqual(area, 6.0, 1e-6));
        }

        [Fact]
        public void PolygonArea_Rectangle_5x3()
        {
            var rect = new List<NestPoint>
            {
                new NestPoint(0, 0),
                new NestPoint(5, 0),
                new NestPoint(5, 3),
                new NestPoint(0, 3)
            };

            double area = Math.Abs(GeometryUtil.PolygonArea(rect));
            Assert.True(GeometryUtil.AlmostEqual(area, 15.0, 1e-6));
        }

        #endregion

        #region GetPolygonBounds

        [Fact]
        public void GetPolygonBounds_UnitSquare()
        {
            var square = new List<NestPoint>
            {
                new NestPoint(1, 2),
                new NestPoint(4, 2),
                new NestPoint(4, 5),
                new NestPoint(1, 5)
            };

            var bounds = GeometryUtil.GetPolygonBounds(square);
            Assert.NotNull(bounds);
            Assert.True(GeometryUtil.AlmostEqual(bounds.Value.X, 1, 1e-6));
            Assert.True(GeometryUtil.AlmostEqual(bounds.Value.Y, 2, 1e-6));
            Assert.True(GeometryUtil.AlmostEqual(bounds.Value.Width, 3, 1e-6));
            Assert.True(GeometryUtil.AlmostEqual(bounds.Value.Height, 3, 1e-6));
        }

        [Fact]
        public void GetPolygonBounds_NullOrEmpty_ReturnsNull()
        {
            Assert.Null(GeometryUtil.GetPolygonBounds(null));
            Assert.Null(GeometryUtil.GetPolygonBounds(new List<NestPoint>()));
            Assert.Null(GeometryUtil.GetPolygonBounds(new List<NestPoint> { new NestPoint(0, 0) }));
        }

        #endregion

        #region PointInPolygon

        [Fact]
        public void PointInPolygon_InsideSquare_ReturnsTrue()
        {
            var square = new List<NestPoint>
            {
                new NestPoint(0, 0),
                new NestPoint(10, 0),
                new NestPoint(10, 10),
                new NestPoint(0, 10)
            };

            var result = GeometryUtil.PointInPolygon(new NestPoint(5, 5), square);
            Assert.True(result == true);
        }

        [Fact]
        public void PointInPolygon_OutsideSquare_ReturnsFalse()
        {
            var square = new List<NestPoint>
            {
                new NestPoint(0, 0),
                new NestPoint(10, 0),
                new NestPoint(10, 10),
                new NestPoint(0, 10)
            };

            var result = GeometryUtil.PointInPolygon(new NestPoint(15, 5), square);
            Assert.True(result == false);
        }

        [Fact]
        public void PointInPolygon_OnEdge_ReturnsNull()
        {
            var square = new List<NestPoint>
            {
                new NestPoint(0, 0),
                new NestPoint(10, 0),
                new NestPoint(10, 10),
                new NestPoint(0, 10)
            };

            var result = GeometryUtil.PointInPolygon(new NestPoint(5, 0), square);
            Assert.Null(result);
        }

        [Fact]
        public void PointInPolygon_OnVertex_ReturnsNull()
        {
            var square = new List<NestPoint>
            {
                new NestPoint(0, 0),
                new NestPoint(10, 0),
                new NestPoint(10, 10),
                new NestPoint(0, 10)
            };

            var result = GeometryUtil.PointInPolygon(new NestPoint(0, 0), square);
            Assert.Null(result);
        }

        [Fact]
        public void PointInPolygon_WithOffset()
        {
            var square = new List<NestPoint>
            {
                new NestPoint(0, 0),
                new NestPoint(10, 0),
                new NestPoint(10, 10),
                new NestPoint(0, 10)
            };

            // Point at (15,5) is outside, but with offset (-10, 0) the polygon
            // shifts to (-10,0)-(0,0)-(0,10)-(-10,10), so (15,5) is still outside
            var result = GeometryUtil.PointInPolygon(new NestPoint(5, 5), square, 10, 10);
            // Polygon is now at (10,10)-(20,10)-(20,20)-(10,20), point (5,5) is outside
            Assert.True(result == false);
        }

        #endregion

        #region LineIntersect

        [Fact]
        public void LineIntersect_CrossingSegments_ReturnsIntersection()
        {
            var p = GeometryUtil.LineIntersect(
                new NestPoint(0, 0), new NestPoint(10, 10),
                new NestPoint(10, 0), new NestPoint(0, 10));

            Assert.NotNull(p);
            Assert.True(GeometryUtil.AlmostEqual(p.Value.X, 5, 1e-6));
            Assert.True(GeometryUtil.AlmostEqual(p.Value.Y, 5, 1e-6));
        }

        [Fact]
        public void LineIntersect_NonCrossing_ReturnsNull()
        {
            var p = GeometryUtil.LineIntersect(
                new NestPoint(0, 0), new NestPoint(5, 0),
                new NestPoint(0, 5), new NestPoint(5, 5));

            Assert.Null(p);
        }

        [Fact]
        public void LineIntersect_InfiniteLines()
        {
            // Parallel non-crossing segments, but infinite lines DO cross
            var p = GeometryUtil.LineIntersect(
                new NestPoint(0, 0), new NestPoint(1, 1),
                new NestPoint(10, 0), new NestPoint(10, 10),
                infinite: true);

            Assert.NotNull(p);
            Assert.True(GeometryUtil.AlmostEqual(p.Value.X, 10, 1e-6));
            Assert.True(GeometryUtil.AlmostEqual(p.Value.Y, 10, 1e-6));
        }

        #endregion

        #region OnSegment

        [Fact]
        public void OnSegment_MidPoint_ReturnsTrue()
        {
            Assert.True(GeometryUtil.OnSegment(
                new NestPoint(0, 0), new NestPoint(10, 0), new NestPoint(5, 0)));
        }

        [Fact]
        public void OnSegment_Endpoint_ReturnsFalse()
        {
            Assert.False(GeometryUtil.OnSegment(
                new NestPoint(0, 0), new NestPoint(10, 0), new NestPoint(0, 0)));
        }

        [Fact]
        public void OnSegment_Outside_ReturnsFalse()
        {
            Assert.False(GeometryUtil.OnSegment(
                new NestPoint(0, 0), new NestPoint(10, 0), new NestPoint(15, 0)));
        }

        [Fact]
        public void OnSegment_Diagonal()
        {
            Assert.True(GeometryUtil.OnSegment(
                new NestPoint(0, 0), new NestPoint(10, 10), new NestPoint(5, 5)));
        }

        #endregion

        #region NormalizeVector

        [Fact]
        public void NormalizeVector_ResultHasUnitLength()
        {
            var v = GeometryUtil.NormalizeVector(new NestPoint(3, 4));
            double len = Math.Sqrt(v.X * v.X + v.Y * v.Y);
            Assert.True(GeometryUtil.AlmostEqual(len, 1.0, 1e-9));
        }

        [Fact]
        public void NormalizeVector_AlreadyUnit_ReturnsSame()
        {
            var v = GeometryUtil.NormalizeVector(new NestPoint(1, 0));
            Assert.True(GeometryUtil.AlmostEqual(v.X, 1.0));
            Assert.True(GeometryUtil.AlmostEqual(v.Y, 0.0));
        }

        #endregion

        #region IsRectangle

        [Fact]
        public void IsRectangle_AxisAligned_ReturnsTrue()
        {
            var rect = new List<NestPoint>
            {
                new NestPoint(0, 0),
                new NestPoint(10, 0),
                new NestPoint(10, 5),
                new NestPoint(0, 5)
            };

            Assert.True(GeometryUtil.IsRectangle(rect));
        }

        [Fact]
        public void IsRectangle_Triangle_ReturnsFalse()
        {
            var tri = new List<NestPoint>
            {
                new NestPoint(0, 0),
                new NestPoint(10, 0),
                new NestPoint(5, 5)
            };

            Assert.False(GeometryUtil.IsRectangle(tri));
        }

        [Fact]
        public void IsRectangle_Pentagon_ReturnsFalse()
        {
            var pent = new List<NestPoint>
            {
                new NestPoint(0, 0),
                new NestPoint(10, 0),
                new NestPoint(12, 5),
                new NestPoint(5, 10),
                new NestPoint(-2, 5)
            };

            Assert.False(GeometryUtil.IsRectangle(pent));
        }

        #endregion

        #region RotatePolygon

        [Fact]
        public void RotatePolygon_90Degrees()
        {
            var square = new NestPolygon();
            square.Points.Add(new NestPoint(1, 0));
            square.Points.Add(new NestPoint(0, 0));
            square.Points.Add(new NestPoint(0, 1));
            square.Points.Add(new NestPoint(1, 1));

            var rotated = GeometryUtil.RotatePolygon(square, 90);

            // After 90 degree rotation: (x, y) -> (-y, x)
            // Point (1, 0) -> (0, 1)
            Assert.True(GeometryUtil.AlmostEqual(rotated.Points[0].X, 0, 1e-6));
            Assert.True(GeometryUtil.AlmostEqual(rotated.Points[0].Y, 1, 1e-6));
        }

        [Fact]
        public void RotatePolygon_360Degrees_SameResult()
        {
            var square = new NestPolygon();
            square.Points.Add(new NestPoint(0, 0));
            square.Points.Add(new NestPoint(5, 0));
            square.Points.Add(new NestPoint(5, 3));
            square.Points.Add(new NestPoint(0, 3));

            var rotated = GeometryUtil.RotatePolygon(square, 360);

            for (int i = 0; i < square.Points.Count; i++)
            {
                Assert.True(GeometryUtil.AlmostEqual(square.Points[i].X, rotated.Points[i].X, 1e-6));
                Assert.True(GeometryUtil.AlmostEqual(square.Points[i].Y, rotated.Points[i].Y, 1e-6));
            }
        }

        [Fact]
        public void RotatePolygon_0Degrees_Unchanged()
        {
            var tri = new NestPolygon();
            tri.Points.Add(new NestPoint(0, 0));
            tri.Points.Add(new NestPoint(3, 0));
            tri.Points.Add(new NestPoint(0, 4));

            var rotated = GeometryUtil.RotatePolygon(tri, 0);

            for (int i = 0; i < tri.Points.Count; i++)
            {
                Assert.True(GeometryUtil.AlmostEqual(tri.Points[i].X, rotated.Points[i].X));
                Assert.True(GeometryUtil.AlmostEqual(tri.Points[i].Y, rotated.Points[i].Y));
            }
        }

        #endregion

        #region NestPolygon DeepClone

        [Fact]
        public void DeepClone_ProducesIndependentCopy()
        {
            var poly = new NestPolygon { Id = 1, Source = 2, Rotation = 45 };
            poly.Points.Add(new NestPoint(0, 0));
            poly.Points.Add(new NestPoint(10, 0));
            poly.Points.Add(new NestPoint(10, 10));

            poly.Children = new List<NestPolygon>
            {
                new NestPolygon { Id = 3, Source = 2 }
            };
            poly.Children[0].Points.Add(new NestPoint(2, 2));
            poly.Children[0].Points.Add(new NestPoint(8, 2));
            poly.Children[0].Points.Add(new NestPoint(5, 8));

            var clone = poly.DeepClone();

            // Same values
            Assert.Equal(poly.Id, clone.Id);
            Assert.Equal(poly.Source, clone.Source);
            Assert.Equal(poly.Points.Count, clone.Points.Count);
            Assert.NotNull(clone.Children);
            Assert.Single(clone.Children);

            // Independent — mutating clone doesn't affect original
            clone.Points[0] = new NestPoint(99, 99);
            Assert.True(GeometryUtil.AlmostEqual(poly.Points[0].X, 0));
        }

        #endregion
    }
}

using System;
using System.Collections.Generic;

namespace DeepNestRhino.Geometry
{
    /// <summary>
    /// Port of geometryutil.js — pure geometry math functions for polygon operations.
    /// All functions operate on NestPoint/NestPolygon types.
    /// </summary>
    public static class GeometryUtil
    {
        public const double TOL = 1e-9;

        public static bool AlmostEqual(double a, double b, double tolerance = 0)
        {
            if (tolerance == 0) tolerance = TOL;
            return Math.Abs(a - b) < tolerance;
        }

        public static bool AlmostEqualPoints(NestPoint a, NestPoint b, double tolerance = 0)
        {
            if (tolerance == 0) tolerance = TOL;
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return (dx * dx + dy * dy) < (tolerance * tolerance);
        }

        public static bool WithinDistance(NestPoint p1, NestPoint p2, double distance)
        {
            var dx = p1.X - p2.X;
            var dy = p1.Y - p2.Y;
            return (dx * dx + dy * dy) < distance * distance;
        }

        public static NestPoint NormalizeVector(NestPoint v)
        {
            if (AlmostEqual(v.X * v.X + v.Y * v.Y, 1))
                return v;
            var len = Math.Sqrt(v.X * v.X + v.Y * v.Y);
            var inv = 1.0 / len;
            return new NestPoint(v.X * inv, v.Y * inv);
        }

        /// <summary>
        /// Returns true if p lies on segment AB, but not at endpoints.
        /// </summary>
        public static bool OnSegment(NestPoint A, NestPoint B, NestPoint p, double tolerance = 0)
        {
            if (tolerance == 0) tolerance = TOL;

            // vertical line
            if (AlmostEqual(A.X, B.X, tolerance) && AlmostEqual(p.X, A.X, tolerance))
            {
                if (!AlmostEqual(p.Y, B.Y, tolerance) && !AlmostEqual(p.Y, A.Y, tolerance)
                    && p.Y < Math.Max(B.Y, A.Y) && p.Y > Math.Min(B.Y, A.Y))
                    return true;
                return false;
            }

            // horizontal line
            if (AlmostEqual(A.Y, B.Y, tolerance) && AlmostEqual(p.Y, A.Y, tolerance))
            {
                if (!AlmostEqual(p.X, B.X, tolerance) && !AlmostEqual(p.X, A.X, tolerance)
                    && p.X < Math.Max(B.X, A.X) && p.X > Math.Min(B.X, A.X))
                    return true;
                return false;
            }

            // range check
            if ((p.X < A.X && p.X < B.X) || (p.X > A.X && p.X > B.X)
                || (p.Y < A.Y && p.Y < B.Y) || (p.Y > A.Y && p.Y > B.Y))
                return false;

            // exclude endpoints
            if ((AlmostEqual(p.X, A.X, tolerance) && AlmostEqual(p.Y, A.Y, tolerance))
                || (AlmostEqual(p.X, B.X, tolerance) && AlmostEqual(p.Y, B.Y, tolerance)))
                return false;

            var cross = (p.Y - A.Y) * (B.X - A.X) - (p.X - A.X) * (B.Y - A.Y);
            if (Math.Abs(cross) > tolerance)
                return false;

            var dot = (p.X - A.X) * (B.X - A.X) + (p.Y - A.Y) * (B.Y - A.Y);
            if (dot < 0 || AlmostEqual(dot, 0, tolerance))
                return false;

            var len2 = (B.X - A.X) * (B.X - A.X) + (B.Y - A.Y) * (B.Y - A.Y);
            if (dot > len2 || AlmostEqual(dot, len2, tolerance))
                return false;

            return true;
        }

        /// <summary>
        /// Returns intersection point of line segments AB and EF, or null.
        /// If infinite=true, treats them as infinite lines.
        /// </summary>
        public static NestPoint? LineIntersect(NestPoint A, NestPoint B, NestPoint E, NestPoint F, bool infinite = false)
        {
            double a1 = B.Y - A.Y;
            double b1 = A.X - B.X;
            double c1 = B.X * A.Y - A.X * B.Y;
            double a2 = F.Y - E.Y;
            double b2 = E.X - F.X;
            double c2 = F.X * E.Y - E.X * F.Y;

            double denom = a1 * b2 - a2 * b1;
            double x = (b1 * c2 - b2 * c1) / denom;
            double y = (a2 * c1 - a1 * c2) / denom;

            if (!double.IsFinite(x) || !double.IsFinite(y))
                return null;

            if (!infinite)
            {
                if (Math.Abs(A.X - B.X) > TOL && ((A.X < B.X) ? x < A.X || x > B.X : x > A.X || x < B.X)) return null;
                if (Math.Abs(A.Y - B.Y) > TOL && ((A.Y < B.Y) ? y < A.Y || y > B.Y : y > A.Y || y < B.Y)) return null;
                if (Math.Abs(E.X - F.X) > TOL && ((E.X < F.X) ? x < E.X || x > F.X : x > E.X || x < F.X)) return null;
                if (Math.Abs(E.Y - F.Y) > TOL && ((E.Y < F.Y) ? y < E.Y || y > F.Y : y > E.Y || y < F.Y)) return null;
            }

            return new NestPoint(x, y);
        }

        public static RectangleBounds? GetPolygonBounds(List<NestPoint> polygon)
        {
            if (polygon == null || polygon.Count < 3)
                return null;

            double xmin = polygon[0].X, xmax = polygon[0].X;
            double ymin = polygon[0].Y, ymax = polygon[0].Y;

            for (int i = 1; i < polygon.Count; i++)
            {
                if (polygon[i].X > xmax) xmax = polygon[i].X;
                else if (polygon[i].X < xmin) xmin = polygon[i].X;
                if (polygon[i].Y > ymax) ymax = polygon[i].Y;
                else if (polygon[i].Y < ymin) ymin = polygon[i].Y;
            }

            return new RectangleBounds(xmin, ymin, xmax - xmin, ymax - ymin);
        }

        /// <summary>
        /// Returns true if point is inside polygon, false if outside, null if on edge.
        /// </summary>
        public static bool? PointInPolygon(NestPoint point, List<NestPoint> polygon, double offsetX = 0, double offsetY = 0, double tolerance = 0)
        {
            if (polygon == null || polygon.Count < 3)
                return null;

            if (tolerance == 0) tolerance = TOL;

            bool inside = false;

            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                double xi = polygon[i].X + offsetX;
                double yi = polygon[i].Y + offsetY;
                double xj = polygon[j].X + offsetX;
                double yj = polygon[j].Y + offsetY;

                if (AlmostEqual(xi, point.X, tolerance) && AlmostEqual(yi, point.Y, tolerance))
                    return null;

                if (OnSegment(new NestPoint(xi, yi), new NestPoint(xj, yj), point, tolerance))
                    return null;

                if (AlmostEqual(xi, xj, tolerance) && AlmostEqual(yi, yj, tolerance))
                    continue;

                bool intersect = ((yi > point.Y) != (yj > point.Y))
                    && (point.X < (xj - xi) * (point.Y - yi) / (yj - yi) + xi);
                if (intersect) inside = !inside;
            }

            return inside;
        }

        /// <summary>
        /// Signed polygon area. Negative = CCW winding.
        /// </summary>
        public static double PolygonArea(List<NestPoint> polygon)
        {
            double area = 0;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                area += (polygon[j].X + polygon[i].X) * (polygon[j].Y - polygon[i].Y);
            }
            return 0.5 * area;
        }

        /// <summary>
        /// Returns true if polygons A and B intersect (share interior area).
        /// </summary>
        public static bool PolygonsIntersect(NestPolygon A, NestPolygon B)
        {
            double aox = A.OffsetX, aoy = A.OffsetY;
            double box = B.OffsetX, boy = B.OffsetY;

            var ap = A.Points;
            var bp = B.Points;

            for (int i = 0; i < ap.Count - 1; i++)
            {
                for (int j = 0; j < bp.Count - 1; j++)
                {
                    var a1 = new NestPoint(ap[i].X + aox, ap[i].Y + aoy);
                    var a2 = new NestPoint(ap[i + 1].X + aox, ap[i + 1].Y + aoy);
                    var b1 = new NestPoint(bp[j].X + box, bp[j].Y + boy);
                    var b2 = new NestPoint(bp[j + 1].X + box, bp[j + 1].Y + boy);

                    int prevbi = (j == 0) ? bp.Count - 1 : j - 1;
                    int prevai = (i == 0) ? ap.Count - 1 : i - 1;
                    int nextbi = (j + 1 == bp.Count - 1) ? 0 : j + 2;
                    int nextai = (i + 1 == ap.Count - 1) ? 0 : i + 2;

                    if (AlmostEqual(bp[prevbi].X, bp[j].X) && AlmostEqual(bp[prevbi].Y, bp[j].Y))
                        prevbi = (prevbi == 0) ? bp.Count - 1 : prevbi - 1;
                    if (AlmostEqual(ap[prevai].X, ap[i].X) && AlmostEqual(ap[prevai].Y, ap[i].Y))
                        prevai = (prevai == 0) ? ap.Count - 1 : prevai - 1;
                    if (AlmostEqual(bp[nextbi].X, bp[j + 1].X) && AlmostEqual(bp[nextbi].Y, bp[j + 1].Y))
                        nextbi = (nextbi == bp.Count - 1) ? 0 : nextbi + 1;
                    if (AlmostEqual(ap[nextai].X, ap[i + 1].X) && AlmostEqual(ap[nextai].Y, ap[i + 1].Y))
                        nextai = (nextai == ap.Count - 1) ? 0 : nextai + 1;

                    var a0 = new NestPoint(ap[prevai].X + aox, ap[prevai].Y + aoy);
                    var b0 = new NestPoint(bp[prevbi].X + box, bp[prevbi].Y + boy);
                    var a3 = new NestPoint(ap[nextai].X + aox, ap[nextai].Y + aoy);
                    var b3 = new NestPoint(bp[nextbi].X + box, bp[nextbi].Y + boy);

                    if (OnSegment(a1, a2, b1) || (AlmostEqual(a1.X, b1.X) && AlmostEqual(a1.Y, b1.Y)))
                    {
                        var b0in = PointInPolygon(b0, ap, aox, aoy);
                        var b2in = PointInPolygon(b2, ap, aox, aoy);
                        if ((b0in == true && b2in == false) || (b0in == false && b2in == true))
                            return true;
                        continue;
                    }

                    if (OnSegment(a1, a2, b2) || (AlmostEqual(a2.X, b2.X) && AlmostEqual(a2.Y, b2.Y)))
                    {
                        var b1in = PointInPolygon(b1, ap, aox, aoy);
                        var b3in = PointInPolygon(b3, ap, aox, aoy);
                        if ((b1in == true && b3in == false) || (b1in == false && b3in == true))
                            return true;
                        continue;
                    }

                    if (OnSegment(b1, b2, a1) || (AlmostEqual(a1.X, b2.X) && AlmostEqual(a1.Y, b2.Y)))
                    {
                        var a0in = PointInPolygon(a0, bp, box, boy);
                        var a2in = PointInPolygon(a2, bp, box, boy);
                        if ((a0in == true && a2in == false) || (a0in == false && a2in == true))
                            return true;
                        continue;
                    }

                    if (OnSegment(b1, b2, a2) || (AlmostEqual(a2.X, b1.X) && AlmostEqual(a2.Y, b1.Y)))
                    {
                        var a1in = PointInPolygon(a1, bp, box, boy);
                        var a3in = PointInPolygon(a3, bp, box, boy);
                        if ((a1in == true && a3in == false) || (a1in == false && a3in == true))
                            return true;
                        continue;
                    }

                    var p = LineIntersect(b1, b2, a1, a2);
                    if (p != null)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Normal distance from point p to line segment s1-s2 along the given normal direction.
        /// </summary>
        public static double? PointLineDistance(NestPoint p, NestPoint s1, NestPoint s2, NestPoint normal, bool s1inclusive = false, bool s2inclusive = false)
        {
            normal = NormalizeVector(normal);
            var dir = new NestPoint(normal.Y, -normal.X);

            double pdot = p.X * dir.X + p.Y * dir.Y;
            double s1dot = s1.X * dir.X + s1.Y * dir.Y;
            double s2dot = s2.X * dir.X + s2.Y * dir.Y;

            double pdotnorm = p.X * normal.X + p.Y * normal.Y;
            double s1dotnorm = s1.X * normal.X + s1.Y * normal.Y;
            double s2dotnorm = s2.X * normal.X + s2.Y * normal.Y;

            if (AlmostEqual(pdot, s1dot) && AlmostEqual(pdot, s2dot))
            {
                if (AlmostEqual(pdotnorm, s1dotnorm)) return null;
                if (AlmostEqual(pdotnorm, s2dotnorm)) return null;

                if (pdotnorm > s1dotnorm && pdotnorm > s2dotnorm)
                    return Math.Min(pdotnorm - s1dotnorm, pdotnorm - s2dotnorm);
                if (pdotnorm < s1dotnorm && pdotnorm < s2dotnorm)
                    return -Math.Min(s1dotnorm - pdotnorm, s2dotnorm - pdotnorm);

                double diff1 = pdotnorm - s1dotnorm;
                double diff2 = pdotnorm - s2dotnorm;
                return diff1 > 0 ? diff1 : diff2;
            }

            if (AlmostEqual(pdot, s1dot))
                return s1inclusive ? pdotnorm - s1dotnorm : (double?)null;
            if (AlmostEqual(pdot, s2dot))
                return s2inclusive ? pdotnorm - s2dotnorm : (double?)null;

            if ((pdot < s1dot && pdot < s2dot) || (pdot > s1dot && pdot > s2dot))
                return null;

            return pdotnorm - s1dotnorm + (s1dotnorm - s2dotnorm) * (s1dot - pdot) / (s1dot - s2dot);
        }

        /// <summary>
        /// Point-to-segment distance along direction (for segment distance calculation).
        /// </summary>
        public static double? PointDistance(NestPoint p, NestPoint s1, NestPoint s2, NestPoint normal, bool infinite = false)
        {
            normal = NormalizeVector(normal);
            var dir = new NestPoint(normal.Y, -normal.X);

            double pdot = p.X * dir.X + p.Y * dir.Y;
            double s1dot = s1.X * dir.X + s1.Y * dir.Y;
            double s2dot = s2.X * dir.X + s2.Y * dir.Y;

            double pdotnorm = p.X * normal.X + p.Y * normal.Y;
            double s1dotnorm = s1.X * normal.X + s1.Y * normal.Y;
            double s2dotnorm = s2.X * normal.X + s2.Y * normal.Y;

            if (!infinite)
            {
                if (((pdot < s1dot || AlmostEqual(pdot, s1dot)) && (pdot < s2dot || AlmostEqual(pdot, s2dot)))
                    || ((pdot > s1dot || AlmostEqual(pdot, s1dot)) && (pdot > s2dot || AlmostEqual(pdot, s2dot))))
                    return null;
                if (AlmostEqual(pdot, s1dot) && AlmostEqual(pdot, s2dot) && pdotnorm > s1dotnorm && pdotnorm > s2dotnorm)
                    return Math.Min(pdotnorm - s1dotnorm, pdotnorm - s2dotnorm);
                if (AlmostEqual(pdot, s1dot) && AlmostEqual(pdot, s2dot) && pdotnorm < s1dotnorm && pdotnorm < s2dotnorm)
                    return -Math.Min(s1dotnorm - pdotnorm, s2dotnorm - pdotnorm);
            }

            return -(pdotnorm - s1dotnorm + (s1dotnorm - s2dotnorm) * (s1dot - pdot) / (s1dot - s2dot));
        }

        /// <summary>
        /// Distance between two line segments AB and EF along the given direction.
        /// </summary>
        public static double? SegmentDistance(NestPoint A, NestPoint B, NestPoint E, NestPoint F, NestPoint direction)
        {
            var normal = new NestPoint(direction.Y, -direction.X);
            var reverse = new NestPoint(-direction.X, -direction.Y);

            double dotA = A.X * normal.X + A.Y * normal.Y;
            double dotB = B.X * normal.X + B.Y * normal.Y;
            double dotE = E.X * normal.X + E.Y * normal.Y;
            double dotF = F.X * normal.X + F.Y * normal.Y;

            double crossA = A.X * direction.X + A.Y * direction.Y;
            double crossB = B.X * direction.X + B.Y * direction.Y;
            double crossE = E.X * direction.X + E.Y * direction.Y;
            double crossF = F.X * direction.X + F.Y * direction.Y;

            double crossABmin = Math.Min(crossA, crossB);
            double crossABmax = Math.Max(crossA, crossB);
            double crossEFmin = Math.Min(crossE, crossF);
            double crossEFmax = Math.Max(crossE, crossF);

            double ABmin = Math.Min(dotA, dotB);
            double ABmax = Math.Max(dotA, dotB);
            double EFmin = Math.Min(dotE, dotF);
            double EFmax = Math.Max(dotE, dotF);

            if (AlmostEqual(ABmax, EFmin, TOL) || AlmostEqual(ABmin, EFmax, TOL))
                return null;
            if (ABmax < EFmin || ABmin > EFmax)
                return null;

            double overlap;
            if ((ABmax > EFmax && ABmin < EFmin) || (EFmax > ABmax && EFmin < ABmin))
            {
                overlap = 1;
            }
            else
            {
                double minMax = Math.Min(ABmax, EFmax);
                double maxMin = Math.Max(ABmin, EFmin);
                double maxMax = Math.Max(ABmax, EFmax);
                double minMin = Math.Min(ABmin, EFmin);
                overlap = (minMax - maxMin) / (maxMax - minMin);
            }

            double crossABE = (E.Y - A.Y) * (B.X - A.X) - (E.X - A.X) * (B.Y - A.Y);
            double crossABF = (F.Y - A.Y) * (B.X - A.X) - (F.X - A.X) * (B.Y - A.Y);

            // colinear lines
            if (AlmostEqual(crossABE, 0) && AlmostEqual(crossABF, 0))
            {
                var ABnorm = new NestPoint(B.Y - A.Y, A.X - B.X);
                var EFnorm = new NestPoint(F.Y - E.Y, E.X - F.X);

                double ABnormLen = Math.Sqrt(ABnorm.X * ABnorm.X + ABnorm.Y * ABnorm.Y);
                ABnorm = new NestPoint(ABnorm.X / ABnormLen, ABnorm.Y / ABnormLen);

                double EFnormLen = Math.Sqrt(EFnorm.X * EFnorm.X + EFnorm.Y * EFnorm.Y);
                EFnorm = new NestPoint(EFnorm.X / EFnormLen, EFnorm.Y / EFnormLen);

                if (Math.Abs(ABnorm.Y * EFnorm.X - ABnorm.X * EFnorm.Y) < TOL
                    && ABnorm.Y * EFnorm.Y + ABnorm.X * EFnorm.X < 0)
                {
                    double normdot = ABnorm.Y * direction.Y + ABnorm.X * direction.X;
                    if (AlmostEqual(normdot, 0, TOL))
                        return null;
                    if (normdot < 0)
                        return 0;
                }
                return null;
            }

            var distances = new List<double>();

            if (AlmostEqual(dotA, dotE))
                distances.Add(crossA - crossE);
            else if (AlmostEqual(dotA, dotF))
                distances.Add(crossA - crossF);
            else if (dotA > EFmin && dotA < EFmax)
            {
                var d = PointDistance(A, E, F, reverse);
                if (d != null && AlmostEqual(d.Value, 0))
                {
                    var dB = PointDistance(B, E, F, reverse, true);
                    if (dB < 0 || AlmostEqual(dB.GetValueOrDefault() * overlap, 0))
                        d = null;
                }
                if (d != null) distances.Add(d.Value);
            }

            if (AlmostEqual(dotB, dotE))
                distances.Add(crossB - crossE);
            else if (AlmostEqual(dotB, dotF))
                distances.Add(crossB - crossF);
            else if (dotB > EFmin && dotB < EFmax)
            {
                var d = PointDistance(B, E, F, reverse);
                if (d != null && AlmostEqual(d.Value, 0))
                {
                    var dA = PointDistance(A, E, F, reverse, true);
                    if (dA < 0 || AlmostEqual(dA.GetValueOrDefault() * overlap, 0))
                        d = null;
                }
                if (d != null) distances.Add(d.Value);
            }

            if (dotE > ABmin && dotE < ABmax)
            {
                var d = PointDistance(E, A, B, direction);
                if (d != null && AlmostEqual(d.Value, 0))
                {
                    var dF = PointDistance(F, A, B, direction, true);
                    if (dF < 0 || AlmostEqual(dF.GetValueOrDefault() * overlap, 0))
                        d = null;
                }
                if (d != null) distances.Add(d.Value);
            }

            if (dotF > ABmin && dotF < ABmax)
            {
                var d = PointDistance(F, A, B, direction);
                if (d != null && AlmostEqual(d.Value, 0))
                {
                    var dE = PointDistance(E, A, B, direction, true);
                    if (dE < 0 || AlmostEqual(dE.GetValueOrDefault() * overlap, 0))
                        d = null;
                }
                if (d != null) distances.Add(d.Value);
            }

            if (distances.Count == 0)
                return null;

            double min = distances[0];
            for (int i = 1; i < distances.Count; i++)
                if (distances[i] < min) min = distances[i];
            return min;
        }

        /// <summary>
        /// Slide distance between two polygons along a direction vector.
        /// </summary>
        public static double? PolygonSlideDistance(List<NestPoint> A, double aox, double aoy,
            List<NestPoint> B, double box, double boy,
            NestPoint direction, bool ignoreNegative)
        {
            var dir = NormalizeVector(direction);

            // create closed copies
            var edgeA = new List<NestPoint>(A);
            if (edgeA.Count > 0 && !AlmostEqualPoints(edgeA[0], edgeA[edgeA.Count - 1]))
                edgeA.Add(edgeA[0]);

            var edgeB = new List<NestPoint>(B);
            if (edgeB.Count > 0 && !AlmostEqualPoints(edgeB[0], edgeB[edgeB.Count - 1]))
                edgeB.Add(edgeB[0]);

            double? distance = null;

            for (int i = 0; i < edgeB.Count - 1; i++)
            {
                for (int j = 0; j < edgeA.Count - 1; j++)
                {
                    var A1 = new NestPoint(edgeA[j].X + aox, edgeA[j].Y + aoy);
                    var A2 = new NestPoint(edgeA[j + 1].X + aox, edgeA[j + 1].Y + aoy);
                    var B1 = new NestPoint(edgeB[i].X + box, edgeB[i].Y + boy);
                    var B2 = new NestPoint(edgeB[i + 1].X + box, edgeB[i + 1].Y + boy);

                    if ((AlmostEqual(A1.X, A2.X) && AlmostEqual(A1.Y, A2.Y))
                        || (AlmostEqual(B1.X, B2.X) && AlmostEqual(B1.Y, B2.Y)))
                        continue;

                    var d = SegmentDistance(A1, A2, B1, B2, dir);

                    if (d != null && (distance == null || d.Value < distance.Value))
                    {
                        if (!ignoreNegative || d.Value > 0 || AlmostEqual(d.Value, 0))
                            distance = d;
                    }
                }
            }
            return distance;
        }

        /// <summary>
        /// Project each point of B onto A in the given direction.
        /// </summary>
        public static double? PolygonProjectionDistance(List<NestPoint> A, double aox, double aoy,
            List<NestPoint> B, double box, double boy,
            NestPoint direction)
        {
            var edgeA = new List<NestPoint>(A);
            if (edgeA.Count > 0 && !AlmostEqualPoints(edgeA[0], edgeA[edgeA.Count - 1]))
                edgeA.Add(edgeA[0]);

            var edgeB = new List<NestPoint>(B);
            if (edgeB.Count > 0 && !AlmostEqualPoints(edgeB[0], edgeB[edgeB.Count - 1]))
                edgeB.Add(edgeB[0]);

            double? distance = null;

            for (int i = 0; i < edgeB.Count; i++)
            {
                double? minprojection = null;
                for (int j = 0; j < edgeA.Count - 1; j++)
                {
                    var p = new NestPoint(edgeB[i].X + box, edgeB[i].Y + boy);
                    var s1 = new NestPoint(edgeA[j].X + aox, edgeA[j].Y + aoy);
                    var s2 = new NestPoint(edgeA[j + 1].X + aox, edgeA[j + 1].Y + aoy);

                    if (Math.Abs((s2.Y - s1.Y) * direction.X - (s2.X - s1.X) * direction.Y) < TOL)
                        continue;

                    var d = PointDistance(p, s1, s2, direction);
                    if (d != null && (minprojection == null || d.Value < minprojection.Value))
                        minprojection = d;
                }
                if (minprojection != null && (distance == null || minprojection.Value > distance.Value))
                    distance = minprojection;
            }
            return distance;
        }

        /// <summary>
        /// Check if a polygon is a rectangle (all vertices on bounding box edges).
        /// </summary>
        public static bool IsRectangle(List<NestPoint> poly, double tolerance = 0)
        {
            var bb = GetPolygonBounds(poly);
            if (bb == null) return false;
            var b = bb.Value;
            if (tolerance == 0) tolerance = TOL;

            for (int i = 0; i < poly.Count; i++)
            {
                if (!AlmostEqual(poly[i].X, b.X) && !AlmostEqual(poly[i].X, b.X + b.Width))
                    return false;
                if (!AlmostEqual(poly[i].Y, b.Y) && !AlmostEqual(poly[i].Y, b.Y + b.Height))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Fast inner NFP for the special case where A is a rectangle.
        /// Returns null if B doesn't fit inside A.
        /// </summary>
        public static List<List<NestPoint>> NoFitPolygonRectangle(List<NestPoint> A, List<NestPoint> B)
        {
            double minAx = A[0].X, minAy = A[0].Y, maxAx = A[0].X, maxAy = A[0].Y;
            for (int i = 1; i < A.Count; i++)
            {
                if (A[i].X < minAx) minAx = A[i].X;
                if (A[i].Y < minAy) minAy = A[i].Y;
                if (A[i].X > maxAx) maxAx = A[i].X;
                if (A[i].Y > maxAy) maxAy = A[i].Y;
            }

            double minBx = B[0].X, minBy = B[0].Y, maxBx = B[0].X, maxBy = B[0].Y;
            for (int i = 1; i < B.Count; i++)
            {
                if (B[i].X < minBx) minBx = B[i].X;
                if (B[i].Y < minBy) minBy = B[i].Y;
                if (B[i].X > maxBx) maxBx = B[i].X;
                if (B[i].Y > maxBy) maxBy = B[i].Y;
            }

            if (maxBx - minBx > maxAx - minAx) return null;
            if (maxBy - minBy > maxAy - minAy) return null;

            return new List<List<NestPoint>>
            {
                new List<NestPoint>
                {
                    new NestPoint(minAx - minBx + B[0].X, minAy - minBy + B[0].Y),
                    new NestPoint(maxAx - maxBx + B[0].X, minAy - minBy + B[0].Y),
                    new NestPoint(maxAx - maxBx + B[0].X, maxAy - maxBy + B[0].Y),
                    new NestPoint(minAx - minBx + B[0].X, maxAy - maxBy + B[0].Y)
                }
            };
        }

        /// <summary>
        /// Rotate polygon points by the given angle in degrees about the origin.
        /// </summary>
        public static NestPolygon RotatePolygon(NestPolygon polygon, double degrees)
        {
            var rotated = new NestPolygon
            {
                Id = polygon.Id,
                Source = polygon.Source,
                Rotation = degrees,
            };

            double angle = degrees * Math.PI / 180.0;
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);

            for (int i = 0; i < polygon.Points.Count; i++)
            {
                double x = polygon.Points[i].X;
                double y = polygon.Points[i].Y;
                rotated.Points.Add(new NestPoint(
                    x * cos - y * sin,
                    x * sin + y * cos,
                    polygon.Points[i].Exact));
            }

            if (polygon.Children != null && polygon.Children.Count > 0)
            {
                rotated.Children = new List<NestPolygon>();
                foreach (var child in polygon.Children)
                    rotated.Children.Add(RotatePolygon(child, degrees));
            }

            return rotated;
        }

        /// <summary>
        /// Rotate a flat list of points by degrees about the origin. Returns a new list.
        /// </summary>
        public static List<NestPoint> RotatePoints(List<NestPoint> points, double degrees)
        {
            var rotated = new List<NestPoint>(points.Count);
            double angle = degrees * Math.PI / 180.0;
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);

            for (int i = 0; i < points.Count; i++)
            {
                double x = points[i].X;
                double y = points[i].Y;
                rotated.Add(new NestPoint(
                    x * cos - y * sin,
                    x * sin + y * cos,
                    points[i].Exact));
            }
            return rotated;
        }

        /// <summary>
        /// Shift polygon points by the given offset.
        /// </summary>
        public static NestPolygon ShiftPolygon(NestPolygon p, double shiftX, double shiftY)
        {
            var shifted = new NestPolygon
            {
                Id = p.Id,
                Source = p.Source,
                Rotation = p.Rotation
            };

            for (int i = 0; i < p.Points.Count; i++)
            {
                shifted.Points.Add(new NestPoint(p.Points[i].X + shiftX, p.Points[i].Y + shiftY, p.Points[i].Exact));
            }

            if (p.Children != null && p.Children.Count > 0)
            {
                shifted.Children = new List<NestPolygon>();
                foreach (var child in p.Children)
                    shifted.Children.Add(ShiftPolygon(child, shiftX, shiftY));
            }

            return shifted;
        }
    }
}

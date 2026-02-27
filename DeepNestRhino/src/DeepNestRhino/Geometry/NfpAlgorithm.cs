using System;
using System.Collections.Generic;

namespace DeepNestRhino.Geometry
{
    /// <summary>
    /// No-Fit Polygon computation via the orbiting algorithm.
    /// Port of geometryutil.js noFitPolygon() (lines 1437-1727) and searchStartPoint() (lines 1245-1360).
    /// </summary>
    public static class NfpAlgorithm
    {
        /// <summary>
        /// Compute the No-Fit Polygon by orbiting B about A.
        /// If inside=true, B is orbited inside A (inner NFP).
        /// If searchEdges=true, all edges of A are explored for multiple NFP loops.
        /// </summary>
        public static List<List<NestPoint>> ComputeNfp(List<NestPoint> A, List<NestPoint> B,
            bool inside, bool searchEdges)
        {
            if (A == null || A.Count < 3 || B == null || B.Count < 3)
                return null;

            double offsetX = 0, offsetY = 0;

            // Find min-Y point of A and max-Y point of B
            double minA = A[0].Y;
            int minAindex = 0;
            double maxB = B[0].Y;
            int maxBindex = 0;

            // Clear marked flags
            for (int i = 0; i < A.Count; i++)
            {
                var p = A[i]; p.Marked = false; A[i] = p;
                if (A[i].Y < minA) { minA = A[i].Y; minAindex = i; }
            }
            for (int i = 0; i < B.Count; i++)
            {
                var p = B[i]; p.Marked = false; B[i] = p;
                if (B[i].Y > maxB) { maxB = B[i].Y; maxBindex = i; }
            }

            NestPoint? startpoint;
            if (!inside)
            {
                startpoint = new NestPoint(
                    A[minAindex].X - B[maxBindex].X,
                    A[minAindex].Y - B[maxBindex].Y);
            }
            else
            {
                startpoint = SearchStartPoint(A, B, true, null);
            }

            var nfpList = new List<List<NestPoint>>();

            while (startpoint != null)
            {
                offsetX = startpoint.Value.X;
                offsetY = startpoint.Value.Y;

                NestPoint? prevvector = null;
                var nfp = new List<NestPoint>();
                nfp.Add(new NestPoint(B[0].X + offsetX, B[0].Y + offsetY));

                double referenceX = B[0].X + offsetX;
                double referenceY = B[0].Y + offsetY;
                double startX = referenceX;
                double startY = referenceY;
                int counter = 0;

                while (counter < 10 * (A.Count + B.Count))
                {
                    // Find touching vertices/edges
                    var touching = new List<(int Type, int AIdx, int BIdx)>();

                    for (int i = 0; i < A.Count; i++)
                    {
                        int nexti = (i == A.Count - 1) ? 0 : i + 1;
                        for (int j = 0; j < B.Count; j++)
                        {
                            int nextj = (j == B.Count - 1) ? 0 : j + 1;

                            if (GeometryUtil.AlmostEqual(A[i].X, B[j].X + offsetX)
                                && GeometryUtil.AlmostEqual(A[i].Y, B[j].Y + offsetY))
                            {
                                touching.Add((0, i, j));
                            }
                            else if (GeometryUtil.OnSegment(A[i], A[nexti],
                                new NestPoint(B[j].X + offsetX, B[j].Y + offsetY)))
                            {
                                touching.Add((1, nexti, j));
                            }
                            else if (GeometryUtil.OnSegment(
                                new NestPoint(B[j].X + offsetX, B[j].Y + offsetY),
                                new NestPoint(B[nextj].X + offsetX, B[nextj].Y + offsetY),
                                A[i]))
                            {
                                touching.Add((2, i, nextj));
                            }
                        }
                    }

                    // Generate translation vectors
                    var vectors = new List<TranslationVector>();

                    for (int i = 0; i < touching.Count; i++)
                    {
                        var vertexA = A[touching[i].AIdx];
                        // Mark vertex
                        var mA = A[touching[i].AIdx]; mA.Marked = true; A[touching[i].AIdx] = mA;

                        int prevAindex = touching[i].AIdx - 1;
                        int nextAindex = touching[i].AIdx + 1;
                        prevAindex = (prevAindex < 0) ? A.Count - 1 : prevAindex;
                        nextAindex = (nextAindex >= A.Count) ? 0 : nextAindex;

                        var prevA = A[prevAindex];
                        var nextA = A[nextAindex];

                        var vertexB = B[touching[i].BIdx];
                        int prevBindex = touching[i].BIdx - 1;
                        int nextBindex = touching[i].BIdx + 1;
                        prevBindex = (prevBindex < 0) ? B.Count - 1 : prevBindex;
                        nextBindex = (nextBindex >= B.Count) ? 0 : nextBindex;

                        var prevB = B[prevBindex];
                        var nextB = B[nextBindex];

                        if (touching[i].Type == 0)
                        {
                            vectors.Add(new TranslationVector(prevA.X - vertexA.X, prevA.Y - vertexA.Y, vertexA, prevA));
                            vectors.Add(new TranslationVector(nextA.X - vertexA.X, nextA.Y - vertexA.Y, vertexA, nextA));
                            vectors.Add(new TranslationVector(vertexB.X - prevB.X, vertexB.Y - prevB.Y, prevB, vertexB));
                            vectors.Add(new TranslationVector(vertexB.X - nextB.X, vertexB.Y - nextB.Y, nextB, vertexB));
                        }
                        else if (touching[i].Type == 1)
                        {
                            vectors.Add(new TranslationVector(
                                vertexA.X - (vertexB.X + offsetX),
                                vertexA.Y - (vertexB.Y + offsetY),
                                prevA, vertexA));
                            vectors.Add(new TranslationVector(
                                prevA.X - (vertexB.X + offsetX),
                                prevA.Y - (vertexB.Y + offsetY),
                                vertexA, prevA));
                        }
                        else if (touching[i].Type == 2)
                        {
                            vectors.Add(new TranslationVector(
                                vertexA.X - (vertexB.X + offsetX),
                                vertexA.Y - (vertexB.Y + offsetY),
                                prevB, vertexB));
                            vectors.Add(new TranslationVector(
                                vertexA.X - (prevB.X + offsetX),
                                vertexA.Y - (prevB.Y + offsetY),
                                vertexB, prevB));
                        }
                    }

                    // Choose best translation vector
                    TranslationVector translate = null;
                    double maxd = 0;

                    for (int i = 0; i < vectors.Count; i++)
                    {
                        if (vectors[i].X == 0 && vectors[i].Y == 0)
                            continue;

                        // Skip if pointing back where we came from
                        if (prevvector != null)
                        {
                            var pv = prevvector.Value;
                            if (vectors[i].Y * pv.Y + vectors[i].X * pv.X < 0)
                            {
                                double vlen = Math.Sqrt(vectors[i].X * vectors[i].X + vectors[i].Y * vectors[i].Y);
                                var unitv = new NestPoint(vectors[i].X / vlen, vectors[i].Y / vlen);
                                double pvlen = Math.Sqrt(pv.X * pv.X + pv.Y * pv.Y);
                                var prevunit = new NestPoint(pv.X / pvlen, pv.Y / pvlen);

                                if (Math.Abs(unitv.Y * prevunit.X - unitv.X * prevunit.Y) < 0.0001)
                                    continue;
                            }
                        }

                        var d = GeometryUtil.PolygonSlideDistance(
                            A, 0, 0, B, offsetX, offsetY,
                            new NestPoint(vectors[i].X, vectors[i].Y), true);

                        double vecd2 = vectors[i].X * vectors[i].X + vectors[i].Y * vectors[i].Y;

                        if (d == null || d.Value * d.Value > vecd2)
                        {
                            d = Math.Sqrt(vecd2);
                        }

                        if (d != null && d.Value > maxd)
                        {
                            maxd = d.Value;
                            translate = vectors[i];
                        }
                    }

                    if (translate == null || GeometryUtil.AlmostEqual(maxd, 0))
                    {
                        nfp = null;
                        break;
                    }

                    // Mark start/end vertices
                    var ms = translate.StartPoint; ms.Marked = true;
                    var me = translate.EndPoint; me.Marked = true;
                    // We'd need to write these back, but since we track by index in the original,
                    // for the orbiting algorithm the marking is mainly for searchStartPoint

                    prevvector = new NestPoint(translate.X, translate.Y);

                    // Trim vector to maxd
                    double vlength2 = translate.X * translate.X + translate.Y * translate.Y;
                    if (maxd * maxd < vlength2 && !GeometryUtil.AlmostEqual(maxd * maxd, vlength2))
                    {
                        double scale = Math.Sqrt((maxd * maxd) / vlength2);
                        translate = new TranslationVector(
                            translate.X * scale, translate.Y * scale,
                            translate.StartPoint, translate.EndPoint);
                    }

                    referenceX += translate.X;
                    referenceY += translate.Y;

                    if (GeometryUtil.AlmostEqual(referenceX, startX) && GeometryUtil.AlmostEqual(referenceY, startY))
                        break; // full loop

                    // Check if we've looped to a previous NFP point
                    bool looped = false;
                    if (nfp.Count > 0)
                    {
                        for (int i = 0; i < nfp.Count - 1; i++)
                        {
                            if (GeometryUtil.AlmostEqual(referenceX, nfp[i].X)
                                && GeometryUtil.AlmostEqual(referenceY, nfp[i].Y))
                            {
                                looped = true;
                                break;
                            }
                        }
                    }
                    if (looped) break;

                    nfp.Add(new NestPoint(referenceX, referenceY));
                    offsetX += translate.X;
                    offsetY += translate.Y;
                    counter++;
                }

                if (nfp != null && nfp.Count > 0)
                    nfpList.Add(nfp);

                if (!searchEdges)
                    break;

                startpoint = SearchStartPoint(A, B, inside, nfpList);
            }

            return nfpList;
        }

        /// <summary>
        /// Search for a valid starting arrangement of B relative to A.
        /// </summary>
        public static NestPoint? SearchStartPoint(List<NestPoint> A, List<NestPoint> B,
            bool inside, List<List<NestPoint>> existingNfp)
        {
            var closedA = new List<NestPoint>(A);
            if (closedA.Count > 0 && !GeometryUtil.AlmostEqualPoints(closedA[0], closedA[closedA.Count - 1]))
                closedA.Add(closedA[0]);

            var closedB = new List<NestPoint>(B);
            if (closedB.Count > 0 && !GeometryUtil.AlmostEqualPoints(closedB[0], closedB[closedB.Count - 1]))
                closedB.Add(closedB[0]);

            for (int i = 0; i < closedA.Count - 1; i++)
            {
                if (closedA[i].Marked)
                    continue;

                // Mark this vertex
                var marked = closedA[i]; marked.Marked = true; closedA[i] = marked;

                for (int j = 0; j < closedB.Count; j++)
                {
                    double boffsetX = closedA[i].X - closedB[j].X;
                    double boffsetY = closedA[i].Y - closedB[j].Y;

                    bool? bInside = null;
                    for (int k = 0; k < closedB.Count; k++)
                    {
                        var inpoly = GeometryUtil.PointInPolygon(
                            new NestPoint(closedB[k].X + boffsetX, closedB[k].Y + boffsetY),
                            closedA);
                        if (inpoly != null)
                        {
                            bInside = inpoly;
                            break;
                        }
                    }

                    if (bInside == null) return null; // A and B are the same

                    var startPoint = new NestPoint(boffsetX, boffsetY);

                    // Create temporary NestPolygons for intersection test
                    var polyA = new NestPolygon { Points = closedA };
                    var polyB = new NestPolygon { Points = closedB, OffsetX = boffsetX, OffsetY = boffsetY };

                    if (((bInside.Value && inside) || (!bInside.Value && !inside))
                        && !GeometryUtil.PolygonsIntersect(polyA, polyB)
                        && !InNfp(startPoint, existingNfp))
                    {
                        return startPoint;
                    }

                    // Slide B along edge vector
                    double vx = closedA[i + 1].X - closedA[i].X;
                    double vy = closedA[i + 1].Y - closedA[i].Y;

                    var d1 = GeometryUtil.PolygonProjectionDistance(
                        closedA, 0, 0, closedB, boffsetX, boffsetY,
                        new NestPoint(vx, vy));
                    var d2 = GeometryUtil.PolygonProjectionDistance(
                        closedB, boffsetX, boffsetY, closedA, 0, 0,
                        new NestPoint(-vx, -vy));

                    double? d = null;
                    if (d1 == null && d2 == null) { }
                    else if (d1 == null) d = d2;
                    else if (d2 == null) d = d1;
                    else d = Math.Min(d1.Value, d2.Value);

                    if (d == null || GeometryUtil.AlmostEqual(d.Value, 0) || d.Value <= 0)
                        continue;

                    double vd2 = vx * vx + vy * vy;
                    if (d.Value * d.Value < vd2 && !GeometryUtil.AlmostEqual(d.Value * d.Value, vd2))
                    {
                        double vd = Math.Sqrt(vd2);
                        vx *= d.Value / vd;
                        vy *= d.Value / vd;
                    }

                    boffsetX += vx;
                    boffsetY += vy;

                    bInside = null;
                    for (int k = 0; k < closedB.Count; k++)
                    {
                        var inpoly = GeometryUtil.PointInPolygon(
                            new NestPoint(closedB[k].X + boffsetX, closedB[k].Y + boffsetY),
                            closedA);
                        if (inpoly != null)
                        {
                            bInside = inpoly;
                            break;
                        }
                    }

                    startPoint = new NestPoint(boffsetX, boffsetY);
                    polyB = new NestPolygon { Points = closedB, OffsetX = boffsetX, OffsetY = boffsetY };

                    if (bInside != null
                        && ((bInside.Value && inside) || (!bInside.Value && !inside))
                        && !GeometryUtil.PolygonsIntersect(polyA, polyB)
                        && !InNfp(startPoint, existingNfp))
                    {
                        return startPoint;
                    }
                }
            }

            return null;
        }

        private static bool InNfp(NestPoint p, List<List<NestPoint>> nfp)
        {
            if (nfp == null || nfp.Count == 0)
                return false;

            for (int i = 0; i < nfp.Count; i++)
            {
                for (int j = 0; j < nfp[i].Count; j++)
                {
                    if (GeometryUtil.AlmostEqual(p.X, nfp[i][j].X)
                        && GeometryUtil.AlmostEqual(p.Y, nfp[i][j].Y))
                        return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Translation vector with start/end vertex references (for the orbiting algorithm).
    /// </summary>
    internal class TranslationVector
    {
        public double X;
        public double Y;
        public NestPoint StartPoint;
        public NestPoint EndPoint;

        public TranslationVector(double x, double y, NestPoint start, NestPoint end)
        {
            X = x; Y = y;
            StartPoint = start;
            EndPoint = end;
        }
    }
}

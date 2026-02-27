using System;
using System.Collections.Generic;
using Clipper2Lib;
using DeepNestRhino.Core;
using DeepNestRhino.Geometry;

namespace DeepNestRhino.Algorithm
{
    /// <summary>
    /// Evaluates a single GA individual by placing parts on sheets.
    /// Port of placeParts() from background.js (lines 804-1173).
    /// </summary>
    public class PlacementWorker
    {
        private readonly NfpGenerator _nfpGen;
        private readonly NestingConfig _config;

        public PlacementWorker(NfpGenerator nfpGen, NestingConfig config)
        {
            _nfpGen = nfpGen;
            _config = config;
        }

        /// <summary>
        /// Place all parts on the given sheets and return the fitness result.
        /// </summary>
        public NestResult PlaceParts(List<NestPolygon> sheets, List<NestPolygon> parts)
        {
            if (sheets == null || sheets.Count == 0)
                return null;

            int totalNum = parts.Count;
            double totalSheetArea = 0;
            double totalMerged = 0;

            // Rotate parts by their assigned rotation
            var rotated = new List<NestPolygon>();
            for (int i = 0; i < parts.Count; i++)
            {
                var r = GeometryUtil.RotatePolygon(parts[i], parts[i].Rotation);
                r.Rotation = parts[i].Rotation;
                r.Source = parts[i].Source;
                r.Id = parts[i].Id;
                rotated.Add(r);
            }

            var remaining = new List<NestPolygon>(rotated);
            var allPlacements = new List<SheetPlacement>();
            double fitness = 0;
            int sheetIndex = 0;

            while (remaining.Count > 0 && sheetIndex < sheets.Count)
            {
                var placed = new List<NestPolygon>();
                var placements = new List<PartPlacement>();

                var sheet = sheets[sheetIndex];
                double sheetArea = Math.Abs(GeometryUtil.PolygonArea(sheet.Points));
                totalSheetArea += sheetArea;
                fitness += sheetArea;

                double minWidth = 0;
                double minArea = 0;

                for (int i = 0; i < remaining.Count; i++)
                {
                    var part = remaining[i];

                    // Get inner NFP (where part can go inside sheet)
                    List<NestPolygon> sheetNfp = null;

                    // Try all rotations for first part on each sheet
                    for (int j = 0; j < (placed.Count == 0 ? 360 / _config.Rotations : 1); j++)
                    {
                        sheetNfp = _nfpGen.GetInnerNfp(sheet, part);
                        if (sheetNfp != null)
                            break;

                        // Try next rotation
                        var r = GeometryUtil.RotatePolygon(part, 360.0 / _config.Rotations);
                        r.Rotation = part.Rotation + (360.0 / _config.Rotations);
                        r.Source = part.Source;
                        r.Id = part.Id;
                        part = r;
                        remaining[i] = r;

                        if (part.Rotation > 360)
                            part.Rotation %= 360;
                    }

                    if (sheetNfp == null || sheetNfp.Count == 0)
                        continue;

                    PartPlacement position = null;

                    if (placed.Count == 0)
                    {
                        // First part: place at top-left corner
                        for (int j = 0; j < sheetNfp.Count; j++)
                        {
                            for (int k = 0; k < sheetNfp[j].Points.Count; k++)
                            {
                                double px = sheetNfp[j].Points[k].X - part.Points[0].X;
                                double py = sheetNfp[j].Points[k].Y - part.Points[0].Y;

                                if (position == null || px < position.X
                                    || (GeometryUtil.AlmostEqual(px, position.X) && py < position.Y))
                                {
                                    position = new PartPlacement
                                    {
                                        X = px, Y = py,
                                        Id = part.Id,
                                        Rotation = part.Rotation,
                                        Source = part.Source
                                    };
                                }
                            }
                        }

                        placements.Add(position);
                        placed.Add(part);
                        continue;
                    }

                    // Compute combined exclusion zone from placed parts
                    var combinedNfp = new Paths64();
                    bool error = false;

                    for (int j = 0; j < placed.Count; j++)
                    {
                        var nfp = _nfpGen.GetOuterNfp(placed[j], part);
                        if (nfp == null) { error = true; break; }

                        // Shift NFP to placed position
                        var shifted = new NestPolygon();
                        foreach (var pt in nfp.Points)
                        {
                            shifted.Points.Add(new NestPoint(
                                pt.X + placements[j].X, pt.Y + placements[j].Y));
                        }

                        if (nfp.Children != null)
                        {
                            shifted.Children = new List<NestPolygon>();
                            foreach (var child in nfp.Children)
                            {
                                var shiftedChild = new NestPolygon();
                                foreach (var pt in child.Points)
                                {
                                    shiftedChild.Points.Add(new NestPoint(
                                        pt.X + placements[j].X, pt.Y + placements[j].Y));
                                }
                                shifted.Children.Add(shiftedChild);
                            }
                        }

                        var clipperNfp = ClipperHelper.NfpToClipperPaths(shifted, _config.ClipperScale);
                        combinedNfp.AddRange(clipperNfp);
                    }

                    if (error)
                        continue;

                    // Union all NFPs
                    var united = ClipperHelper.Union(combinedNfp, _config.ClipperScale);

                    // Convert inner NFP to clipper paths
                    var clipperSheetNfp = ClipperHelper.InnerNfpToClipperPaths(sheetNfp, _config.ClipperScale);

                    // Difference: valid positions = sheet NFP minus exclusion zones
                    var finalNfp = ClipperHelper.Difference(clipperSheetNfp, united,
                        FillRule.EvenOdd, FillRule.NonZero);

                    if (finalNfp.Count == 0)
                        continue;

                    // Convert back to nest coordinates
                    var finalNfpPoints = new List<List<NestPoint>>();
                    for (int j = 0; j < finalNfp.Count; j++)
                    {
                        finalNfpPoints.Add(ClipperHelper.FromPath64(finalNfp[j], _config.ClipperScale));
                    }

                    // Choose best position
                    double? minarea = null;
                    double? minx = null;
                    double? miny = null;

                    // Collect all currently placed points for bounding box
                    var allpoints = new List<NestPoint>();
                    for (int m = 0; m < placed.Count; m++)
                    {
                        for (int n = 0; n < placed[m].Points.Count; n++)
                        {
                            allpoints.Add(new NestPoint(
                                placed[m].Points[n].X + placements[m].X,
                                placed[m].Points[n].Y + placements[m].Y));
                        }
                    }

                    var allbounds = GeometryUtil.GetPolygonBounds(allpoints);
                    var partbounds = GeometryUtil.GetPolygonBounds(part.Points);

                    for (int j = 0; j < finalNfpPoints.Count; j++)
                    {
                        for (int k = 0; k < finalNfpPoints[j].Count; k++)
                        {
                            var sv = new PartPlacement
                            {
                                X = finalNfpPoints[j][k].X - part.Points[0].X,
                                Y = finalNfpPoints[j][k].Y - part.Points[0].Y,
                                Id = part.Id,
                                Source = part.Source,
                                Rotation = part.Rotation
                            };

                            double area;
                            RectangleBounds? rectbounds = null;

                            if (allbounds != null && partbounds != null)
                            {
                                var ab = allbounds.Value;
                                var pb = partbounds.Value;

                                rectbounds = GeometryUtil.GetPolygonBounds(new List<NestPoint>
                                {
                                    new NestPoint(ab.X, ab.Y),
                                    new NestPoint(ab.X + ab.Width, ab.Y),
                                    new NestPoint(ab.X + ab.Width, ab.Y + ab.Height),
                                    new NestPoint(ab.X, ab.Y + ab.Height),
                                    new NestPoint(pb.X + sv.X, pb.Y + sv.Y),
                                    new NestPoint(pb.X + pb.Width + sv.X, pb.Y + sv.Y),
                                    new NestPoint(pb.X + pb.Width + sv.X, pb.Y + pb.Height + sv.Y),
                                    new NestPoint(pb.X + sv.X, pb.Y + pb.Height + sv.Y)
                                });

                                if (_config.PlacementType == "gravity")
                                    area = rectbounds.Value.Width * 2 + rectbounds.Value.Height;
                                else
                                    area = rectbounds.Value.Width * rectbounds.Value.Height;
                            }
                            else
                            {
                                area = 0;
                            }

                            if (minarea == null || area < minarea.Value
                                || (GeometryUtil.AlmostEqual(minarea.Value, area) && (minx == null || sv.X < minx.Value))
                                || (GeometryUtil.AlmostEqual(minarea.Value, area) && minx != null
                                    && GeometryUtil.AlmostEqual(sv.X, minx.Value) && sv.Y < (miny ?? double.MaxValue)))
                            {
                                minarea = area;
                                minWidth = rectbounds?.Width ?? 0;
                                position = sv;
                                if (minx == null || sv.X < minx.Value) minx = sv.X;
                                if (miny == null || sv.Y < miny.Value) miny = sv.Y;
                            }
                        }
                    }

                    if (position != null)
                    {
                        placed.Add(part);
                        placements.Add(position);
                    }
                }

                fitness += (minWidth / (totalSheetArea > 0 ? totalSheetArea : 1)) + minArea;

                // Remove placed parts from remaining
                foreach (var p in placed)
                    remaining.Remove(p);

                if (placements.Count > 0)
                {
                    allPlacements.Add(new SheetPlacement
                    {
                        SheetSource = sheet.Source,
                        SheetId = sheet.Id,
                        SheetPlacements = placements
                    });
                }
                else
                {
                    break; // nothing placed, stop
                }

                sheetIndex++;
            }

            // Penalize unplaced parts heavily
            for (int i = 0; i < remaining.Count; i++)
            {
                double partArea = Math.Abs(GeometryUtil.PolygonArea(remaining[i].Points));
                fitness += 100000000 * (partArea / (totalSheetArea > 0 ? totalSheetArea : 1));
            }

            return new NestResult
            {
                Placements = allPlacements,
                Fitness = fitness,
                Area = totalSheetArea,
                MergedLength = totalMerged
            };
        }
    }
}

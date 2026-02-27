using System;
using System.Collections.Generic;
using DeepNestRhino.Core;
using DeepNestRhino.Geometry;

namespace DeepNestRhino.Conversion
{
    /// <summary>
    /// Builds parent/child polygon trees from flat lists of polygons.
    /// Port of toTree() from deepnest.js (lines 673-755).
    /// Detects which polygons are inside others to establish hole relationships.
    /// </summary>
    public static class PolygonTreeBuilder
    {
        /// <summary>
        /// Given a flat list of polygons, build a tree where parent polygons contain
        /// child polygons (holes). Returns only root-level (parentless) polygons.
        /// </summary>
        public static List<NestPolygon> BuildTree(List<NestPolygon> polygons, double clipperScale = ClipperHelper.DefaultScale)
        {
            if (polygons == null || polygons.Count == 0)
                return polygons;

            return ToTree(polygons, 0, clipperScale);
        }

        private static List<NestPolygon> ToTree(List<NestPolygon> list, int idStart, double clipperScale)
        {
            var parents = new List<NestPolygon>();
            int id = idStart;

            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                bool isChild = false;

                for (int j = 0; j < list.Count; j++)
                {
                    if (j == i) continue;
                    if (p.Points.Count < 2) continue;

                    // Sample up to 10 points to test containment
                    int inside = 0;
                    int fullInside = Math.Min(10, p.Points.Count);

                    var containerPath = ClipperHelper.ToPath64(list[j].Points, clipperScale);

                    for (int k = 0; k < fullInside; k++)
                    {
                        var pt = new Point64(
                            (long)(p.Points[k].X * clipperScale),
                            (long)(p.Points[k].Y * clipperScale));

                        if (Clipper2Lib.Clipper.PointInPolygon(pt, containerPath) != PointInPolygonResult.IsOutside)
                        {
                            inside++;
                        }
                    }

                    if (inside > 0.5 * fullInside)
                    {
                        if (list[j].Children == null)
                            list[j].Children = new List<NestPolygon>();
                        list[j].Children.Add(p);
                        isChild = true;
                        break;
                    }
                }

                if (!isChild)
                    parents.Add(p);
            }

            // Remove children from the flat list
            list.RemoveAll(p => !parents.Contains(p));

            // Assign IDs to parents
            foreach (var parent in parents)
            {
                parent.Id = id++;
            }

            // Recursively process children
            foreach (var parent in parents)
            {
                if (parent.Children != null && parent.Children.Count > 0)
                {
                    var childParents = ToTree(parent.Children, id, clipperScale);
                    id += CountIds(childParents);
                }
            }

            return parents;
        }

        private static int CountIds(List<NestPolygon> polygons)
        {
            int count = 0;
            foreach (var p in polygons)
            {
                count++;
                if (p.Children != null)
                    count += CountIds(p.Children);
            }
            return count;
        }
    }
}

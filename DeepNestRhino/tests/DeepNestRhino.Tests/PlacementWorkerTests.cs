using System;
using System.Collections.Generic;
using Xunit;
using DeepNestRhino.Algorithm;
using DeepNestRhino.Core;
using DeepNestRhino.Geometry;

namespace DeepNestRhino.Tests
{
    public class PlacementWorkerTests
    {
        private static NestPolygon MakeSquarePoly(double size, int id = 0, int source = 0)
        {
            var poly = new NestPolygon { Id = id, Source = source };
            poly.Points.Add(new NestPoint(0, 0));
            poly.Points.Add(new NestPoint(size, 0));
            poly.Points.Add(new NestPoint(size, size));
            poly.Points.Add(new NestPoint(0, size));
            return poly;
        }

        private static NestPolygon MakeRectPoly(double w, double h, int id = 0, int source = 0)
        {
            var poly = new NestPolygon { Id = id, Source = source };
            poly.Points.Add(new NestPoint(0, 0));
            poly.Points.Add(new NestPoint(w, 0));
            poly.Points.Add(new NestPoint(w, h));
            poly.Points.Add(new NestPoint(0, h));
            return poly;
        }

        [Fact]
        public void PlaceParts_NullSheets_ReturnsNull()
        {
            var config = new NestingConfig();
            var cache = new NfpCache();
            var nfpGen = new NfpGenerator(cache, config);
            var worker = new PlacementWorker(nfpGen, config);

            var result = worker.PlaceParts(null, new List<NestPolygon>());
            Assert.Null(result);
        }

        [Fact]
        public void PlaceParts_EmptySheets_ReturnsNull()
        {
            var config = new NestingConfig();
            var cache = new NfpCache();
            var nfpGen = new NfpGenerator(cache, config);
            var worker = new PlacementWorker(nfpGen, config);

            var result = worker.PlaceParts(new List<NestPolygon>(), new List<NestPolygon>());
            Assert.Null(result);
        }

        [Fact]
        public void PlaceParts_SinglePartOnLargeSheet_PlacesPart()
        {
            var config = new NestingConfig { Rotations = 4, PlacementType = "gravity" };
            var cache = new NfpCache();
            var nfpGen = new NfpGenerator(cache, config);
            var worker = new PlacementWorker(nfpGen, config);

            var sheet = MakeRectPoly(100, 100, 0, 0);
            var part = MakeSquarePoly(10, 1, 1);

            var result = worker.PlaceParts(
                new List<NestPolygon> { sheet },
                new List<NestPolygon> { part });

            Assert.NotNull(result);
            Assert.True(result.Placements.Count > 0, "Should have at least one sheet placement");
            Assert.True(result.Placements[0].SheetPlacements.Count == 1,
                "Should place the single part");
        }

        [Fact]
        public void PlaceParts_TwoSmallPartsOnLargeSheet_PlacesBoth()
        {
            var config = new NestingConfig { Rotations = 4, PlacementType = "gravity" };
            var cache = new NfpCache();
            var nfpGen = new NfpGenerator(cache, config);
            var worker = new PlacementWorker(nfpGen, config);

            var sheet = MakeRectPoly(100, 100, 0, 0);
            var part1 = MakeSquarePoly(10, 1, 1);
            var part2 = MakeSquarePoly(10, 2, 2);

            var result = worker.PlaceParts(
                new List<NestPolygon> { sheet },
                new List<NestPolygon> { part1, part2 });

            Assert.NotNull(result);
            int totalPlaced = 0;
            foreach (var sp in result.Placements)
                totalPlaced += sp.SheetPlacements.Count;

            Assert.Equal(2, totalPlaced);
        }

        [Fact]
        public void PlaceParts_PartTooLargeForSheet_HighFitnessPenalty()
        {
            var config = new NestingConfig { Rotations = 4, PlacementType = "gravity" };
            var cache = new NfpCache();
            var nfpGen = new NfpGenerator(cache, config);
            var worker = new PlacementWorker(nfpGen, config);

            var sheet = MakeRectPoly(5, 5, 0, 0);
            var part = MakeSquarePoly(100, 1, 1); // Way too big

            var result = worker.PlaceParts(
                new List<NestPolygon> { sheet },
                new List<NestPolygon> { part });

            Assert.NotNull(result);
            // Unplaced parts should produce a very high fitness penalty
            Assert.True(result.Fitness > 1000000,
                "Unplaced parts should produce a very high fitness penalty");
        }

        [Fact]
        public void NfpCache_StoreAndRetrieve()
        {
            var cache = new NfpCache();

            var nfp = new NestPolygon();
            nfp.Points.Add(new NestPoint(0, 0));
            nfp.Points.Add(new NestPoint(10, 0));
            nfp.Points.Add(new NestPoint(5, 10));

            cache.InsertOuter(0, 1, 0, 0, nfp);

            var retrieved = cache.FindOuter(0, 1, 0, 0);
            Assert.NotNull(retrieved);
            Assert.Equal(nfp.Points.Count, retrieved.Points.Count);
        }

        [Fact]
        public void NfpCache_Miss_ReturnsNull()
        {
            var cache = new NfpCache();
            var retrieved = cache.FindOuter(0, 1, 0, 0);
            Assert.Null(retrieved);
        }

        [Fact]
        public void NfpCache_InnerNfp_StoreAndRetrieve()
        {
            var cache = new NfpCache();

            var nfps = new List<NestPolygon>
            {
                new NestPolygon
                {
                    Points = new List<NestPoint>
                    {
                        new NestPoint(1, 1),
                        new NestPoint(9, 1),
                        new NestPoint(9, 9),
                        new NestPoint(1, 9)
                    }
                }
            };

            cache.InsertInner(0, 1, 0, 90, nfps);

            var retrieved = cache.FindInner(0, 1, 0, 90);
            Assert.NotNull(retrieved);
            Assert.Single(retrieved);
        }
    }
}

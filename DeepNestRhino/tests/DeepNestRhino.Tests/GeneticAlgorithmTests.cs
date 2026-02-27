using System;
using System.Collections.Generic;
using Xunit;
using DeepNestRhino.Algorithm;
using DeepNestRhino.Core;
using DeepNestRhino.Geometry;

namespace DeepNestRhino.Tests
{
    public class GeneticAlgorithmTests
    {
        private static List<NestPolygon> CreateTestParts(int count)
        {
            var parts = new List<NestPolygon>();
            for (int i = 0; i < count; i++)
            {
                double size = (count - i) * 10; // decreasing sizes
                var poly = new NestPolygon { Id = i, Source = i };
                poly.Points.Add(new NestPoint(0, 0));
                poly.Points.Add(new NestPoint(size, 0));
                poly.Points.Add(new NestPoint(size, size));
                poly.Points.Add(new NestPoint(0, size));
                parts.Add(poly);
            }
            return parts;
        }

        private static NestingConfig DefaultConfig => new NestingConfig
        {
            PopulationSize = 10,
            MutationRate = 10,
            Rotations = 4
        };

        [Fact]
        public void Constructor_CreatesPopulationOfCorrectSize()
        {
            var parts = CreateTestParts(5);
            var ga = new GeneticAlgorithm(parts, DefaultConfig);

            Assert.Equal(10, ga.Population.Count);
        }

        [Fact]
        public void Constructor_FirstIndividualIsAdam()
        {
            var parts = CreateTestParts(5);
            var ga = new GeneticAlgorithm(parts, DefaultConfig);

            // First individual should have the same placement order as input
            Assert.Equal(5, ga.Population[0].Placement.Count);
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal(parts[i].Id, ga.Population[0].Placement[i].Id);
            }
        }

        [Fact]
        public void Constructor_EachIndividualHasMatchingRotationCount()
        {
            var parts = CreateTestParts(5);
            var ga = new GeneticAlgorithm(parts, DefaultConfig);

            foreach (var ind in ga.Population)
            {
                Assert.Equal(ind.Placement.Count, ind.Rotation.Count);
            }
        }

        [Fact]
        public void Constructor_RotationsAreValidAngles()
        {
            var config = new NestingConfig { PopulationSize = 10, MutationRate = 10, Rotations = 4 };
            var parts = CreateTestParts(5);
            var ga = new GeneticAlgorithm(parts, config);

            double step = 360.0 / config.Rotations;
            foreach (var ind in ga.Population)
            {
                foreach (var rot in ind.Rotation)
                {
                    double remainder = rot % step;
                    Assert.True(
                        GeometryUtil.AlmostEqual(remainder, 0, 0.001) ||
                        GeometryUtil.AlmostEqual(remainder, step, 0.001),
                        $"Rotation {rot} should be a multiple of {step}");
                }
            }
        }

        [Fact]
        public void Mutate_PreservesAllPartIds()
        {
            var parts = CreateTestParts(8);
            var ga = new GeneticAlgorithm(parts, DefaultConfig);

            var original = ga.Population[0];
            var mutant = ga.Mutate(original);

            // All original IDs should still be present
            var originalIds = new HashSet<int>();
            foreach (var p in original.Placement)
                originalIds.Add(p.Id);

            var mutantIds = new HashSet<int>();
            foreach (var p in mutant.Placement)
                mutantIds.Add(p.Id);

            Assert.Equal(originalIds.Count, mutantIds.Count);
            foreach (var id in originalIds)
                Assert.Contains(id, mutantIds);
        }

        [Fact]
        public void Mutate_DoesNotModifyOriginal()
        {
            var parts = CreateTestParts(5);
            var ga = new GeneticAlgorithm(parts, DefaultConfig);

            var original = ga.Population[0];
            var originalOrder = new List<int>();
            foreach (var p in original.Placement)
                originalOrder.Add(p.Id);

            var originalRotations = new List<double>(original.Rotation);

            ga.Mutate(original);

            // Original should be unchanged
            for (int i = 0; i < original.Placement.Count; i++)
            {
                Assert.Equal(originalOrder[i], original.Placement[i].Id);
                Assert.Equal(originalRotations[i], original.Rotation[i]);
            }
        }

        [Fact]
        public void Mate_ChildrenContainAllParts()
        {
            var parts = CreateTestParts(6);
            var ga = new GeneticAlgorithm(parts, DefaultConfig);

            var (child1, child2) = ga.Mate(ga.Population[0], ga.Population[1]);

            var ids1 = new HashSet<int>();
            foreach (var p in child1.Placement)
                ids1.Add(p.Id);

            var ids2 = new HashSet<int>();
            foreach (var p in child2.Placement)
                ids2.Add(p.Id);

            Assert.Equal(6, ids1.Count);
            Assert.Equal(6, ids2.Count);
        }

        [Fact]
        public void Mate_ChildrenHaveCorrectRotationCount()
        {
            var parts = CreateTestParts(6);
            var ga = new GeneticAlgorithm(parts, DefaultConfig);

            var (child1, child2) = ga.Mate(ga.Population[0], ga.Population[1]);

            Assert.Equal(child1.Placement.Count, child1.Rotation.Count);
            Assert.Equal(child2.Placement.Count, child2.Rotation.Count);
        }

        [Fact]
        public void Generation_MaintainsPopulationSize()
        {
            var parts = CreateTestParts(5);
            var ga = new GeneticAlgorithm(parts, DefaultConfig);

            // Assign fitness to all individuals
            for (int i = 0; i < ga.Population.Count; i++)
            {
                ga.Population[i].Fitness = i * 100.0;
            }

            int originalSize = ga.Population.Count;
            ga.Generation();

            Assert.Equal(originalSize, ga.Population.Count);
        }

        [Fact]
        public void Generation_PreservesFittestIndividual()
        {
            var parts = CreateTestParts(5);
            var ga = new GeneticAlgorithm(parts, DefaultConfig);

            // Assign fitness: first individual is fittest
            for (int i = 0; i < ga.Population.Count; i++)
            {
                ga.Population[i].Fitness = i * 100.0;
            }

            var fittestBefore = ga.Population[0];
            ga.Generation();

            // Elitism: fittest should be first in new population
            Assert.Same(fittestBefore, ga.Population[0]);
        }

        [Fact]
        public void RandomWeightedIndividual_ReturnsNonNull()
        {
            var parts = CreateTestParts(5);
            var ga = new GeneticAlgorithm(parts, DefaultConfig);

            var selected = ga.RandomWeightedIndividual();
            Assert.NotNull(selected);
        }

        [Fact]
        public void RandomWeightedIndividual_ExcludesSpecified()
        {
            var parts = CreateTestParts(5);
            var config = new NestingConfig { PopulationSize = 2, MutationRate = 10, Rotations = 4 };
            var ga = new GeneticAlgorithm(parts, config);

            var exclude = ga.Population[0];

            // With only 2 in population, excluding one means we must get the other
            for (int i = 0; i < 10; i++)
            {
                var selected = ga.RandomWeightedIndividual(exclude);
                Assert.NotSame(exclude, selected);
            }
        }
    }
}

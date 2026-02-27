using System;
using System.Collections.Generic;
using DeepNestRhino.Core;
using DeepNestRhino.Geometry;

namespace DeepNestRhino.Algorithm
{
    /// <summary>
    /// Genetic algorithm for nesting optimization.
    /// Port of GeneticAlgorithm from deepnest.js (lines 1329-1464).
    /// </summary>
    public class GeneticAlgorithm
    {
        private readonly NestingConfig _config;
        private readonly Random _rng = new();

        public List<Individual> Population { get; set; }

        public GeneticAlgorithm(List<NestPolygon> adam, NestingConfig config)
        {
            _config = config;

            // Create initial rotation angles
            var angles = new List<double>();
            for (int i = 0; i < adam.Count; i++)
            {
                double angle = Math.Floor(_rng.NextDouble() * _config.Rotations)
                    * (360.0 / _config.Rotations);
                angles.Add(angle);
            }

            Population = new List<Individual>
            {
                new Individual { Placement = adam, Rotation = angles }
            };

            // Fill population with mutants of adam
            while (Population.Count < config.PopulationSize)
            {
                var mutant = Mutate(Population[0]);
                Population.Add(mutant);
            }
        }

        /// <summary>
        /// Create a mutated copy of an individual.
        /// Port of GeneticAlgorithm.prototype.mutate (lines 1349-1371).
        /// </summary>
        public Individual Mutate(Individual individual)
        {
            var clone = new Individual
            {
                Placement = new List<NestPolygon>(individual.Placement),
                Rotation = new List<double>(individual.Rotation)
            };

            for (int i = 0; i < clone.Placement.Count; i++)
            {
                if (_rng.NextDouble() < 0.01 * _config.MutationRate)
                {
                    // Swap with next part
                    int j = i + 1;
                    if (j < clone.Placement.Count)
                    {
                        (clone.Placement[i], clone.Placement[j]) = (clone.Placement[j], clone.Placement[i]);
                    }
                }

                if (_rng.NextDouble() < 0.01 * _config.MutationRate)
                {
                    clone.Rotation[i] = Math.Floor(_rng.NextDouble() * _config.Rotations)
                        * (360.0 / _config.Rotations);
                }
            }

            return clone;
        }

        /// <summary>
        /// Single-point crossover of two individuals.
        /// Port of GeneticAlgorithm.prototype.mate (lines 1374-1409).
        /// </summary>
        public (Individual, Individual) Mate(Individual male, Individual female)
        {
            int cutpoint = (int)Math.Round(
                Math.Min(Math.Max(_rng.NextDouble(), 0.1), 0.9)
                * (male.Placement.Count - 1));

            var gene1 = new List<NestPolygon>(male.Placement.GetRange(0, cutpoint));
            var rot1 = new List<double>(male.Rotation.GetRange(0, cutpoint));

            var gene2 = new List<NestPolygon>(female.Placement.GetRange(0, cutpoint));
            var rot2 = new List<double>(female.Rotation.GetRange(0, cutpoint));

            // Fill gene1 from female
            for (int i = 0; i < female.Placement.Count; i++)
            {
                if (!ContainsId(gene1, female.Placement[i].Id))
                {
                    gene1.Add(female.Placement[i]);
                    rot1.Add(female.Rotation[i]);
                }
            }

            // Fill gene2 from male
            for (int i = 0; i < male.Placement.Count; i++)
            {
                if (!ContainsId(gene2, male.Placement[i].Id))
                {
                    gene2.Add(male.Placement[i]);
                    rot2.Add(male.Rotation[i]);
                }
            }

            return (
                new Individual { Placement = gene1, Rotation = rot1 },
                new Individual { Placement = gene2, Rotation = rot2 }
            );
        }

        /// <summary>
        /// Advance to the next generation.
        /// Port of GeneticAlgorithm.prototype.generation (lines 1411-1437).
        /// </summary>
        public void Generation()
        {
            // Sort by fitness (lower is better)
            Population.Sort((a, b) =>
            {
                double fa = a.Fitness ?? double.MaxValue;
                double fb = b.Fitness ?? double.MaxValue;
                return fa.CompareTo(fb);
            });

            // Elitism: preserve the fittest
            var newPopulation = new List<Individual> { Population[0] };

            while (newPopulation.Count < Population.Count)
            {
                var male = RandomWeightedIndividual();
                var female = RandomWeightedIndividual(male);

                var (child1, child2) = Mate(male, female);

                newPopulation.Add(Mutate(child1));

                if (newPopulation.Count < Population.Count)
                    newPopulation.Add(Mutate(child2));
            }

            Population = newPopulation;
        }

        /// <summary>
        /// Select a random individual, weighted toward the front of the sorted list (better fitness).
        /// Port of GeneticAlgorithm.prototype.randomWeightedIndividual (lines 1440-1463).
        /// </summary>
        public Individual RandomWeightedIndividual(Individual exclude = null)
        {
            var pop = new List<Individual>(Population);

            if (exclude != null)
            {
                int idx = pop.IndexOf(exclude);
                if (idx >= 0) pop.RemoveAt(idx);
            }

            double rand = _rng.NextDouble();
            double lower = 0;
            double weight = 1.0 / pop.Count;
            double upper = weight;

            for (int i = 0; i < pop.Count; i++)
            {
                if (rand > lower && rand < upper)
                    return pop[i];

                lower = upper;
                upper += 2 * weight * ((double)(pop.Count - i) / pop.Count);
            }

            return pop[0];
        }

        private static bool ContainsId(List<NestPolygon> gene, int id)
        {
            for (int i = 0; i < gene.Count; i++)
            {
                if (gene[i].Id == id) return true;
            }
            return false;
        }
    }
}

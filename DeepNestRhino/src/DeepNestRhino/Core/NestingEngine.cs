using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DeepNestRhino.Algorithm;
using DeepNestRhino.Geometry;

namespace DeepNestRhino.Core
{
    /// <summary>
    /// Top-level nesting orchestrator. Replaces the Electron IPC architecture
    /// from deepnest.js with a Task-based async loop.
    /// Port of DeepNest.start(), DeepNest.launchWorkers(), and the
    /// background-response handler from deepnest.js (lines 943-1153).
    /// </summary>
    public class NestingEngine
    {
        private NestingConfig _config;
        private GeneticAlgorithm _ga;
        private CancellationTokenSource _cts;
        private readonly List<NestResult> _nests = new();
        private readonly object _nestLock = new();

        public NestingConfig Config
        {
            get => _config;
            set => _config = value;
        }

        /// <summary>
        /// Parts to nest (set before calling StartAsync).
        /// </summary>
        public List<NestPart> Parts { get; set; } = new();

        /// <summary>
        /// Whether the engine is currently running.
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Current generation number.
        /// </summary>
        public int Generation { get; private set; }

        /// <summary>
        /// Best result found so far. Null if no placement yet.
        /// </summary>
        public NestResult BestResult
        {
            get
            {
                lock (_nestLock)
                {
                    return _nests.Count > 0 ? _nests[0] : null;
                }
            }
        }

        /// <summary>
        /// Fired when progress updates (generation count, status).
        /// </summary>
        public event Action<int> ProgressChanged;

        /// <summary>
        /// Fired when a new best placement is found.
        /// </summary>
        public event Action<NestResult> PlacementFound;

        public NestingEngine(NestingConfig config)
        {
            _config = config ?? new NestingConfig();
        }

        /// <summary>
        /// Start the nesting optimization on a background thread.
        /// Port of DeepNest.start() (lines 943-1016) and launchWorkers() (lines 1040-1153).
        /// </summary>
        public async Task StartAsync(CancellationToken externalToken = default)
        {
            if (IsRunning)
                return;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            var token = _cts.Token;

            IsRunning = true;
            Generation = 0;

            try
            {
                await Task.Run(() => RunLoop(token), token);
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
            finally
            {
                IsRunning = false;
            }
        }

        /// <summary>
        /// Stop the nesting optimization.
        /// </summary>
        public void Stop()
        {
            _cts?.Cancel();
        }

        /// <summary>
        /// Reset the engine state.
        /// </summary>
        public void Reset()
        {
            Stop();
            _ga = null;
            lock (_nestLock)
            {
                _nests.Clear();
            }
            Generation = 0;
        }

        private void RunLoop(CancellationToken token)
        {
            // 1. Prepare parts: clone polygon trees, apply spacing offsets
            var preparedParts = PrepareParts();

            // 2. Separate sheets and nestable parts
            var sheets = new List<NestPolygon>();
            var nestParts = new List<NestPolygon>();

            int id = 0;
            for (int i = 0; i < preparedParts.Count; i++)
            {
                var part = preparedParts[i];
                if (part.IsSheet)
                {
                    for (int j = 0; j < part.Quantity; j++)
                    {
                        var sheetPoly = part.PolygonTree.DeepClone();
                        sheetPoly.Source = i;
                        sheetPoly.Id = id++;
                        sheets.Add(sheetPoly);
                    }
                }
                else
                {
                    for (int j = 0; j < part.Quantity; j++)
                    {
                        var poly = part.PolygonTree.DeepClone();
                        poly.Id = id++;
                        poly.Source = i;
                        nestParts.Add(poly);
                    }
                }
            }

            if (sheets.Count == 0 || nestParts.Count == 0)
                return;

            // 3. Sort by decreasing area (adam)
            nestParts.Sort((a, b) =>
                Math.Abs(GeometryUtil.PolygonArea(b.Points))
                    .CompareTo(Math.Abs(GeometryUtil.PolygonArea(a.Points))));

            // 4. Initialize GA
            _ga = new GeneticAlgorithm(nestParts, _config);

            // 5. Create NFP generator
            var cache = new NfpCache();
            var nfpGen = new NfpGenerator(cache, _config);
            var worker = new PlacementWorker(nfpGen, _config);

            // 6. Main loop
            while (!token.IsCancellationRequested)
            {
                token.ThrowIfCancellationRequested();

                // Evaluate each unevaluated individual
                for (int i = 0; i < _ga.Population.Count; i++)
                {
                    token.ThrowIfCancellationRequested();

                    var individual = _ga.Population[i];
                    if (individual.Fitness.HasValue)
                        continue;

                    // Assign rotations to parts
                    var partsToPlace = new List<NestPolygon>();
                    for (int j = 0; j < individual.Placement.Count; j++)
                    {
                        var p = individual.Placement[j].DeepClone();
                        p.Rotation = individual.Rotation[j];
                        partsToPlace.Add(p);
                    }

                    // Evaluate placement
                    var result = worker.PlaceParts(sheets, partsToPlace);
                    if (result != null)
                    {
                        individual.Fitness = result.Fitness;
                        result.Index = i;

                        // Check if this is a new best
                        bool isNewBest = false;
                        lock (_nestLock)
                        {
                            if (_nests.Count == 0 || _nests[0].Fitness > result.Fitness)
                            {
                                _nests.Insert(0, result);
                                if (_nests.Count > 10)
                                    _nests.RemoveAt(_nests.Count - 1);
                                isNewBest = true;
                            }
                        }

                        if (isNewBest)
                        {
                            PlacementFound?.Invoke(result);
                        }
                    }
                }

                // All individuals evaluated — advance generation
                Generation++;
                _ga.Generation();
                ProgressChanged?.Invoke(Generation);
            }
        }

        /// <summary>
        /// Clone parts and apply spacing offsets.
        /// Port of the offset logic in DeepNest.start() (lines 962-1005).
        /// </summary>
        private List<NestPart> PrepareParts()
        {
            var prepared = new List<NestPart>();
            for (int i = 0; i < Parts.Count; i++)
            {
                var part = Parts[i];
                var clone = new NestPart
                {
                    PolygonTree = part.PolygonTree.DeepClone(),
                    Quantity = part.Quantity,
                    IsSheet = part.IsSheet,
                    RhinoObjectId = part.RhinoObjectId,
                    Area = part.Area
                };

                // Apply spacing offset
                if (_config.Spacing > 0)
                {
                    double offset = part.IsSheet
                        ? -0.5 * _config.Spacing
                        : 0.5 * _config.Spacing;

                    PolygonHelper.OffsetTree(
                        clone.PolygonTree, offset,
                        _config.ClipperScale, _config.CurveTolerance,
                        part.IsSheet);
                }

                prepared.Add(clone);
            }
            return prepared;
        }
    }
}

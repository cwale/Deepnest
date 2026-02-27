using System;
using Rhino;
using Rhino.Commands;

namespace DeepNestRhino.Commands
{
    /// <summary>
    /// Start the nesting optimization.
    /// </summary>
    public class DeepNestStartCommand : Command
    {
        public override string EnglishName => "DeepNestStart";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var plugin = DeepNestPlugin.Instance;
            if (plugin == null)
                return Result.Failure;

            if (plugin.Engine.IsRunning)
            {
                RhinoApp.WriteLine("Nesting is already running.");
                return Result.Nothing;
            }

            var parts = plugin.Engine.Parts.FindAll(p => !p.IsSheet);
            var sheets = plugin.Engine.Parts.FindAll(p => p.IsSheet);

            if (parts.Count == 0)
            {
                RhinoApp.WriteLine("No parts to nest. Use DeepNestAddParts first.");
                return Result.Nothing;
            }

            if (sheets.Count == 0)
            {
                RhinoApp.WriteLine("No sheet defined. Use DeepNestSetSheet first.");
                return Result.Nothing;
            }

            // Wire up events
            plugin.Engine.Config = plugin.Config;
            plugin.Engine.ProgressChanged += OnProgress;
            plugin.Engine.PlacementFound += OnPlacementFound;

            RhinoApp.WriteLine($"Starting nesting: {parts.Count} part(s) on {sheets.Count} sheet(s)...");

            // Fire and forget — the engine runs on a background task
            _ = plugin.Engine.StartAsync();

            return Result.Success;
        }

        private static void OnProgress(int generation)
        {
            RhinoApp.InvokeOnUiThread((Action)(() =>
            {
                var best = DeepNestPlugin.Instance?.Engine?.BestResult;
                string fitnessStr = best != null ? $", best fitness: {best.Fitness:F2}" : "";
                RhinoApp.WriteLine($"Generation {generation}{fitnessStr}");
            }));
        }

        private static void OnPlacementFound(Core.NestResult result)
        {
            RhinoApp.InvokeOnUiThread((Action)(() =>
            {
                int placed = 0;
                foreach (var sp in result.Placements)
                    placed += sp.SheetPlacements.Count;

                RhinoApp.WriteLine(
                    $"New best placement: fitness={result.Fitness:F2}, " +
                    $"{placed} parts on {result.Placements.Count} sheet(s)");

                // Trigger display conduit update if available
                var conduit = UI.NestingDisplayConduit.Instance;
                conduit?.UpdateResult(result, DeepNestPlugin.Instance.Engine.Parts);

                RhinoDoc.ActiveDoc?.Views.Redraw();
            }));
        }
    }
}

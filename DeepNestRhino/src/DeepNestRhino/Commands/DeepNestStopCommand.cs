using Rhino;
using Rhino.Commands;

namespace DeepNestRhino.Commands
{
    /// <summary>
    /// Stop the nesting optimization, keeping the best result.
    /// </summary>
    public class DeepNestStopCommand : Command
    {
        public override string EnglishName => "DeepNestStop";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var plugin = DeepNestPlugin.Instance;
            if (plugin == null)
                return Result.Failure;

            if (!plugin.Engine.IsRunning)
            {
                RhinoApp.WriteLine("Nesting is not running.");
                return Result.Nothing;
            }

            plugin.Engine.Stop();
            RhinoApp.WriteLine(
                $"Nesting stopped after {plugin.Engine.Generation} generation(s).");

            var best = plugin.Engine.BestResult;
            if (best != null)
            {
                int placed = 0;
                foreach (var sp in best.Placements)
                    placed += sp.SheetPlacements.Count;

                RhinoApp.WriteLine(
                    $"Best result: fitness={best.Fitness:F2}, " +
                    $"{placed} parts on {best.Placements.Count} sheet(s). " +
                    $"Use DeepNestApply to bake results.");
            }

            return Result.Success;
        }
    }
}

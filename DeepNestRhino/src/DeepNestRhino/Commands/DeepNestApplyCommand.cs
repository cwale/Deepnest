using Rhino;
using Rhino.Commands;
using DeepNestRhino.Conversion;

namespace DeepNestRhino.Commands
{
    /// <summary>
    /// Bake the best nesting result into the Rhino document.
    /// Creates transformed copies on a "DeepNest Results" layer.
    /// </summary>
    public class DeepNestApplyCommand : Command
    {
        public override string EnglishName => "DeepNestApply";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var plugin = DeepNestPlugin.Instance;
            if (plugin == null)
                return Result.Failure;

            var result = plugin.Engine.BestResult;
            if (result == null)
            {
                RhinoApp.WriteLine("No nesting result available. Run DeepNestStart first.");
                return Result.Nothing;
            }

            // Stop if still running
            if (plugin.Engine.IsRunning)
            {
                plugin.Engine.Stop();
                RhinoApp.WriteLine("Nesting stopped.");
            }

            // Build the list of sheet polygons for reference
            var sheets = new System.Collections.Generic.List<Geometry.NestPolygon>();
            foreach (var part in plugin.Engine.Parts)
            {
                if (part.IsSheet)
                    sheets.Add(part.PolygonTree);
            }

            RhinoGeometryConverter.ApplyPlacement(
                doc, result, plugin.Engine.Parts, sheets);

            int placed = 0;
            foreach (var sp in result.Placements)
                placed += sp.SheetPlacements.Count;

            RhinoApp.WriteLine(
                $"Applied nesting result: {placed} part(s) on {result.Placements.Count} sheet(s) " +
                $"to layer 'DeepNest Results'.");

            return Result.Success;
        }
    }
}

using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input.Custom;
using DeepNestRhino.Conversion;

namespace DeepNestRhino.Commands
{
    /// <summary>
    /// Select closed curves to add as nesting parts.
    /// </summary>
    public class DeepNestAddPartsCommand : Command
    {
        public override string EnglishName => "DeepNestAddParts";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var plugin = DeepNestPlugin.Instance;
            if (plugin == null)
                return Result.Failure;

            var go = new GetObject();
            go.SetCommandPrompt("Select closed curves as parts");
            go.GeometryFilter = ObjectType.Curve;
            go.SubObjectSelect = false;
            go.GetMultiple(1, 0);

            if (go.CommandResult() != Result.Success)
                return go.CommandResult();

            var parts = RhinoGeometryConverter.CurvesToNestParts(
                go.Objects(),
                plugin.Config.CurveTolerance,
                plugin.Config.ClipperScale);

            if (parts.Count == 0)
            {
                RhinoApp.WriteLine("No valid closed curves found.");
                return Result.Nothing;
            }

            foreach (var part in parts)
            {
                plugin.Engine.Parts.Add(part);
            }

            int holeCount = 0;
            foreach (var part in parts)
            {
                if (part.PolygonTree.Children != null)
                    holeCount += part.PolygonTree.Children.Count;
            }

            RhinoApp.WriteLine(
                $"Added {parts.Count} part(s) with {holeCount} hole(s). " +
                $"Total parts: {plugin.Engine.Parts.FindAll(p => !p.IsSheet).Count}");

            return Result.Success;
        }
    }
}

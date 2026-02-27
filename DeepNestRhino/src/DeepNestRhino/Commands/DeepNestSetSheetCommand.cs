using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input;
using Rhino.Input.Custom;
using DeepNestRhino.Conversion;

namespace DeepNestRhino.Commands
{
    /// <summary>
    /// Select a closed curve as the nesting sheet/bin, or define a rectangle.
    /// </summary>
    public class DeepNestSetSheetCommand : Command
    {
        public override string EnglishName => "DeepNestSetSheet";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var plugin = DeepNestPlugin.Instance;
            if (plugin == null)
                return Result.Failure;

            // Remove existing sheets
            plugin.Engine.Parts.RemoveAll(p => p.IsSheet);

            var go = new GetObject();
            go.SetCommandPrompt("Select closed curve as sheet, or press Enter for rectangle");
            go.GeometryFilter = ObjectType.Curve;
            go.SubObjectSelect = false;
            go.AcceptNothing(true);
            go.GetMultiple(0, 1);

            if (go.CommandResult() == Result.Success && go.ObjectCount > 0)
            {
                // User selected a curve
                var curve = go.Object(0).Curve();
                if (curve == null || !curve.IsClosed)
                {
                    RhinoApp.WriteLine("Selected curve must be closed.");
                    return Result.Failure;
                }

                var sheet = RhinoGeometryConverter.CurveToSheet(
                    curve, plugin.Config.CurveTolerance);

                if (sheet == null)
                {
                    RhinoApp.WriteLine("Could not convert curve to sheet.");
                    return Result.Failure;
                }

                sheet.RhinoObjectId = go.Object(0).ObjectId;
                plugin.Engine.Parts.Add(sheet);

                RhinoApp.WriteLine("Sheet set from selected curve.");
            }
            else
            {
                // Rectangle mode
                double width = 0, height = 0;
                var rc = RhinoGet.GetNumber("Sheet width", false, ref width, 0.1, double.MaxValue);
                if (rc != Result.Success) return rc;

                rc = RhinoGet.GetNumber("Sheet height", false, ref height, 0.1, double.MaxValue);
                if (rc != Result.Success) return rc;

                var sheet = RhinoGeometryConverter.CreateRectangleSheet(width, height);
                plugin.Engine.Parts.Add(sheet);

                RhinoApp.WriteLine($"Rectangular sheet set: {width} x {height}");
            }

            // Get sheet quantity
            int quantity = 1;
            var qrc = RhinoGet.GetInteger("Number of sheets available", true, ref quantity, 1, 100);
            if (qrc == Result.Success)
            {
                var sheets = plugin.Engine.Parts.FindAll(p => p.IsSheet);
                foreach (var s in sheets)
                    s.Quantity = quantity;
            }

            return Result.Success;
        }
    }
}

using Rhino;
using Rhino.Commands;
using DeepNestRhino.UI;

namespace DeepNestRhino.Commands
{
    /// <summary>
    /// Opens/shows the DeepNest panel in Rhino.
    /// </summary>
    public class DeepNestCommand : Command
    {
        public override string EnglishName => "DeepNest";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var panelId = typeof(DeepNestPanel).GUID;
            Rhino.UI.Panels.OpenPanel(panelId);
            return Result.Success;
        }
    }
}

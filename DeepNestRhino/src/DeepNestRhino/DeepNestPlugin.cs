using System;
using Rhino;
using Rhino.PlugIns;
using Rhino.UI;
using DeepNestRhino.Core;
using DeepNestRhino.UI;

namespace DeepNestRhino
{
    /// <summary>
    /// Rhino plugin entry point for DeepNest nesting optimizer.
    /// </summary>
    public class DeepNestPlugin : PlugIn
    {
        public static DeepNestPlugin Instance { get; private set; }

        /// <summary>
        /// Shared nesting engine instance.
        /// </summary>
        public NestingEngine Engine { get; private set; }

        /// <summary>
        /// Current configuration.
        /// </summary>
        public NestingConfig Config { get; set; }

        public DeepNestPlugin()
        {
            Instance = this;
            Config = new NestingConfig();
            Engine = new NestingEngine(Config);
        }

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            // Register the dockable panel
            var panelType = typeof(DeepNestPanel);
            Panels.RegisterPanel(this, panelType, "DeepNest", null);

            RhinoApp.WriteLine("DeepNest for Rhino loaded.");
            return LoadReturnCode.Success;
        }
    }
}

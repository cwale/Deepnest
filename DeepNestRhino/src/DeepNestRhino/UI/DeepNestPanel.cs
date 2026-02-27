using System;
using System.Collections.Generic;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.UI;
using DeepNestRhino.Core;

namespace DeepNestRhino.UI
{
    /// <summary>
    /// Main dockable panel for DeepNest in Rhino.
    /// Provides parts list, sheet info, configuration, controls, and results display.
    /// </summary>
    [System.Runtime.InteropServices.Guid("D7E5A5F2-8C1E-4B3D-9F2A-1C5D8E6F7A0B")]
    public class DeepNestPanel : Panel, IPanel
    {
        public static Guid PanelId => typeof(DeepNestPanel).GUID;

        // UI elements
        private readonly Label _partsLabel;
        private readonly Label _sheetLabel;
        private readonly Label _statusLabel;
        private readonly Label _fitnessLabel;
        private readonly Label _generationLabel;
        private readonly Button _startButton;
        private readonly Button _stopButton;
        private readonly Button _applyButton;
        private readonly ProgressBar _progressBar;

        // Configuration controls
        private readonly NumericStepper _spacingStepper;
        private readonly NumericStepper _rotationsStepper;
        private readonly NumericStepper _populationStepper;
        private readonly NumericStepper _mutationStepper;
        private readonly DropDown _placementDropDown;

        private NestingDisplayConduit _conduit;

        public DeepNestPanel()
        {
            // Parts info section
            _partsLabel = new Label { Text = "Parts: 0" };
            _sheetLabel = new Label { Text = "Sheet: not set" };

            // Status section
            _statusLabel = new Label { Text = "Ready" };
            _fitnessLabel = new Label { Text = "Fitness: --" };
            _generationLabel = new Label { Text = "Generation: 0" };
            _progressBar = new ProgressBar { MinValue = 0, MaxValue = 100, Value = 0 };

            // Buttons
            _startButton = new Button { Text = "Start Nesting" };
            _startButton.Click += OnStartClick;

            _stopButton = new Button { Text = "Stop", Enabled = false };
            _stopButton.Click += OnStopClick;

            _applyButton = new Button { Text = "Apply Result", Enabled = false };
            _applyButton.Click += OnApplyClick;

            // Configuration
            _spacingStepper = new NumericStepper
            {
                MinValue = 0, MaxValue = 100, Value = 0,
                DecimalPlaces = 2, Increment = 0.5
            };
            _spacingStepper.ValueChanged += (s, e) =>
            {
                if (DeepNestPlugin.Instance != null)
                    DeepNestPlugin.Instance.Config.Spacing = _spacingStepper.Value;
            };

            _rotationsStepper = new NumericStepper
            {
                MinValue = 1, MaxValue = 360, Value = 4, DecimalPlaces = 0
            };
            _rotationsStepper.ValueChanged += (s, e) =>
            {
                if (DeepNestPlugin.Instance != null)
                    DeepNestPlugin.Instance.Config.Rotations = (int)_rotationsStepper.Value;
            };

            _populationStepper = new NumericStepper
            {
                MinValue = 2, MaxValue = 100, Value = 10, DecimalPlaces = 0
            };
            _populationStepper.ValueChanged += (s, e) =>
            {
                if (DeepNestPlugin.Instance != null)
                    DeepNestPlugin.Instance.Config.PopulationSize = (int)_populationStepper.Value;
            };

            _mutationStepper = new NumericStepper
            {
                MinValue = 1, MaxValue = 100, Value = 10, DecimalPlaces = 0
            };
            _mutationStepper.ValueChanged += (s, e) =>
            {
                if (DeepNestPlugin.Instance != null)
                    DeepNestPlugin.Instance.Config.MutationRate = (int)_mutationStepper.Value;
            };

            _placementDropDown = new DropDown();
            _placementDropDown.Items.Add("Gravity");
            _placementDropDown.Items.Add("Box");
            _placementDropDown.SelectedIndex = 0;
            _placementDropDown.SelectedIndexChanged += (s, e) =>
            {
                if (DeepNestPlugin.Instance != null)
                {
                    DeepNestPlugin.Instance.Config.PlacementType =
                        _placementDropDown.SelectedIndex == 0 ? "gravity" : "box";
                }
            };

            // Layout
            var configGroup = new GroupBox
            {
                Text = "Configuration",
                Content = new TableLayout
                {
                    Spacing = new Size(5, 5),
                    Padding = new Padding(5),
                    Rows =
                    {
                        new TableRow(new Label { Text = "Spacing" }, _spacingStepper),
                        new TableRow(new Label { Text = "Rotations" }, _rotationsStepper),
                        new TableRow(new Label { Text = "Population" }, _populationStepper),
                        new TableRow(new Label { Text = "Mutation %" }, _mutationStepper),
                        new TableRow(new Label { Text = "Placement" }, _placementDropDown),
                    }
                }
            };

            var buttonLayout = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Items = { _startButton, _stopButton, _applyButton }
            };

            Content = new StackLayout
            {
                Padding = new Padding(10),
                Spacing = 8,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Items =
                {
                    new Label { Text = "DeepNest for Rhino", Font = new Font(SystemFont.Bold, 12) },
                    _partsLabel,
                    _sheetLabel,
                    configGroup,
                    buttonLayout,
                    _progressBar,
                    _statusLabel,
                    _generationLabel,
                    _fitnessLabel,
                }
            };
        }

        public void PanelShown(uint documentSerialNumber, ShowPanelReason reason)
        {
            UpdatePartsDisplay();
            WireEngineEvents();
        }

        public void PanelHidden(uint documentSerialNumber, ShowPanelReason reason)
        {
            UnwireEngineEvents();
        }

        public void PanelClosing(uint documentSerialNumber, bool onCloseDocument)
        {
            UnwireEngineEvents();
            DisableConduit();
        }

        private void WireEngineEvents()
        {
            var engine = DeepNestPlugin.Instance?.Engine;
            if (engine == null) return;

            engine.ProgressChanged += OnEngineProgress;
            engine.PlacementFound += OnEnginePlacement;
        }

        private void UnwireEngineEvents()
        {
            var engine = DeepNestPlugin.Instance?.Engine;
            if (engine == null) return;

            engine.ProgressChanged -= OnEngineProgress;
            engine.PlacementFound -= OnEnginePlacement;
        }

        private void OnStartClick(object sender, EventArgs e)
        {
            var plugin = DeepNestPlugin.Instance;
            if (plugin == null) return;

            var engine = plugin.Engine;
            var partCount = engine.Parts.FindAll(p => !p.IsSheet).Count;
            var sheetCount = engine.Parts.FindAll(p => p.IsSheet).Count;

            if (partCount == 0)
            {
                RhinoApp.WriteLine("No parts. Use DeepNestAddParts command first.");
                return;
            }
            if (sheetCount == 0)
            {
                RhinoApp.WriteLine("No sheet. Use DeepNestSetSheet command first.");
                return;
            }

            // Enable display conduit
            EnableConduit();

            // Update config from UI
            engine.Config = plugin.Config;

            _startButton.Enabled = false;
            _stopButton.Enabled = true;
            _applyButton.Enabled = false;
            _statusLabel.Text = "Nesting...";

            _ = engine.StartAsync();
        }

        private void OnStopClick(object sender, EventArgs e)
        {
            DeepNestPlugin.Instance?.Engine?.Stop();

            _startButton.Enabled = true;
            _stopButton.Enabled = false;
            _applyButton.Enabled = DeepNestPlugin.Instance?.Engine?.BestResult != null;
            _statusLabel.Text = "Stopped";
        }

        private void OnApplyClick(object sender, EventArgs e)
        {
            var plugin = DeepNestPlugin.Instance;
            if (plugin?.Engine?.BestResult == null) return;

            var sheets = new List<Geometry.NestPolygon>();
            foreach (var part in plugin.Engine.Parts)
            {
                if (part.IsSheet)
                    sheets.Add(part.PolygonTree);
            }

            Conversion.RhinoGeometryConverter.ApplyPlacement(
                RhinoDoc.ActiveDoc,
                plugin.Engine.BestResult,
                plugin.Engine.Parts,
                sheets);

            DisableConduit();
            _statusLabel.Text = "Result applied";
        }

        private void OnEngineProgress(int generation)
        {
            Application.Instance.AsyncInvoke(() =>
            {
                _generationLabel.Text = $"Generation: {generation}";
                _progressBar.Value = Math.Min(generation, 100);
            });
        }

        private void OnEnginePlacement(NestResult result)
        {
            Application.Instance.AsyncInvoke(() =>
            {
                _fitnessLabel.Text = $"Fitness: {result.Fitness:F2}";

                int placed = 0;
                foreach (var sp in result.Placements)
                    placed += sp.SheetPlacements.Count;

                _statusLabel.Text =
                    $"{placed} part(s) on {result.Placements.Count} sheet(s)";

                _applyButton.Enabled = true;

                // Update display conduit
                _conduit?.UpdateResult(result, DeepNestPlugin.Instance?.Engine?.Parts);
                RhinoDoc.ActiveDoc?.Views.Redraw();
            });
        }

        private void UpdatePartsDisplay()
        {
            var engine = DeepNestPlugin.Instance?.Engine;
            if (engine == null) return;

            int partCount = engine.Parts.FindAll(p => !p.IsSheet).Count;
            int sheetCount = engine.Parts.FindAll(p => p.IsSheet).Count;

            _partsLabel.Text = $"Parts: {partCount}";
            _sheetLabel.Text = sheetCount > 0 ? $"Sheets: {sheetCount}" : "Sheet: not set";
        }

        private void EnableConduit()
        {
            if (_conduit == null)
                _conduit = new NestingDisplayConduit();
            _conduit.Enabled = true;
        }

        private void DisableConduit()
        {
            if (_conduit != null)
                _conduit.Enabled = false;
        }
    }
}

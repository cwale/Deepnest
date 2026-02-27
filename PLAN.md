# DeepNest for Rhino 8 — Implementation Plan

Port of [Deepnest](https://github.com/nicholasgasior/deepnest) (Electron/JS nesting optimizer) to a Rhino 8 C# plugin using RhinoCommon and Eto.Forms.

---

## 1. Project Structure

### Solution Layout

```
DeepNestRhino/
  DeepNestRhino.sln
  src/
    DeepNestRhino/                        # Main Rhino plugin project
      DeepNestRhino.csproj
      DeepNestPlugin.cs                   # Plugin entry point (inherits PlugIn)
      Commands/
        DeepNestCommand.cs                # Opens the main panel
        DeepNestStartCommand.cs           # Starts nesting
        DeepNestStopCommand.cs            # Stops nesting
        DeepNestAddPartsCommand.cs        # Select curves as parts
        DeepNestSetSheetCommand.cs        # Select curve as sheet/bin
        DeepNestApplyCommand.cs           # Bake best result into document
      Core/
        NestingEngine.cs                  # Top-level orchestrator (from deepnest.js)
        NestingConfig.cs                  # Configuration POCO
        NestPart.cs                       # Part definition with polygon tree
        NestResult.cs                     # Placement results
        PolygonTree.cs                    # Tree of polygons (parts + holes)
      Algorithm/
        GeneticAlgorithm.cs              # GA: population, mutation, crossover, elitism
        Individual.cs                     # GA individual (placement order + rotations)
        PlacementWorker.cs               # Fitness evaluation / part placement
        NfpGenerator.cs                   # NFP pair computation dispatcher
        NfpCache.cs                       # Thread-safe NFP cache (ConcurrentDictionary)
      Geometry/
        GeometryUtil.cs                   # Port of geometryutil.js (NFP, slide, etc.)
        NfpAlgorithm.cs                   # Orbiting NFP computation
        MinkowskiSum.cs                   # Minkowski sum via Clipper2
        PolygonHelper.cs                  # Offset, clean, simplify, area, bounds
        NestPoint.cs                      # Simple 2D point struct
        NestPolygon.cs                    # Polygon type with children, id, source, rotation
      Conversion/
        RhinoGeometryConverter.cs         # Rhino Curve -> NestPolygon and back
        PolygonTreeBuilder.cs             # Builds parent/child tree from flat list
      UI/
        DeepNestPanel.cs                  # Eto.Forms panel (docked in Rhino)
        ConfigurationPanel.cs             # Settings sub-panel
        ProgressPanel.cs                  # Progress display
        NestingDisplayConduit.cs          # Custom DisplayConduit for preview
      Properties/
        AssemblyInfo.cs
  tests/
    DeepNestRhino.Tests/
      DeepNestRhino.Tests.csproj
      GeometryUtilTests.cs
      GeneticAlgorithmTests.cs
      NfpTests.cs
      PlacementWorkerTests.cs
```

### Namespaces

| Namespace | Purpose |
|-----------|---------|
| `DeepNestRhino` | Plugin root |
| `DeepNestRhino.Commands` | Rhino commands |
| `DeepNestRhino.Core` | Engine orchestration |
| `DeepNestRhino.Algorithm` | GA + placement |
| `DeepNestRhino.Geometry` | All geometry math |
| `DeepNestRhino.Conversion` | Rhino <-> internal format |
| `DeepNestRhino.UI` | Eto.Forms UI + display conduit |

### Project File (csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net48;net7.0-windows</TargetFrameworks>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    <Title>DeepNest for Rhino</Title>
    <Description>Nesting optimizer for laser cutting</Description>
    <Version>1.0.0</Version>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="RhinoCommon" Version="8.0.*" ExcludeAssets="runtime" />
    <PackageReference Include="Clipper2" Version="1.3.*" />
  </ItemGroup>
</Project>
```

Clipper2 (by Angus Johnson, same author as the original Clipper) supports `double` coordinates natively via `PointD`/`ClipperD`, eliminating the manual integer scaling in the original JS code.

---

## 2. Core Algorithm Port

### 2.1 Data Types

The original JS code attaches properties to arrays (`polygon.id`, `polygon.source`, `polygon.children`). In C# these become proper types.

**NestPoint** — Simple 2D point:
```csharp
public struct NestPoint
{
    public double X;
    public double Y;
    public bool Exact;   // for line merge detection
    public bool Marked;  // for NFP traversal
}
```

**NestPolygon** — Polygon with metadata and children (holes):
```csharp
public class NestPolygon
{
    public List<NestPoint> Points { get; set; }
    public List<NestPolygon> Children { get; set; }  // holes
    public int Id { get; set; }
    public int Source { get; set; }
    public double Rotation { get; set; }
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
}
```

**NestPart** — Part with quantity and Rhino link:
```csharp
public class NestPart
{
    public NestPolygon PolygonTree { get; set; }
    public int Quantity { get; set; } = 1;
    public bool IsSheet { get; set; }
    public Guid RhinoObjectId { get; set; }  // link back to Rhino doc
}
```

### 2.2 deepnest.js → NestingEngine

**Source**: `main/deepnest.js` (1464 lines)

| JS Method | C# Method | Notes |
|-----------|-----------|-------|
| `DeepNest.config()` | `NestingEngine.Configure(NestingConfig)` | Store config, reset GA state |
| `DeepNest.start()` | `NestingEngine.StartAsync(CancellationToken)` | Background Task, replaces `setInterval` |
| `DeepNest.stop()` | `NestingEngine.Stop()` | Sets cancellation token |
| `DeepNest.reset()` | `NestingEngine.Reset()` | Clear GA, nests list |
| `DeepNest.launchWorkers()` | `NestingEngine.RunGeneration()` | Evaluate population, advance GA |
| `DeepNest.getParts()` | `PolygonTreeBuilder.BuildTree()` | Separate class (no SVG parsing) |
| `DeepNest.simplifyPolygon()` | `PolygonHelper.SimplifyPolygon()` | RDP + offset simplification |
| `DeepNest.cleanPolygon()` | `PolygonHelper.CleanPolygon()` | Via Clipper2 |
| `DeepNest.polygonOffset()` | `PolygonHelper.PolygonOffset()` | Via Clipper2 ClipperOffset |
| `DeepNest.pointInPolygon()` | `GeometryUtil.PointInPolygon()` | Ray casting algorithm |
| `offsetTree()` | `PolygonHelper.OffsetTree()` | Recursive spacing offset |

The `NestingEngine` replaces the Electron IPC architecture. Instead of messages between main/background windows, it uses `Task.Run()` with `CancellationToken`:

```csharp
public class NestingEngine
{
    private NestingConfig _config;
    private GeneticAlgorithm _ga;
    private CancellationTokenSource _cts;
    private List<NestResult> _nests = new();

    public event Action<double> ProgressChanged;
    public event Action<NestResult> PlacementFound;

    public async Task StartAsync(List<NestPart> parts, CancellationToken ct)
    {
        // 1. Offset trees for spacing
        // 2. Initialize GA with "adam" individual sorted by area
        // 3. Loop: evaluate population -> advance generation
        // All computation on background thread via Task.Run
    }
}
```

### 2.3 svgnest.js → Removed/Merged

`svgnest.js` (912 lines) is an earlier, simpler algorithm. Deepnest supersedes it with multi-sheet support, merged-line optimization, and native Minkowski sum. We port the `background.js` version, which is what Deepnest actually executes.

### 2.4 geometryutil.js → GeometryUtil + NfpAlgorithm

**Source**: `main/util/geometryutil.js` (1911 lines) — the largest and most critical file.

**GeometryUtil** — Pure math functions:

| JS Function | C# Method |
|-------------|-----------|
| `almostEqual` | `AlmostEqual(double, double, double)` |
| `withinDistance` | `WithinDistance(NestPoint, NestPoint, double)` |
| `lineIntersect` | `LineIntersect(...)` |
| `getPolygonBounds` | `GetPolygonBounds(List<NestPoint>)` |
| `pointInPolygon` | `PointInPolygon(NestPoint, NestPolygon)` |
| `polygonArea` | `PolygonArea(List<NestPoint>)` |
| `intersect` | `PolygonsIntersect(NestPolygon, NestPolygon)` |
| `rotatePolygon` | `RotatePolygon(NestPolygon, double)` |
| `isRectangle` | `IsRectangle(NestPolygon)` |
| `noFitPolygonRectangle` | `NoFitPolygonRectangle(NestPolygon, NestPolygon)` |
| `polygonSlideDistance` | `PolygonSlideDistance(...)` |
| `polygonProjectionDistance` | `PolygonProjectionDistance(...)` |
| `segmentDistance` | `SegmentDistance(...)` |

**Bezier/Arc linearization** (`QuadraticBezier`, `CubicBezier`, `Arc` — ~320 lines): **Not needed** in the Rhino port. RhinoCommon provides `Curve.ToPolyline()` which handles all curve-to-polyline conversion natively.

**NfpAlgorithm** — The NFP orbit algorithm (lines 1437–1727):

| JS Function | C# Method |
|-------------|-----------|
| `noFitPolygon` | `ComputeNfp(NestPolygon, NestPolygon, bool, bool)` |
| `searchStartPoint` | `SearchStartPoint(NestPolygon, NestPolygon, bool, ...)` |

The orbiting approach:
1. Place B touching A at a starting point
2. Find touching vertices/edges between A and B
3. Generate translation vectors from touching geometry
4. Choose the vector that allows maximum slide distance
5. Slide B along that vector
6. Repeat until B returns to start, forming the NFP

Must be ported precisely, preserving tolerance constant `TOL = 1e-9`.

### 2.5 placementworker.js + background.js → PlacementWorker

**Sources**: `main/util/placementworker.js` (291 lines) and the advanced `placeParts()` in `main/background.js` (lines 804–1173).

```csharp
public class PlacementWorker
{
    public NestResult PlaceParts(List<NestPolygon> sheets, List<NestPolygon> parts)
    {
        // For each sheet:
        //   For each unplaced part:
        //     1. Get inner NFP (sheet, part) — valid positions inside sheet
        //     2. Get outer NFP for each placed part — exclusion zones
        //     3. Union all outer NFPs using Clipper2
        //     4. Subtract from inner NFP → remaining valid positions
        //     5. Choose position minimizing bounding box (gravity/box/hull)
        //     6. Apply merge-line bonus if enabled
        // Return fitness score
    }
}
```

Key porting details from `background.js`:
- **Lines 857–879**: Retry-rotation loop for first part on each sheet
- **Lines 912–958**: NFP accumulation with clip cache optimization
- **Lines 1043–1064**: Three placement strategies: gravity (`width*2 + height`), box (`width * height`), convexhull (hull area)
- **Lines 1082–1094**: Merge-line bonus: `merged.totalLength * config.timeRatio`
- **Lines 620–710**: `getOuterNfp()` with cache check + Clipper MinkowskiSum fallback
- **Lines 734–801**: `getInnerNfp()` using frame approach

### 2.6 clipper.js → Clipper2 NuGet

Replace Clipper 6.2.1 (JS) with Clipper2 NuGet package:

| Clipper 6 (JS) | Clipper2 (C#) |
|-----------------|---------------|
| `ClipperLib.IntPoint` | `Clipper2Lib.PointD` (double precision) |
| `ClipperLib.Clipper.Area()` | `Clipper2Lib.Clipper.Area()` |
| `ClipperLib.ClipperOffset` | `Clipper2Lib.ClipperOffset` |
| `ClipperLib.Clipper.MinkowskiSum()` | `Clipper2Lib.Clipper.MinkowskiSum()` |
| `ClipperLib.Clipper.SimplifyPolygon()` | `Clipper2Lib.Clipper.SimplifyPaths()` |
| `ClipperLib.Clipper.CleanPolygon()` | `Clipper2Lib.Clipper.RamerDouglasPeucker()` |
| `clipperScale = 10000000` | Can use `PointD` directly |

A `ClipperHelper` wrapper class abstracts API differences.

### 2.7 Genetic Algorithm

**Source**: `main/deepnest.js` lines 1329–1464

```csharp
public class Individual
{
    public List<NestPolygon> Placement { get; set; }  // order of parts
    public List<double> Rotation { get; set; }         // rotation per part
    public double? Fitness { get; set; }
}

public class GeneticAlgorithm
{
    // Initialization: "adam" sorted by decreasing area, fill population with mutants
    // Mutation: swap adjacent parts or assign random rotation (probability = mutationRate/100)
    // Crossover: single-point, preserve part IDs
    // Generation: sort by fitness, elitism, weighted selection + mating + mutation
    // Weighted selection: front-weighted random (better fitness = higher probability)
}
```

### 2.8 Minkowski Sum → Clipper2

**Source**: `minkowski.cc` (270 lines) — Boost.Polygon `convolve_two_polygon_sets()`

**Recommended approach**: Use Clipper2's `MinkowskiSum()`. For polygons with holes:
1. Compute outer NFP ignoring holes
2. For each hole in A large enough to fit B, compute inner NFP
3. Attach inner NFPs as children

If accuracy issues arise with complex hole polygons, fall back to a direct C# port of the Boost.Polygon convolution algorithm.

---

## 3. Rhino Integration

### 3.1 RhinoGeometryConverter

Replaces the entire `svgparser.js` (1582 lines). Instead of parsing SVG/DXF, we use RhinoCommon's built-in curve tessellation.

```csharp
public static class RhinoGeometryConverter
{
    public static NestPolygon CurveToNestPolygon(Curve curve, double tolerance)
    {
        // 1. Ensure curve is closed
        // 2. Curve.ToPolyline(tolerance, ...) — RhinoCommon handles all curve types
        // 3. Extract points, skip closing duplicate
        // 4. Ensure correct winding direction (CCW = negative area)
        return polygon;
    }

    public static void PlaceInRhino(RhinoDoc doc, NestPart part, PlacementResult placement)
    {
        // 1. Get original Rhino object by stored Guid
        // 2. Build transform: rotate about origin, then translate
        // 3. Add transformed copy on "DeepNest Results" layer
    }
}
```

### 3.2 Select Curves as Parts (DeepNestAddParts)

```csharp
var go = new GetObject();
go.SetCommandPrompt("Select closed curves as parts");
go.GeometryFilter = ObjectType.Curve;
go.GetMultiple(1, 0);
// Convert each selected curve to NestPolygon via RhinoGeometryConverter
```

### 3.3 Define Sheet/Bin (DeepNestSetSheet)

- Select a closed curve as the sheet, **or**
- Press Enter for a rectangle and enter width/height dimensions

### 3.4 PolygonTreeBuilder

Replaces `toTree()` in deepnest.js (lines 673–755). Detects parent/child (part/hole) relationships from a flat list of selected curves using `PointInPolygon`.

### 3.5 Coordinate System

Deepnest uses SVG coordinates (Y-down). Rhino uses Y-up. The `RhinoGeometryConverter` handles the flip at the boundary. Internally, the nesting engine uses a consistent convention (CCW = negative area, matching original).

---

## 4. UI Design (Eto.Forms)

### 4.1 DeepNestPanel

A dockable panel in the Rhino sidebar with sections:

1. **Parts List** — `GridView` showing parts, quantities, sheet flag
2. **Sheet Info** — Current sheet dimensions
3. **Configuration** — Collapsible settings group (see below)
4. **Controls** — Start/Stop buttons, progress bar
5. **Results** — Best fitness, sheets used, parts placed, utilization %

### 4.2 Configuration Settings

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| Spacing | double | 0 | Gap between parts (document units) |
| Curve Tolerance | double | 0.3 | Polyline approximation tolerance |
| Rotations | int | 4 | Number of rotation steps (360/N degrees) |
| Population Size | int | 10 | GA population size |
| Mutation Rate | int | 10 | Mutation probability (%) |
| Placement Type | enum | Gravity | Gravity / Box / ConvexHull |
| Merge Lines | bool | true | Optimize shared cut lines |
| Time Ratio | double | 0.5 | Material vs laser time weight |
| Simplify | bool | false | Convex hull simplification |
| Sheet Quantity | int | 1 | Number of available sheets |

### 4.3 Progress Display

Events from `NestingEngine` marshaled to UI thread via `Application.Instance.AsyncInvoke()`:
- Progress bar (generation count)
- Status label (generation #, parts placed / total)
- Fitness value
- Utilization percentage

### 4.4 NestingDisplayConduit

Custom `DisplayConduit` for live viewport preview:
- Sheet outlines in **blue**
- Placed parts in **green**
- Holes in **red**
- Unplaced parts in **orange** (dashed)

Updated via `Interlocked.Exchange` when new best result is found.

---

## 5. Commands

| Command | Description |
|---------|-------------|
| `DeepNest` | Open/show the DeepNest panel |
| `DeepNestAddParts` | Select closed curves as parts |
| `DeepNestSetSheet` | Select or define the sheet/bin |
| `DeepNestStart` | Start the nesting optimization |
| `DeepNestStop` | Stop nesting, keep best result |
| `DeepNestApply` | Bake best result into document on "DeepNest Results" layer |

---

## 6. Data Flow

```
User selects curves in Rhino viewport
         │
         ▼
[DeepNestAddParts Command]
         │
    Curve.ToPolyline()
         │
         ▼
[RhinoGeometryConverter.CurveToNestPolygon()]
    • Tessellate curves to polylines
    • Ensure closed, correct winding
    • Store RhinoObjectId reference
         │
         ▼
[NestPart] objects stored in NestingEngine.Parts
         │
    User sets sheet via [DeepNestSetSheet]
         │
         ▼
[PolygonTreeBuilder.BuildTree()]
    • Detect parent/child (part/hole) relationships
    • Assign unique IDs
         │
         ▼
[NestingEngine.StartAsync()]  ── runs on background Task
    │
    ├── 1. Apply spacing offsets (Clipper2 ClipperOffset)
    │
    ├── 2. Initialize GeneticAlgorithm
    │      • "Adam" = parts sorted by decreasing area
    │      • Fill population with mutants
    │
    ├── 3. Main loop:
    │      │
    │      ├── For each individual in population:
    │      │      │
    │      │      ├── NfpGenerator.ComputeAllNfps()
    │      │      │      • Check NfpCache (ConcurrentDictionary)
    │      │      │      • If miss: MinkowskiSum.Compute() or NfpAlgorithm.ComputeNfp()
    │      │      │      • Computed in parallel via Parallel.ForEach
    │      │      │
    │      │      └── PlacementWorker.PlaceParts()
    │      │             • Inner NFP (sheet, part) → valid positions
    │      │             • Outer NFP union (placed parts) → exclusion zones
    │      │             • Difference → remaining valid positions (Clipper2)
    │      │             • Score each vertex → choose minimum
    │      │             • Return fitness
    │      │
    │      ├── GeneticAlgorithm.Generation()
    │      │      • Sort by fitness, elitism, crossover, mutation
    │      │
    │      ├── Fire ProgressChanged event
    │      ├── If new best: Fire PlacementFound event
    │      └── Check CancellationToken, loop
    │
    ▼
[PlacementFound event → UI thread]
    │
    ├── Update NestingDisplayConduit
    └── Redraw Rhino viewport (live preview)
         │
         ▼
[DeepNestStop / DeepNestApply Command]
         │
         ▼
[NestingEngine.ApplyResultToRhinoDoc()]
    • Get original Rhino curves by stored Guid
    • Apply rotation + translation Transform
    • Add copies on "DeepNest Results" layer
```

---

## 7. Threading

### Architecture

| Thread | Responsibility |
|--------|---------------|
| **UI thread** (Rhino main) | All Rhino document access, UI updates, DisplayConduit drawing |
| **Computation thread** (`Task.Run`) | GA loop, NFP computation, placement evaluation |
| **Parallel NFP** (`Parallel.ForEach`) | Independent NFP pair computation within computation thread |

### Thread Safety

- **NfpCache**: `ConcurrentDictionary<NfpKey, NestPolygon>` — thread-safe for parallel NFP computation
- **UI Updates**: Marshaled via `RhinoApp.InvokeOnUiThread()` or `Application.Instance.AsyncInvoke()`
- **Rhino Document**: Never accessed from background thread. Geometry stored in `NestPolygon` format upfront
- **DisplayConduit**: `_currentResult` updated via `Interlocked.Exchange` or lock

### Cancellation

```csharp
public void Stop()
{
    _cts?.Cancel();
    // Task.Run loop checks cancellation and exits cleanly
}
```

---

## 8. Build and Distribution

### Yak Package

```yaml
name: deepnest
version: 1.0.0
authors:
  - DeepNest Contributors
description: >
  Nesting optimizer for laser cutting.
  Arranges parts on sheets to minimize material waste.
keywords:
  - nesting
  - laser cutting
  - cnc
  - bin packing
  - optimization
```

Multi-target build structure for Yak:
```
build/
  manifest.yml
  net48/
    DeepNestRhino.rhp
  net7.0/
    DeepNestRhino.rhp
```

---

## 9. Implementation Phases

### Phase 1: Core Geometry
1. Set up solution structure and csproj
2. Port `NestPoint`, `NestPolygon`, data types
3. Port `GeometryUtil` (all pure math functions)
4. Port `PolygonHelper` (clean, offset, simplify via Clipper2)
5. Write unit tests for geometry functions

### Phase 2: NFP Algorithm
6. Port `NfpAlgorithm.ComputeNfp()` (orbiting algorithm, ~290 lines)
7. Implement `MinkowskiSum.Compute()` using Clipper2
8. Implement `NfpGenerator` with `GetOuterNfp()` and `GetInnerNfp()`
9. Implement `NfpCache` with `ConcurrentDictionary`
10. Write NFP tests with known polygon pairs

### Phase 3: Placement and GA
11. Port `PlacementWorker.PlaceParts()` from background.js
12. Port `GeneticAlgorithm` class (mutation, crossover, generation)
13. Implement `NestingEngine` orchestrator with async loop
14. Write placement and GA tests

### Phase 4: Rhino Integration
15. Implement `RhinoGeometryConverter` (Curve ↔ NestPolygon)
16. Implement `PolygonTreeBuilder` (detect holes)
17. Implement `DeepNestPlugin` class
18. Implement all Rhino commands
19. Test with simple Rhino curves

### Phase 5: UI and Polish
20. Build `DeepNestPanel` with Eto.Forms
21. Build `ConfigurationPanel`
22. Implement `NestingDisplayConduit` for viewport preview
23. Wire up progress reporting and result display
24. End-to-end testing

### Phase 6: Distribution
25. Multi-target build verification (net48 + net7.0)
26. Yak package creation
27. Performance optimization (profile NFP computation, tune parallelism)

---

## 10. Key Porting Challenges

| Challenge | Mitigation |
|-----------|------------|
| JS array-as-object pattern (`polygon.id`, `polygon.children`) | Proper `NestPolygon` class wrapping `List<NestPoint>` with metadata |
| Floating point precision (`TOL = 1e-9`) | C# `double` is identical to JS `number` (IEEE 754 64-bit). Port constants directly |
| Clipper 6 → Clipper2 API differences | `ClipperHelper` wrapper class with same semantics |
| Winding direction (SVG Y-down vs Rhino Y-up) | `RhinoGeometryConverter` handles flip at boundary |
| Performance (web workers → C#) | `Parallel.ForEach` is more efficient (shared memory, no serialization). `NfpCache` is critical |
| Minkowski sum for polygons with holes | Start with Clipper2; fall back to C# port of Boost.Polygon convolution if needed |

### Critical Source Files for Reference

| File | Lines | Importance |
|------|-------|------------|
| `main/util/geometryutil.js` | 1911 | NFP orbiting algorithm, polygon math — must port precisely |
| `main/background.js` | ~1173 | Production placement algorithm, NFP computation with cache |
| `main/deepnest.js` | 1464 | Orchestrator, GeneticAlgorithm, config, polygon offset |
| `main/util/placementworker.js` | 291 | Simpler placement reference |
| `minkowski.cc` | 270 | Native Minkowski sum reference |

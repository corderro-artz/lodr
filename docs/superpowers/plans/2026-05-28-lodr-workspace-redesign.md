# LODR Workspace Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the step-wizard prototype with a full workspace-based pallet management application supporting multiple trailers, multiple pallet types, click/drag placement, and local save/load.

**Architecture:** WorkspaceState (scoped DI) owns all mutable data; pure services (CapacityService, SlotService) do computation; Blazor components render state; IndexedDB via existing JS interop handles persistence. Five-panel layout: Header | Sidebar | Canvas | Inspector | StatusBar.

**Tech Stack:** .NET 10 Blazor WASM, TailwindCSS v4.3, xUnit 2.9.3, bUnit 2.7.2, IndexedDB via JS interop.

---

## File Map

### New files (create)
- `src/Lodr/Models/Workspace.cs` — Workspace, TrailerInstance, PalletType, PlacedPallet, WorkspacePreset, PresetTrailer models
- `src/Lodr/Services/CapacityService.cs` — capacity math + FullState enum
- `src/Lodr/Services/SlotService.cs` — next-slot computation with overlap detection
- `src/Lodr/State/WorkspaceState.cs` — replaces AppState
- `src/Lodr/Services/AppLogService.cs` — ILoggerProvider + buffer
- `src/Lodr/Components/Layout/AppHeader.razor` — brand + workspace name + save + log badge
- `src/Lodr/Components/Layout/StatusBar.razor` — global counts + autosave
- `src/Lodr/Components/Layout/Sidebar.razor` — tab strip + panel body
- `src/Lodr/Components/Workspace/PalettePanel.razor` — pallet type cards
- `src/Lodr/Components/Workspace/WorkspacePanel.razor` — save/load/presets
- `src/Lodr/Components/Workspace/TrailerCanvas.razor` — multi-trailer grid + tab strip
- `src/Lodr/Components/Workspace/TrailerCard.razor` — single trailer tile
- `src/Lodr/Components/Workspace/PalletCell.razor` — single pallet cell
- `src/Lodr/Components/Workspace/TrailerInspector.razor` — right panel stats
- `src/Lodr/Components/Workspace/AddTrailerDialog.razor` — add trailer modal
- `src/Lodr/Components/Workspace/LogPanel.razor` — slide-over log viewer
- `tests/Lodr.Tests/Services/CapacityServiceTests.cs`
- `tests/Lodr.Tests/Services/SlotServiceTests.cs`
- `tests/Lodr.Tests/State/WorkspaceStateTests.cs`

### Modified files
- `src/Lodr/Models/` — keep TrailerDimensions, TrailerType; delete PalletSpec, PalletPosition, PlacementResult
- `src/Lodr/Services/OrientationService.cs` — add PalletType overload
- `src/Lodr/wwwroot/js/indexeddb-interop.js` — bump DB_VERSION to 2, add new stores
- `src/Lodr/Components/Layout/AppShell.razor` — full five-panel layout
- `src/Lodr/Pages/Index.razor` — render AppShell directly (remove step switch)
- `src/Lodr/_Imports.razor` — add Workspace namespace, remove Steps namespace
- `src/Lodr/Program.cs` — register new services, remove old

### Deleted files (Task 14)
- `src/Lodr/Models/PalletSpec.cs`
- `src/Lodr/Models/PalletPosition.cs`
- `src/Lodr/Models/PlacementResult.cs`
- `src/Lodr/Services/FitCalculator.cs`
- `src/Lodr/Services/PlacementEngine.cs`
- `src/Lodr/Components/Steps/TrailerStep.razor`
- `src/Lodr/Components/Steps/PalletStep.razor`
- `src/Lodr/Components/Steps/ResultStep.razor`
- `src/Lodr/Components/Steps/StepContent.razor`
- `src/Lodr/Components/Layout/StepNav.razor`
- `src/Lodr/State/AppState.cs`
- `tests/Lodr.Tests/Services/FitCalculatorTests.cs`
- `tests/Lodr.Tests/Services/PlacementEngineTests.cs`
- `tests/Lodr.Tests/Components/TrailerStepTests.cs`
- `tests/Lodr.Tests/Components/PalletStepTests.cs`
- `tests/Lodr.Tests/Components/ResultStepTests.cs`
- `tests/Lodr.Tests/CounterCSharpTest.cs`

---

### Task 1: Domain Models + OrientationService Overload

**Files:**
- Create: `src/Lodr/Models/Workspace.cs`
- Modify: `src/Lodr/Services/OrientationService.cs`

- [ ] **Step 1: Write the new domain models**

```csharp
// src/Lodr/Models/Workspace.cs
namespace Lodr.Models;

public class Workspace
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New Workspace";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<TrailerInstance> Trailers { get; set; } = [];
    public List<PalletType> PalletTypes { get; set; } = [];
}

public class TrailerInstance
{
    public string Id { get; set; } = string.Empty;        // "T-01", "T-02"
    public string? FriendlyName { get; set; }
    public TrailerDimensions Dimensions { get; set; } = new(0, 0, 0);
    public List<PlacedPallet> PlacedPallets { get; set; } = [];
}

public record PalletType(
    string Id,
    string Name,
    float Length,
    float Width,
    float Height,
    bool CanRotate,
    string Color
);

public record PlacedPallet(
    int Index,
    string TypeId,
    float X,
    float Y,
    bool Rotated
);

public class WorkspacePreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public List<PresetTrailer> DefaultTrailers { get; set; } = [];
    public List<PalletType> PalletTypes { get; set; } = [];
}

public record PresetTrailer(string Label, TrailerDimensions Dimensions);
```

- [ ] **Step 2: Add PalletType overload to OrientationService**

Replace full content of `src/Lodr/Services/OrientationService.cs`:

```csharp
using Lodr.Models;

namespace Lodr.Services;

public class OrientationService
{
    public IReadOnlyList<(float Length, float Width)> GetOrientations(PalletSpec spec)
        => GetOrientationsCore(spec.Length, spec.Width, spec.CanRotate);

    public IReadOnlyList<(float Length, float Width)> GetOrientations(PalletType type)
        => GetOrientationsCore(type.Length, type.Width, type.CanRotate);

    private static IReadOnlyList<(float Length, float Width)> GetOrientationsCore(
        float length, float width, bool canRotate)
    {
        var original = (length, width);
        if (!canRotate || length == width) return [original];
        return [original, (width, length)];
    }
}
```

- [ ] **Step 3: Build to verify no compile errors**

Run: `dotnet build src/Lodr/Lodr.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Lodr/Models/Workspace.cs src/Lodr/Services/OrientationService.cs
git commit -m "feat: add workspace domain models and PalletType overload for OrientationService"
```

---

### Task 2: CapacityService

**Files:**
- Create: `src/Lodr/Services/CapacityService.cs`
- Create: `tests/Lodr.Tests/Services/CapacityServiceTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Lodr.Tests/Services/CapacityServiceTests.cs
using Lodr.Models;
using Lodr.Services;

namespace Lodr.Tests.Services;

public class CapacityServiceTests
{
    private readonly CapacityService _svc = new(new OrientationService());

    private static PalletType GMA => new("gma", "GMA", 48f, 40f, 48f, true, "#22c55e");
    private static PalletType EUR => new("eur", "EUR", 47.24f, 39.37f, 57.09f, true, "#3b82f6");
    private static TrailerDimensions Trailer53 => new(630f, 98f, 110f);

    [Fact]
    public void GetCapacity_GmaIn53_Returns30()
    {
        // floor(630/48) * floor(98/40) = 13 * 2 = 26
        // rotated: floor(630/40) * floor(98/48) = 15 * 2 = 30
        var result = _svc.GetCapacity(Trailer53, GMA);
        Assert.Equal(30, result);
    }

    [Fact]
    public void GetCapacity_NoRotate_UsesOriginalOnly()
    {
        var noRotate = new PalletType("x", "X", 48f, 40f, 48f, false, "#fff");
        // floor(630/48) * floor(98/40) = 13 * 2 = 26
        var result = _svc.GetCapacity(Trailer53, noRotate);
        Assert.Equal(26, result);
    }

    [Fact]
    public void GetUsedFraction_ZeroPlaced_ReturnsZero()
    {
        var trailer = new TrailerInstance { Id = "T-01", Dimensions = Trailer53 };
        var types = new List<PalletType> { GMA };
        Assert.Equal(0f, _svc.GetUsedFraction(trailer, types));
    }

    [Fact]
    public void GetUsedFraction_HalfFull_Returns0Point5()
    {
        var trailer = new TrailerInstance { Id = "T-01", Dimensions = Trailer53 };
        // capacity = 30, place 15
        for (int i = 0; i < 15; i++)
            trailer.PlacedPallets.Add(new PlacedPallet(i + 1, "gma", 0, 0, false));
        var types = new List<PalletType> { GMA };
        var fraction = _svc.GetUsedFraction(trailer, types);
        Assert.Equal(0.5f, fraction, precision: 4);
    }

    [Fact]
    public void GetFullState_Under80_IsNormal()
    {
        var trailer = new TrailerInstance { Id = "T-01", Dimensions = Trailer53 };
        var types = new List<PalletType> { GMA };
        Assert.Equal(FullState.Normal, _svc.GetFullState(trailer, types));
    }

    [Fact]
    public void GetFullState_At80Percent_IsNearFull()
    {
        var trailer = new TrailerInstance { Id = "T-01", Dimensions = Trailer53 };
        for (int i = 0; i < 24; i++) // 24/30 = 0.8
            trailer.PlacedPallets.Add(new PlacedPallet(i + 1, "gma", 0, 0, false));
        var types = new List<PalletType> { GMA };
        Assert.Equal(FullState.NearFull, _svc.GetFullState(trailer, types));
    }

    [Fact]
    public void GetFullState_At100Percent_IsFull()
    {
        var trailer = new TrailerInstance { Id = "T-01", Dimensions = Trailer53 };
        for (int i = 0; i < 30; i++)
            trailer.PlacedPallets.Add(new PlacedPallet(i + 1, "gma", 0, 0, false));
        var types = new List<PalletType> { GMA };
        Assert.Equal(FullState.Full, _svc.GetFullState(trailer, types));
    }

    [Fact]
    public void GetCapacity_EmptyTypes_ReturnsZeroFraction()
    {
        var trailer = new TrailerInstance { Id = "T-01", Dimensions = Trailer53 };
        Assert.Equal(0f, _svc.GetUsedFraction(trailer, []));
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure (CapacityService not defined)**

Run: `dotnet test tests/Lodr.Tests/Lodr.Tests.csproj --filter "CapacityServiceTests" 2>&1 | head -20`
Expected: Build error — `CapacityService` not found.

- [ ] **Step 3: Implement CapacityService**

```csharp
// src/Lodr/Services/CapacityService.cs
using Lodr.Models;

namespace Lodr.Services;

public enum FullState { Normal, NearFull, Full }

public class CapacityService(OrientationService orientationService)
{
    public int GetCapacity(TrailerDimensions trailer, PalletType type)
    {
        var orientations = orientationService.GetOrientations(type);
        int best = 0;
        foreach (var (length, width) in orientations)
        {
            int fit = (int)Math.Floor(trailer.Length / length)
                    * (int)Math.Floor(trailer.Width / width);
            if (fit > best) best = fit;
        }
        return best;
    }

    public float GetUsedFraction(TrailerInstance trailer, IEnumerable<PalletType> types)
    {
        var typeList = types.ToList();
        if (typeList.Count == 0) return 0f;
        int maxCap = typeList.Max(t => GetCapacity(trailer.Dimensions, t));
        if (maxCap == 0) return 0f;
        return (float)trailer.PlacedPallets.Count / maxCap;
    }

    public FullState GetFullState(TrailerInstance trailer, IEnumerable<PalletType> types)
    {
        float fraction = GetUsedFraction(trailer, types);
        return fraction >= 1f ? FullState.Full
             : fraction >= 0.8f ? FullState.NearFull
             : FullState.Normal;
    }
}
```

- [ ] **Step 4: Run tests — expect all pass**

Run: `dotnet test tests/Lodr.Tests/Lodr.Tests.csproj --filter "CapacityServiceTests" -v minimal`
Expected: 7 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Lodr/Services/CapacityService.cs tests/Lodr.Tests/Services/CapacityServiceTests.cs
git commit -m "feat: add CapacityService with full-state thresholds (TDD)"
```

---

### Task 3: SlotService

**Files:**
- Create: `src/Lodr/Services/SlotService.cs`
- Create: `tests/Lodr.Tests/Services/SlotServiceTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Lodr.Tests/Services/SlotServiceTests.cs
using Lodr.Models;
using Lodr.Services;

namespace Lodr.Tests.Services;

public class SlotServiceTests
{
    private static readonly OrientationService _orient = new();
    private static readonly CapacityService _cap = new(_orient);
    private readonly SlotService _svc = new(_orient, _cap);

    private static PalletType GMA => new("gma", "GMA", 48f, 40f, 48f, true, "#22c55e");
    private static TrailerDimensions Trailer53 => new(630f, 98f, 110f);

    [Fact]
    public void FindNextSlot_EmptyTrailer_ReturnsFirstSlot()
    {
        var slot = _svc.FindNextSlot(Trailer53, GMA, [], [GMA], 1);
        Assert.NotNull(slot);
        Assert.Equal(1, slot!.Index);
        Assert.Equal(0f, slot.X);
        Assert.Equal(0f, slot.Y);
    }

    [Fact]
    public void FindNextSlot_FullTrailer_ReturnsNull()
    {
        // 30 pallets fill a 53' trailer with GMA rotated
        var placed = Enumerable.Range(1, 30)
            .Select(i => new PlacedPallet(i, "gma", 0, 0, false))
            .ToList();
        // Fill them with correct positions via repeated calls
        var filledTrailer = new TrailerInstance { Id = "T-01", Dimensions = Trailer53 };
        var currentPlaced = new List<PlacedPallet>();
        for (int i = 1; i <= 30; i++)
        {
            var slot = _svc.FindNextSlot(Trailer53, GMA, currentPlaced, [GMA], i);
            Assert.NotNull(slot);
            currentPlaced.Add(slot!);
        }
        var overflow = _svc.FindNextSlot(Trailer53, GMA, currentPlaced, [GMA], 31);
        Assert.Null(overflow);
    }

    [Fact]
    public void FindNextSlot_SecondSlot_IsAdjacentToFirst()
    {
        var first = _svc.FindNextSlot(Trailer53, GMA, [], [GMA], 1)!;
        var second = _svc.FindNextSlot(Trailer53, GMA, [first], [GMA], 2)!;
        Assert.NotNull(second);
        // Second slot should differ in X or Y from first
        Assert.False(first.X == second.X && first.Y == second.Y);
    }

    [Fact]
    public void FindNextSlot_UsesRotatedOrientation_WhenBetter()
    {
        // GMA rotated (40x48) fits better in 98" wide trailer
        var slot = _svc.FindNextSlot(Trailer53, GMA, [], [GMA], 1);
        Assert.NotNull(slot);
        // Rotated means pallet.Length=40, pallet.Width=48
        // First slot: X=0, Y=0; width used is 48 (rotated)
        // Just assert no crash and valid position
        Assert.True(slot!.X >= 0 && slot.Y >= 0);
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

Run: `dotnet test tests/Lodr.Tests/Lodr.Tests.csproj --filter "SlotServiceTests" 2>&1 | head -20`
Expected: Build error — `SlotService` not found.

- [ ] **Step 3: Implement SlotService**

```csharp
// src/Lodr/Services/SlotService.cs
using Lodr.Models;

namespace Lodr.Services;

public class SlotService(OrientationService orientationService, CapacityService capacityService)
{
    public PlacedPallet? FindNextSlot(
        TrailerDimensions trailer,
        PalletType type,
        IReadOnlyList<PlacedPallet> existing,
        IReadOnlyList<PalletType> allTypes,
        int nextIndex)
    {
        // Pick best orientation for this type
        var orientations = orientationService.GetOrientations(type);
        (float length, float width) bestOrientation = orientations[0];
        int bestFit = 0;
        foreach (var o in orientations)
        {
            int fit = (int)Math.Floor(trailer.Length / o.Length)
                    * (int)Math.Floor(trailer.Width / o.Width);
            if (fit > bestFit) { bestFit = fit; bestOrientation = o; }
        }

        bool rotated = bestOrientation.Length != type.Length;
        float palletL = bestOrientation.Length;
        float palletW = bestOrientation.Width;
        int cols = (int)Math.Floor(trailer.Length / palletL);
        int rows = (int)Math.Floor(trailer.Width / palletW);

        // Build a lookup of existing pallet rects (using their actual type dims)
        var existingRects = existing.Select(p =>
        {
            var t = allTypes.FirstOrDefault(t => t.Id == p.TypeId);
            float l = t?.Length ?? palletL;
            float w = t?.Width ?? palletW;
            if (p.Rotated) (l, w) = (w, l);
            return (p.X, p.Y, l, w);
        }).ToList();

        for (int row = 0; row < rows; row++)
        for (int col = 0; col < cols; col++)
        {
            float x = col * palletL;
            float y = row * palletW;
            if (!Overlaps(x, y, palletL, palletW, existingRects))
                return new PlacedPallet(nextIndex, type.Id, x, y, rotated);
        }

        return null;
    }

    private static bool Overlaps(
        float x, float y, float l, float w,
        List<(float X, float Y, float L, float W)> rects)
    {
        foreach (var r in rects)
        {
            bool xOverlap = x < r.X + r.L && x + l > r.X;
            bool yOverlap = y < r.Y + r.W && y + w > r.Y;
            if (xOverlap && yOverlap) return true;
        }
        return false;
    }
}
```

- [ ] **Step 4: Run tests — expect pass**

Run: `dotnet test tests/Lodr.Tests/Lodr.Tests.csproj --filter "SlotServiceTests" -v minimal`
Expected: 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Lodr/Services/SlotService.cs tests/Lodr.Tests/Services/SlotServiceTests.cs
git commit -m "feat: add SlotService for next-slot computation with overlap detection (TDD)"
```

---

### Task 4: WorkspaceState

**Files:**
- Create: `src/Lodr/State/WorkspaceState.cs`
- Create: `tests/Lodr.Tests/State/WorkspaceStateTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Lodr.Tests/State/WorkspaceStateTests.cs
using Lodr.Models;
using Lodr.Services;
using Lodr.State;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lodr.Tests.State;

public class WorkspaceStateTests
{
    private static WorkspaceState MakeState()
    {
        var orient = new OrientationService();
        var cap = new CapacityService(orient);
        var slot = new SlotService(orient, cap);
        return new WorkspaceState(cap, slot, NullLogger<WorkspaceState>.Instance);
    }

    private static PalletType GMA => new("gma", "GMA", 48f, 40f, 48f, true, "#22c55e");

    [Fact]
    public void NewBlankWorkspace_SetsCurrentWorkspace()
    {
        var state = MakeState();
        state.NewBlankWorkspace();
        Assert.NotNull(state.Current);
        Assert.Equal("New Workspace", state.Current!.Name);
    }

    [Fact]
    public void AddTrailer_AppendsTrailer()
    {
        var state = MakeState();
        state.NewBlankWorkspace();
        state.AddTrailer(new TrailerDimensions(630f, 98f, 110f), "Morning Run");
        Assert.Single(state.Current!.Trailers);
        Assert.Equal("T-01", state.Current.Trailers[0].Id);
        Assert.Equal("Morning Run", state.Current.Trailers[0].FriendlyName);
    }

    [Fact]
    public void AddTrailer_IdsIncrementCorrectly()
    {
        var state = MakeState();
        state.NewBlankWorkspace();
        state.AddTrailer(new TrailerDimensions(630f, 98f, 110f));
        state.AddTrailer(new TrailerDimensions(576f, 98f, 110f));
        Assert.Equal("T-01", state.Current!.Trailers[0].Id);
        Assert.Equal("T-02", state.Current.Trailers[1].Id);
    }

    [Fact]
    public void RemoveTrailer_RemovesCorrectTrailer()
    {
        var state = MakeState();
        state.NewBlankWorkspace();
        state.AddTrailer(new TrailerDimensions(630f, 98f, 110f));
        state.AddTrailer(new TrailerDimensions(576f, 98f, 110f));
        state.RemoveTrailer("T-01");
        Assert.Single(state.Current!.Trailers);
        Assert.Equal("T-02", state.Current.Trailers[0].Id);
    }

    [Fact]
    public void AddPalletType_AppendsType()
    {
        var state = MakeState();
        state.NewBlankWorkspace();
        state.AddPalletType(GMA);
        Assert.Single(state.Current!.PalletTypes);
    }

    [Fact]
    public void TryAddPallet_NoTrailerSelected_ReturnsFalse()
    {
        var state = MakeState();
        state.NewBlankWorkspace();
        state.AddTrailer(new TrailerDimensions(630f, 98f, 110f));
        state.AddPalletType(GMA);
        Assert.False(state.TryAddPallet(null, "gma"));
    }

    [Fact]
    public void TryAddPallet_ValidTrailerAndType_AddsPallet()
    {
        var state = MakeState();
        state.NewBlankWorkspace();
        state.AddTrailer(new TrailerDimensions(630f, 98f, 110f));
        state.AddPalletType(GMA);
        var added = state.TryAddPallet("T-01", "gma");
        Assert.True(added);
        Assert.Single(state.Current!.Trailers[0].PlacedPallets);
        Assert.Equal(1, state.Current.Trailers[0].PlacedPallets[0].Index);
    }

    [Fact]
    public void RemovePallet_RemovesCorrectPallet()
    {
        var state = MakeState();
        state.NewBlankWorkspace();
        state.AddTrailer(new TrailerDimensions(630f, 98f, 110f));
        state.AddPalletType(GMA);
        state.TryAddPallet("T-01", "gma");
        state.TryAddPallet("T-01", "gma");
        state.RemovePallet("T-01", 1);
        Assert.Single(state.Current!.Trailers[0].PlacedPallets);
        Assert.Equal(2, state.Current.Trailers[0].PlacedPallets[0].Index);
    }

    [Fact]
    public void OnChange_FiredOnAddTrailer()
    {
        var state = MakeState();
        state.NewBlankWorkspace();
        bool fired = false;
        state.OnChange += () => fired = true;
        state.AddTrailer(new TrailerDimensions(630f, 98f, 110f));
        Assert.True(fired);
    }

    [Fact]
    public void OpenTrailerTab_AddsToOpenTabs()
    {
        var state = MakeState();
        state.NewBlankWorkspace();
        state.AddTrailer(new TrailerDimensions(630f, 98f, 110f));
        state.OpenTrailerTab("T-01");
        Assert.Contains("T-01", state.OpenTabIds);
    }

    [Fact]
    public void CloseTrailerTab_RemovesFromOpenTabs()
    {
        var state = MakeState();
        state.NewBlankWorkspace();
        state.AddTrailer(new TrailerDimensions(630f, 98f, 110f));
        state.OpenTrailerTab("T-01");
        state.CloseTrailerTab("T-01");
        Assert.DoesNotContain("T-01", state.OpenTabIds);
    }
}
```

- [ ] **Step 2: Run — expect compile failure**

Run: `dotnet test tests/Lodr.Tests/Lodr.Tests.csproj --filter "WorkspaceStateTests" 2>&1 | head -20`
Expected: Build error — `WorkspaceState` not found.

- [ ] **Step 3: Implement WorkspaceState**

```csharp
// src/Lodr/State/WorkspaceState.cs
using Lodr.Models;
using Lodr.Services;
using Microsoft.Extensions.Logging;

namespace Lodr.State;

public class WorkspaceState(
    CapacityService capacityService,
    SlotService slotService,
    ILogger<WorkspaceState> logger)
{
    public event Action? OnChange;

    public Workspace? Current { get; private set; }
    public string? SelectedTrailerId { get; private set; }
    public List<string> OpenTabIds { get; } = [];
    public bool IsDirty { get; private set; }

    public void LoadWorkspace(Workspace ws)
    {
        Current = ws;
        IsDirty = false;
        Notify();
    }

    public void NewBlankWorkspace()
    {
        Current = new Workspace();
        IsDirty = false;
        Notify();
    }

    // Trailer management
    public void AddTrailer(TrailerDimensions dims, string? friendlyName = null)
    {
        if (Current is null) return;
        int n = Current.Trailers.Count + 1;
        Current.Trailers.Add(new TrailerInstance
        {
            Id = $"T-{n:D2}",
            FriendlyName = friendlyName,
            Dimensions = dims
        });
        Touch();
    }

    public void RemoveTrailer(string trailerId)
    {
        if (Current is null) return;
        Current.Trailers.RemoveAll(t => t.Id == trailerId);
        OpenTabIds.Remove(trailerId);
        if (SelectedTrailerId == trailerId) SelectedTrailerId = null;
        Touch();
    }

    public void RenameTrailer(string trailerId, string friendlyName)
    {
        var trailer = GetTrailer(trailerId);
        if (trailer is null) return;
        trailer.FriendlyName = friendlyName;
        Touch();
    }

    public void SelectTrailer(string? trailerId)
    {
        SelectedTrailerId = trailerId;
        Notify();
    }

    public void OpenTrailerTab(string trailerId)
    {
        if (!OpenTabIds.Contains(trailerId))
            OpenTabIds.Add(trailerId);
        Notify();
    }

    public void CloseTrailerTab(string trailerId)
    {
        OpenTabIds.Remove(trailerId);
        Notify();
    }

    // Pallet type management
    public void AddPalletType(PalletType type)
    {
        Current?.PalletTypes.Add(type);
        Touch();
    }

    public void RemovePalletType(string typeId)
    {
        Current?.PalletTypes.RemoveAll(t => t.Id == typeId);
        Touch();
    }

    // Pallet placement
    public bool TryAddPallet(string? trailerId, string typeId)
    {
        if (Current is null || trailerId is null) return false;
        var trailer = GetTrailer(trailerId);
        var type = Current.PalletTypes.FirstOrDefault(t => t.Id == typeId);
        if (trailer is null || type is null)
        {
            logger.LogWarning("TryAddPallet: trailer={TrailerId} type={TypeId} not found", trailerId, typeId);
            return false;
        }
        if (capacityService.GetFullState(trailer, Current.PalletTypes) == FullState.Full)
        {
            logger.LogInformation("TryAddPallet: {TrailerId} is full", trailerId);
            return false;
        }
        int nextIndex = trailer.PlacedPallets.Count == 0
            ? 1
            : trailer.PlacedPallets.Max(p => p.Index) + 1;
        var slot = slotService.FindNextSlot(
            trailer.Dimensions, type, trailer.PlacedPallets, Current.PalletTypes, nextIndex);
        if (slot is null) return false;
        trailer.PlacedPallets.Add(slot);
        Touch();
        return true;
    }

    public void RemovePallet(string trailerId, int palletIndex)
    {
        var trailer = GetTrailer(trailerId);
        trailer?.PlacedPallets.RemoveAll(p => p.Index == palletIndex);
        Touch();
    }

    public bool TryMovePallet(string fromTrailerId, int palletIndex, string toTrailerId)
    {
        if (Current is null) return false;
        var from = GetTrailer(fromTrailerId);
        var to = GetTrailer(toTrailerId);
        if (from is null || to is null) return false;
        var pallet = from.PlacedPallets.FirstOrDefault(p => p.Index == palletIndex);
        if (pallet is null) return false;
        var type = Current.PalletTypes.FirstOrDefault(t => t.Id == pallet.TypeId);
        if (type is null) return false;
        if (capacityService.GetFullState(to, Current.PalletTypes) == FullState.Full) return false;
        int nextIndex = to.PlacedPallets.Count == 0 ? 1 : to.PlacedPallets.Max(p => p.Index) + 1;
        var slot = slotService.FindNextSlot(
            to.Dimensions, type, to.PlacedPallets, Current.PalletTypes, nextIndex);
        if (slot is null) return false;
        from.PlacedPallets.RemoveAll(p => p.Index == palletIndex);
        to.PlacedPallets.Add(slot);
        Touch();
        return true;
    }

    public void SwapPalletType(string trailerId, int palletIndex, string newTypeId)
    {
        var trailer = GetTrailer(trailerId);
        if (trailer is null) return;
        int idx = trailer.PlacedPallets.FindIndex(p => p.Index == palletIndex);
        if (idx < 0) return;
        var old = trailer.PlacedPallets[idx];
        trailer.PlacedPallets[idx] = old with { TypeId = newTypeId };
        Touch();
    }

    private TrailerInstance? GetTrailer(string id) =>
        Current?.Trailers.FirstOrDefault(t => t.Id == id);

    private void Touch()
    {
        if (Current is not null) Current.UpdatedAt = DateTime.UtcNow;
        IsDirty = true;
        Notify();
    }

    private void Notify() => OnChange?.Invoke();
}
```

- [ ] **Step 4: Run tests — expect pass**

Run: `dotnet test tests/Lodr.Tests/Lodr.Tests.csproj --filter "WorkspaceStateTests" -v minimal`
Expected: 11 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Lodr/State/WorkspaceState.cs tests/Lodr.Tests/State/WorkspaceStateTests.cs
git commit -m "feat: add WorkspaceState (TDD) replacing AppState"
```

---

### Task 5: AppLogService

**Files:**
- Create: `src/Lodr/Services/AppLogService.cs`

- [ ] **Step 1: Implement AppLogService**

```csharp
// src/Lodr/Services/AppLogService.cs
using Microsoft.Extensions.Logging;

namespace Lodr.Services;

public record LogEntry(
    LogLevel Level,
    string Source,
    string Message,
    DateTime Timestamp
);

public class AppLogService : ILoggerProvider
{
    private const int MaxEntries = 500;
    private readonly Queue<LogEntry> _entries = new();

    public event Action? OnChange;

    public IReadOnlyList<LogEntry> Entries => _entries.ToList();
    public int WarningCount => _entries.Count(e => e.Level >= LogLevel.Warning);

    public ILogger CreateLogger(string categoryName) =>
        new AppLogger(categoryName, this);

    public void AddEntry(LogLevel level, string source, string message)
    {
        if (_entries.Count >= MaxEntries) _entries.Dequeue();
        _entries.Enqueue(new LogEntry(level, source, message, DateTime.UtcNow));

        if (level >= LogLevel.Warning)
            Console.Error.WriteLine($"[{level}] {source}: {message}");

        OnChange?.Invoke();
    }

    public string ExportText() =>
        string.Join('\n', _entries.Select(e =>
            $"[{e.Timestamp:HH:mm:ss}] [{e.Level}] {e.Source}: {e.Message}"));

    public void Dispose() { }

    private sealed class AppLogger(string category, AppLogService service) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Information;

        public void Log<TState>(
            LogLevel level, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(level)) return;
            var msg = formatter(state, exception);
            if (exception is not null) msg += $"\n{exception}";
            service.AddEntry(level, category, msg);
        }
    }
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build src/Lodr/Lodr.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/Lodr/Services/AppLogService.cs
git commit -m "feat: add AppLogService as ILoggerProvider with 500-entry buffer and terminal output"
```

---

### Task 6: IndexedDB Extension + WorkspacePersistenceInterop

**Files:**
- Modify: `src/Lodr/wwwroot/js/indexeddb-interop.js`
- Create: `src/Lodr/Interop/WorkspacePersistenceInterop.cs`

- [ ] **Step 1: Bump IndexedDB to version 2 with new stores**

Replace full content of `src/Lodr/wwwroot/js/indexeddb-interop.js`:

```javascript
const DB_NAME = 'lodr-db';
const DB_VERSION = 2;
const STORES = [
    'trailer-presets',
    'pallet-presets',
    'last-used',
    'workspaces',
    'workspace-presets',
    'last-workspace-id',
];

let _dbPromise = null;

function openDB() {
    if (_dbPromise) return _dbPromise;
    _dbPromise = new Promise((resolve, reject) => {
        const req = indexedDB.open(DB_NAME, DB_VERSION);
        req.onupgradeneeded = e => {
            const db = e.target.result;
            STORES.forEach(name => {
                if (!db.objectStoreNames.contains(name))
                    db.createObjectStore(name, { keyPath: 'id' });
            });
        };
        req.onsuccess = e => resolve(e.target.result);
        req.onerror = e => { _dbPromise = null; reject(e.target.error); };
    });
    return _dbPromise;
}

function tx(storeName, mode, fn) {
    return openDB().then(db => new Promise((resolve, reject) => {
        const t = db.transaction(storeName, mode);
        const store = t.objectStore(storeName);
        const req = fn(store);
        req.onsuccess = () => resolve(req.result ?? null);
        req.onerror = () => reject(req.error);
    }));
}

export function saveItem(storeName, item) {
    return tx(storeName, 'readwrite', store => store.put(item));
}

export function getItem(storeName, id) {
    return tx(storeName, 'readonly', store => store.get(id));
}

export function getAllItems(storeName) {
    return openDB().then(db => new Promise((resolve, reject) => {
        const t = db.transaction(storeName, 'readonly');
        const req = t.objectStore(storeName).getAll();
        req.onsuccess = () => resolve(req.result);
        req.onerror = () => reject(req.error);
    }));
}

export function deleteItem(storeName, id) {
    return tx(storeName, 'readwrite', store => store.delete(id));
}
```

- [ ] **Step 2: Create WorkspacePersistenceInterop**

```csharp
// src/Lodr/Interop/WorkspacePersistenceInterop.cs
using Lodr.Models;
using Microsoft.JSInterop;
using System.Text.Json;

namespace Lodr.Interop;

public class WorkspacePersistenceInterop(IJSRuntime js)
{
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task SaveWorkspaceAsync(Workspace ws)
    {
        ws.UpdatedAt = DateTime.UtcNow;
        var obj = JsonSerializer.SerializeToElement(ws, _json);
        await js.InvokeVoidAsync("lodrDb.saveItem", "workspaces", obj);
    }

    public async Task<Workspace?> GetWorkspaceAsync(string id)
    {
        var result = await js.InvokeAsync<JsonElement?>("lodrDb.getItem", "workspaces", id);
        if (result is null) return null;
        return JsonSerializer.Deserialize<Workspace>(result.Value.GetRawText(), _json);
    }

    public async Task<List<Workspace>> GetAllWorkspacesAsync()
    {
        var result = await js.InvokeAsync<JsonElement[]>("lodrDb.getAllItems", "workspaces");
        return result
            .Select(e => JsonSerializer.Deserialize<Workspace>(e.GetRawText(), _json))
            .Where(w => w is not null)
            .Select(w => w!)
            .ToList();
    }

    public async Task DeleteWorkspaceAsync(string id) =>
        await js.InvokeVoidAsync("lodrDb.deleteItem", "workspaces", id);

    public async Task SavePresetAsync(WorkspacePreset preset)
    {
        var obj = JsonSerializer.SerializeToElement(preset, _json);
        await js.InvokeVoidAsync("lodrDb.saveItem", "workspace-presets", obj);
    }

    public async Task<List<WorkspacePreset>> GetAllPresetsAsync()
    {
        var result = await js.InvokeAsync<JsonElement[]>("lodrDb.getAllItems", "workspace-presets");
        return result
            .Select(e => JsonSerializer.Deserialize<WorkspacePreset>(e.GetRawText(), _json))
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();
    }

    public async Task SaveLastWorkspaceIdAsync(string id) =>
        await js.InvokeVoidAsync("lodrDb.saveItem", "last-workspace-id",
            new { id = "last", workspaceId = id });

    public async Task<string?> GetLastWorkspaceIdAsync()
    {
        var result = await js.InvokeAsync<JsonElement?>("lodrDb.getItem", "last-workspace-id", "last");
        if (result is null) return null;
        return result.Value.TryGetProperty("workspaceId", out var prop) ? prop.GetString() : null;
    }
}
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build src/Lodr/Lodr.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Lodr/wwwroot/js/indexeddb-interop.js src/Lodr/Interop/WorkspacePersistenceInterop.cs
git commit -m "feat: extend IndexedDB to version 2 with workspace stores; add WorkspacePersistenceInterop"
```

---

### Task 7: Program.cs + _Imports.razor Wiring

**Files:**
- Modify: `src/Lodr/Program.cs`
- Modify: `src/Lodr/_Imports.razor`

- [ ] **Step 1: Update Program.cs**

Replace full content of `src/Lodr/Program.cs`:

```csharp
using Lodr;
using Lodr.Interop;
using Lodr.Services;
using Lodr.State;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Logging
var logService = new AppLogService();
builder.Services.AddSingleton<AppLogService>(logService);
builder.Logging.ClearProviders();
builder.Logging.AddProvider(logService);

// Services
builder.Services.AddScoped<OrientationService>();
builder.Services.AddScoped<CapacityService>();
builder.Services.AddScoped<SlotService>();

// State
builder.Services.AddScoped<WorkspaceState>();

// Interop
builder.Services.AddScoped<PersistenceInterop>();
builder.Services.AddScoped<WorkspacePersistenceInterop>();
builder.Services.AddScoped<VisualizationInterop>();

await builder.Build().RunAsync();
```

- [ ] **Step 2: Update _Imports.razor**

Replace full content of `src/Lodr/_Imports.razor`:

```razor
@using System.Net.Http
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.AspNetCore.Components.Web.Virtualization
@using Microsoft.AspNetCore.Components.WebAssembly.Http
@using Microsoft.JSInterop
@using Lodr
@using Lodr.Models
@using Lodr.Services
@using Lodr.State
@using Lodr.Interop
@using Lodr.Components.Layout
@using Lodr.Components.Shared
@using Lodr.Components.Workspace
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build src/Lodr/Lodr.csproj`
Expected: Build succeeded (Steps namespace removed but step files still exist — that's fine, they're not referenced by _Imports anymore).

- [ ] **Step 4: Commit**

```bash
git add src/Lodr/Program.cs src/Lodr/_Imports.razor
git commit -m "feat: wire new services and state into DI; update global imports"
```

---

### Task 8: AppShell Layout Skeleton

**Files:**
- Modify: `src/Lodr/Components/Layout/AppShell.razor`
- Create: `src/Lodr/Components/Layout/AppHeader.razor`
- Create: `src/Lodr/Components/Layout/StatusBar.razor`

- [ ] **Step 1: Rewrite AppShell.razor**

Replace full content of `src/Lodr/Components/Layout/AppShell.razor`:

```razor
@inherits LayoutComponentBase
@inject WorkspaceState WS
@inject AppLogService LogService
@implements IDisposable

<div class="h-screen w-screen overflow-hidden flex flex-col bg-gray-950 text-white select-none">
    <AppHeader OnAddTrailer="ShowAddTrailerDialog" />

    <div class="flex-1 flex overflow-hidden">
        <Sidebar />

        <main class="flex-1 flex overflow-hidden">
            @if (WS.Current is null)
            {
                <div class="flex-1 flex items-center justify-center text-gray-600">
                    <div class="text-center">
                        <p class="text-2xl font-bold mb-2">No workspace open</p>
                        <p class="text-sm">Open the Workspaces panel to create or load a workspace.</p>
                    </div>
                </div>
            }
            else
            {
                <TrailerCanvas />
                <TrailerInspector />
            }
        </main>
    </div>

    <StatusBar />
</div>

@if (_showAddTrailer)
{
    <AddTrailerDialog OnConfirm="OnAddTrailerConfirmed" OnCancel="() => _showAddTrailer = false" />
}

@if (_showLog)
{
    <LogPanel OnClose="() => _showLog = false" />
}

@code {
    private bool _showAddTrailer;
    private bool _showLog;

    protected override void OnInitialized()
    {
        WS.OnChange += StateHasChanged;
        LogService.OnChange += StateHasChanged;
    }

    private void ShowAddTrailerDialog() => _showAddTrailer = true;

    private void OnAddTrailerConfirmed((TrailerDimensions dims, string? name) args)
    {
        _showAddTrailer = false;
        WS.AddTrailer(args.dims, args.name);
    }

    public void Dispose()
    {
        WS.OnChange -= StateHasChanged;
        LogService.OnChange -= StateHasChanged;
    }
}
```

- [ ] **Step 2: Create AppHeader.razor**

```razor
@* src/Lodr/Components/Layout/AppHeader.razor *@
@inject WorkspaceState WS
@inject AppLogService LogService

<header class="flex-none h-12 px-4 border-b border-gray-800 flex items-center gap-3 bg-gray-950 z-10">
    <div class="w-7 h-7 bg-blue-600 rounded flex items-center justify-center font-black text-xs flex-shrink-0">L</div>
    <span class="text-xs text-gray-500 font-semibold tracking-widest uppercase">LODR</span>

    @if (WS.Current is not null)
    {
        <div class="w-px h-5 bg-gray-700 mx-1"></div>
        @if (_editingName)
        {
            <input class="bg-transparent text-sm font-medium text-white border-b border-blue-500 outline-none w-48"
                   @bind="_nameBuffer"
                   @onblur="CommitName"
                   @onkeydown="OnNameKeyDown"
                   @ref="_nameInput" />
        }
        else
        {
            <span class="text-sm font-medium cursor-pointer hover:text-blue-400 transition-colors"
                  @ondblclick="StartEditName">
                @WS.Current.Name
            </span>
        }
    }

    <div class="flex-1"></div>

    @if (LogService.WarningCount > 0)
    {
        <button class="flex items-center gap-1 text-xs text-amber-400 hover:text-amber-300 transition-colors"
                @onclick="OnLogClick">
            <span>⚠</span>
            <span>@LogService.WarningCount</span>
        </button>
    }

    @if (WS.Current is not null)
    {
        <button class="px-3 py-1 text-xs bg-blue-600 hover:bg-blue-500 rounded transition-colors font-medium"
                @onclick="Save">
            Save
        </button>
    }

    <button class="px-3 py-1 text-xs border border-gray-700 hover:border-gray-500 rounded transition-colors"
            @onclick="OnAddTrailer">
        + Trailer
    </button>
</header>

@code {
    [Parameter] public EventCallback OnAddTrailer { get; set; }
    [Parameter] public EventCallback OnLogOpen { get; set; }

    private bool _editingName;
    private string _nameBuffer = string.Empty;
    private ElementReference _nameInput;

    private async Task StartEditName()
    {
        _nameBuffer = WS.Current?.Name ?? string.Empty;
        _editingName = true;
        await Task.Delay(10);
        await _nameInput.FocusAsync();
    }

    private void CommitName()
    {
        if (WS.Current is not null && !string.IsNullOrWhiteSpace(_nameBuffer))
            WS.Current.Name = _nameBuffer.Trim();
        _editingName = false;
    }

    private void OnNameKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") CommitName();
        if (e.Key == "Escape") _editingName = false;
    }

    private void Save() { /* WorkspacePersistenceInterop injected in AppShell, exposed via WS event */ }
    private void OnLogClick() => OnLogOpen.InvokeAsync();
}
```

- [ ] **Step 3: Create StatusBar.razor**

```razor
@* src/Lodr/Components/Layout/StatusBar.razor *@
@inject WorkspaceState WS
@implements IDisposable

<footer class="flex-none h-8 px-4 border-t border-gray-800 flex items-center gap-4 text-xs text-gray-500 bg-gray-950">
    @if (WS.Current is not null)
    {
        <span>@WS.Current.Trailers.Count trailer@(WS.Current.Trailers.Count == 1 ? "" : "s")</span>
        <span>·</span>
        <span>@TotalPallets pallet@(TotalPallets == 1 ? "" : "s")</span>
        <span>·</span>
        <span class="@(WS.IsDirty ? "text-amber-500" : "text-green-600")">
            @(WS.IsDirty ? "Unsaved changes" : "Saved")
        </span>
    }
    else
    {
        <span>No workspace open</span>
    }
</footer>

@code {
    private int TotalPallets =>
        WS.Current?.Trailers.Sum(t => t.PlacedPallets.Count) ?? 0;

    protected override void OnInitialized() => WS.OnChange += StateHasChanged;
    public void Dispose() => WS.OnChange -= StateHasChanged;
}
```

- [ ] **Step 4: Build to verify (stubs for missing components OK)**

Run: `dotnet build src/Lodr/Lodr.csproj 2>&1 | grep -E "error|warning" | head -20`
Expected: Errors only for missing component stubs (Sidebar, TrailerCanvas, etc.) — not for AppShell/AppHeader/StatusBar themselves.

- [ ] **Step 5: Commit**

```bash
git add src/Lodr/Components/Layout/AppShell.razor src/Lodr/Components/Layout/AppHeader.razor src/Lodr/Components/Layout/StatusBar.razor
git commit -m "feat: five-panel AppShell layout with AppHeader and StatusBar"
```

---

### Task 9: Sidebar + PalettePanel + WorkspacePanel

**Files:**
- Create: `src/Lodr/Components/Layout/Sidebar.razor`
- Create: `src/Lodr/Components/Workspace/PalettePanel.razor`
- Create: `src/Lodr/Components/Workspace/WorkspacePanel.razor`

- [ ] **Step 1: Create Sidebar.razor**

```razor
@* src/Lodr/Components/Layout/Sidebar.razor *@
@inject WorkspaceState WS

<aside class="w-64 flex-shrink-0 flex flex-col border-r border-gray-800 bg-gray-900">
    <div class="flex border-b border-gray-800">
        <button class="flex-1 py-2 text-xs font-semibold uppercase tracking-wider transition-colors @(_activeTab == Tab.Palette ? "text-white border-b-2 border-blue-500" : "text-gray-500 hover:text-gray-300")"
                @onclick="() => _activeTab = Tab.Palette">
            Palette
        </button>
        <button class="flex-1 py-2 text-xs font-semibold uppercase tracking-wider transition-colors @(_activeTab == Tab.Workspaces ? "text-white border-b-2 border-blue-500" : "text-gray-500 hover:text-gray-300")"
                @onclick="() => _activeTab = Tab.Workspaces">
            Workspaces
        </button>
    </div>

    <div class="flex-1 overflow-y-auto">
        @if (_activeTab == Tab.Palette)
        {
            <PalettePanel />
        }
        else
        {
            <WorkspacePanel />
        }
    </div>
</aside>

@code {
    private enum Tab { Palette, Workspaces }
    private Tab _activeTab = Tab.Palette;
}
```

- [ ] **Step 2: Create PalettePanel.razor**

```razor
@* src/Lodr/Components/Workspace/PalettePanel.razor *@
@inject WorkspaceState WS

<div class="p-3 flex flex-col gap-2">
    <div class="flex items-center justify-between mb-1">
        <span class="text-xs text-gray-500 font-semibold uppercase tracking-wider">Pallet Types</span>
        <button class="text-xs text-blue-400 hover:text-blue-300 transition-colors"
                @onclick="ShowAddType">
            + Add
        </button>
    </div>

    @if (WS.Current is null)
    {
        <p class="text-xs text-gray-600 text-center py-4">Open a workspace to manage pallet types.</p>
    }
    else if (!WS.Current.PalletTypes.Any())
    {
        <p class="text-xs text-gray-600 text-center py-4">No pallet types yet. Click + Add to define one.</p>
    }
    else
    {
        @foreach (var type in WS.Current.PalletTypes)
        {
            <div class="rounded border border-gray-700 p-2 cursor-grab active:cursor-grabbing"
                 draggable="true"
                 @ondragstart="() => OnDragStart(type.Id)"
                 style="border-left: 3px solid @type.Color">
                <div class="flex items-center justify-between">
                    <span class="text-sm font-medium">@type.Name</span>
                    <button class="w-6 h-6 rounded bg-blue-700 hover:bg-blue-500 text-white text-xs font-bold transition-colors"
                            title="Add to selected trailer"
                            @onclick="() => AddToSelected(type.Id)"
                            @onclick:stopPropagation>
                        +
                    </button>
                </div>
                <p class="text-xs text-gray-500 mt-0.5">@type.Length" × @type.Width" × @type.Height"</p>
            </div>
        }
    }

    @if (_showAddType)
    {
        <div class="mt-2 p-2 rounded border border-gray-600 bg-gray-800 flex flex-col gap-2">
            <input class="bg-gray-700 rounded px-2 py-1 text-xs outline-none focus:ring-1 ring-blue-500"
                   placeholder="Name (e.g. GMA)"
                   @bind="_newName" />
            <div class="flex gap-1">
                <input class="bg-gray-700 rounded px-2 py-1 text-xs outline-none w-16" placeholder="L&quot;" @bind="_newL" />
                <input class="bg-gray-700 rounded px-2 py-1 text-xs outline-none w-16" placeholder="W&quot;" @bind="_newW" />
                <input class="bg-gray-700 rounded px-2 py-1 text-xs outline-none w-16" placeholder="H&quot;" @bind="_newH" />
            </div>
            <label class="flex items-center gap-2 text-xs text-gray-400">
                <input type="checkbox" @bind="_newCanRotate" /> Can rotate
            </label>
            <div class="flex gap-2">
                <button class="flex-1 py-1 text-xs bg-blue-600 hover:bg-blue-500 rounded transition-colors" @onclick="CommitAddType">Add</button>
                <button class="flex-1 py-1 text-xs border border-gray-600 hover:border-gray-400 rounded transition-colors" @onclick="() => _showAddType = false">Cancel</button>
            </div>
        </div>
    }
</div>

@code {
    private static readonly string[] ColorPool = ["#22c55e","#3b82f6","#f97316","#a855f7","#ec4899","#14b8a6"];
    private bool _showAddType;
    private string _newName = string.Empty;
    private float _newL = 48f, _newW = 40f, _newH = 48f;
    private bool _newCanRotate = true;

    private void ShowAddType()
    {
        _showAddType = true;
        _newName = string.Empty;
    }

    private void CommitAddType()
    {
        if (string.IsNullOrWhiteSpace(_newName) || WS.Current is null) return;
        int idx = WS.Current.PalletTypes.Count % ColorPool.Length;
        var type = new PalletType(
            Guid.NewGuid().ToString(), _newName.Trim(),
            _newL, _newW, _newH, _newCanRotate, ColorPool[idx]);
        WS.AddPalletType(type);
        _showAddType = false;
    }

    private void AddToSelected(string typeId) => WS.TryAddPallet(WS.SelectedTrailerId, typeId);

    private void OnDragStart(string typeId)
    {
        // Store dragged type id in state for drop handler
        _draggedTypeId = typeId;
    }

    private static string? _draggedTypeId;
    public static string? DraggedTypeId => _draggedTypeId;
    public static void ClearDrag() => _draggedTypeId = null;
}
```

- [ ] **Step 3: Create WorkspacePanel.razor**

```razor
@* src/Lodr/Components/Workspace/WorkspacePanel.razor *@
@inject WorkspaceState WS
@inject WorkspacePersistenceInterop Persistence
@implements IDisposable

<div class="p-3 flex flex-col gap-2">
    <div class="flex items-center justify-between mb-1">
        <span class="text-xs text-gray-500 font-semibold uppercase tracking-wider">Workspaces</span>
    </div>

    <button class="w-full py-1.5 text-xs bg-blue-700 hover:bg-blue-600 rounded transition-colors font-medium"
            @onclick="NewWorkspace">
        + New Workspace
    </button>

    @if (WS.Current is not null)
    {
        <div class="flex gap-1 mt-1">
            <button class="flex-1 py-1 text-xs bg-gray-700 hover:bg-gray-600 rounded transition-colors"
                    @onclick="SaveCurrent">
                Save
            </button>
            <button class="flex-1 py-1 text-xs border border-gray-700 hover:border-gray-500 rounded transition-colors"
                    @onclick="SaveAsNew">
                Save As…
            </button>
        </div>
        <button class="w-full py-1 text-xs text-red-400 hover:text-red-300 border border-gray-700 hover:border-red-800 rounded transition-colors mt-1"
                @onclick="CloseWorkspace">
            Close Workspace
        </button>
    }

    @if (_allWorkspaces.Any())
    {
        <div class="mt-3">
            <p class="text-xs text-gray-600 mb-1">Saved</p>
            @foreach (var ws in _allWorkspaces)
            {
                <div class="flex items-center gap-2 py-1 border-b border-gray-800 last:border-0">
                    <button class="flex-1 text-xs text-left hover:text-blue-400 transition-colors truncate"
                            @onclick="() => LoadWorkspace(ws)">
                        @ws.Name
                    </button>
                    <span class="text-xs text-gray-600">@ws.UpdatedAt.ToLocalTime().ToString("MM/dd HH:mm")</span>
                </div>
            }
        </div>
    }

    @if (_allPresets.Any())
    {
        <div class="mt-3">
            <p class="text-xs text-gray-600 mb-1">Presets</p>
            @foreach (var preset in _allPresets)
            {
                <button class="w-full text-xs text-left py-1 hover:text-blue-400 transition-colors truncate"
                        @onclick="() => LoadPreset(preset)">
                    @preset.Name
                </button>
            }
        </div>
    }

    @if (_promptSaveName)
    {
        <div class="mt-2 p-2 rounded border border-gray-600 bg-gray-800 flex flex-col gap-2">
            <input class="bg-gray-700 rounded px-2 py-1 text-xs outline-none focus:ring-1 ring-blue-500"
                   placeholder="Workspace name"
                   @bind="_saveNameBuffer" />
            <div class="flex gap-2">
                <button class="flex-1 py-1 text-xs bg-blue-600 hover:bg-blue-500 rounded" @onclick="CommitSaveAs">Save</button>
                <button class="flex-1 py-1 text-xs border border-gray-600 rounded" @onclick="() => _promptSaveName = false">Cancel</button>
            </div>
        </div>
    }
</div>

@code {
    private List<Workspace> _allWorkspaces = [];
    private List<WorkspacePreset> _allPresets = [];
    private bool _promptSaveName;
    private string _saveNameBuffer = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        WS.OnChange += StateHasChanged;
        await RefreshLists();
    }

    private async Task RefreshLists()
    {
        _allWorkspaces = await Persistence.GetAllWorkspacesAsync();
        _allPresets = await Persistence.GetAllPresetsAsync();
    }

    private void NewWorkspace()
    {
        WS.NewBlankWorkspace();
    }

    private async Task SaveCurrent()
    {
        if (WS.Current is null) return;
        await Persistence.SaveWorkspaceAsync(WS.Current);
        await Persistence.SaveLastWorkspaceIdAsync(WS.Current.Id);
        await RefreshLists();
    }

    private void SaveAsNew()
    {
        _saveNameBuffer = WS.Current?.Name ?? "New Workspace";
        _promptSaveName = true;
    }

    private async Task CommitSaveAs()
    {
        if (WS.Current is null || string.IsNullOrWhiteSpace(_saveNameBuffer)) return;
        var copy = new Workspace
        {
            Id = Guid.NewGuid().ToString(),
            Name = _saveNameBuffer.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Trailers = WS.Current.Trailers,
            PalletTypes = WS.Current.PalletTypes
        };
        await Persistence.SaveWorkspaceAsync(copy);
        _promptSaveName = false;
        await RefreshLists();
    }

    private void CloseWorkspace()
    {
        WS.LoadWorkspace(null!);   // clears current
    }

    private void LoadWorkspace(Workspace ws) => WS.LoadWorkspace(ws);

    private void LoadPreset(WorkspacePreset preset)
    {
        var ws = new Workspace
        {
            Name = preset.Name,
            PalletTypes = preset.PalletTypes,
            Trailers = preset.DefaultTrailers.Select((pt, i) => new TrailerInstance
            {
                Id = $"T-{i+1:D2}",
                FriendlyName = pt.Label,
                Dimensions = pt.Dimensions
            }).ToList()
        };
        WS.LoadWorkspace(ws);
    }

    public void Dispose() => WS.OnChange -= StateHasChanged;
}
```

- [ ] **Step 4: Build to verify**

Run: `dotnet build src/Lodr/Lodr.csproj 2>&1 | grep " error " | head -20`
Expected: Only errors for not-yet-created components (TrailerCanvas, TrailerInspector, AddTrailerDialog, LogPanel).

- [ ] **Step 5: Commit**

```bash
git add src/Lodr/Components/Layout/Sidebar.razor src/Lodr/Components/Workspace/PalettePanel.razor src/Lodr/Components/Workspace/WorkspacePanel.razor
git commit -m "feat: add Sidebar tab strip, PalettePanel with drag source, WorkspacePanel with save/load"
```

---

### Task 10: PalletCell + TrailerCard + AddTrailerDialog

**Files:**
- Create: `src/Lodr/Components/Workspace/PalletCell.razor`
- Create: `src/Lodr/Components/Workspace/TrailerCard.razor`
- Create: `src/Lodr/Components/Workspace/AddTrailerDialog.razor`

- [ ] **Step 1: Create PalletCell.razor**

```razor
@* src/Lodr/Components/Workspace/PalletCell.razor *@

<div class="relative flex flex-col items-center justify-center rounded text-white text-center cursor-default select-none transition-all"
     style="background-color: @BgColor; border: 1px solid @BorderColor; min-width: 48px; min-height: 40px; padding: 2px;"
     @oncontextmenu="OnContextMenu"
     @oncontextmenu:preventDefault>
    <span class="font-bold text-xs leading-none">@Pallet.Index</span>
    <span class="text-[9px] leading-none text-white/70 mt-0.5">@DimLabel</span>
</div>

@if (_showMenu)
{
    <div class="fixed inset-0 z-40" @onclick="CloseMenu"></div>
    <div class="fixed z-50 bg-gray-800 border border-gray-700 rounded shadow-xl py-1 text-xs"
         style="left: @(_menuX)px; top: @(_menuY)px; min-width: 160px;">
        <button class="w-full text-left px-3 py-1.5 hover:bg-gray-700 text-red-400"
                @onclick="Remove">Remove</button>
        @if (OtherTrailers.Any())
        {
            <div class="px-3 py-1 text-gray-500">Move to…</div>
            @foreach (var t in OtherTrailers)
            {
                <button class="w-full text-left px-4 py-1 hover:bg-gray-700"
                        @onclick="() => MoveTo(t.Id)">
                    @t.Id @(t.FriendlyName is not null ? $"· {t.FriendlyName}" : "")
                </button>
            }
        }
        @if (OtherTypes.Any())
        {
            <div class="px-3 py-1 text-gray-500">Swap type…</div>
            @foreach (var type in OtherTypes)
            {
                <button class="w-full text-left px-4 py-1 hover:bg-gray-700"
                        @onclick="() => SwapType(type.Id)">
                    <span class="inline-block w-2 h-2 rounded-full mr-1" style="background:@type.Color"></span>
                    @type.Name
                </button>
            }
        }
    </div>
}

@code {
    [Parameter, EditorRequired] public PlacedPallet Pallet { get; set; } = null!;
    [Parameter, EditorRequired] public string TrailerId { get; set; } = null!;
    [Parameter, EditorRequired] public PalletType PalletType { get; set; } = null!;
    [Parameter] public IReadOnlyList<TrailerInstance> OtherTrailers { get; set; } = [];
    [Parameter] public IReadOnlyList<PalletType> OtherTypes { get; set; } = [];
    [Parameter] public EventCallback<int> OnRemove { get; set; }
    [Parameter] public EventCallback<(int, string)> OnMove { get; set; }
    [Parameter] public EventCallback<(int, string)> OnSwapType { get; set; }

    [Inject] private WorkspaceState WS { get; set; } = null!;

    private bool _showMenu;
    private double _menuX, _menuY;

    private string BgColor => PalletType.Color;
    private string BorderColor => PalletType.Color + "cc";
    private string DimLabel => Pallet.Rotated
        ? $"{PalletType.Width}×{PalletType.Length}"
        : $"{PalletType.Length}×{PalletType.Width}";

    private void OnContextMenu(MouseEventArgs e)
    {
        _menuX = e.ClientX;
        _menuY = e.ClientY;
        _showMenu = true;
    }

    private void CloseMenu() => _showMenu = false;

    private void Remove()
    {
        _showMenu = false;
        WS.RemovePallet(TrailerId, Pallet.Index);
    }

    private void MoveTo(string toId)
    {
        _showMenu = false;
        WS.TryMovePallet(TrailerId, Pallet.Index, toId);
    }

    private void SwapType(string typeId)
    {
        _showMenu = false;
        WS.SwapPalletType(TrailerId, Pallet.Index, typeId);
    }
}
```

- [ ] **Step 2: Create TrailerCard.razor**

```razor
@* src/Lodr/Components/Workspace/TrailerCard.razor *@
@inject WorkspaceState WS
@inject CapacityService CapSvc

<div class="relative rounded-lg border-2 bg-gray-900 transition-all cursor-pointer flex-shrink-0 @BorderClass @ShadowClass"
     style="min-width: 280px;"
     @onclick="Select"
     @ondragover:preventDefault
     @ondrop="OnDrop"
     @oncontextmenu="OnContextMenu"
     @oncontextmenu:preventDefault>

    @* Header *@
    <div class="flex items-center justify-between px-3 py-2 border-b border-gray-700">
        <div class="flex items-center gap-2">
            <span class="text-sm font-bold text-white">@Trailer.Id</span>
            @if (Trailer.FriendlyName is not null)
            {
                <span class="text-xs text-gray-400">@Trailer.FriendlyName</span>
            }
        </div>
        <div class="flex items-center gap-2">
            @if (FullState == FullState.NearFull)
            {
                <span class="text-[10px] font-bold text-amber-400 uppercase tracking-wider">Near Full</span>
            }
            else if (FullState == FullState.Full)
            {
                <span class="text-[10px] font-bold text-red-400 uppercase tracking-wider">Full</span>
            }
            <span class="text-xs text-gray-500">@Trailer.PlacedPallets.Count / @MaxCap</span>
        </div>
    </div>

    @* Pallet grid *@
    <div class="p-2 flex flex-wrap gap-1 min-h-[60px]">
        @foreach (var pallet in Trailer.PlacedPallets.OrderBy(p => p.Index))
        {
            var type = GetType(pallet.TypeId);
            if (type is null) continue;
            <PalletCell Pallet="pallet"
                        TrailerId="Trailer.Id"
                        PalletType="type"
                        OtherTrailers="OtherTrailers"
                        OtherTypes="OtherTypes" />
        }

        @if (!Trailer.PlacedPallets.Any())
        {
            <div class="w-full text-center text-xs text-gray-600 py-2">Empty — drag a pallet type here</div>
        }
    </div>

    @* Capacity bar *@
    <div class="h-1 mx-3 mb-2 rounded-full bg-gray-800 overflow-hidden">
        <div class="h-full rounded-full transition-all @BarColor" style="width: @BarWidth"></div>
    </div>
</div>

@if (_showMenu)
{
    <div class="fixed inset-0 z-40" @onclick="() => _showMenu = false"></div>
    <div class="fixed z-50 bg-gray-800 border border-gray-700 rounded shadow-xl py-1 text-xs"
         style="left: @(_menuX)px; top: @(_menuY)px; min-width: 160px;">
        <button class="w-full text-left px-3 py-1.5 hover:bg-gray-700"
                @onclick="OpenInTab">Open in tab</button>
        <button class="w-full text-left px-3 py-1.5 hover:bg-gray-700 text-red-400"
                @onclick="Remove">Remove trailer</button>
    </div>
}

@code {
    [Parameter, EditorRequired] public TrailerInstance Trailer { get; set; } = null!;
    [Parameter] public IReadOnlyList<TrailerInstance> OtherTrailers { get; set; } = [];
    [Parameter] public IReadOnlyList<PalletType> OtherTypes { get; set; } = [];

    private bool _showMenu;
    private double _menuX, _menuY;

    private FullState FullState =>
        WS.Current is null ? FullState.Normal
        : CapSvc.GetFullState(Trailer, WS.Current.PalletTypes);

    private int MaxCap =>
        WS.Current is null || !WS.Current.PalletTypes.Any() ? 0
        : WS.Current.PalletTypes.Max(t => CapSvc.GetCapacity(Trailer.Dimensions, t));

    private float Fraction =>
        WS.Current is null ? 0f
        : CapSvc.GetUsedFraction(Trailer, WS.Current.PalletTypes);

    private string BorderClass => FullState switch
    {
        FullState.Full => "border-red-600",
        FullState.NearFull => "border-amber-500",
        _ => WS.SelectedTrailerId == Trailer.Id ? "border-blue-500" : "border-gray-700"
    };

    private string ShadowClass => WS.SelectedTrailerId == Trailer.Id ? "shadow-lg shadow-blue-900/40" : "";

    private string BarColor => FullState switch
    {
        FullState.Full => "bg-red-500",
        FullState.NearFull => "bg-amber-500",
        _ => "bg-blue-500"
    };

    private string BarWidth => $"{Math.Min(Fraction * 100f, 100f):F0}%";

    private PalletType? GetType(string typeId) =>
        WS.Current?.PalletTypes.FirstOrDefault(t => t.Id == typeId);

    private void Select() => WS.SelectTrailer(Trailer.Id);

    private void OnDrop()
    {
        var typeId = PalettePanel.DraggedTypeId;
        PalettePanel.ClearDrag();
        if (typeId is null) return;
        WS.TryAddPallet(Trailer.Id, typeId);
    }

    private void OnContextMenu(MouseEventArgs e)
    {
        e.StopPropagation();
        _menuX = e.ClientX; _menuY = e.ClientY;
        _showMenu = true;
    }

    private void OpenInTab()
    {
        _showMenu = false;
        WS.OpenTrailerTab(Trailer.Id);
    }

    private void Remove()
    {
        _showMenu = false;
        WS.RemoveTrailer(Trailer.Id);
    }
}
```

- [ ] **Step 3: Create AddTrailerDialog.razor**

```razor
@* src/Lodr/Components/Workspace/AddTrailerDialog.razor *@

<div class="fixed inset-0 z-50 flex items-center justify-center bg-black/60">
    <div class="bg-gray-900 border border-gray-700 rounded-xl shadow-2xl p-6 w-96 flex flex-col gap-4">
        <h2 class="text-sm font-bold uppercase tracking-wider text-gray-300">Add Trailer</h2>

        <div>
            <label class="text-xs text-gray-500 mb-1 block">Type</label>
            <select class="w-full bg-gray-800 border border-gray-700 rounded px-3 py-1.5 text-sm outline-none focus:ring-1 ring-blue-500"
                    @onchange="OnTypeChanged">
                <option value="Standard53">53' Standard (630"×98"×110")</option>
                <option value="Standard48">48' Standard (576"×98"×110")</option>
                <option value="Standard40">40' Standard (480"×98"×110")</option>
                <option value="Standard28">28' Standard (336"×96"×110")</option>
                <option value="Custom">Custom…</option>
            </select>
        </div>

        @if (_isCustom)
        {
            <div class="flex gap-2">
                <div class="flex-1">
                    <label class="text-xs text-gray-500 block mb-1">Length (in)</label>
                    <input class="w-full bg-gray-800 border border-gray-700 rounded px-2 py-1.5 text-sm outline-none focus:ring-1 ring-blue-500"
                           type="number" @bind="_customL" />
                </div>
                <div class="flex-1">
                    <label class="text-xs text-gray-500 block mb-1">Width (in)</label>
                    <input class="w-full bg-gray-800 border border-gray-700 rounded px-2 py-1.5 text-sm outline-none focus:ring-1 ring-blue-500"
                           type="number" @bind="_customW" />
                </div>
                <div class="flex-1">
                    <label class="text-xs text-gray-500 block mb-1">Height (in)</label>
                    <input class="w-full bg-gray-800 border border-gray-700 rounded px-2 py-1.5 text-sm outline-none focus:ring-1 ring-blue-500"
                           type="number" @bind="_customH" />
                </div>
            </div>
        }

        <div>
            <label class="text-xs text-gray-500 mb-1 block">Friendly name (optional)</label>
            <input class="w-full bg-gray-800 border border-gray-700 rounded px-3 py-1.5 text-sm outline-none focus:ring-1 ring-blue-500"
                   placeholder="e.g. Morning Run"
                   @bind="_friendlyName" />
        </div>

        <div class="flex gap-3 mt-2">
            <button class="flex-1 py-2 text-sm bg-blue-600 hover:bg-blue-500 rounded font-medium transition-colors"
                    @onclick="Confirm">
                Add
            </button>
            <button class="flex-1 py-2 text-sm border border-gray-700 hover:border-gray-500 rounded transition-colors"
                    @onclick="() => OnCancel.InvokeAsync()">
                Cancel
            </button>
        </div>
    </div>
</div>

@code {
    [Parameter] public EventCallback<(TrailerDimensions dims, string? name)> OnConfirm { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }

    private bool _isCustom;
    private float _customL = 630f, _customW = 98f, _customH = 110f;
    private string? _friendlyName;
    private TrailerType _selectedType = TrailerType.Standard53;

    private void OnTypeChanged(ChangeEventArgs e)
    {
        if (Enum.TryParse<TrailerType>(e.Value?.ToString(), out var t) && t != TrailerType.Custom)
        {
            _selectedType = t;
            _isCustom = false;
        }
        else
        {
            _isCustom = true;
        }
    }

    private void Confirm()
    {
        TrailerDimensions dims = _isCustom
            ? new(_customL, _customW, _customH)
            : TrailerDimensions.Presets[_selectedType];
        OnConfirm.InvokeAsync((dims, string.IsNullOrWhiteSpace(_friendlyName) ? null : _friendlyName.Trim()));
    }
}
```

- [ ] **Step 4: Build to verify**

Run: `dotnet build src/Lodr/Lodr.csproj 2>&1 | grep " error " | head -20`
Expected: Errors only for not-yet-created TrailerCanvas, TrailerInspector, LogPanel.

- [ ] **Step 5: Commit**

```bash
git add src/Lodr/Components/Workspace/PalletCell.razor src/Lodr/Components/Workspace/TrailerCard.razor src/Lodr/Components/Workspace/AddTrailerDialog.razor
git commit -m "feat: add PalletCell with context menu, TrailerCard with drag/drop and full-state badge, AddTrailerDialog"
```

---

### Task 11: TrailerCanvas + LogPanel

**Files:**
- Create: `src/Lodr/Components/Workspace/TrailerCanvas.razor`
- Create: `src/Lodr/Components/Workspace/LogPanel.razor`

- [ ] **Step 1: Create TrailerCanvas.razor**

```razor
@* src/Lodr/Components/Workspace/TrailerCanvas.razor *@
@inject WorkspaceState WS
@implements IDisposable

<div class="flex-1 flex flex-col overflow-hidden">
    @* Tab strip for pop-out trailers *@
    @if (WS.OpenTabIds.Any())
    {
        <div class="flex-none flex items-center gap-1 px-3 pt-2 border-b border-gray-800 bg-gray-950">
            <button class="px-3 py-1 text-xs rounded-t @(_focusedTabId is null ? "bg-gray-800 text-white" : "text-gray-500 hover:text-gray-300")"
                    @onclick="() => _focusedTabId = null">
                All Trailers
            </button>
            @foreach (var tabId in WS.OpenTabIds)
            {
                var trailer = WS.Current?.Trailers.FirstOrDefault(t => t.Id == tabId);
                if (trailer is null) continue;
                <div class="flex items-center gap-1">
                    <button class="px-3 py-1 text-xs rounded-t @(_focusedTabId == tabId ? "bg-gray-800 text-white" : "text-gray-500 hover:text-gray-300")"
                            @onclick="() => _focusedTabId = tabId">
                        @tabId @(trailer.FriendlyName is not null ? $"· {trailer.FriendlyName}" : "")
                    </button>
                    <button class="text-gray-600 hover:text-gray-300 text-xs pr-1" @onclick="() => WS.CloseTrailerTab(tabId)">×</button>
                </div>
            }
        </div>
    }

    @* Canvas body *@
    <div class="flex-1 overflow-auto p-4">
        @if (WS.Current is null || !WS.Current.Trailers.Any())
        {
            <div class="h-full flex items-center justify-center text-gray-600">
                <div class="text-center">
                    <p class="text-lg font-semibold mb-1">No trailers yet</p>
                    <p class="text-sm">Click "+ Trailer" in the header to add one.</p>
                </div>
            </div>
        }
        else
        {
            var trailers = _focusedTabId is not null
                ? WS.Current.Trailers.Where(t => t.Id == _focusedTabId).ToList()
                : WS.Current.Trailers;

            <div class="flex flex-wrap gap-4 items-start">
                @foreach (var trailer in trailers)
                {
                    <TrailerCard Trailer="trailer"
                                 OtherTrailers="WS.Current.Trailers.Where(t => t.Id != trailer.Id).ToList()"
                                 OtherTypes="WS.Current.PalletTypes" />
                }
            </div>
        }
    </div>
</div>

@code {
    private string? _focusedTabId;

    protected override void OnInitialized() => WS.OnChange += StateHasChanged;
    public void Dispose() => WS.OnChange -= StateHasChanged;
}
```

- [ ] **Step 2: Create LogPanel.razor**

```razor
@* src/Lodr/Components/Workspace/LogPanel.razor *@
@inject AppLogService LogService
@inject IJSRuntime JS

<div class="fixed inset-0 z-50 flex justify-end">
    <div class="w-full max-w-md bg-gray-900 border-l border-gray-700 shadow-2xl flex flex-col">
        <div class="flex items-center justify-between px-4 py-3 border-b border-gray-700">
            <span class="text-sm font-semibold">Application Log</span>
            <div class="flex items-center gap-3">
                <button class="text-xs text-blue-400 hover:text-blue-300 transition-colors" @onclick="Download">
                    Download logs
                </button>
                <button class="text-gray-500 hover:text-white transition-colors text-lg leading-none" @onclick="Close">×</button>
            </div>
        </div>
        <div class="flex-1 overflow-y-auto font-mono text-xs p-3 space-y-1">
            @foreach (var entry in LogService.Entries.OrderByDescending(e => e.Timestamp))
            {
                <div class="flex gap-2 @LevelClass(entry.Level)">
                    <span class="text-gray-600 flex-shrink-0">@entry.Timestamp.ToLocalTime().ToString("HH:mm:ss")</span>
                    <span class="flex-shrink-0 @LevelBadge(entry.Level) uppercase text-[9px] font-bold px-1 rounded self-start mt-0.5">@entry.Level</span>
                    <span class="text-gray-400 flex-shrink-0 truncate max-w-[100px]" title="@entry.Source">@ShortSource(entry.Source)</span>
                    <span class="flex-1 break-words">@entry.Message</span>
                </div>
            }
        </div>
    </div>
    <div class="flex-1" @onclick="Close"></div>
</div>

@code {
    [Parameter] public EventCallback OnClose { get; set; }

    private void Close() => OnClose.InvokeAsync();

    private async Task Download()
    {
        var text = LogService.ExportText();
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var b64 = Convert.ToBase64String(bytes);
        await JS.InvokeVoidAsync("eval",
            $"const a=document.createElement('a');a.href='data:text/plain;base64,{b64}';a.download='lodr-logs.txt';a.click()");
    }

    private static string LevelClass(Microsoft.Extensions.Logging.LogLevel level) => level switch
    {
        Microsoft.Extensions.Logging.LogLevel.Error or
        Microsoft.Extensions.Logging.LogLevel.Critical => "text-red-300",
        Microsoft.Extensions.Logging.LogLevel.Warning => "text-amber-300",
        _ => "text-gray-300"
    };

    private static string LevelBadge(Microsoft.Extensions.Logging.LogLevel level) => level switch
    {
        Microsoft.Extensions.Logging.LogLevel.Error or
        Microsoft.Extensions.Logging.LogLevel.Critical => "bg-red-900 text-red-300",
        Microsoft.Extensions.Logging.LogLevel.Warning => "bg-amber-900 text-amber-300",
        _ => "bg-gray-800 text-gray-400"
    };

    private static string ShortSource(string source)
    {
        var parts = source.Split('.');
        return parts.Length > 1 ? parts.Last() : source;
    }
}
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build src/Lodr/Lodr.csproj 2>&1 | grep " error " | head -20`
Expected: Errors only for not-yet-created TrailerInspector.

- [ ] **Step 4: Commit**

```bash
git add src/Lodr/Components/Workspace/TrailerCanvas.razor src/Lodr/Components/Workspace/LogPanel.razor
git commit -m "feat: add TrailerCanvas with tab strip and LogPanel with export"
```

---

### Task 12: TrailerInspector

**Files:**
- Create: `src/Lodr/Components/Workspace/TrailerInspector.razor`

- [ ] **Step 1: Create TrailerInspector.razor**

```razor
@* src/Lodr/Components/Workspace/TrailerInspector.razor *@
@inject WorkspaceState WS
@inject CapacityService CapSvc
@implements IDisposable

<aside class="w-56 flex-shrink-0 flex flex-col border-l border-gray-800 bg-gray-900 overflow-y-auto">
    @if (_trailer is null)
    {
        <div class="flex-1 flex items-center justify-center text-gray-600 text-xs text-center p-4">
            Select a trailer to inspect
        </div>
    }
    else
    {
        <div class="p-3 border-b border-gray-800">
            <div class="flex items-center justify-between">
                <span class="text-sm font-bold">@_trailer.Id</span>
                <button class="text-xs text-gray-600 hover:text-gray-400 transition-colors"
                        @onclick="() => WS.SelectTrailer(null)">×</button>
            </div>
            @if (_trailer.FriendlyName is not null)
            {
                <p class="text-xs text-gray-400 mt-0.5">@_trailer.FriendlyName</p>
            }
            <p class="text-xs text-gray-600 mt-1">
                @_trailer.Dimensions.Length" × @_trailer.Dimensions.Width" × @_trailer.Dimensions.Height"
            </p>
        </div>

        @* Capacity bar *@
        <div class="p-3 border-b border-gray-800">
            <div class="flex justify-between text-xs mb-1">
                <span class="text-gray-500">Utilization</span>
                <span class="@FractionColor">@((Fraction * 100f).ToString("F0"))%</span>
            </div>
            <div class="h-2 rounded-full bg-gray-800">
                <div class="h-full rounded-full transition-all @BarColor" style="width: @BarWidth"></div>
            </div>
            <p class="text-xs text-gray-500 mt-1">@_trailer.PlacedPallets.Count placed</p>
        </div>

        @* Per-type breakdown *@
        @if (WS.Current?.PalletTypes.Any() == true)
        {
            <div class="p-3">
                <p class="text-xs text-gray-500 font-semibold uppercase tracking-wider mb-2">Capacity by Type</p>
                @foreach (var type in WS.Current.PalletTypes)
                {
                    int cap = CapSvc.GetCapacity(_trailer.Dimensions, type);
                    int placed = _trailer.PlacedPallets.Count(p => p.TypeId == type.Id);
                    <div class="mb-2">
                        <div class="flex justify-between text-xs mb-0.5">
                            <span class="flex items-center gap-1">
                                <span class="inline-block w-2 h-2 rounded-full flex-shrink-0" style="background: @type.Color"></span>
                                @type.Name
                            </span>
                            <span class="text-gray-400">@placed / @cap</span>
                        </div>
                        <div class="h-1 rounded-full bg-gray-800">
                            <div class="h-full rounded-full" style="width: @(cap == 0 ? 0 : Math.Min(placed * 100 / cap, 100))%; background: @type.Color"></div>
                        </div>
                    </div>
                }
            </div>
        }
    }
</aside>

@code {
    private TrailerInstance? _trailer =>
        WS.SelectedTrailerId is null ? null
        : WS.Current?.Trailers.FirstOrDefault(t => t.Id == WS.SelectedTrailerId);

    private FullState FullState =>
        _trailer is null || WS.Current is null ? FullState.Normal
        : CapSvc.GetFullState(_trailer, WS.Current.PalletTypes);

    private float Fraction =>
        _trailer is null || WS.Current is null ? 0f
        : CapSvc.GetUsedFraction(_trailer, WS.Current.PalletTypes);

    private string FractionColor => FullState switch
    {
        FullState.Full => "text-red-400",
        FullState.NearFull => "text-amber-400",
        _ => "text-green-400"
    };

    private string BarColor => FullState switch
    {
        FullState.Full => "bg-red-500",
        FullState.NearFull => "bg-amber-500",
        _ => "bg-green-500"
    };

    private string BarWidth => $"{Math.Min(Fraction * 100f, 100f):F0}%";

    protected override void OnInitialized() => WS.OnChange += StateHasChanged;
    public void Dispose() => WS.OnChange -= StateHasChanged;
}
```

- [ ] **Step 2: Build — expect full success**

Run: `dotnet build src/Lodr/Lodr.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/Lodr/Components/Workspace/TrailerInspector.razor
git commit -m "feat: add TrailerInspector with per-type capacity breakdown"
```

---

### Task 13: Index.razor Rewire + AppShell Fix

**Files:**
- Modify: `src/Lodr/Pages/Index.razor`
- Modify: `src/Lodr/Components/Layout/AppShell.razor` (fix LogPanel wiring + save)

- [ ] **Step 1: Rewrite Index.razor**

Replace full content of `src/Lodr/Pages/Index.razor`:

```razor
@page "/"
@layout AppShell
```

- [ ] **Step 2: Fix AppShell to wire log panel open callback from AppHeader**

In `src/Lodr/Components/Layout/AppShell.razor`, the `AppHeader` component has `OnLogOpen` parameter — update the AppShell's AppHeader usage and add the save method. Replace the AppShell content:

```razor
@inherits LayoutComponentBase
@inject WorkspaceState WS
@inject AppLogService LogService
@inject WorkspacePersistenceInterop Persistence
@implements IDisposable

<div class="h-screen w-screen overflow-hidden flex flex-col bg-gray-950 text-white select-none">
    <AppHeader OnAddTrailer="ShowAddTrailerDialog" OnLogOpen="() => _showLog = true" />

    <div class="flex-1 flex overflow-hidden">
        <Sidebar />

        <main class="flex-1 flex overflow-hidden">
            @if (WS.Current is null)
            {
                <div class="flex-1 flex items-center justify-center text-gray-600">
                    <div class="text-center">
                        <p class="text-2xl font-bold mb-2">No workspace open</p>
                        <p class="text-sm">Open the Workspaces panel to create or load a workspace.</p>
                    </div>
                </div>
            }
            else
            {
                <TrailerCanvas />
                <TrailerInspector />
            }
        </main>
    </div>

    <StatusBar />
</div>

@if (_showAddTrailer)
{
    <AddTrailerDialog OnConfirm="OnAddTrailerConfirmed" OnCancel="() => _showAddTrailer = false" />
}

@if (_showLog)
{
    <LogPanel OnClose="() => _showLog = false" />
}

@code {
    private bool _showAddTrailer;
    private bool _showLog;
    private System.Threading.Timer? _autosaveTimer;

    protected override async Task OnInitializedAsync()
    {
        WS.OnChange += StateHasChanged;
        LogService.OnChange += StateHasChanged;
        _autosaveTimer = new System.Threading.Timer(_ => InvokeAsync(AutoSave), null,
            TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));

        // Restore last workspace
        var lastId = await Persistence.GetLastWorkspaceIdAsync();
        if (lastId is not null)
        {
            var ws = await Persistence.GetWorkspaceAsync(lastId);
            if (ws is not null) WS.LoadWorkspace(ws);
        }
    }

    private void ShowAddTrailerDialog() => _showAddTrailer = true;

    private void OnAddTrailerConfirmed((TrailerDimensions dims, string? name) args)
    {
        _showAddTrailer = false;
        WS.AddTrailer(args.dims, args.name);
    }

    private async Task AutoSave()
    {
        if (WS.Current is not null && WS.IsDirty)
        {
            await Persistence.SaveWorkspaceAsync(WS.Current);
            await Persistence.SaveLastWorkspaceIdAsync(WS.Current.Id);
        }
    }

    public void Dispose()
    {
        WS.OnChange -= StateHasChanged;
        LogService.OnChange -= StateHasChanged;
        _autosaveTimer?.Dispose();
    }
}
```

- [ ] **Step 3: Build — expect clean**

Run: `dotnet build src/Lodr/Lodr.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Lodr/Pages/Index.razor src/Lodr/Components/Layout/AppShell.razor
git commit -m "feat: rewire Index.razor to workspace canvas; add autosave and last-workspace restore"
```

---

### Task 14: Cleanup — Remove Wizard + Old Models

**Files to delete:**
- `src/Lodr/Models/PalletSpec.cs`
- `src/Lodr/Models/PalletPosition.cs`
- `src/Lodr/Models/PlacementResult.cs`
- `src/Lodr/Services/FitCalculator.cs`
- `src/Lodr/Services/PlacementEngine.cs`
- `src/Lodr/State/AppState.cs`
- `src/Lodr/Components/Steps/TrailerStep.razor`
- `src/Lodr/Components/Steps/PalletStep.razor`
- `src/Lodr/Components/Steps/ResultStep.razor`
- `src/Lodr/Components/Steps/StepContent.razor`
- `src/Lodr/Components/Layout/StepNav.razor`
- `src/Lodr/Components/Shared/SharedComponent.razor`
- `src/Lodr/Components/Shared/NavMenu.razor`
- `src/Lodr/Components/Shared/PresetSelector.razor`
- `tests/Lodr.Tests/Services/FitCalculatorTests.cs`
- `tests/Lodr.Tests/Services/PlacementEngineTests.cs`
- `tests/Lodr.Tests/Components/TrailerStepTests.cs`
- `tests/Lodr.Tests/Components/PalletStepTests.cs`
- `tests/Lodr.Tests/Components/ResultStepTests.cs`
- `tests/Lodr.Tests/CounterCSharpTest.cs`

**Modify:**
- `src/Lodr/Services/OrientationService.cs` — remove `PalletSpec` overload (only keep `PalletType`)

- [ ] **Step 1: Delete old files**

```bash
git rm src/Lodr/Models/PalletSpec.cs src/Lodr/Models/PalletPosition.cs src/Lodr/Models/PlacementResult.cs
git rm src/Lodr/Services/FitCalculator.cs src/Lodr/Services/PlacementEngine.cs
git rm src/Lodr/State/AppState.cs
git rm src/Lodr/Components/Steps/TrailerStep.razor src/Lodr/Components/Steps/PalletStep.razor src/Lodr/Components/Steps/ResultStep.razor src/Lodr/Components/Steps/StepContent.razor
git rm src/Lodr/Components/Layout/StepNav.razor
git rm src/Lodr/Components/Shared/SharedComponent.razor src/Lodr/Components/Shared/NavMenu.razor src/Lodr/Components/Shared/PresetSelector.razor
git rm tests/Lodr.Tests/Services/FitCalculatorTests.cs tests/Lodr.Tests/Services/PlacementEngineTests.cs
git rm tests/Lodr.Tests/Components/TrailerStepTests.cs tests/Lodr.Tests/Components/PalletStepTests.cs tests/Lodr.Tests/Components/ResultStepTests.cs
git rm tests/Lodr.Tests/CounterCSharpTest.cs
```

- [ ] **Step 2: Strip PalletSpec overload from OrientationService**

Replace full content of `src/Lodr/Services/OrientationService.cs`:

```csharp
using Lodr.Models;

namespace Lodr.Services;

public class OrientationService
{
    public IReadOnlyList<(float Length, float Width)> GetOrientations(PalletType type)
        => GetOrientationsCore(type.Length, type.Width, type.CanRotate);

    private static IReadOnlyList<(float Length, float Width)> GetOrientationsCore(
        float length, float width, bool canRotate)
    {
        var original = (length, width);
        if (!canRotate || length == width) return [original];
        return [original, (width, length)];
    }
}
```

- [ ] **Step 3: Build — expect clean**

Run: `dotnet build src/Lodr/Lodr.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Run all tests — expect pass**

Run: `dotnet test tests/Lodr.Tests/Lodr.Tests.csproj -v minimal`
Expected: All tests pass (CapacityServiceTests, SlotServiceTests, WorkspaceStateTests, OrientationServiceTests).

- [ ] **Step 5: Rebuild CSS**

Run: `cd src/Lodr && npm run build:css`
Expected: CSS rebuilt successfully.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: remove step wizard, old models, and old tests; finalize workspace redesign"
```

---

## Self-Review Checklist

### Spec Coverage

| Spec Requirement | Task |
|---|---|
| 5-panel layout (Header, Sidebar, Canvas, Inspector, StatusBar) | Task 8 |
| Sidebar tabs: Palette / Workspaces | Task 9 |
| Trailer pop-out tab strip | Task 11 |
| No step wizard | Task 14 |
| New domain models (Workspace, TrailerInstance, PalletType, etc.) | Task 1 |
| CapacityService with FullState (≥80% NearFull, 100% Full) | Task 2 |
| SlotService next-slot with overlap | Task 3 |
| WorkspaceState (all CRUD methods) | Task 4 |
| AppLogService ILoggerProvider + terminal + export | Task 5 |
| IndexedDB v2 with workspace stores | Task 6 |
| WorkspacePersistenceInterop | Task 6 |
| DI wiring + _Imports update | Task 7 |
| Pallet type cards + click-to-add + drag source | Task 9 |
| PalletCell with context menu (remove/move/swap) | Task 10 |
| TrailerCard with full-state border + capacity bar | Task 10 |
| AddTrailerDialog with presets + custom | Task 10 |
| TrailerInspector with per-type breakdown | Task 12 |
| Autosave every 60s | Task 13 |
| Last-workspace restore on startup | Task 13 |
| CSS rebuild | Task 14 |

### Type Consistency

- `PalletType` used as `record` throughout ✓
- `PlacedPallet` used as `record` throughout ✓
- `FullState` enum defined in `CapacityService.cs`, referenced in TrailerCard + Inspector ✓
- `PalettePanel.DraggedTypeId` static field used in TrailerCard.OnDrop ✓
- `WorkspaceState.IsDirty` used in StatusBar ✓

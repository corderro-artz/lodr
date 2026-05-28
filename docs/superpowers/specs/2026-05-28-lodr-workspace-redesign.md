# LODR Workspace Redesign — Design Spec

## Goal

Replace the step-wizard prototype with a full workspace-based pallet management application. Users manage multiple trailers across multiple named workspaces, manually placing mixed pallet types via click or drag, with local save/load and preset templates — all offline, no backend.

## Architecture

Five layout regions, always visible:

```
┌─────────────────────────────────────────────────────┐
│  Header: brand · workspace name · save · actions    │
├──────────────┬──────────────────────────┬───────────┤
│ Sidebar      │                          │           │
│ ┌──┬──────┐  │   Canvas                 │ Inspector │
│ │Pl│ WS   │  │   (multi-trailer grid,   │ (selected │
│ │et│ tabs │  │    scrollable)           │  trailer) │
│ └──┴──────┘  │                          │           │
│  panel body  │                          │           │
├──────────────┴──────────────────────────┴───────────┤
│  Status bar: trailer count · pallet count · autosave│
└─────────────────────────────────────────────────────┘
```

**Sidebar tabs:** "Palette" (pallet type cards) and "Workspaces" (save/load/presets). Switching tabs replaces the panel body — the tab strip always stays visible.

**Trailer pop-out:** Right-click a trailer on canvas → "Open in tab" adds a browser-style tab beneath the header. That tab shows a single focused trailer view filling the canvas. Multiple trailers can be open in tabs simultaneously. Closing the tab returns to the multi-trailer canvas.

**No 3D renderer:** PixiJS/VisualizationInterop files are preserved but the canvas uses HTML/CSS rendering only. 3D view is a future feature.

**No step wizard:** TrailerStep, PalletStep, ResultStep, and StepNav are removed. Index.razor routes directly to the workspace canvas.

---

## Data Model

### New models (replace current placement models)

```
Workspace
  Id: string (uuid)
  Name: string
  CreatedAt: DateTime
  UpdatedAt: DateTime
  Trailers: List<TrailerInstance>
  PalletTypes: List<PalletType>

TrailerInstance
  Id: string            // display ID: "T-01", "T-02", ...
  FriendlyName: string? // user-entered, e.g. "Morning Run"
  Dimensions: TrailerDimensions
  PlacedPallets: List<PlacedPallet>

PalletType
  Id: string
  Name: string          // e.g. "GMA", "EUR", "Custom"
  Length: float
  Width: float
  Height: float
  CanRotate: bool
  Color: string         // hex, assigned from fixed pool on creation: ["#22c55e","#3b82f6","#f97316","#a855f7","#ec4899","#14b8a6"]

PlacedPallet
  Index: int            // 1-based display number
  TypeId: string        // references PalletType.Id
  X: float              // inches from trailer front
  Y: float              // inches from trailer left wall
  Rotated: bool
```

### Preserved models (keep, possibly extend)

- `TrailerDimensions` — unchanged
- `TrailerType` enum — unchanged  
- `OrientationService` — unchanged, used to compute orientations per type
- `FitCalculator` — unchanged, used to compute per-type capacity
- `PlacementEngine` — keep but repurpose: generates the next open slot position for click-to-add

### Removed models

- `PalletSpec` — replaced by `PalletType` (workspace-scoped, multiple per workspace)
- `PalletPosition` — replaced by `PlacedPallet`
- `PlacementResult` — replaced by `TrailerInstance` state

### Preset model

```
WorkspacePreset
  Id: string
  Name: string
  DefaultTrailers: List<PresetTrailer>   // trailer configs, no placed pallets
  PalletTypes: List<PalletType>

PresetTrailer
  Label: string          // e.g. "Trailer 1"
  Dimensions: TrailerDimensions
```

---

## Capacity Calculation

For a given `TrailerInstance`, capacity is computed per pallet type independently (not mixed bin-packing):

```
CapacityForType(trailer, palletType):
  orientations = OrientationService.GetOrientations(palletType)
  bestFit = max over orientations of:
    floor(trailer.Length / pallet.Length) * floor(trailer.Width / pallet.Width)
  return bestFit

TotalPlaced(trailer) = trailer.PlacedPallets.Count
UsedFraction(trailer) = TotalPlaced / max(CapacityForType for all types)
```

Full indicator thresholds (applied to `UsedFraction`):
- `< 0.80` → normal (green accent)
- `>= 0.80` → amber border + "NEAR FULL" badge
- `>= 1.00` → red border + "FULL" badge; adding pallets blocked

---

## State Management

`AppState` is replaced by `WorkspaceState` (scoped DI):

```csharp
public class WorkspaceState
{
    public event Action? OnChange;

    public Workspace? Current { get; }          // active workspace
    public string? SelectedTrailerId { get; }   // which trailer inspector shows
    public List<string> OpenTabIds { get; }     // trailers open in pop-out tabs

    // Workspace lifecycle
    void LoadWorkspace(Workspace ws);
    void NewBlankWorkspace();
    Task SaveCurrentAsync();
    Task SaveAsPresetAsync(string name);

    // Trailer management
    void AddTrailer(TrailerDimensions dims, string? friendlyName = null);
    void RemoveTrailer(string trailerId);
    void RenameTrailer(string trailerId, string friendlyName);
    void SelectTrailer(string trailerId);
    void OpenTrailerTab(string trailerId);
    void CloseTrailerTab(string trailerId);

    // Pallet type management
    void AddPalletType(PalletType type);
    void RemovePalletType(string typeId);

    // Pallet placement
    bool TryAddPallet(string trailerId, string typeId);  // click-to-add: next slot
    void RemovePallet(string trailerId, int palletIndex);
    bool TryMovePallet(string fromTrailerId, int palletIndex, string toTrailerId);
    void SwapPalletType(string trailerId, int palletIndex, string newTypeId);
}
```

---

## Component Map

### Layout

```
AppShell.razor              — header + sidebar + canvas + inspector + status bar
  Header.razor              — brand, workspace name (editable inline), save btn
  Sidebar.razor             — tab strip (Palette | Workspaces) + panel body
    PalettePanel.razor      — pallet type cards with click+ and drag handle
    WorkspacePanel.razor    — workspace list, presets, save/close/new actions
  TrailerCanvas.razor       — multi-trailer layout; tab strip when pop-outs active
    TrailerCard.razor       — single trailer on canvas (HTML grid, no PixiJS)
      PalletCell.razor      — individual pallet: number + dims badge + state color
  TrailerInspector.razor    — right panel: stats, pallet type breakdown, cap bar
  StatusBar.razor           — global counts + autosave indicator
```

### Pages

```
Index.razor                 — renders AppShell directly (no step routing)
NotFound.razor              — unchanged
```

### Removed components

- `Steps/TrailerStep.razor`
- `Steps/PalletStep.razor`
- `Steps/ResultStep.razor`
- `Layout/StepNav.razor`
- `Shared/DimensionInput.razor` — may keep if reused in pallet type editor
- `Shared/PresetSelector.razor` — replaced by WorkspacePanel

---

## Interaction Flows

### Startup

1. App loads → `WorkspaceState` checks IndexedDB for last saved workspace
2. If found → auto-restore, show toast "Restored: [name] · saved [time]"
3. If none → sidebar opens on Workspaces tab, canvas shows empty state with hint
4. User can switch to Workspaces tab at any time to save/close/open

### Adding a trailer

1. Header or canvas has "+ Add Trailer" button
2. Dialog: choose standard type (53', 48', 40', 28') or custom dims + optional friendly name
3. Trailer added as "T-N" where N = next available number; displayed on canvas immediately

### Adding a pallet (click)

1. Pallet type card in Palette panel → click "+" icon
2. If a trailer is selected: pallet of that type added to its next open slot
3. If no trailer selected: prompt user to select one (highlight selectable trailers)
4. If trailer is full: button is disabled, tooltip "T-01 is full"

### Adding a pallet (drag)

1. Drag pallet type card from sidebar onto a trailer on canvas
2. Trailer highlights as drop zone while dragging
3. On drop: pallet added to next open slot (position auto-calculated)
4. If trailer is full: drop rejected, trailer shows red pulse animation

### Right-click pallet context menu

- Remove pallet
- Move to [other trailer name] (submenu if >1 trailer)
- Swap type → [pallet type name] (submenu)

### Saving a workspace

- Header "Save" button → saves to IndexedDB under current name
- Workspaces panel → "Save As…" → prompts for name → saves new copy
- Auto-save: every 60 seconds if workspace is dirty (any change since last save)

### Saving a preset

- Workspaces panel → "Save as Preset…" → saves trailer configs + pallet types, no placed pallets

### Closing a workspace

- Workspaces panel → "Close" on current → if unsaved changes, prompt "Save before closing?"
- Returns to empty state / Workspaces panel open

---

## Error Logging

Blazor WASM cannot write to the filesystem directly. Logging strategy:

1. **`ILogger` throughout** — all services and state classes use `ILogger<T>` injection
2. **`AppLogService`** — scoped service that buffers the last 500 log entries in memory (level, message, timestamp, source)
3. **Terminal visibility** — errors/warnings call `Console.Error.WriteLine(...)` which surfaces in `dotnet run` terminal output during development
4. **Log panel** — header has a "⚠ N" badge when errors/warnings exist; clicking opens a slide-over log panel showing recent entries with level, source, and timestamp
5. **Export** — log panel has "Download logs" button that triggers browser file download of a `.txt` log dump

`AppLogService` implements a custom `ILoggerProvider` so all `ILogger<T>` calls are automatically captured without manual plumbing in every class.

---

## Persistence

Extends existing `PersistenceInterop` (IndexedDB) with new stores:

| Store | Key | Value |
|---|---|---|
| `workspaces` | workspace id | full `Workspace` JSON |
| `workspace-presets` | preset id | `WorkspacePreset` JSON |
| `last-workspace-id` | `"last"` | id of last opened workspace |
| `trailer-presets` | existing | kept for compatibility |
| `pallet-presets` | existing | kept for compatibility |

---

## PWA / Offline

No changes to service worker, manifest, or offline strategy. All workspace data stays in IndexedDB.

---

## Removed Features

| Feature | Status |
|---|---|
| Step wizard (TrailerStep / PalletStep / ResultStep) | Removed |
| StepNav (Back/Next/dots) | Removed |
| Auto-calculate max fit button | Removed (capacity shown passively in inspector) |
| PixiJS 3D/canvas renderer | Files kept, no UI entry point — future feature |
| Single-pallet-type constraint | Removed — workspaces support multiple types |

---

## Out of Scope (future)

- 3D trailer view (PixiJS re-enable)
- Pallet weight tracking
- Print / export layout to PDF
- Multi-user sync

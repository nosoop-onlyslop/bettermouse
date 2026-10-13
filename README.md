# BetterMouse

A minimal Windows tray application that remaps the mouse **position** as it crosses between
displays with different DPI, plus the corner-unsticking and edge-jumping behaviours from
MouseUnSnag.

It is deliberately small: a global low-level mouse hook, a pure mapping engine, and a
right-click tray menu. Configuration is a single JSON file, editable through a direct-
manipulation layout editor (or by hand).

## Why

Windows arranges monitors in a pixel-continuous virtual desktop: each monitor is placed so
that its native pixel edges touch. When two adjacent monitors have different scaling
factors, the pixel-continuous mapping is not physically continuous, so the cursor lands at
the wrong height (and jumps in speed) when it crosses. BetterMouse keeps the cursor at the
same *physical* position by mapping crossings through a DPI-corrected correspondence.

This mirrors the coordinates model used by LittleBigMouse (a display has both a pixel
rectangle and a physical position), but replaces "drag whole monitor rectangles around"
with "drag handles along a single shared edge".

## The model

- **Pixels** — the Windows virtual desktop. `GetCursorPos` / `SetCursorPos`, the low-level
  hook's `MSLLHOOKSTRUCT.pt`, and monitor rectangles all live here. The process is
  **PerMonitorV2** aware, so these are true physical pixels (see `app.manifest`).
- **Physical** — approximated from the Windows scaling factor: `dip = pixel / scale`. This is
  used to derive a sensible default correspondence but is never trusted as ground truth.
- A **border** between two displays is a piecewise-linear map between the two facing edges,
  authored as normalized control points `(A, B)`. This is exactly what the handles edit.
- Crossing: resolve the border for the exited edge, map the crossing coordinate, land just
  inside the target display. If the border has no authored range there, it is a wall and the
  MouseUnSnag fallbacks apply.

Defaults are generated per border from resolution + scale, so most mixed-DPI setups need no
editing at all. Where two monitors do not physically overlap, the default leaves a wall.

The engine (`BetterMouse.Core/RemapEngine.cs`) is pure and unit-tested against a fake cursor.
All OS access is behind `ICursorEnv`.

## Layout editor

Double-click the tray icon (or *Edit layout...*):

- Displays are drawn at their true relative pixel positions, labelled with resolution and
  scale.
- Each control point appears as a paired handle on the two facing edges, joined by a dashed
  connector. **Drag** a handle along its edge.
- **Double-click** near an edge to add a control point there (the counterpart is placed using
  the current mapping).
- **Right-click** a handle to delete it (a border keeps at least two).

Remapping is suspended while the editor is open so the cursor never fights a drag.

## Tray menu

- **Enabled** — master switch.
- **Unstick from corners** — if the intended position lands over another display, move there.
- **Jump between displays** — jump to the nearest display in the direction of travel.
- **Wrap around desktop** — wrap off the far edge to the opposite side.
- **Edit layout...**, **Reset layout to defaults**, **Reload displays**, **Open config file**, **Exit**.

## Configuration

Stored at `%APPDATA%\BetterMouse\config.json`. Example border:

```json
{
  "Enabled": true,
  "Unstick": true,
  "Jump": true,
  "Wrap": false,
  "Borders": [
    {
      "AId": "\\\\?\\DISPLAY#GSM5B08#5&...",
      "BId": "\\\\?\\DISPLAY#SAM0F35#4&...",
      "ASide": "Right",
      "Points": [
        { "A": 0.0, "B": 0.0 },
        { "A": 1.0, "B": 1.0 }
      ]
    }
  ]
}
```

- `AId` / `BId` are stable monitor device interface paths, so settings survive reboots and
  reconnects.
- `ASide` is the edge of A that faces B (canonical `Right` or `Bottom`; `Left`/`Top` are
  never stored).
- `Points` are normalized `[0,1]` along the two facing edges and are sorted by `A`. Points
  outside the authored range act as a wall.

## Build and run

Requires the .NET 8 SDK (or newer):

```
dotnet build BetterMouse.sln -c Release
dotnet run --project src/BetterMouse
```

Tests:

```
dotnet test BetterMouse.sln
```

## Known limitations

- **Position only.** DPI changes the cursor's on-screen speed as well as its position;
  velocity scaling is intentionally out of scope for now.
- **No elevation / secure desktop.** Cursor control over elevated windows or the UAC secure
  desktop requires a signed `uiAccess` application. The app runs `asInvoker`.
- **Games using raw input** that hide or clip the cursor may fight the hook. LittleBigMouse
  has an elaborate "freelook" bypass; not implemented here.
- **Config is not reconciled across orientation changes.** If displays are rearranged so that
  a pair swaps left/right, that border is regenerated from defaults (handles are lost).
- All monitors at the same DPI still install the hook (for unstick/jump); the DPI mapping is
  then the identity.

## Validation notes

- `Screen.AllScreens` returns physical pixel rectangles and `GetDpiForMonitor` the effective
  DPI under a PerMonitorV2 manifest (verified on Windows 10 19045).
- A `WH_MOUSE_LL` hook reports the *intended* position, which can lie well outside the
  clamped cursor/desktop (verified: `hook=(-4280,590)` while `cursor=(-1280,590)`). This is
  what makes the unstick/jump fallbacks possible without `ClipCursor`.

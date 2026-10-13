> [!NOTE]
>
> Human-written foreword:
>
> This was designed to scratch the itch where [MouseUnSnag] had the UX I wanted (and is what
> I use day-to-day), but [LittleBigMouse] implemented the DPI-aware border handling.
> 
> Additionally, I wanted an intuitive way to adjust the coordinate mapping at display edges.
> Here's how that looks (from my actual setup, scaled down with proportions intact):
> 
> <img width="1120" height="270" alt="image" src="https://github.com/user-attachments/assets/0eba96ca-036c-479a-881c-0d01df6c87da" />
>
> These are drawn as fullscreen overlays.  Basically, you drag the handles across the edge so
> they are visually lined up on the actual displays; the application then maps them so the mouse
> ends up where you expect it to be.  While the above is a capture of the desktop, here's an
> approximation of what I actually see &mdash; notice how the displays are rescaled so the
> handles are lined up:
>
> <img width="1088" height="270" alt="image" src="https://github.com/user-attachments/assets/1231f918-fcae-404f-9190-1bc60e8d55fd" />
> 
> My initial impression is that this hits all of my targets.  All for ~$0.56 USD in token spend.
>
> Has no relation to other applications named BetterMouse.  Surely not a name that can be
> registered, right?

# BetterMouse

A minimal Windows tray application that remaps the mouse **position** as it crosses between
displays with different DPI, plus the corner-unsticking and edge-jumping behaviours from
[MouseUnSnag].

It is deliberately small: a global low-level mouse hook, a pure mapping engine, and a
right-click tray menu. Configuration is a single JSON file, editable through a
physical on-display overlay (or by hand).

Built by studying two prior projects:

- [MouseUnSnag] — the minimal tray UX and the corner-unstick / edge-jump / wrap behaviours.
- [LittleBigMouse] — the DPI-corrected coordinates model (a display has both a pixel rectangle
  and a physical position) and the zone/link mapping this project's border profiles mirror.

[MouseUnSnag]: https://github.com/MouseUnSnag/MouseUnSnag
[LittleBigMouse]: https://github.com/mgth/LittleBigMouse

## Why

Windows arranges monitors in a pixel-continuous virtual desktop: each monitor is placed so
that its native pixel edges touch. When two adjacent monitors have different scaling
factors, the pixel-continuous mapping is not physically continuous, so the cursor lands at
the wrong height (and jumps in speed) when it crosses. BetterMouse keeps the cursor at the
same *physical* position by mapping crossings through a DPI-corrected correspondence.

This mirrors the coordinates model used by [LittleBigMouse] (a display has both a pixel
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
  inside the target display. If the border has no authored range there, it is a wall: the
  cursor is held on the source display, and the MouseUnSnag fallbacks (unstick/jump/wrap)
  apply only when they are enabled. In particular, the event is swallowed rather than handed
  back to Windows, whose own pixel-continuous cross-display positioning would otherwise
  ignore the authored correspondence.
- A wall crossing with **unstick** enabled lands at the nearest authored point on that border
  (the clamp of the crossing coordinate), so the cursor keeps the physically-corresponding
  height rather than the raw pixel height. Where no border exists, unstick keeps MouseUnSnag's
  raw-position behaviour.
- **Corners** are left to Windows' own sticky-corner behaviour: a move diagonally off a display
  corner is not remapped, so the cursor sticks there. With **unstick** enabled the fallback
  crosses instead, as above.

Defaults are generated per border from resolution + scale, so most mixed-DPI setups need no
editing at all. Where two monitors do not physically overlap, the default leaves a wall.

The engine (`BetterMouse.Core/RemapEngine.cs`) is pure and unit-tested against a fake cursor.
All OS access is behind `ICursorEnv`.

## Physical overlay (validate on the real displays)

This is the intended way to configure and *physically* validate the mapping. Toggled from
the tray:

- **Show overlay** — one transparent, topmost, click-through window per display, drawing each
  border's control points on the actual monitor edges. Each handle is a perpendicular tick
  plus a guide line spanning the whole display, so a point on one monitor can be compared
  directly with its counterpart across the bezel. The desktop stays usable.
- **Edit overlay** — the same overlay but interactive: **drag** a handle along its edge,
  **double-click** an edge to add one, **right-click** a handle to delete it (a border keeps
  at least two). A *Done editing* button appears on every display. Remapping is suspended
  while editing.

Because the handles live on the physical panels, aligning them is exactly the act of
calibrating the physical correspondence. Changes apply live and are saved immediately.

Start-up flags for scripting / quick checks: `--overlay` and `--edit-overlay`.

## Editor window (overview)

The **Editor window...** tray item opens a single-window overview: the same handles drawn in
one canvas at their true relative pixel positions, useful when you want to see the whole
arrangement at once rather than reach across monitors.

## Tray menu

- **Enabled** — master switch.
- **Unstick from corners** — if the intended position lands over another display, move there.
- **Jump between displays** — jump to the nearest display in the direction of travel.
- **Wrap around desktop** — wrap off the far edge to the opposite side.
- **Show overlay** / **Edit overlay** — the physical overlay described above.
- **Editor window...**, **Reset layout to defaults**, **Reload displays**, **Open config file**, **Exit**.

## Configuration

Stored at `%APPDATA%\BetterMouse\config.json`. Example border:

```json
{
  "Enabled": true,
  "Unstick": true,
  "Jump": true,
  "Wrap": false,
  "ShowOverlay": false,
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
  reconnects. Profiles for a display that is currently disconnected are retained, so an
  occasionally-connected TV or drawing tablet keeps its calibration (and a transient
  topology collapse, e.g. during a GPU driver update, cannot wipe it).
- `ASide` is the edge of A that faces B (canonical `Right` or `Bottom`; `Left`/`Top` are
  never stored).
- `Points` are normalized `[0,1]` along the two facing edges and are sorted by `A`. Points
  outside the authored range act as a wall.
- `ShowOverlay` remembers whether the overlay was left visible at exit.

## Build and run

### Running a release

Release builds are **framework-dependent**: the downloadable `BetterMouse.exe` is small, but
it needs the **.NET 8 Desktop Runtime** installed separately. Download the *Windows Desktop*
Runtime (x64) from <https://dotnet.microsoft.com/download/dotnet/8.0> if it is not already
present, then run `BetterMouse.exe`.

### From source

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

# AsteroidsC_

A small **Asteroids-style** game used as a **runnable C_ sample**: fixed-step simulation, pre-allocated entity slots, and frame drawing that avoids per-frame heap traffic wherever the **`C_.Analyzer`** rules apply. Graphics are **[Raylib-cs](https://www.nuget.org/packages/Raylib-cs)**; logic and layout are ordinary C# shaped for **predictable realtime behavior**.

For the **C_ dialect** (hot path vs exempt, rules, and exemptions), see **[docs/lang.md](../../docs/lang.md)** and **[docs/analyzer.md](../../docs/analyzer.md)**. Repo layout and building **`C_.Analyzer`** for the local feed are in the root **[README.md](../../README.md)**.

---

## Run

From the repository root (after a successful build of **`C_.Analyzer`** into **`feed/analyzers`**, same as other examples):

```bash
dotnet run --project examples/AsteroidsC_/AsteroidsC_.csproj -c Release
```

**Controls:** arrows turn and thrust, **Space** fires, **Enter** restarts after game over, **F3** toggles the debug panel, **Esc** quits (Raylib default close).

---

## Files (what lives where)

| File | Role |
|------|------|
| **`Application.cs`** | **`Main`**: window lifetime, **`PresentFrame`** (restart handoff → sim → draw). |
| **`Game.Const.cs`** | Tuning **`const`**s, HUD constants, **`BackgroundColor`**, and **`SpriteSheet`** atlas rects for **`Assets/sprites.png`**. |
| **`Game.State.cs`** | Slot structs (**ship, asteroid, bullet**), texture handle, scores, RNG, F3 snapshot fields—**no** tuning constants. |
| **`Game.Bootstrap.cs`** | One-time / cold **`InitializeAfterWindow`**, texture load/unload, RNG seed, **`StartNewGame`** / waves / spawns. |
| **`Game.Loop.cs`** | **`RunSimulation`**: input, **`dt`** clamp, physics **substeps**, integration, collisions, waves; **F3** metric sampling (**`RefreshDebugHudStats`**). |
| **`Game.Render.cs`** | **World draw** (`DrawTexturePro`, wraps **`DrawScene`**) and **HUD** (stackalloc UTF-32 buffer + **`DrawTextCodepoints`**, no `string` in the hot draw path for stats text). |

The game is a **`partial`** **`Game`** type split across those files so readers can jump to **constants vs state vs sim vs draw** without one huge type.

---

## `[C_.Exempt]` (what and why)

The repo’s **default** **`c_.default_scope`** is **hot** (strict): unmarked methods are on the **hot path** and the analyzer enforces allocations, I/O, and other jitter-prone patterns there.

Only a **narrow cold layer** is exempt so startup and teardown stay practical:

| Location | Reason (summary) |
|----------|------------------|
| **`Application.Main`** | Raylib **`InitWindow`** / **`CloseWindow`**, the host loop, **`GCSettings`** tweak—platform and policy, not per-frame gameplay. |
| **`Game.InitializeAfterWindow`** | Allocates fixed **asteroid/bullet** arrays (once), seed path, chains into load and first wave. |
| **`Game.LoadSprites`** | **`Path.Combine`**, **`LoadTexture`**—strings and native I/O. |
| **`Game.UnloadSprites`** | **`UnloadTexture`** at shutdown. |

**Not exempt:** everything else. **`PresentFrame`**, **`RunSimulation`**, **`SimStep`**, collision helpers, **`DrawScene`**, **`DrawHud`** - all written so the steady-state loop stays compatible with **hot-path** expectations (e.g. **`stackalloc`** for HUD scratch, no **`new`** in the per-frame draw for those code paths). 

---

## Design choices

- **Fixed slots** instead of lists: **`MaxAsteroids`** / **`MaxBullets`** arrays, **active** flags—no collection growth in the frame loop.
- **Substepping** (**`MaxPhysicsStepSec`**) so large **`dt`** does not tunnel bullets through small rocks.
- **World wrap** in **`Wrap`**: positions stay inside **`ScreenW`** × **`ScreenH`**; same space as the window (no separate virtual-resolution camera).
- **F3 panel:** **GC / FPS** figures are **read in the loop** (**`RefreshDebugHudStats`**). **Alloc/sec (thread)** uses a **~1 second** rolling sum (**`DebugAllocRateWindowSec`** in **`Game.Const.cs`**). Uses `Raylib.DrawTextCodepoints` to render text from a `stackalloc`'ed buffer.
- **`DrawTexturePro`:** **`dest.x` / `dest.y`** are the **pivot** on screen; **origin** is half size so logical **(X, Y)** is the **center** of the quad (raylib subtracts **origin** to get the top-left). See comments near **`DrawShip`**.

---

## Assets

We use free (creative commons) assets from [Arcade Island](https://arcadeisland.itch.io/space-shooter-wang-tiles) and [Daniel Kole](https://dkproductions.itch.io/pixel-art-package-asteroids). Thank you so much guys!

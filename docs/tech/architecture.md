# Architecture

## Projects

```text
LudumDare60.Game.Browser ──► LudumDare60.Game ──► LudumDare60.Engine ──► LudumDare60.Engine.Platform
          │                                                                        ▲
          └──────────────► LudumDare60.Engine.Platform.Browser ────────────────────┘
```

| Project | SDK | Role |
| --- | --- | --- |
| `src/engine/LudumDare60.Engine.Platform` | `Microsoft.NET.Sdk` | Platform contracts: `IPlatform`, `IFrameSink`. No dependencies. |
| `src/engine/LudumDare60.Engine` | `Microsoft.NET.Sdk` | `GameEngine`, `IGame`, `FrameContext`. Depends only on the platform contracts. |
| `src/engine/platforms/LudumDare60.Engine.Platform.Browser` | `Microsoft.NET.Sdk.StaticWebAssets` | `BrowserPlatform : IPlatform`. Its `wwwroot/` (when added) ships with the app under `_content/LudumDare60.Engine.Platform.Browser/`. |
| `src/game/LudumDare60.Game` | `Microsoft.NET.Sdk` | `LudumDare60Game : IGame`. No platform dependency. |
| `src/game/LudumDare60.Game.Browser` | `Microsoft.NET.Sdk.WebAssembly` | Entry point and host page. Only wires the game and the browser platform together. |

The engine and the game never reference a concrete platform. Only the entry-point project chooses one:

```csharp
await new GameEngine().RunAsync(new LudumDare60Game(), new BrowserPlatform());
```

## Frame loop

`GameEngine.RunAsync(game, platform)` stores the game and awaits `platform.RunAsync(engine)`. The engine is the
platform's `IFrameSink`. The platform owns timing (in the browser: `requestAnimationFrame`) and calls, once per
frame:

1. `IFrameSink.Update(dt)`, with `dt` = seconds since the previous frame.
2. `IFrameSink.Render()`.

Inside `GameEngine.Update(dt)`:

1. `dt` is clamped to `[0, GameEngine.MaxDeltaTime]` (0.25 s), and NaN counts as 0. This prevents a catch-up
   spiral after a hidden tab or a debugger pause.
2. `dt` is added to an accumulator. While the accumulator holds at least `GameEngine.FixedDeltaTime` (1/60 s),
   the engine calls `IGame.FixedUpdate` once and subtracts one step. A frame can run zero or more fixed steps.
3. `IGame.Update` runs once, with the frame's clamped `dt`.
4. `IGame.PrepareRender(alpha)` runs last, with `alpha = accumulator / FixedDeltaTime` in `[0, 1)`. It tells how
   far the current time is between the last fixed step and the next one. Use it to interpolate render state.

`GameEngine.Render()` forwards to `IGame.Render()`.

### FrameContext

`FrameContext(DeltaTime, TotalTime, FrameIndex)` is passed by `in` to `FixedUpdate` and `Update`. All times are
in seconds.

| Field | `FixedUpdate` | `Update` |
| --- | --- | --- |
| `DeltaTime` | Always `FixedDeltaTime`. | The frame's clamped `dt`. |
| `TotalTime` | `(FrameIndex + 1) * FixedDeltaTime`: simulated time after this step. Computed from the step count, so it does not drift. | Sum of all clamped `dt`, including this frame. |
| `FrameIndex` | Zero-based fixed step counter. | Zero-based frame counter. |

All counters reset at the start of each `RunAsync`. Starting a second `RunAsync` on an engine that is already
running throws `InvalidOperationException`. So does calling `Update` or `Render` when no game is running.

### Rules

- Gameplay that must be deterministic (physics, collisions, timers that affect rules) goes into `FixedUpdate`.
- `Update` is for per-frame work that may depend on the actual frame rate: input sampling, camera smoothing, UI.
- `PrepareRender` reads simulation state and builds render data. It must not change simulation state.
- `Render` submits the prepared data. It has no access to timing on purpose.

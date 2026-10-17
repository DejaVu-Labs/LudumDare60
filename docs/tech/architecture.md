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
| `src/engine/platforms/LudumDare60.Engine.Platform.Browser` | `Microsoft.NET.Sdk.StaticWebAssets` | `BrowserPlatform : IPlatform` and its JS: `wwwroot/boot.js` (runtime start) and `wwwroot/frame-loop.js`. Its `wwwroot/` ships with the app under `_content/LudumDare60.Engine.Platform.Browser/`. |
| `src/game/LudumDare60.Game` | `Microsoft.NET.Sdk` | `LudumDare60Game : IGame`. No platform dependency. |
| `src/game/LudumDare60.Game.Browser` | `Microsoft.NET.Sdk.WebAssembly` | Entry point, host page and `main.js`. Only wires the game and the browser platform together. |

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

### Browser startup

1. `index.html` loads the app's `main.js`, which imports `boot.js` from
   `_content/LudumDare60.Engine.Platform.Browser/` and calls `boot('./_framework/')`.
2. `boot(frameworkRoot)` resolves `dotnet.js` in `frameworkRoot` against the page URL (`document.baseURI`), imports
   it with `import()`, calls `dotnet.create()` and then `runMain()`. The root is a parameter so an app can ship
   several runtime builds (for example single- and multi-threaded) and pick one at startup; today there is one.
   The root is resolved against the page, not against `boot.js`, so the resulting URL matches the import map key
   and loads the fingerprinted file.
3. `runMain()` runs `Program.Main`, which picks the game and the platform:
   `GameEngine.RunAsync(new LudumDare60Game(), new BrowserPlatform())`.

### Browser frame loop

`BrowserPlatform.RunAsync(sink)` imports `frame-loop.js` with `JSHost.ImportAsync` and calls its `run(onFrame)`
through `[JSImport]`, passing an `Action<double>` that calls `sink.Update(dt)` and then `sink.Render()`.

- `frame-loop.js` schedules one `requestAnimationFrame` callback per frame and calls `onFrame(dt)` from it, so a
  frame costs exactly one JS-to-.NET call. `dt` is the difference between two rAF timestamps, in seconds; the
  first frame reports 0. The engine clamps large values (a hidden tab pauses rAF).
- If a frame throws, the loop stops and the promise returned by `run` rejects. The `[JSImport]` task faults with
  the original .NET exception, so `BrowserPlatform.RunAsync` and then `GameEngine.RunAsync` fault with it.
  Otherwise the task never completes.
- The module URL is `../_content/LudumDare60.Engine.Platform.Browser/frame-loop.js`, relative to
  `_framework/dotnet.runtime.js` (the runtime performs the `import()`). On build and publish the file gets a
  content hash in its name (`frame-loop.<hash>.js`), and the import map that the SDK writes into `index.html`
  maps the plain URL to it. See [build-and-deploy.md](build-and-deploy.md#static-web-assets).

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

# Architecture

## Projects

```text
LudumDare60.Game.Browser ──► LudumDare60.Game ──► LudumDare60.Engine ──► LudumDare60.Engine.Platform
          │                                                                        ▲
          └──────────────► LudumDare60.Engine.Platform.Browser ────────────────────┘
```

| Project | SDK | Role |
| --- | --- | --- |
| `src/engine/LudumDare60.Engine.Platform` | `Microsoft.NET.Sdk` | Platform contracts: `IPlatform` (with `ReportError`, a no-op by default), `IFrameSink`, raw input (`RawInputEvent`, `RawInputEventKind`, `Key`, `PointerType`), asset reads (`IAssetSource`, `AssetLoadException`), the GPU (`IGraphicsBackend`, `RenderCommandKind`, `SpriteInstance`, `Color`), sound output (`IAudioBackend`, `AudioCommandKind`, `AudioBus`, `AudioEvent`, `AudioEventKind`, `AudioLimits`). No dependencies. |
| `src/engine/LudumDare60.Engine` | `Microsoft.NET.Sdk` | `GameEngine`, `IGame`, `FrameContext`, `Input/` (`InputState`, `PointerButton`), `Assets/` (`AssetManager` and loaders), `Rendering/` (`PixelViewport`, `Graphics`, `Texture`, `SpriteBatch`, `SpriteOptions`, `IntRect`), `Sounds/` (`Audio`, `Sound`, `SoundState`, `Voice`, `SoundOptions`). Depends only on the platform contracts and the managed `StbImageSharp` package (image decoding). |
| `src/engine/platforms/LudumDare60.Engine.Platform.Browser` | `Microsoft.NET.Sdk.StaticWebAssets` | `BrowserPlatform : IPlatform` and its JS: `wwwroot/boot.js` (runtime start), `wwwroot/load-log.js` (load timing), `wwwroot/reporting.js` (errors and load timing to Sentry), `wwwroot/frame-loop.js`, `wwwroot/input.js`, `wwwroot/assets.js`, `wwwroot/webgl2.js` (renderer, shaders in `wwwroot/shaders/`) and `wwwroot/webaudio.js` (sound). Its `wwwroot/` ships with the app under `_content/LudumDare60.Engine.Platform.Browser/`. |
| `src/game/LudumDare60.Game` | `Microsoft.NET.Sdk` | `LudumDare60Game : IGame`, `Parts/` (the touch loop: `PartDrag` moves a part along a `PartPath` (a `LinePath`, or a screw turned by circling it) with the `DragFeel` it is given, fed one `PointerSample` per fixed step, and with a `FreeMove` lets it lie anywhere on the work surface once out and stick back to its rail, or drop straight into its seat; `PartAssembly` steps the parts of a gun and enforces which part blocks which), `Cleaning/` (`DirtLayer`: dirt pixels scrubbed off a part and counted; `BrushTool`: a tool the player picks up and puts down anywhere, the brush that scrubs and the oil can that oils), `Orders/` (the order data of `orders.json`, its loader and validation, and `RevolverParts`), `Bench/` (`OrderBench`: one order on the bench with its parts, dirt, oil, spares and tools; `ActionTest`: the hammer at half cock to take the revolver apart, then full cock and the trigger to test it; `BenchLayout`: where the revolver, its parts, the tools and the tray of spares lie), `Customers/` (`Shop`: customers coming in, the repair, the hand-back, the next customer), and placeholder parts and sounds generated in code. No platform dependency. |
| `src/game/LudumDare60.Game.Browser` | `Microsoft.NET.Sdk.WebAssembly` | Entry point, host page and `main.js`. Only wires the game and the browser platform together. |

The engine and the game never reference a concrete platform. Only the entry-point project chooses one:

```csharp
await new GameEngine(640, 360).RunAsync(new LudumDare60Game(), new BrowserPlatform());
```

## Frame loop

`GameEngine.RunAsync(game, platform)` stores the game, creates an `AssetManager` on `platform.Assets`, a `Graphics`
on `platform.Graphics` and an `Audio` on `platform.Audio`, calls `IGame.Initialize(assets, graphics, audio)` and awaits
`platform.RunAsync(engine)`. When the run ends, it unloads every asset and destroys every texture and sound. If the run
failed, the run's exception is the one rethrown, even when a loader, the GPU or the audio device also throws while
cleaning up. The
engine is the platform's `IFrameSink`. The platform owns timing (in the browser: `requestAnimationFrame`) and calls, once per
frame:

1. `IFrameSink.Update(dt, events)`, with `dt` = seconds since the previous frame and `events` = the raw input
   events that arrived since then (see [Input](#input)).
2. `IFrameSink.Render()`.

Inside `GameEngine.Update(dt, events)`:

1. `AssetManager.Update()` applies the asset reads that finished since the previous frame (see [Assets](#assets)).
   The last `Resize` event of the frame, if any, sets the viewport (see [Screen](#screen)).
2. `dt` is clamped to `[0, GameEngine.MaxDeltaTime]` (0.25 s), and NaN counts as 0. This prevents a catch-up
   spiral after a hidden tab or a debugger pause.
3. `dt` is added to an accumulator. While the accumulator holds at least `GameEngine.FixedDeltaTime` (1/60 s),
   the engine calls `IGame.FixedUpdate` once and subtracts one step. A frame can run zero or more fixed steps.
4. `IGame.Update` runs once, with the frame's clamped `dt` and the frame's input events, passed through without
   copying. `FixedUpdate` gets no events.
5. `IGame.PrepareRender(alpha)` runs last, with `alpha = accumulator / FixedDeltaTime` in `[0, 1)`. It tells how
   far the current time is between the last fixed step and the next one. Use it to interpolate render state.
   From here to the end of the frame, recording audio commands throws.

`GameEngine.Render()` calls `IGame.Render(batch)` and then submits the recorded frame to the GPU (see
[Rendering](#rendering)). Last, it submits the frame's audio commands and applies the decode results the platform
reports (see [Audio](#audio)).

### Browser startup

1. `index.html` loads the app's `main.js`, which imports `boot.js` from
   `_content/LudumDare60.Engine.Platform.Browser/` and calls `boot('./_framework/')`.
2. `boot(frameworkRoot)` resolves `dotnet.js` in `frameworkRoot` against the page URL (`document.baseURI`), imports
   it with `import()`, sets a resource loader (`dotnet.withResourceLoader`), calls `dotnet.create()` and then
   `runMain()`. The loader downloads the runtime's `.wasm` files with `fetchWithRetry`: a network error or a 408,
   429 or 5xx response is retried six times over about 31 s (`RETRY_DELAYS_MS`), where the runtime's own retry gives
   up after about 100 ms. Each attempt goes through `createWatchedFetch`, which reads the body itself and aborts the
   request when no bytes arrive for 10 s (`STALL_MS`), so a frozen connection fails fast instead of at the browser's
   120 s timeout. Because it reads the body, it also checks the `integrity` hash itself (`crypto.subtle.digest`):
   with `integrity` on the request, `fetch` resolves only after the whole body, and a stall could not be seen. Retries bypass the HTTP cache (`cache: 'reload'`): a body that failed its integrity check
   stays cached, and reading it again would fail the same way. The first attempt drops the runtime's
   `cache: 'no-cache'`, so a returning player loads the content-hashed files from the HTTP cache without
   revalidating each one. The root is a parameter so an app can ship
   several runtime builds (for example single- and multi-threaded) and pick one at startup; today there is one.
   The root is resolved against the page, not against `boot.js`, so the resulting URL matches the import map key
   and loads the fingerprinted file.
3. `runMain()` runs `Program.Main`, which picks the game and the platform:
   `GameEngine.RunAsync(new LudumDare60Game(), new BrowserPlatform())`.
4. The page's `<div id="status">` (optional, `boot`'s second parameter) shows "Loading..." while the runtime
   downloads, is hidden right before `runMain()`, and shows an error message if startup fails or `Main` ends with an
   exception (a frame that throws stops the frame loop and ends `Main`). The error itself goes to the console. A
   module that knows why the game stopped sets a better message with `setErrorText` before it throws: `webgl2.js`
   does so when the browser has no WebGL2 and when the GPU context is lost.
5. The runtime imports its own JS modules (`dotnet.native.js`, `dotnet.runtime.js`), so the resource loader cannot
   retry them. When one fails to load ("Failed to fetch dynamically imported module", for example a corrupt cached
   copy that fails its import map integrity), `boot` downloads every `_framework/` target of the import map again
   with `cache: 'reload'` and reloads the page. It does this once per tab (`repairOnce`, a `sessionStorage` flag);
   a second failure shows the error text.

### Load log

`load-log.js` records how startup goes on the player's network, in every build. Each step is a `performance.mark`
named `[load] <step>` (DevTools: Performance > Timings) and a console line that starts with `[load]` and gives the
time since the page started loading. Filter the console by `[load]` to see only these lines:

```text
[load] 180 ms  boot
[load] 410 ms  runtime script imported
[load] 2.0 s  dotnet.native.abc.wasm failed (TypeError: Failed to fetch), retry 1/4 in 1.0 s
[load] 31.2 s  runtime downloaded: 20 files, 3.21 MB transferred, 9.30 MB decoded
[load] 31.9 s  runtime created, running Main
[load] 32.1 s  first frame
[load] 33.4 s  assets: 12 files, 812.0 KB, in 1.2 s
```

- `boot`, `runtime script imported` and `runtime created, running Main` come from `boot.js`. The gap between the
  last download and `runtime created` is compiling and starting the runtime.
- `runtime downloaded` is a collapsed group. It holds a table with one row per `_framework/` file: start, duration,
  bytes transferred (0 when served from the cache) and bytes decoded. The numbers come from the Resource Timing API.
- A retry line is a warning, written by the resource loader before each wait.
- `first frame` is written by `frame-loop.js` once the first `onFrame` returns.
- `assets` comes from `assets.js` each time the in-flight asset reads drop to zero. Its table lists each file, with a
  negative byte count for a failed read (`-404`, or `-1` for a network error).

The log also keeps a snapshot (first time of each step, retry count, first asset batch) and notifies listeners of
every line. `reporting.js` uses both: lines become breadcrumbs, and the snapshot becomes the `load` transaction sent
to Sentry when reporting is configured (see
[Error and load reports](build-and-deploy.md#error-and-load-reports)).

### Browser frame loop

`BrowserPlatform.RunAsync(sink)` creates the WebGL2 context (see [Browser renderer](#browser-renderer)) and the
`AudioContext` (see [Browser audio](#browser-audio)), imports
`frame-loop.js` and `input.js` with `JSHost.ImportAsync`, attaches input
(see [Browser input](#browser-input)) and calls `run(onFrame)` through `[JSImport]`. `onFrame` is an
`Action<double, double>` (`FrameDispatcher.OnFrame`) that drains input, then calls `sink.Update(dt, events)` and
`sink.Render()`. Input listeners are removed when the loop ends.

- `frame-loop.js` schedules one `requestAnimationFrame` callback per frame and calls `onFrame(dt, timestamp)` from
  it, so a frame costs one JS-to-.NET call, plus the .NET-to-JS call that drains input. `timestamp` is the rAF
  timestamp in milliseconds. `dt` is the difference between two rAF timestamps, in seconds; the
  first frame reports 0. The engine clamps large values (a hidden tab pauses rAF).
- If a frame throws, the loop stops and the promise returned by `run` rejects. The `[JSImport]` task faults with
  the original .NET exception, so `BrowserPlatform.RunAsync` and then `GameEngine.RunAsync` fault with it.
  Otherwise the task never completes.
- The module URL is `../_content/LudumDare60.Engine.Platform.Browser/frame-loop.js`, relative to
  `_framework/dotnet.runtime.js` (the runtime performs the `import()`). On build and publish the file gets a
  content hash in its name (`frame-loop.<hash>.js`), and the import map that the SDK writes into `index.html`
  maps the plain URL to it. See [build-and-deploy.md](build-and-deploy.md#static-web-assets).

## Assets

`AssetManager` (in `LudumDare60.Engine.Assets`) loads files through the platform's `IAssetSource`, parses them with
registered loaders and caches the result by path. Asset files live in `src/game/LudumDare60.Game.Browser/wwwroot/assets/`
and are requested by their path below that folder, for example `sounds/cylinder-click.ogg`. Keep the paths of a game
as `const string` fields in one static class (`AssetPaths`), so a typo fails at one place.

```csharp
// FontLoader and Font stand for a future parser.
public void Initialize(AssetManager assets, Graphics graphics)
{
    _assets = assets;
    assets.Register(new FontLoader());                  // one IAssetLoader<T> per type; byte[] is built in
    _font = assets.Request<Font>(AssetPaths.MainFont);  // never blocks
}

public void Update(in FrameContext context, ReadOnlySpan<RawInputEvent> events)
{
    if (_assets.TryGet(_font, out var font)) { /* ... */ } // array index, no allocation
}
```

- **Requests never block.** `Request<T>(path)` returns an `AssetHandle<T>` at once and starts the read. The read runs
  in the background; its result is applied only in `AssetManager.Update()`, which the engine calls at the start of
  every frame. The asset's state (`Loading`, `Loaded`, `Failed`) therefore changes at one known point of the frame,
  and parsing happens there too. A loading screen waits for `PendingCount == 0`.
- **Handles.** An `AssetHandle<T>` is a slot index plus a generation. `Get`, `TryGet` and `GetState` index an array
  and allocate nothing, so per-frame code may call them. A handle that was released, or unloaded by `UnloadAll`, is
  stale: every call with it throws `InvalidOperationException` instead of returning another asset.
- **Cache and reference counting.** The cache key is the path, and a path has one type: requesting it as another type
  throws. Each `Request` adds a reference and needs one `Release`. Requests for a cached asset return it at once;
  concurrent requests share one read. The last `Release` unloads the asset (`IAssetLoader<T>.Unload`) right away.
  A read still in progress when its asset is released finishes, and its result is dropped. `UnloadAll` unloads
  everything, whatever the reference counts. A loader that throws from `Unload` does not stop it: the other assets
  are unloaded too, and the failures are thrown afterwards as one `AggregateException`.
- **Loaders.** `IAssetLoader<T>.Load(path, data)` turns the file bytes into a `T` and owns the array. Register them
  explicitly with `Register<T>`; there is no reflection. `RawBytesLoader` (`byte[]`, the file itself) is registered
  in every manager. `ImageLoader` (`Image`, see [Images](#images)) is not: register it when the game has images.
  The game registers `OrderBookLoader` for `data/orders.json`: source-generated `System.Text.Json` (reflection-based
  serialization is off in every project), strict about unknown properties, missing values and nulls, followed by a
  validation pass. Parsers for Aseprite files and fonts are not written yet (see
  [docs/backlog.md](../backlog.md#asset-parsers)). Sounds need no loader: request the file as `byte[]` and hand it to
  `Audio.CreateSound` (see [Audio](#audio)). One asset is one file: a loader cannot depend on other assets.
- **Errors.** A failed read or a throwing loader moves the asset to `Failed`. `GetError` returns an
  `AssetLoadException` with the path, the HTTP status (0 without a response) and the original error as
  `InnerException`. Failures are not cached: the next `Request` of that path reads again, for every handle.
- **Paths** are checked by `AssetPath.IsValid` before anything is read: relative, `/` separators, segments of
  lowercase ASCII letters, digits, `-`, `_` and `.`, and no empty, `.` or `..` segment. Upper case is rejected on
  purpose: itch.io file names are case-sensitive, and the desktop tests catch the mistake.

### Images

`ImageLoader` decodes image files with [StbImageSharp](https://github.com/StbSharp/StbImageSharp), a managed C# port
of `stb_image.h` (no native code, no reflection, marked trimmable). It is not built into `AssetManager`, so a game that
never registers it does not ship the decoder.

```csharp
assets.Register(new ImageLoader());
_revolver = assets.Request<Image>(AssetPaths.Revolver);
```

- **Format.** `Image` holds 8-bit RGBA pixels, row by row from the top-left corner, rows tightly packed
  (`Stride = Width * 4`), straight alpha. This is the `rgba8unorm` texture layout, so the pixels upload as they are.
  Every file is expanded to it: gray goes to red, green and blue, and a missing alpha channel is opaque. Pixels are
  not premultiplied and rows are not flipped. `Graphics.CreateTexture` uploads them as they are (see
  [Rendering](#rendering)).
- **Files.** Ship PNG. The decoder also reads JPEG, BMP, TGA, PSD, HDR (reduced to 8 bits) and the first frame of a
  GIF, but JPEG artifacts ruin pixel art, and Aseprite files get their own parser.
- **Memory.** Decoding allocates the pixel array once, at load time in `AssetManager.Update`, and `Image` keeps it.
  `Image.Pixels` is a `ReadOnlySpan<byte>`: one loaded image is shared by every handle to its path. Unloading drops the
  array for the GC.
- **Errors.** An empty, corrupt or unsupported file throws `InvalidOperationException` with stb's reason
  (`unknown image type`, `bad png sig`, ...), and the asset becomes `Failed` like any other parse failure.

### Browser assets

`BrowserPlatform.Assets` is a `BrowserAssetSource` on `wwwroot/assets.js`. The asset root (`assetRoot`, default
`assets/`) is resolved against the page (`document.baseURI`), not against `_framework/`. A file costs two interop
calls: `read(id, url, noCache)` fetches it, keeps the bytes in JS and resolves with their length (or with minus the
HTTP status), then C# allocates a `byte[]` of that length and `take(id, span)` copies the bytes into it. The module is
imported on the first read, because a game requests assets in `Initialize`, before `BrowserPlatform.RunAsync` runs. A
failed import (a network error) is tried again by the next read (`LazyImport`).

Reads download like the runtime's files (see [Browser startup](#browser-startup)): `createRetryingFetch` and
`createWatchedFetch` from `boot.js` abort a read after 10 s without data and retry network errors and 408, 429 and 5xx
responses six times. Only when every attempt fails does the asset become `Failed`. The engine reports that to Sentry
(`IPlatform.ReportError`), and the game goes on without it unless it checks the asset's state.

Release builds append `?v=<commit hash>` to every asset URL, and Debug builds fetch with `cache: 'no-cache'`. See
[build-and-deploy.md](build-and-deploy.md#asset-caching) for why.

## Input

The platform delivers raw input with each frame: `IFrameSink.Update(dt, ReadOnlySpan<RawInputEvent> events)`. The
engine folds the events into `FrameContext.Input` (see [Input state](#input-state)), which is what gameplay reads. It
also passes the raw span to `IGame.Update` as is, for tools and diagnostics such as `samples/InputMonitor`. Bindings
and gamepad state come later, see [docs/backlog.md](../backlog.md#input-bindings).

`RawInputEvent` (in `LudumDare60.Engine.Platform`) is a 24-byte blittable struct, and its layout is the wire format:
the browser writes events as little-endian bytes and C# reads them in place with `MemoryMarshal.Cast`.

| Offset | Field | Type | Meaning |
| --- | --- | --- | --- |
| 0 | `Kind` | `byte` | `RawInputEventKind`. |
| 1 | `Detail` | `byte` | Pointer button, pressed or locked flag, gamepad `standard` flag. |
| 2 | `Code` | `ushort` | `Key`, `PointerType`, gamepad button or axis index. |
| 4 | `Id` | `int` | Pointer id, gamepad index, dropped event count. |
| 8 | `Time` | `float` | Seconds relative to the start of the delivering frame, usually in `[-dt, 0]`, never positive. |
| 12 | `X` | `float` | Pointer x, motion dx, wheel dx, axis or button value, width. |
| 16 | `Y` | `float` | Pointer y, motion dy, wheel dy, height. |
| 20 | `Z` | `float` | Wheel dz, device pixel ratio. |

What each field means for each kind is documented on `RawInputEventKind`. `samples/InputMonitor` shows every event
live and is a small example of a consumer. The rules every platform follows:

- Events are in arrival order, not sorted by `Time`.
- `Key` values are USB HID usage IDs, named after `KeyboardEvent.code`. Keys outside the table are dropped. Key
  auto-repeat is not reported. Modifiers are ordinary keys; there is no modifier mask.
- Pointer coordinates are logical (CSS) pixels from the top-left corner of the drawing surface. A locked mouse
  sends `PointerMotion` (relative) instead of `PointerMove`. Each button change is its own `PointerDown` or
  `PointerUp`, even when several buttons are held.
- `Resize` comes first at startup, then the focus state (`FocusGained` or `FocusLost`). Its logical size may be
  fractional: `X * Z` and `Y * Z`, rounded, are the exact surface size in device pixels where the platform knows it.
- `FocusLost` and `Overflow` mean that key-up and button-up events may be missing: release everything.
- `GamepadConnected` with `Detail` 1 means the W3C Standard Gamepad layout: a platform that sets the flag must remap
  to it. With 0 the indices are raw and device specific.
- The test fixture `tests/fixtures/raw-input-events.json` holds encoded bytes that both `input.js`
  (`tests/js/input.test.mjs`) and `RawInputEvent` (`RawInputEventTests`) must agree on, and the `code` to `Key`
  table.

### Browser input

`wwwroot/input.js` has a pure core, tested in Node, and thin bindings for C#:

- `createInputQueue(capacity)` is a ring buffer of 4096 events in an `ArrayBuffer`. Handlers write through a
  `DataView` and allocate nothing. When the ring is full, new events are dropped, and one `Overflow` event (with
  the dropped count in `Id`) takes their place after the events queued before them.
- `createInput(env)` installs the listeners: keys and focus on `window`, visibility and pointer lock on `document`,
  pointer events, `wheel` and `contextmenu` on the canvas, a `ResizeObserver` on the canvas. The observer watches
  the `device-pixel-content-box`, and `Resize` reports that size divided by `devicePixelRatio`, because
  `clientWidth * devicePixelRatio` can be one pixel off (at 1.25, for example) and a canvas one pixel off is
  resampled. Browsers without that box (Safari) report `clientWidth` and `clientHeight`. Gamepads have no
  events, so every drain polls `navigator.getGamepads()` and sends what changed since the previous poll; values
  that moved by less than 1/256 count as unchanged.
- `attach(canvasId)`, `drain(span, frameTimeMs)` and `detach()` are bound through `InputInterop`. The canvas is
  `<canvas id="screen">` in `index.html` (`BrowserPlatform.DefaultCanvasId`).

Each frame, `FrameDispatcher.OnFrame` drains up to `FrameDispatcher.InputCapacity` (512) events into a preallocated
`RawInputEvent[]` viewed as bytes. Events that do not fit stay in the ring for the next frame.

Browser defaults the game turns off: page scrolling and focus moves from keys in the `Key` table (F1-F12 and
Ctrl/Meta shortcuts stay with the browser), wheel scrolling and the context menu on the canvas, and touch gestures
(`touch-action: none`). `pointerdown` is not prevented: on itch.io that click is what focuses the game's iframe.

### Input state

`InputState` (in `LudumDare60.Engine.Input`) holds keys and pointer buttons (`IsDown`, `IsPressed`, `IsReleased`),
`PointerPosition` and `PointerDelta` in virtual pixels, `IsPointerOnScreen`, `Wheel` and `HasFocus`. It allocates
nothing. The game gets it as `context.Input`:

```csharp
public void FixedUpdate(in FrameContext context)
{
    if (context.Input.IsPressed(PointerButton.Primary) && context.Input.IsPointerOnScreen)
    {
        _grab.Begin(context.Input.PointerPosition);
    }
}
```

- **Two views.** `FixedUpdate` and `Update` get different `InputState`s. `Update` sees every event of the frame at
  once, with the edges (pressed, released) of this frame. `FixedUpdate` sees the events that happened before the end
  of its step, with the edges of this step. Gameplay reads input in `FixedUpdate`; UI, cursors and cameras may read
  it in `Update`.
- **No extra latency.** `InputRouter` assigns each event to the fixed step whose time span contains it, using
  `RawInputEvent.Time`: a step that ends at frame time `-accumulator` gets the queued events with time at most that.
  A press reaches the step it happened in, not the next frame's first step.
- **Edges are never lost or doubled.** Events later than the frame's last step wait for the next frame, with their
  times moved back by its `dt`, so a frame without steps loses nothing. Edges are cleared at the start of each step
  (and each frame for the `Update` view), so a frame with several steps reports a press once. A key pressed and
  released within one step is pressed and released, and not down.
- **Order.** Events are taken in arrival order: one waits while an earlier-arrived one waits, so a key's up never
  overtakes its down.
- **Release everything.** `FocusLost` and `Overflow` release every key and button, with released edges, and
  `PointerCancel` releases the buttons. A new run starts with nothing down and the pointer at the screen center.
- **Pointers.** Buttons are merged across pointers (one mouse or one finger at a time). Positions are converted with
  the frame's viewport, so they are virtual pixels; on the bars they fall outside the screen. A locked pointer's
  motion goes into `PointerDelta` only.

### FrameContext

`FrameContext(DeltaTime, TotalTime, FrameIndex, Viewport, Input)` is passed by `in` to `FixedUpdate` and `Update`.
All times are in seconds. `Viewport` is the frame's `PixelViewport` (see [Screen](#screen)), the same in both.

| Field | `FixedUpdate` | `Update` |
| --- | --- | --- |
| `DeltaTime` | Always `FixedDeltaTime`. | The frame's clamped `dt`. |
| `TotalTime` | `(FrameIndex + 1) * FixedDeltaTime`: simulated time after this step. Computed from the step count, so it does not drift. | Sum of all clamped `dt`, including this frame. |
| `FrameIndex` | Zero-based fixed step counter. | Zero-based frame counter. |
| `Input` | Input up to the end of this step, edges of this step. | Input up to the start of this frame, edges of this frame. |

All counters reset at the start of each `RunAsync`. Starting a second `RunAsync` on an engine that is already
running throws `InvalidOperationException`. So does calling `Update` or `Render` when no game is running.

### Rules

- Gameplay that must be deterministic (physics, collisions, timers that affect rules) goes into `FixedUpdate`, and
  reads its input there.
- `Update` is for per-frame work that may depend on the actual frame rate: cursors, camera smoothing, UI.
- `PrepareRender` reads simulation state and builds render data. It must not change simulation state.
- `Render` draws the prepared data into the `SpriteBatch`. It has no access to timing on purpose.

## Screen

The game draws a low-resolution virtual screen (640x360 for *Six Chambers*, set by `new GameEngine(640, 360)`) and
the renderer scales it up to the canvas. `Rendering/PixelViewport` holds the math, so the renderer and the input code
agree on where every virtual pixel is:

- `PixelViewport.Fit(virtualWidth, virtualHeight, surfaceWidth, surfaceHeight, pixelRatio)` takes the fields of a
  `Resize` event. The surface is converted to device pixels (`SurfaceWidth`, `SurfaceHeight`, rounded): the canvas
  gets exactly this size, which in browsers with `device-pixel-content-box` is the canvas's real size in device pixels
  (see [Browser input](#browser-input)).
- The scale is the largest whole number of device pixels per virtual pixel that fits, so every virtual pixel is the
  same size. The image is centered (`X`, `Y`, `Width`, `Height`, from the top-left, in device pixels) with bars
  around it. Only a surface smaller than the virtual screen gets a fractional scale, so nothing is cut off.
- `ToVirtual(x, y)` converts pointer positions (logical pixels) to virtual pixels. Positions on the bars fall
  outside the screen: check with `Contains`.

Examples for 640x360: 1280x720 is scale 2, 1920x1080 is scale 3, 1280x720 at pixel ratio 1.5 is scale 3, and
1400x800 is scale 2 with 60 and 40 pixel bars.

The engine owns the viewport: before the fixed steps of a frame it applies the frame's last `Resize` event and
passes the result in `FrameContext.Viewport`, and the renderer receives the same value. Until the first `Resize`
the viewport is the virtual screen at scale 1. Convert pointer positions with `context.Viewport.ToVirtual(x, y)`.

## Rendering

Rendering is a sprite batch over a backend-neutral command stream. The game sees three types in
`LudumDare60.Engine.Rendering`:

```csharp
public void Initialize(AssetManager assets, Graphics graphics)
{
    assets.Register(new ImageLoader());
    _image = assets.Request<Image>(AssetPaths.Revolver);
    _graphics = graphics;
}

public void Update(in FrameContext context, ReadOnlySpan<RawInputEvent> events)
{
    if (!_hasTexture && _assets.TryGet(_image, out var image))
    {
        _texture = _graphics.CreateTexture(image); // one interop call, at load time
        _hasTexture = true;
    }
}

public void Render(SpriteBatch batch)
{
    batch.Clear(MatColor);
    batch.Draw(_texture, new IntRect(0, 0, 32, 32), position, new SpriteOptions { Origin = new(16, 16), Rotation = angle });
}
```

- **Textures.** `Graphics.CreateTexture(Image)` uploads an image and returns a `Texture`: a handle with an id, a
  generation and the size, like `AssetHandle`. One texture per file: a PNG or Aseprite file that holds several frames
  is one texture, and the frame is the source rectangle of `Draw`. There is no atlas packing. Textures can be created
  from the first frame on; in `Initialize` it throws, because the platform sets up the GPU when its frame loop starts.
  Sides are at most `Graphics.MaxTextureSize` (4096). The texture keeps no copy of the pixels: keep the `Image` (or its
  asset handle) for pixel-exact hit tests.
- **Destroying.** `Graphics.DestroyTexture` makes the handle stale at once (drawing with it throws), and the GPU texture
  is deleted after the frame is submitted, so sprites already drawn with it still show. Its id is reused only after
  that. When the run ends, the engine destroys every remaining texture.
- **Drawing.** `SpriteBatch` exists only during `IGame.Render`. `Draw(texture, source, position, options)` adds one
  sprite; `SpriteOptions` holds origin (the pivot, in source pixels), scale (negative flips), rotation (radians,
  clockwise on screen) and tint (multiplies the texels; its alpha fades). Positions are virtual pixels, y down.
  Sprites are drawn in call order. Consecutive sprites with the same texture share one draw call, so a texture change
  costs one. `Clear(color)` fills the virtual screen; nothing clears it otherwise. A frame holds at most
  `SpriteBatch.Capacity` (16384) sprites; one more throws.
- **Pixel grid.** A sprite without rotation is snapped so its top-left corner lands on a whole virtual pixel. The scene
  is drawn into a texture of the virtual size and scaled onto the canvas with nearest filtering, so even rotated
  sprites stay on the virtual pixel grid.
- **Alpha.** Textures hold straight alpha. The sprite shader multiplies by the tint and premultiplies, and blending is
  `ONE, ONE_MINUS_SRC_ALPHA`. With nearest filtering this matches premultiplying at upload time.

### Command stream

`GameEngine.Render` calls `SpriteBatch.Begin`, `IGame.Render(batch)`, then `SpriteBatch.End`, which hands two spans
to `IGraphicsBackend.Submit`: the commands and the frame's `SpriteInstance` array. A backend runs them in one call.

| Command | Arguments (32-bit, little-endian) |
| --- | --- |
| `SetSurface` (1) | Surface width and height (device pixels), virtual width and height, viewport x, y, width, height (device pixels from the top-left). Always first. |
| `Clear` (2) | RGBA as 4 bytes. |
| `DrawSprites` (3) | Texture id, first instance, instance count. |
| `Present` (4) | None. Scales the virtual screen into the viewport, black bars around it. Always last. |

`SpriteInstance` is 40 bytes and is the vertex layout of the sprite shader: position, origin and scale (`float` x2
each), rotation (`float`), tint (4 bytes), source x, y, width, height (`ushort` each). The layouts are documented on
`RenderCommandKind` and `SpriteInstance`. `tests/fixtures/render-frame.json` holds one encoded frame that both
`SpriteBatchTests` (C# must write these bytes) and `tests/js/webgl2.test.mjs` (the backend must run them) check.

Textures are created, updated and destroyed through their own calls (`IGraphicsBackend.CreateTexture`,
`UpdateTexture`, `DestroyTexture`), outside the stream, because the pixels would not fit in it.
`Graphics.UpdateTexture(texture, image, firstRow, rowCount)` replaces whole rows from an image of the texture's size,
for pixels that change while the game runs (a part scrubbed clean): one interop call and one `texSubImage2D` per call,
so gather a frame's changes into one range. Sprites drawn with the texture in that frame show the new rows.

### Browser renderer

`BrowserPlatform.Graphics` is a `BrowserGraphics` on `wwwroot/webgl2.js`. `RunAsync` imports the module and calls
`init(canvasId)`, which creates a WebGL2 context on the canvas (`alpha`, `antialias`, `depth` and `stencil` off) before
the first frame. Each frame is one `submit(commands, instances)` interop call; the module reads both spans in place
(`_unsafe_create_view`) and uploads the instances with one `bufferSubData`.

- `SetSurface` sets `canvas.width` and `canvas.height` when they change and (re)creates the render target: an `RGBA8`
  texture of the virtual size with a framebuffer.
- `DrawSprites` draws `TRIANGLE_STRIP` quads with `drawArraysInstanced`, one instance per sprite. WebGL2 has no base
  instance, so the attribute pointers are moved to the run's first instance before each draw.
- `Present` clears the canvas to black and `blitFramebuffer`s the render target into the viewport with `NEAREST`.
- The shaders are GLSL ES 3.00 strings in `wwwroot/shaders/sprite.glsl.js`. They ship as JS modules, so they get a
  content hash and an import map entry like every other module; a `.glsl` file would be served under a fixed name.
- **Failures.** Without WebGL2, `init` sets the status text to "This game needs WebGL2..." and throws, so `RunAsync`
  faults and the page shows it. A lost context is not restored: the next `submit` throws, the game stops and the page
  says "The graphics context was lost. Reload the page to continue."

WebGPU is not implemented yet (see [docs/backlog.md](../backlog.md#webgpu-backend)). It will consume the same command
stream and instance layout.

## Audio

Sound goes through one class, `Audio` (in `LudumDare60.Engine.Sounds`), which the engine passes to `IGame.Initialize`.
The platform decodes the files, and the game only holds handles. Calls record commands into a preallocated buffer.
At the end of the frame, after the render commands, the engine hands the buffer to `IAudioBackend.Submit` in one call,
which also returns what the platform reports (decode results).

```csharp
public void Initialize(AssetManager assets, Graphics graphics, Audio audio)
{
    _audio = audio;
    _clickFile = assets.Request<byte[]>(AssetPaths.CylinderClick);     // a sound is a byte[] asset
}

public void Update(in FrameContext context, ReadOnlySpan<RawInputEvent> events)
{
    if (!_hasClick && _assets.TryGet(_clickFile, out var bytes))
    {
        _click = _audio.CreateSound(bytes);                            // one interop call; the platform decodes it
        _assets.Release(_clickFile);                                   // the platform keeps its own copy
        _hasClick = true;
    }

    if (_hasClick && context.Input.IsPressed(PointerButton.Primary))
    {
        _audio.Play(_click, AudioBus.Sfx, new SoundOptions { Pitch = 0.95f + (0.1f * _random.NextSingle()) });
    }
}
```

- **Sounds.** `Audio.CreateSound(bytes)` returns a `Sound` handle (id and generation, like `Texture`). The platform
  decodes asynchronously. `GetState` is `Decoding`, then `Ready` or `Failed` (a corrupt file or an unsupported
  format; the platform logs why). The state changes only at the end of a frame. Sounds can be created from the first
  frame on; in `Initialize` it throws, like textures. `DestroySound` stops every voice of the sound, and the engine
  destroys the remaining sounds when the run ends.
- **One-shots.** `Play(sound, bus, options)` is fire and forget. It is skipped while the sound is not `Ready`. The
  platform drops it while audio is locked (before the player's first click or key press) and when
  `AudioLimits.MaxVoices` (32) voices already play on Sfx and Ambience. Vary `Pitch` a little per play, so repeated
  effects do not sound mechanical.
- **Loops.** `PlayLoop(sound, bus, options, fadeIn)` returns a `Voice`. `SetVoice` changes its volume, pitch and pan,
  and it is cheap enough to call every frame (for example to follow the pointer speed). `Stop(voice, fadeOut)` fades
  it out. A stopped voice is stale, and using it throws. A loop of a sound that is still decoding starts once the
  sound is ready. Loops are never dropped, and a loop started before the unlock plays once audio unlocks.
- **Music.** `PlayMusic(sound, crossfade, volume)` loops one track on the Music bus. A new track crossfades with the
  old one, and the same track again does nothing. `StopMusic(fade)` fades it out, and `Music` is the current track.
  Music does not count against the voice limit.
- **Buses.** Every voice plays on `Sfx`, `Ambience` or `Music`, and the three feed `Master`. `SetBusVolume(bus,
  volume)` sets one, and setting `Master` to 0 mutes everything. Bus volumes go back to 1 when the run ends.
- **Units.** Volume is a linear gain (at least 0). Pitch is a playback rate in `(0, 16]`, so 2 is an octave up and
  twice as fast. Pan runs from -1 (left) to 1 (right). Changes to a playing voice or a bus are smoothed over about
  10 ms, so they never click. `default(SoundOptions)` has zero volume and pitch: start from `new SoundOptions { ... }`.
- **When.** Commands may be recorded in `Initialize`, `FixedUpdate` and `Update`. In `PrepareRender` and `Render` they
  throw, because those only draw. A sound plays as soon as the frame's commands reach the platform, so it lands up to
  one frame (about 16 ms) after its input event. Nothing is scheduled by event time.
- **Files.** Ship short effects as 16-bit mono WAV (22 or 44 kHz): every browser decodes WAV, it loops without a gap,
  and itch.io gzips `.wav`. Ship music and ambience loops as OGG Vorbis. MP3 and AAC add encoder padding, which
  leaves a gap at the loop point. Safari decodes Ogg only from 18.4 on, so older Safari plays the game without music
  and ambience, but with effects. Keep the file count low (itch.io allows at most 1000 files). A decoded sound takes
  `seconds x rate x channels x 4` bytes of JS memory: a minute of 44.1 kHz stereo is about 21 MB.

`samples/SoundBoard` plays every kind of sound with test files synthesized by its `generate-sounds.py`.

### Command stream

| Command | Arguments (32-bit, little-endian) |
| --- | --- |
| `Play` (1) | Sound id, bus, volume, pitch, pan. |
| `PlayLoop` (2) | Voice id, sound id, bus, volume, pitch, pan, fade-in seconds. |
| `SetVoice` (3) | Voice id, volume, pitch, pan. |
| `StopVoice` (4) | Voice id, fade-out seconds. The id is free again once the command runs. |
| `SetBusVolume` (5) | Bus, volume. |

Ids and buses are `int32`, everything else is `float32`. The engine chooses voice ids, so a loop costs no reply.
`IAudioBackend.Submit` writes `AudioEvent`s (8 bytes: kind, sound id) into a span: `SoundReady` (1) or
`SoundFailed` (2). Sounds are created and destroyed through their own calls (`CreateSound`, `DestroySound`),
outside the stream, because the file bytes would not fit in it. A destroy reaches the platform after the frame's
commands, so commands recorded before it in the same frame still see the sound.
`tests/fixtures/audio-frame.json` holds one encoded frame that both `AudioTests` (C# must write these bytes) and
`tests/js/webaudio.test.mjs` (the backend must run them) check.

### Browser audio

`BrowserPlatform.Audio` is a `BrowserAudio` on `wwwroot/webaudio.js`. `RunAsync` imports the module and calls `init()`
before the first frame. `init()` creates the `AudioContext`, the four bus `GainNode`s and the listeners. Without
Web Audio the module stays silent: every sound reports `SoundFailed`, and commands are dropped.

- **Graph.** A voice is `AudioBufferSourceNode -> GainNode -> StereoPannerNode -> bus gain -> master gain ->
  destination`. One-shots create their nodes when they play and disconnect them on `ended`. Changes use
  `setTargetAtTime`, fades use `linearRampToValueAtTime`, and a stop without a fade still ramps over 10 ms.
- **Decoding.** `createSound` copies the bytes out of WASM memory and calls `decodeAudioData`. The result is queued as
  an event for the next `submit`. A result for a sound destroyed in the meantime is dropped.
- **Unlock.** Browsers start an `AudioContext` suspended. The first `pointerdown`, `keydown` or `touchend` on the window
  calls `resume()`. Until then one-shots are dropped, because a suspended context would play them late, all at once.
  Loops are created anyway and play once the context runs. On itch.io, *Click to play* provides that first gesture.
- **Hidden page.** `visibilitychange` to hidden calls `suspend()`, because frames stop in a background tab and loops
  would play on without the game. Back to visible calls `resume()`, but only after the unlock. Losing focus alone does
  not suspend anything.

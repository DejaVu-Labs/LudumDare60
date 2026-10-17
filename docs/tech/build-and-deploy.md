# Build and deploy

## Toolchain

- .NET SDK 10.0.402+ (pinned in `global.json`, `rollForward: latestFeature`).
- `wasm-tools` workload, needed for Release AOT: `dotnet workload install wasm-tools`.
- Node.js 20+ for JS tests.
- `butler` (itch.io CLI) on `PATH`. Run `butler login` once locally. CI uses the `BUTLER_API_KEY` secret.

## Browser build

Game projects use `Sdk="Microsoft.NET.Sdk.WebAssembly"`. The root `Directory.Build.props` detects this
(`UsingMicrosoftNETSdkWebAssembly`) and applies the following:

| Setting | Value | Why |
| --- | --- | --- |
| `WasmEnableThreads` | `false` | itch.io cross-origin isolation is opt-in and not reliable in every browser or embed mode. |
| `WasmEnableSIMD` | `true` | Wasm SIMD128 is supported by every browser we target; `System.Numerics` / `Vector128` code gets faster. |
| `WasmEnableExceptionHandling` | `true` | Native Wasm EH gives smaller, faster code than the JS fallback. |
| `CompressionEnabled` | `false` | itch.io already gzips `.wasm`, `.js`, `.html` and `.css`. Pre-compressed `.br`/`.gz` copies would double the file count and would never be served. |
| `InvariantGlobalization` (Release) | `true` | No ICU data files (saves several MB). Culture-sensitive APIs must not be relied on. Only Release sets it, because any value that differs from the runtime pack default forces a native relink on every Debug build (about +10 s). Desktop projects and tests always run invariant. |
| `RunAOTCompilation` (Release) | `true` | IL is compiled to native Wasm. Much faster at runtime, slower to build. |
| `WasmStripILAfterAOT` (Release) | `true` | Drops IL bodies that AOT has already compiled. |
| `TrimMode` (Release) | `full` | Trims the whole app, not only the framework. |
| Feature switches (Release) | off | `DebuggerSupport`, `EventSourceSupport`, `StackTraceSupport`, `MetricsSupport`, `UseSystemResourceKeys` reduce size. Exception messages become resource keys in Release builds. |

Release publish:

```bash
rm -rf artifacts/publish/LudumDare60.Game.Browser
dotnet publish src/game/LudumDare60.Game.Browser -c Release
# output: artifacts/publish/LudumDare60.Game.Browser/release/wwwroot/
```

Always delete the publish folder first. `dotnet publish` does not clean it, and asset names carry a content hash,
so every publish adds new files next to the old ones. `butler push` would upload the stale files too.

Reference point, measured on 2026-10-17 with SDK 10.0.402: an empty `wasmbrowser` app published in Release with
these settings produced **9 files and 6.7 MB uncompressed** (`dotnet.native.wasm` is about 5.5 MB). AOT took
about 4 minutes. The build ran fine when served from a subdirectory and did not need cross-origin isolation.

## itch.io HTML5 requirements

Source: <https://itch.io/docs/creators/html5> (checked 2026-10-17).

- Upload a **ZIP** (no other archive formats) with **`index.html` at the root**. `butler` handles the packaging:
  push the directory itself.
- **Relative paths only.** The game runs in a subdirectory of the itch.io CDN, so a leading `/` breaks it. The
  `wasmbrowser` template uses `./_framework/...`. Keep it that way.
- **File names are case-sensitive** and must be UTF-8. Requesting a folder URL (`.../`) returns 403.
- Limits: **at most 1000 files**, **at most 240 characters per path**, **500 MB in total**, **200 MB per file**.
- Compression is served automatically: `html`, `js`, `css`, `svg`, `wasm`, `wav`, `glb` and `pck` are gzipped on
  the fly. Files ending in `.br` are served with `content-encoding: br`, and `.gz` files are detected by content.
  To use Brotli later, ship only `*.br` files and load them through `dotnet.withResourceLoader(...)`.
- The game runs inside an `iframe`. Embed modes:
  - *Embed in page*: fixed viewport size, and itch.io adds its own fullscreen button.
  - *Click to launch in fullscreen*: arbitrary size. The canvas **must** follow its container size and
    `devicePixelRatio`.
  - *Click to play* is on by default and provides the user gesture needed to start audio. Keep it on.
  - Mobile always launches fullscreen. Tick *Mobile Friendly* only if touch input works.
- **SharedArrayBuffer** is an experimental opt-in embed setting (COOP/COEP headers). We do not depend on it. This
  is why threads are off.
- Requests to other domains must use HTTPS. The build should be self-contained anyway.
- HTML5 projects can only accept donations. They cannot be sold.
- Project settings: Kind of project = **HTML**. After the first push, mark the upload as *This file will be
  played in the browser*.
- Unverified: the MIME type itch.io uses for `.wasm`. If it is not `application/wasm`, the .NET loader falls
  back from streaming instantiation to an ArrayBuffer. That still works but starts slower. Check the network
  tab after the first upload.

## Deploying with butler

Source: <https://itch.io/docs/butler/pushing.html>.

```bash
butler push artifacts/publish/LudumDare60.Game.Browser/release/wwwroot <user>/<game>:html5 --userversion 0.1.0 --if-changed
butler status <user>/<game>:html5
```

- Channel name: `html5`. Channel names only auto-tag desktop and Android platforms. Browser play is enabled in
  the itch.io UI.
- `--dry-run` lists the files without pushing. `butler push-preview` diffs against the last build.
- Push an exact release folder. Do not rely on `--ignore` patterns.
- `BUTLER_API_KEY` is a secret. If it ever appears in a log, revoke it on the itch.io API keys page.

## Pre-release checklist

- [ ] `dotnet build -c Release`, `dotnet test` and `npm test` pass.
- [ ] Published build works when served from a subdirectory (`python3 -m http.server` one level up).
- [ ] Both renderer paths work: WebGPU, and WebGL2 with WebGPU disabled.
- [ ] Canvas resizes correctly in embed and fullscreen modes, and on high-DPI screens.
- [ ] Audio starts after the first click or keypress.
- [ ] No console errors. File count is under 1000.

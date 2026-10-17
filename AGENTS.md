# AGENTS.md

Ludum Dare 60 entry. Theme: **Old Tech**. A browser game written in C# on .NET 10 and compiled to WebAssembly
(`Microsoft.NET.Sdk.WebAssembly`, AOT in Release). It renders with WebGPU and falls back to WebGL2. It ships to
itch.io as an HTML5 build and is pushed with `butler`.

## Hard rules

- **English only** in the repository: code, comments, commit messages, docs, asset names. No exceptions.
- **The build must stay green.** Warnings are errors. Do not add `NoWarn`, `#pragma warning disable` or
  `[SuppressMessage]` unless there is no reasonable fix; when you do, scope it as narrowly as possible and add a
  `Justification`.
- **Everything shipped to the browser must be AOT- and trim-safe.** No reflection-based serialization, no
  `Activator.CreateInstance`, no `Type.GetType(string)`, no `dynamic`, no `System.Linq.Expressions` compilation.
  Use source generators: `[JsonSerializable]`, `[JSImport]` and `[JSExport]`, `[LibraryImport]`.
- **Every gameplay or engine change comes with tests**: C# tests under `tests/`, and JS tests under `tests/js/`
  when the change touches JavaScript.
- Never commit build output (`artifacts/`, `dist/`), API keys or `butler` credentials.

## Layout

| Path | Contents |
| --- | --- |
| `src/engine/LudumDare60.Engine` | Engine class library: math, simulation core, renderer abstraction. Never references the game. |
| `src/game/LudumDare60.Game` | Game class library: rules, content, state. References the engine and has no browser dependency. |
| `src/game/LudumDare60.Game.Browser` | `wasmbrowser` app and entry point: JS interop, WebGPU and WebGL2 backends, `wwwroot/`. References the game. |
| `tests/` | C# test projects (`*.Tests`, xUnit v3 on Microsoft.Testing.Platform) and `tests/js/` (Node test runner). |
| `samples/` | Small runnable experiments (renderer backends, interop spikes). Same rules as `src/`. |
| `docs/tech/` | Technical docs: architecture, build and deploy, guidelines. |
| `docs/gamedesign/` | Game design: concept, mechanics, levels, art and audio direction. |
| `LudumDare60.slnx` | Solution. Add every new project to it (`dotnet sln add <path>`). |

## Commands

```bash
dotnet build                                   # Debug build of the whole solution
dotnet test                                    # all C# tests (Microsoft.Testing.Platform runner, see global.json)
npm test                                       # JS tests (node --test tests/js/)
dotnet format --verify-no-changes              # formatting check, as CI runs it
dotnet publish src/game/LudumDare60.Game.Browser -c Release
# AOT + trimmed browser build -> artifacts/publish/LudumDare60.Game.Browser/release/wwwroot
butler push artifacts/publish/LudumDare60.Game.Browser/release/wwwroot <user>/<game>:html5 --userversion <ver>
```

Release AOT publishing is slow (several minutes for an empty app). Use Debug for iteration and publish Release
only to check size or performance, or to deploy.

## Build configuration

All shared settings live in the root files. Project files should hold only what is specific to that project.

- `Directory.Build.props`: target framework, analyzers (`AnalysisLevel=latest-all`, code style enforced in the
  build), AOT and trim analyzers, artifacts output, lock files, and the WebAssembly and Release-AOT switches.
  These apply automatically to any project using `Sdk="Microsoft.NET.Sdk.WebAssembly"`.
- `tests/Directory.Build.props`: test hosts turn AOT analysis off and get xUnit v3 implicitly.
- `Directory.Packages.props`: central package versions. Never put `Version=` on a `PackageReference`.
- `.editorconfig`: style and analyzer severities. `tests/**` relaxes the rules that conflict with xUnit conventions.
- `global.json`: pins the SDK and selects the Microsoft.Testing.Platform runner for `dotnet test`.

## Code guidelines

Read [docs/tech/coding-guidelines.md](docs/tech/coding-guidelines.md) before writing engine, render or interop
code. The short version:

- **Zero allocations per frame** on the hot path: no LINQ, no closures, no boxing, no `params` arrays, no string
  formatting in `Update` or `Render`. Use pooled or preallocated buffers, `Span<T>`, `stackalloc`, and
  struct-of-arrays data.
- **Few JS interop calls per frame.** Batch render and audio commands into a byte buffer in WASM memory and pass
  it in one call. Never make one interop call per sprite or draw.
- **The GPU gets buffers, not objects.** Use `[StructLayout(LayoutKind.Sequential)]` blittable structs that match
  the WGSL/GLSL layout, write them with `MemoryMarshal.AsBytes`, and upload them once per frame.
- **One thread.** The browser build is single-threaded (`WasmEnableThreads=false`), so never block. Drive the
  loop from `requestAnimationFrame` on the JS side.
- Keep the renderer behind one backend-neutral interface, with WebGPU and WebGL2 as two implementations. Game
  code never talks to either API directly.

## Testing

- C#: xUnit v3 (`[Fact]`, `[Theory]`). Name tests `Method_Condition_ExpectedResult`. Put logic in plain `net10.0`
  libraries so it can be tested without a browser.
- JS: `node:test` and `node:assert/strict`, files named `tests/js/**/*.test.mjs`. Keep JS modules pure where
  possible so they can be tested in Node.
- Browser and end-to-end checks: `Microsoft.Playwright` (version pinned in `Directory.Packages.props`) in a
  `tests/*.E2E` project. Use it for smoke tests of the published build: it boots, reaches the first frame and
  logs no console errors, on both the WebGPU and WebGL2 paths.

## Deploying

See [docs/tech/build-and-deploy.md](docs/tech/build-and-deploy.md) for the itch.io HTML5 limits and the `butler`
workflow. Key constraints: `index.html` at the zip root, relative paths only, case-sensitive file names, at most
1000 files, and no `SharedArrayBuffer`.

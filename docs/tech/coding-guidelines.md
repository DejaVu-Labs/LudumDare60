# Coding guidelines

These rules target a single-threaded, AOT-compiled .NET runtime that runs inside a browser tab. The analyzers
check style and AOT safety. The rules below cover what they cannot check.

## Architecture

- **Pure core, thin platform layer.** Simulation, game rules and math live in plain `net10.0` class libraries
  with no browser dependency, so they are unit-tested on the desktop CLR. The WebAssembly project only wires
  input, timing, rendering and audio to the browser.
- **One renderer abstraction, two backends.** Game code submits draw data to a backend-neutral renderer. WebGPU
  is the primary backend. WebGL2 is the fallback, picked at startup when `navigator.gpu` is missing or
  `requestAdapter()` fails. Both backends consume the same command stream.
- **JS owns the frame loop.** `requestAnimationFrame` calls one `[JSExport]` `Frame(double timestampMs)` per
  frame. Use a fixed simulation timestep with an accumulator, and interpolate for rendering.
- **Data-oriented hot paths.** Use arrays of blittable structs or struct-of-arrays, and index by integer handles
  instead of object references. Class hierarchies are for cold code: menus, setup, tooling.

## Memory

- Per-frame code allocates nothing. Watch for these sources: LINQ, lambdas that capture variables,
  `foreach` over interfaces (it boxes the enumerator), `params`, string concatenation and interpolation,
  `ToString()`, `async` state machines, and `List<T>` growth.
- Preallocate at load time. Use `ArrayPool<T>.Shared` or own pools for transient buffers. Reach for `Span<T>`,
  `stackalloc` (small, bounded sizes) and `ref struct` before you `new` anything.
- Prefer `readonly struct` / `readonly record struct` for small values, and pass large structs with `in` or
  `ref readonly`.
- Seal classes (CA1852 enforces this). Avoid virtual dispatch in inner loops.
- Format strings into a reusable `char` buffer (`TryFormat`, `DefaultInterpolatedStringHandler` into a span)
  and only cross into JS when the text changes.

## CPU and cache

- Iterate linearly over contiguous memory. Keep hot fields together and move cold data into separate arrays.
- Use `System.Numerics` (`Vector2`, `Vector4`, `Matrix4x4`) and `Vector128<T>`. They lower to Wasm SIMD128.
- Avoid `Dictionary` lookups in inner loops. Resolve handles once, outside the loop.
- Branch-light code beats clever code. Measure before you micro-optimize.

## GPU

- Vertex, instance and uniform data are `[StructLayout(LayoutKind.Sequential, Pack = ...)]` structs that mirror
  the WGSL/GLSL layout exactly. Mind WGSL alignment: `vec3<f32>` aligns to 16 bytes, so pad explicitly. Check
  sizes with `Unsafe.SizeOf<T>()` in a unit test.
- Upload once per frame per buffer (`writeBuffer` / `bufferSubData`) from one contiguous span.
- Batch by pipeline and texture. Use texture atlases and instancing. Aim for a two-digit draw call count per frame.
- Create pipelines, bind group layouts and samplers at load time. Never create GPU objects per frame.
- Keep shader sources in `wwwroot/shaders/` (`.wgsl` for WebGPU, `.glsl` for WebGL2). Keep the two variants
  next to each other and in sync.

## JS interop

- Use `[JSImport]` / `[JSExport]` from `System.Runtime.InteropServices.JavaScript` only. They are
  source-generated and AOT-safe. Do not use `IJSRuntime` or anything reflection-based.
- **Each interop call has a fixed cost, so cut the number of calls.** Encode render, audio and DOM commands into
  a preallocated byte buffer and pass it once per frame as a `Span<byte>`
  (`[JSMarshalAs<JSType.MemoryView>]`). A span view is valid only during that call: copy the data out or consume
  it immediately in JS, and never keep a reference to it.
- Pass primitives (`int`, `double`, `bool`) and spans. Avoid strings, `JSObject` and `Task` on the hot path.
  `JSObject` handles must be disposed. Cache them for long-lived objects (canvas, device, context).
- Push input into C# in batches: JS collects events into a ring buffer, and C# reads it once per frame.
- Keep JS modules small, pure ES modules (`export function ...`) with `// @ts-check` and JSDoc types, so Node
  can unit-test them.

## Async and threading

- The browser build has one thread. Never call `.Wait()`, `.Result` or `Thread.Sleep`: they deadlock or freeze
  the tab.
- `async` is for loading (`fetch`, shader and texture setup), never for per-frame logic.

## Assets

- File names are lowercase `kebab-case`, because itch.io paths are case-sensitive.
- Keep the shipped file count low (itch.io limit: 1000). Pack sprites into atlases and audio into a few files.
- Load everything with relative URLs.

## Tests

- Every bug fix gets a regression test first.
- Test simulation determinism: the same seed and inputs must produce the same state.
- Test struct layouts (`Unsafe.SizeOf<T>()`, field offsets) for every GPU-facing struct.
- Test interop command encoding byte by byte from C#. Test decoding with the same bytes in `tests/js/`.

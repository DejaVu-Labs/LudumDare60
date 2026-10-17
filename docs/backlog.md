# Backlog

Work that is agreed or deferred but not done yet. When an item ships, move its design into `docs/tech/` and
delete it here.

## Input bindings

Map keys, pointer buttons and gamepad inputs to game actions. Gamepad bindings rely on the W3C Standard Gamepad
layout (`standard` flag).

- `InputState` has no gamepads yet: add buttons (down/pressed/released, analog value) and axes per gamepad index,
  routed to fixed steps like keys (see [architecture.md](tech/architecture.md#input-state)).
- Multi-touch: `InputState` merges pointers into one; track pointers by id if a mechanic needs two fingers.

## Platform commands (game to platform)

A command channel from the game to the platform, batched like render commands.

- Pointer lock request, with `unadjustedMovement: true` when supported. Browsers allow it only inside a user
  gesture, so the browser platform must issue it from an input handler.
- Probably: cursor visibility, fullscreen.

## Text input

A `TextInput` event kind for chat and name entry (`code` is not text). The UTF-16 code unit goes in `Code`, and a
string becomes several events. The event layout does not change.

## Low-latency rendering

Matters for anything aim-based. Camera look from the latest pointer motion in `Update`/`PrepareRender`, not from
interpolated simulation state. Try the `desynchronized` canvas context hint and measure present latency.

## Asset parsers

`IAssetLoader<T>` implementations for the types the game needs. The asset manager keeps every file as bytes in WASM
memory, so parsing happens in C#.

- Aseprite animations: parse `.aseprite` directly (pixels are zlib-compressed, `ZLibStream` works in WASM). No export
  to PNG + JSON, because one asset is one file.
- Fonts (TTF): glyph outlines or a baked atlas, depending on the text renderer.
- Sounds: decoding in C# or a WebAudio path; decide together with the audio backend.
- JSON configs: `System.Text.Json` with `[JsonSerializable]` source generation, no reflection.

## Asset manager follow-ups

Deferred when the asset manager shipped (see [docs/tech/architecture.md](tech/architecture.md#assets)):

- Check once whether itch.io keeps `html/<id>/` across `butler push`es to one channel (see
  [build-and-deploy.md](tech/build-and-deploy.md#asset-caching)).
- Per-file content hashes instead of the per-build `?v=`: fingerprinted names plus a generated name table, shipped
  as a JS module so the import map keeps it fresh. Only changed files would be downloaded again.
- Cancel reads of released assets (`AbortController`).
- A per-frame parsing budget in `AssetManager.Update`.
- A limit on parallel reads.
- Progress in bytes instead of files.
- Asset packs, to stay far below the 1000-file limit.
- Assets that depend on other assets.
- Hot reload in Debug.

## Dirt masks

Cleaning works pixel by pixel (see [concepts.md](gamedesign/concepts.md#rendering)). Deferred when the WebGL2 renderer
shipped:

- One mask per part at sprite resolution, in a second texture the sprite shader samples (`R8`), and a mask
  rectangle in `SpriteInstance` (grows past 40 bytes: change C#, `webgl2.js`, the shader and the fixture together).
- `Graphics.UpdateTexture(texture, rows)` that re-uploads only changed rows with `texSubImage2D`.
- The brush erases mask pixels with a little dither; the CPU keeps the mask and counts clean pixels exactly.

## Animation frames

`SpriteBatch.Draw` takes a source rectangle; nothing names frames yet. Add a `SpriteSheet` for PNG sheets (a grid of
equal frames) and frame and tag tables from the Aseprite parser (see [Asset parsers](#asset-parsers)), so the game
draws "frame 3 of `cylinder-spin`" instead of rectangles.

## WebGPU backend

A second `IGraphicsBackend` in the browser platform on the same command stream, instance layout and shaders
(`wwwroot/shaders/sprite.wgsl.js` next to `sprite.glsl.js`). Picked at startup when `navigator.gpu` and
`requestAdapter()` succeed, with WebGL2 as the fallback. Not needed for the Compo.

## Browser E2E smoke tests

A `tests/*.E2E` project with `Microsoft.Playwright`: the published build boots, reaches the first frame and logs
no console errors, on the WebGL2 path (and on WebGPU once it exists).

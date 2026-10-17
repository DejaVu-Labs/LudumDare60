# Backlog

Work that is agreed or deferred but not done yet. When an item ships, move its design into `docs/tech/` and
delete it here.

## Input state layer

Replace the raw span in `IGame.Update` with input state the game reads: key and button down/pressed/released,
pointer position and motion, wheel, gamepad state. The raw events it builds on are described in
[docs/tech/architecture.md](tech/architecture.md#input).

- **Hard requirement: no extra frame of latency.** The engine applies events before the `FixedUpdate` loop, not
  after it, and assigns them to fixed steps by `Time`. A press must reach the simulation step it happened in.
  Today `Update` runs after the fixed steps, so input read in `Update` reaches the simulation one frame late.
- `Overflow` and `FocusLost` release everything.
- Pressed/released edges must not be lost when a frame runs zero fixed steps, or counted twice when it runs
  several.

## Input bindings

Map keys, pointer buttons and gamepad inputs to game actions. Gamepad bindings rely on the W3C Standard Gamepad
layout (`standard` flag).

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

## Browser E2E smoke tests

A `tests/*.E2E` project with `Microsoft.Playwright`: the published build boots, reaches the first frame and logs
no console errors, on both the WebGPU and WebGL2 paths.

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
- More JSON files: follow `Orders/OrderBookLoader` (source-generated `System.Text.Json`, strict options, a
  validation pass that names every problem).

## Audio follow-ups

Deferred when the audio system shipped (see [docs/tech/architecture.md](tech/architecture.md#audio)):

- Loop points (an intro played once, then a looped section): `AudioBufferSourceNode.loopStart` and `loopEnd`.
- Streaming long music through `<audio>` and `MediaElementAudioSourceNode` if decoded tracks take too much memory.
- Effects and ducking: a filter or reverb per bus, music ducking under dialogue or phone buzzes.
- Positional (3D) sound. Pan covers the 2D screen today.
- Sample-accurate timing: schedule sounds by `RawInputEvent.Time` on `AudioContext.currentTime` instead of playing them
  when the frame's commands arrive.
- Remember the player's volume and mute between sessions (`localStorage`), together with the
  [platform commands](#platform-commands-game-to-platform).

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

## Touch loop follow-ups

Every part of the five orders drags along a line or turns as a screw, with breakaway, friction and a snap (`Parts/PartDrag`), in the
order their blocking graph allows (`Parts/PartAssembly`). Still to do:

- Tune `DragFeel` by hand with a mouse and a touchpad. A quick flick released early leaves the part short of the end,
  because it trails the pointer at most 160 px/s; check whether that reads as weight or as a missed drag.
- Arc paths for swing (loading gate; the grip panel lifts on a short line today).
- A friction loop sound while a loose part moves, its pitch rising towards the snap, and a small screen shake on the
  snap.
- Parts out on the mat could turn as they are carried, and lie at an angle; today they keep the angle of their rail.
- A screw turns around its middle, but the shader snaps the pivot of a turned sprite to a whole pixel and the
  corner of an unturned one, so a screw shifts half a pixel when it starts to turn. Draw screws with whole-pixel
  middles, or snap both the same way.
- Show the hand how to unscrew: a faint circular arrow around a screw while it is held, for players who drag
  straight.
- An oiled part could show a wet sheen for a moment; today it only glints.
- Replace the code-generated frame, pin, cylinder and click with the real sprites and recordings.

## Shop loop follow-ups

Customers come in, hand over the revolver and take it back (`Customers/Shop`, see
[core-loop.md](gamedesign/core-loop.md#what-is-built)). Still to do:

- The hammer's drag follows the chord of its arc (`Bench/ActionTest`); try a real arc path (see the touch loop
  follow-ups) once it exists, and tune how hard the mainspring holds the hammer down.
- Finds: the find pops out of its part, opens over the bench, flies into the finds box; the customer's surprised
  face (`Expression.Surprised` exists, nothing sets it yet).
- The phone message after the customer leaves, and the counter bell that calls the next customer at once.
- The shop's end once the orders run out; today it just stays empty (`ShopPhase.Closed`).
- Customer sheets for the other four looks (`cardigan`, `work-jacket`, `headphones`, `raincoat`): until they exist
  those customers are code placeholders. A blink, a walk instead of a slide, the door opening.

## Cleaning follow-ups

Scrubbing works on order 1's cylinder (`Cleaning/DirtLayer`, see [concepts.md](gamedesign/concepts.md#rendering)).
Still to do:

- Record a wire brush on rusty steel (and on clean steel) to replace the synthesized scrub loop
  (`PlaceholderSounds.CreateScrubLoop`); keep it a seamless loop that sounds steady at any speed, since the game
  drives its volume and pitch.
- Hide the system cursor while the brush is in the hand (needs the cursor visibility command from
  [Platform commands](#platform-commands-game-to-platform)).
- A dirty part brought to its rail could bounce back a little instead of only not sticking (core-loop step 4).
- Several dirt layers per order, from the order data, and dirt drawn as real art (an Aseprite layer per part).
- Oiling (order 4).

## Animation frames

`SpriteBatch.Draw` takes a source rectangle; nothing names frames yet. Add a `SpriteSheet` for PNG sheets (a grid of
equal frames) and frame and tag tables from the Aseprite parser (see [Asset parsers](#asset-parsers)), so the game
draws "frame 3 of `cylinder-spin`" instead of rectangles.

## WebGPU backend

A second `IGraphicsBackend` in the browser platform on the same command stream, instance layout and shaders
(`wwwroot/shaders/sprite.wgsl.js` next to `sprite.glsl.js`). Picked at startup when `navigator.gpu` and
`requestAdapter()` succeed, with WebGL2 as the fallback. Not needed for the Compo.

## FTUE support

Engine and host work for the first-time experience (see [ftue.md](gamedesign/ftue.md)):

- Poster frame in the host page: the title screen image and a loading bar instead of the `Loading...` text,
  removed when the first frame is rendered.
- Cursor sprites per verb, drawn by the game. Needs the cursor visibility command from
  [Platform commands](#platform-commands-game-to-platform) to hide the system cursor over the canvas.
- Idle timer and hint state (glow, wiggle, ghost hand) in the order logic, tested without a browser.

## Browser E2E smoke tests

A `tests/*.E2E` project with `Microsoft.Playwright`: the published build boots, reaches the first frame and logs
no console errors, on the WebGL2 path (and on WebGPU once it exists).

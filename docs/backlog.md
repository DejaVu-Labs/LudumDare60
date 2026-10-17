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

## Browser E2E smoke tests

A `tests/*.E2E` project with `Microsoft.Playwright`: the published build boots, reaches the first frame and logs
no console errors, on both the WebGPU and WebGL2 paths.

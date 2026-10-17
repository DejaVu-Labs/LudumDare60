// @ts-check

/**
 * Raw input of the browser platform. DOM listeners write events into a ring buffer; once per frame C# drains it into
 * a span in WASM memory (`drain`, bound with `[JSImport]`). Events use the 24-byte little-endian layout of
 * `RawInputEvent` in `LudumDare60.Engine.Platform`. Handlers are created once and write through a `DataView`, so an
 * event allocates nothing on our side.
 */

/** Size of one event in bytes. Matches `RawInputEvent.Size`. */
export const EVENT_SIZE = 24;
const EVENT_WORDS = EVENT_SIZE / 4;
const TIME_OFFSET = 8;

/** Matches `RawInputEventKind`. */
export const Kind = Object.freeze({
  Overflow: 1,
  FocusLost: 2,
  FocusGained: 3,
  Resize: 4,
  KeyDown: 5,
  KeyUp: 6,
  PointerDown: 7,
  PointerUp: 8,
  PointerMove: 9,
  PointerMotion: 10,
  PointerCancel: 11,
  PointerLockChanged: 12,
  Wheel: 13,
  GamepadConnected: 14,
  GamepadDisconnected: 15,
  GamepadButton: 16,
  GamepadAxis: 17,
});

/** Matches `PointerType`. */
export const PointerType = Object.freeze({ Mouse: 0, Touch: 1, Pen: 2 });

/** Ring buffer capacity, in events. */
export const QUEUE_CAPACITY = 4096;

/** A gamepad button or axis reports a change only when its value moves by more than this. */
export const GAMEPAD_THRESHOLD = 1 / 256;

/** Pixels per wheel line (`WheelEvent.DOM_DELTA_LINE`). */
export const WHEEL_LINE_PIXELS = 16;

/** `MouseEvent.buttons` bit index to `MouseEvent.button` number: bit 1 is the right button, bit 2 the middle one. */
const BUTTON_OF_BIT = [0, 2, 1, 3, 4, 5];

/**
 * `KeyboardEvent.code` to `Key` (USB HID usage ID).
 * @type {ReadonlyMap<string, number>}
 */
export const KEY_CODES = (() => {
  /** @type {Map<string, number>} */
  const codes = new Map();
  for (let i = 0; i < 26; i++) {
    codes.set(`Key${String.fromCharCode(65 + i)}`, 0x04 + i);
  }
  for (let i = 1; i <= 9; i++) {
    codes.set(`Digit${i}`, 0x1e + i - 1);
    codes.set(`Numpad${i}`, 0x59 + i - 1);
  }
  codes.set('Digit0', 0x27);
  codes.set('Numpad0', 0x62);
  for (let i = 1; i <= 12; i++) {
    codes.set(`F${i}`, 0x3a + i - 1);
  }
  /** @type {[string, number][]} */
  const named = [
    ['Enter', 0x28], ['Escape', 0x29], ['Backspace', 0x2a], ['Tab', 0x2b], ['Space', 0x2c], ['Minus', 0x2d],
    ['Equal', 0x2e], ['BracketLeft', 0x2f], ['BracketRight', 0x30], ['Backslash', 0x31], ['Semicolon', 0x33],
    ['Quote', 0x34], ['Backquote', 0x35], ['Comma', 0x36], ['Period', 0x37], ['Slash', 0x38], ['CapsLock', 0x39],
    ['Insert', 0x49], ['Home', 0x4a], ['PageUp', 0x4b], ['Delete', 0x4c], ['End', 0x4d], ['PageDown', 0x4e],
    ['ArrowRight', 0x4f], ['ArrowLeft', 0x50], ['ArrowDown', 0x51], ['ArrowUp', 0x52], ['NumLock', 0x53],
    ['NumpadDivide', 0x54], ['NumpadMultiply', 0x55], ['NumpadSubtract', 0x56], ['NumpadAdd', 0x57],
    ['NumpadEnter', 0x58], ['NumpadDecimal', 0x63], ['ControlLeft', 0xe0], ['ShiftLeft', 0xe1], ['AltLeft', 0xe2],
    ['MetaLeft', 0xe3], ['ControlRight', 0xe4], ['ShiftRight', 0xe5], ['AltRight', 0xe6], ['MetaRight', 0xe7],
  ];
  for (const [code, key] of named) {
    codes.set(code, key);
  }
  return codes;
})();

const F1 = 0x3a;
const F12 = 0x45;

/**
 * A preallocated block of events, viewed three ways.
 * @typedef {object} EventBuffer
 * @property {number} capacity Events that fit.
 * @property {Uint8Array} bytes
 * @property {Uint32Array} words
 * @property {DataView} view
 */

/**
 * @param {number} capacity In events.
 * @returns {EventBuffer}
 */
export function createEventBuffer(capacity) {
  const buffer = new ArrayBuffer(capacity * EVENT_SIZE);
  return {
    capacity,
    bytes: new Uint8Array(buffer),
    words: new Uint32Array(buffer),
    view: new DataView(buffer),
  };
}

/**
 * @typedef {object} InputQueue
 * @property {(kind: number, detail: number, code: number, id: number, timeMs: number, x: number, y: number, z: number) => void} push
 *   Appends an event. `timeMs` is on the `performance.now()` timeline. When the queue is full the event is dropped
 *   and an `Overflow` event takes the place of the dropped ones.
 * @property {(target: EventBuffer, frameTimeMs: number) => number} drain Moves up to `target.capacity` events into
 *   `target`, oldest first, with `Time` rewritten as seconds relative to `frameTimeMs` (never positive). Returns the
 *   number of events written. Events that do not fit stay queued for the next drain.
 * @property {() => number} count Events waiting, not counting a pending `Overflow`.
 */

/**
 * Creates a ring buffer of raw input events.
 *
 * @param {number} capacity In events.
 * @returns {InputQueue}
 */
export function createInputQueue(capacity) {
  const ring = createEventBuffer(capacity);
  const times = new Float64Array(capacity);
  let head = 0;
  let size = 0;
  let dropped = 0;
  let droppedTimeMs = 0;

  /**
   * @param {number} kind @param {number} detail @param {number} code @param {number} id @param {number} timeMs
   * @param {number} x @param {number} y @param {number} z
   */
  const write = (kind, detail, code, id, timeMs, x, y, z) => {
    const slot = (head + size) % capacity;
    const offset = slot * EVENT_SIZE;
    const view = ring.view;
    view.setUint8(offset, kind);
    view.setUint8(offset + 1, detail);
    view.setUint16(offset + 2, code, true);
    view.setInt32(offset + 4, id, true);
    view.setFloat32(offset + TIME_OFFSET, 0, true);
    view.setFloat32(offset + 12, x, true);
    view.setFloat32(offset + 16, y, true);
    view.setFloat32(offset + 20, z, true);
    times[slot] = timeMs;
    size++;
  };

  /** Writes the pending `Overflow` event: it takes the place of the dropped events, after those queued before them. */
  const writeOverflow = () => {
    write(Kind.Overflow, 0, 0, dropped, droppedTimeMs, 0, 0, 0);
    dropped = 0;
  };

  return {
    push(kind, detail, code, id, timeMs, x, y, z) {
      if (dropped > 0) {
        // The Overflow event and this one both need a slot; otherwise this one joins the dropped run.
        if (capacity - size < 2) {
          dropped++;
          return;
        }
        writeOverflow();
      }
      if (size === capacity) {
        dropped = 1;
        droppedTimeMs = timeMs;
        return;
      }
      write(kind, detail, code, id, timeMs, x, y, z);
    },

    drain(target, frameTimeMs) {
      let written = 0;
      // Two rounds: once the first one empties the ring, a pending Overflow goes in behind the events before it.
      for (let round = 0; round < 2 && written < target.capacity; round++) {
        if (round === 1 && dropped > 0) {
          writeOverflow();
        }
        while (size > 0 && written < target.capacity) {
          const source = head * EVENT_WORDS;
          const destination = written * EVENT_WORDS;
          for (let w = 0; w < EVENT_WORDS; w++) {
            target.words[destination + w] = ring.words[source + w];
          }
          const time = Math.min(0, (times[head] - frameTimeMs) / 1000);
          target.view.setFloat32(written * EVENT_SIZE + TIME_OFFSET, time, true);
          head = (head + 1) % capacity;
          size--;
          written++;
        }
      }
      return written;
    },

    count() {
      return size;
    },
  };
}

/**
 * The subset of `EventTarget` the input code uses, so tests can pass fakes.
 * @typedef {object} Target
 * @property {(type: string, listener: (event: any) => void, options?: AddEventListenerOptions) => void} addEventListener
 * @property {(type: string, listener: (event: any) => void, options?: EventListenerOptions) => void} removeEventListener
 */

/**
 * @typedef {Target & {
 *   clientWidth: number,
 *   clientHeight: number,
 *   style: { touchAction: string },
 *   setPointerCapture?: (pointerId: number) => void,
 * }} Canvas
 */

/**
 * @typedef {object} GamepadLike
 * @property {number} index
 * @property {string} mapping
 * @property {number} timestamp
 * @property {ReadonlyArray<number>} axes
 * @property {ReadonlyArray<{ pressed: boolean, value: number }>} buttons
 */

/**
 * @typedef {object} InputEnvironment
 * @property {Canvas} canvas Pointer, wheel and context menu events.
 * @property {Target & { devicePixelRatio: number }} window Keyboard and focus events.
 * @property {Target & { visibilityState: string, pointerLockElement: unknown, hasFocus: () => boolean }} document
 * @property {{ getGamepads?: () => ReadonlyArray<GamepadLike | null> }} navigator
 * @property {() => number} now Usually `performance.now`.
 * @property {(new (callback: () => void) => { observe(target: any): void, disconnect(): void }) | undefined} ResizeObserver
 */

/**
 * @typedef {object} Input
 * @property {(target: EventBuffer, frameTimeMs: number) => number} drain Polls gamepads and the pixel ratio, then
 *   drains the queue into `target`.
 * @property {() => void} detach Removes every listener.
 */

/**
 * Listens to the browser and queues raw input events. Sends `Resize` and the current focus state first.
 *
 * @param {InputEnvironment} env
 * @param {number} [capacity]
 * @returns {Input}
 */
export function createInput(env, capacity = QUEUE_CAPACITY) {
  const { canvas, window, document, navigator, now } = env;
  const queue = createInputQueue(capacity);

  /** @type {Map<number, number>} Pointer id to `MouseEvent.buttons` as last reported. */
  const pointerButtons = new Map();
  let locked = false;
  let focused = document.hasFocus() && document.visibilityState === 'visible';
  let width = -1;
  let height = -1;
  let pixelRatio = -1;
  /** @type {({ axes: Float32Array, values: Float32Array, pressed: Uint8Array } | undefined)[]} */
  const pads = [];
  let gamepadsAvailable = typeof navigator.getGamepads === 'function';

  /** @param {number} timeMs */
  const pushSize = (timeMs) => {
    const w = canvas.clientWidth;
    const h = canvas.clientHeight;
    const ratio = window.devicePixelRatio || 1;
    if (w === width && h === height && ratio === pixelRatio) {
      return;
    }
    width = w;
    height = h;
    pixelRatio = ratio;
    queue.push(Kind.Resize, 0, 0, 0, timeMs, w, h, ratio);
  };

  /** @param {boolean} value @param {number} timeMs */
  const setFocused = (value, timeMs) => {
    if (value === focused) {
      return;
    }
    focused = value;
    queue.push(value ? Kind.FocusGained : Kind.FocusLost, 0, 0, 0, timeMs, 0, 0, 0);
  };

  /** @param {KeyboardEvent} event @returns {number} The `Key`, or -1 when the key is not in the table. */
  const handleKey = (event) => {
    const key = KEY_CODES.get(event.code);
    if (key === undefined) {
      return -1;
    }
    // Keep browser shortcuts (reload, dev tools, copy...) working; everything else belongs to the game.
    if (!(key >= F1 && key <= F12) && !event.ctrlKey && !event.metaKey) {
      event.preventDefault();
    }
    return key;
  };

  /** @param {KeyboardEvent} event */
  const onKeyDown = (event) => {
    const key = handleKey(event);
    if (key >= 0 && !event.repeat) {
      queue.push(Kind.KeyDown, 0, key, 0, event.timeStamp, 0, 0, 0);
    }
  };

  /** @param {KeyboardEvent} event */
  const onKeyUp = (event) => {
    const key = handleKey(event);
    if (key >= 0) {
      queue.push(Kind.KeyUp, 0, key, 0, event.timeStamp, 0, 0, 0);
    }
  };

  /** @param {string} type */
  const pointerTypeOf = (type) => (type === 'touch' ? PointerType.Touch : type === 'pen' ? PointerType.Pen : PointerType.Mouse);

  /**
   * Turns changes of `buttons` into one down or up event per button: a second mouse button pressed while another is
   * held arrives as `pointermove`, not `pointerdown`.
   * @param {PointerEvent} event
   * @param {number} type `PointerType`.
   * @returns {boolean} Whether any button changed.
   */
  const diffButtons = (event, type) => {
    const previous = pointerButtons.get(event.pointerId) ?? 0;
    const current = event.buttons;
    const changed = previous ^ current;
    if (changed === 0) {
      return false;
    }
    for (let bit = 0; bit < BUTTON_OF_BIT.length; bit++) {
      const mask = 1 << bit;
      if ((changed & mask) !== 0) {
        const kind = (current & mask) !== 0 ? Kind.PointerDown : Kind.PointerUp;
        queue.push(kind, BUTTON_OF_BIT[bit], type, event.pointerId, event.timeStamp, event.offsetX, event.offsetY, 0);
      }
    }
    if (current === 0) {
      pointerButtons.delete(event.pointerId);
    } else {
      pointerButtons.set(event.pointerId, current);
    }
    return true;
  };

  /** @param {PointerEvent} event */
  const onPointerDown = (event) => {
    const type = pointerTypeOf(event.pointerType);
    if (!diffButtons(event, type) && event.button >= 0) {
      queue.push(Kind.PointerDown, event.button, type, event.pointerId, event.timeStamp, event.offsetX, event.offsetY, 0);
    }
    try {
      canvas.setPointerCapture?.(event.pointerId);
    } catch {
      // The pointer is already gone; its pointerup or pointercancel follows.
    }
  };

  /** @param {PointerEvent} event */
  const onPointerMove = (event) => {
    const type = pointerTypeOf(event.pointerType);
    if (locked && type === PointerType.Mouse) {
      queue.push(Kind.PointerMotion, 0, type, event.pointerId, event.timeStamp, event.movementX, event.movementY, 0);
    } else {
      queue.push(Kind.PointerMove, 0, type, event.pointerId, event.timeStamp, event.offsetX, event.offsetY, 0);
    }
    diffButtons(event, type);
  };

  /** @param {PointerEvent} event */
  const onPointerUp = (event) => {
    const type = pointerTypeOf(event.pointerType);
    if (!diffButtons(event, type) && event.button >= 0) {
      queue.push(Kind.PointerUp, event.button, type, event.pointerId, event.timeStamp, event.offsetX, event.offsetY, 0);
    }
  };

  /** @param {PointerEvent} event */
  const onPointerCancel = (event) => {
    pointerButtons.delete(event.pointerId);
    queue.push(Kind.PointerCancel, 0, pointerTypeOf(event.pointerType), event.pointerId, event.timeStamp, 0, 0, 0);
  };

  /** @param {WheelEvent} event */
  const onWheel = (event) => {
    event.preventDefault();
    const scale = event.deltaMode === 1 ? WHEEL_LINE_PIXELS : event.deltaMode === 2 ? canvas.clientHeight : 1;
    queue.push(Kind.Wheel, 0, 0, 0, event.timeStamp, event.deltaX * scale, event.deltaY * scale, event.deltaZ * scale);
  };

  /** @param {Event} event */
  const onContextMenu = (event) => event.preventDefault();

  /** @param {Event} event */
  const onPointerLockChange = (event) => {
    const value = document.pointerLockElement === canvas;
    if (value !== locked) {
      locked = value;
      queue.push(Kind.PointerLockChanged, value ? 1 : 0, 0, 0, event.timeStamp, 0, 0, 0);
    }
  };

  /** @param {Event} event */
  const onBlur = (event) => setFocused(false, event.timeStamp);
  /** @param {Event} event */
  const onFocus = (event) => setFocused(document.visibilityState === 'visible', event.timeStamp);
  /** @param {Event} event */
  const onVisibilityChange = (event) => setFocused(document.visibilityState === 'visible' && document.hasFocus(), event.timeStamp);

  const onResize = () => pushSize(now());

  /** @param {number} frameTimeMs */
  const pollGamepads = (frameTimeMs) => {
    if (!gamepadsAvailable) {
      return;
    }
    /** @type {ReadonlyArray<GamepadLike | null>} */
    let current;
    try {
      current = navigator.getGamepads?.() ?? [];
    } catch {
      // Blocked by a permissions policy (an iframe without allow="gamepad"): stop asking.
      gamepadsAvailable = false;
      return;
    }

    for (let index = 0; index < Math.max(current.length, pads.length); index++) {
      const pad = current[index] ?? null;
      let state = pads[index];
      if (pad === null) {
        if (state !== undefined) {
          pads[index] = undefined;
          queue.push(Kind.GamepadDisconnected, 0, 0, index, frameTimeMs, 0, 0, 0);
        }
        continue;
      }

      const timeMs = pad.timestamp > 0 ? pad.timestamp : frameTimeMs;
      if (state === undefined) {
        state = {
          axes: new Float32Array(pad.axes.length),
          values: new Float32Array(pad.buttons.length),
          pressed: new Uint8Array(pad.buttons.length),
        };
        pads[index] = state;
        queue.push(Kind.GamepadConnected, pad.mapping === 'standard' ? 1 : 0, 0, index, timeMs, 0, 0, 0);
      }

      const buttonCount = Math.min(pad.buttons.length, state.values.length);
      for (let b = 0; b < buttonCount; b++) {
        const button = pad.buttons[b];
        const pressed = button.pressed ? 1 : 0;
        if (pressed !== state.pressed[b] || Math.abs(button.value - state.values[b]) > GAMEPAD_THRESHOLD) {
          state.pressed[b] = pressed;
          state.values[b] = button.value;
          queue.push(Kind.GamepadButton, pressed, b, index, timeMs, button.value, 0, 0);
        }
      }

      const axisCount = Math.min(pad.axes.length, state.axes.length);
      for (let a = 0; a < axisCount; a++) {
        const value = pad.axes[a];
        if (Math.abs(value - state.axes[a]) > GAMEPAD_THRESHOLD) {
          state.axes[a] = value;
          queue.push(Kind.GamepadAxis, 0, a, index, timeMs, value, 0, 0);
        }
      }
    }
  };

  /** @type {[Target, string, (event: any) => void, AddEventListenerOptions | undefined][]} */
  const listeners = [
    [window, 'keydown', onKeyDown, undefined],
    [window, 'keyup', onKeyUp, undefined],
    [window, 'blur', onBlur, undefined],
    [window, 'focus', onFocus, undefined],
    [document, 'visibilitychange', onVisibilityChange, undefined],
    [document, 'pointerlockchange', onPointerLockChange, undefined],
    [canvas, 'pointerdown', onPointerDown, undefined],
    [canvas, 'pointermove', onPointerMove, undefined],
    [canvas, 'pointerup', onPointerUp, undefined],
    [canvas, 'pointercancel', onPointerCancel, undefined],
    [canvas, 'wheel', onWheel, { passive: false }],
    [canvas, 'contextmenu', onContextMenu, undefined],
  ];
  for (const [target, type, listener, options] of listeners) {
    target.addEventListener(type, listener, options);
  }
  // Touch gestures (scroll, pinch zoom) would otherwise cancel the pointer.
  canvas.style.touchAction = 'none';

  const resizeObserver = env.ResizeObserver === undefined ? undefined : new env.ResizeObserver(onResize);
  resizeObserver?.observe(canvas);

  const startMs = now();
  pushSize(startMs);
  queue.push(focused ? Kind.FocusGained : Kind.FocusLost, 0, 0, 0, startMs, 0, 0, 0);

  return {
    drain(target, frameTimeMs) {
      // The pixel ratio changes without a resize when the window moves to another monitor. Reading the canvas size
      // only then avoids a layout query every frame.
      if ((window.devicePixelRatio || 1) !== pixelRatio) {
        pushSize(frameTimeMs);
      }
      pollGamepads(frameTimeMs);
      return queue.drain(target, frameTimeMs);
    },

    detach() {
      for (const [target, type, listener, options] of listeners) {
        target.removeEventListener(type, listener, options);
      }
      resizeObserver?.disconnect();
    },
  };
}

/** @type {Input | undefined} */
let active;
/** @type {EventBuffer | undefined} */
let staging;

/**
 * Starts listening to the canvas with id `canvasId` and to the window. Bound from C# with `[JSImport]`.
 *
 * @param {string} canvasId
 */
export function attach(canvasId) {
  if (active !== undefined) {
    throw new Error('Input is already attached.');
  }
  const canvas = globalThis.document.getElementById(canvasId);
  if (!(canvas instanceof HTMLCanvasElement)) {
    throw new Error(`No <canvas id="${canvasId}"> in the page.`);
  }
  active = createInput({
    canvas,
    window: globalThis.window,
    document: globalThis.document,
    navigator: globalThis.navigator,
    now: () => globalThis.performance.now(),
    ResizeObserver: globalThis.ResizeObserver,
  });
}

/** Stops listening. Bound from C# with `[JSImport]`. */
export function detach() {
  active?.detach();
  active = undefined;
}

/**
 * The span view that `[JSImport]` passes for a `Span<byte>`; valid only during the call.
 * @typedef {object} MemoryView
 * @property {number} byteLength
 * @property {(source: Uint8Array, targetOffset?: number) => void} set
 */

/**
 * Copies the events queued since the last call into `destination`. Bound from C# with `[JSImport]`.
 *
 * @param {MemoryView} destination Room for `byteLength / EVENT_SIZE` events.
 * @param {number} frameTimeMs Timestamp of the current animation frame.
 * @returns {number} Number of events written.
 */
export function drain(destination, frameTimeMs) {
  if (active === undefined) {
    return 0;
  }
  const capacity = Math.floor(destination.byteLength / EVENT_SIZE);
  if (staging === undefined || staging.capacity !== capacity) {
    staging = createEventBuffer(capacity);
  }
  const count = active.drain(staging, frameTimeMs);
  if (count > 0) {
    destination.set(staging.bytes.subarray(0, count * EVENT_SIZE), 0);
  }
  return count;
}

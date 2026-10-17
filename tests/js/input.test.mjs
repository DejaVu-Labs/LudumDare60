// @ts-check
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

import {
  EVENT_SIZE,
  GAMEPAD_THRESHOLD,
  KEY_CODES,
  Kind,
  PointerType,
  WHEEL_LINE_PIXELS,
  createEventBuffer,
  createInput,
  createInputQueue,
  drain,
} from '../../src/engine/platforms/LudumDare60.Engine.Platform.Browser/wwwroot/input.js';

/** Shared with RawInputEventTests.cs. */
const fixture = JSON.parse(readFileSync(new URL('../fixtures/raw-input-events.json', import.meta.url), 'utf8'));

/**
 * @typedef {{ kind: number, detail: number, code: number, id: number, time: number, x: number, y: number, z: number }} Decoded
 */

/**
 * @param {import('../../src/engine/platforms/LudumDare60.Engine.Platform.Browser/wwwroot/input.js').EventBuffer} buffer
 * @param {number} count
 * @returns {Decoded[]}
 */
function decode(buffer, count) {
  const view = buffer.view;
  /** @type {Decoded[]} */
  const events = [];
  for (let i = 0; i < count; i++) {
    const o = i * EVENT_SIZE;
    events.push({
      kind: view.getUint8(o),
      detail: view.getUint8(o + 1),
      code: view.getUint16(o + 2, true),
      id: view.getInt32(o + 4, true),
      time: view.getFloat32(o + 8, true),
      x: view.getFloat32(o + 12, true),
      y: view.getFloat32(o + 16, true),
      z: view.getFloat32(o + 20, true),
    });
  }
  return events;
}

/**
 * @param {ReturnType<typeof createInputQueue>} queue
 * @param {number} id Stored in the event's `Id` so tests can tell events apart.
 * @param {number} [timeMs]
 */
function pushKey(queue, id, timeMs = 0) {
  queue.push(Kind.KeyDown, 0, KEY_CODES.get('KeyA') ?? 0, id, timeMs, 0, 0, 0);
}

/**
 * @param {ReturnType<typeof createInputQueue>} queue
 * @param {number} capacity
 * @param {number} [frameTimeMs]
 */
function drainQueue(queue, capacity, frameTimeMs = 0) {
  const target = createEventBuffer(capacity);
  return decode(target, queue.drain(target, frameTimeMs));
}

// --- Wire format ---------------------------------------------------------------------------------------------------

test('drain_FixtureEvents_WritesFixtureBytes', () => {
  const queue = createInputQueue(16);
  for (const e of fixture.events) {
    queue.push(e.kind, e.detail, e.code, e.id, e.timeMs, e.x, e.y, e.z);
  }
  const target = createEventBuffer(16);

  const count = queue.drain(target, fixture.frameTimeMs);

  assert.equal(Buffer.from(target.bytes.subarray(0, count * EVENT_SIZE)).toString('hex'), fixture.hex);
});

test('KEY_CODES_MatchesFixtureTable', () => {
  assert.deepEqual(Object.fromEntries(KEY_CODES), fixture.keys);
});

test('KEY_CODES_UsesHidUsageIds', () => {
  assert.equal(KEY_CODES.get('KeyA'), 0x04);
  assert.equal(KEY_CODES.get('KeyZ'), 0x1d);
  assert.equal(KEY_CODES.get('Digit1'), 0x1e);
  assert.equal(KEY_CODES.get('Digit0'), 0x27);
  assert.equal(KEY_CODES.get('Space'), 0x2c);
  assert.equal(KEY_CODES.get('F12'), 0x45);
  assert.equal(KEY_CODES.get('ArrowUp'), 0x52);
  assert.equal(KEY_CODES.get('Numpad0'), 0x62);
  assert.equal(KEY_CODES.get('MetaRight'), 0xe7);
});

// --- Queue ---------------------------------------------------------------------------------------------------------

test('drain_TimesAreSecondsRelativeToFrame', () => {
  const queue = createInputQueue(4);
  pushKey(queue, 1, 990);
  pushKey(queue, 2, 1000);

  const events = drainQueue(queue, 4, 1000);

  assert.deepEqual(events.map((e) => e.time), [Math.fround(-0.01), 0]);
});

test('drain_EventAfterFrameTimestamp_ClampsTimeToZero', () => {
  const queue = createInputQueue(4);
  pushKey(queue, 1, 1005);

  assert.equal(drainQueue(queue, 4, 1000)[0].time, 0);
});

test('drain_KeepsArrivalOrderAndEmptiesQueue', () => {
  const queue = createInputQueue(4);
  pushKey(queue, 1);
  pushKey(queue, 2);
  pushKey(queue, 3);

  assert.deepEqual(drainQueue(queue, 4).map((e) => e.id), [1, 2, 3]);
  assert.equal(queue.count(), 0);
  assert.deepEqual(drainQueue(queue, 4), []);
});

test('drain_TargetTooSmall_KeepsRestForNextDrain', () => {
  const queue = createInputQueue(8);
  for (let i = 1; i <= 5; i++) {
    pushKey(queue, i);
  }

  assert.deepEqual(drainQueue(queue, 2).map((e) => e.id), [1, 2]);
  assert.deepEqual(drainQueue(queue, 2).map((e) => e.id), [3, 4]);
  assert.deepEqual(drainQueue(queue, 2).map((e) => e.id), [5]);
});

test('drain_RingWrapsAround_KeepsOrder', () => {
  const queue = createInputQueue(4);
  for (let i = 1; i <= 3; i++) {
    pushKey(queue, i);
  }
  drainQueue(queue, 2);
  for (let i = 4; i <= 6; i++) {
    pushKey(queue, i);
  }

  assert.deepEqual(drainQueue(queue, 8).map((e) => e.id), [3, 4, 5, 6]);
});

test('push_QueueFull_ReplacesDroppedEventsWithOneOverflow', () => {
  const queue = createInputQueue(3);
  for (let i = 1; i <= 6; i++) {
    pushKey(queue, i, i * 10);
  }

  const events = drainQueue(queue, 8, 100);

  assert.deepEqual(events.map((e) => e.kind), [Kind.KeyDown, Kind.KeyDown, Kind.KeyDown, Kind.Overflow]);
  assert.equal(events[3].id, 3, 'Id counts the dropped events');
  assert.equal(events[3].time, Math.fround(-0.06), 'Time is when the first event was dropped');
});

test('push_AfterOverflowAndDrain_OverflowStaysBetweenOldAndNewEvents', () => {
  const queue = createInputQueue(4);
  for (let i = 1; i <= 5; i++) {
    pushKey(queue, i);
  }
  assert.deepEqual(drainQueue(queue, 2).map((e) => e.id), [1, 2]);

  pushKey(queue, 6);

  const events = drainQueue(queue, 8);
  assert.deepEqual(events.map((e) => [e.kind, e.id]), [[Kind.KeyDown, 3], [Kind.KeyDown, 4], [Kind.Overflow, 1], [Kind.KeyDown, 6]]);
});

test('push_NoRoomForOverflowAndEvent_DropsEventIntoSameOverflow', () => {
  const queue = createInputQueue(2);
  for (let i = 1; i <= 3; i++) {
    pushKey(queue, i);
  }
  drainQueue(queue, 1);

  pushKey(queue, 4);

  const events = drainQueue(queue, 8);
  assert.deepEqual(events.map((e) => [e.kind, e.id]), [[Kind.KeyDown, 2], [Kind.Overflow, 2]]);
});

test('drain_TargetFullBeforeOverflow_DeliversOverflowNextDrain', () => {
  const queue = createInputQueue(2);
  for (let i = 1; i <= 3; i++) {
    pushKey(queue, i);
  }

  assert.deepEqual(drainQueue(queue, 2).map((e) => e.kind), [Kind.KeyDown, Kind.KeyDown]);
  assert.deepEqual(drainQueue(queue, 2).map((e) => [e.kind, e.id]), [[Kind.Overflow, 1]]);
});

// --- Browser listeners ---------------------------------------------------------------------------------------------

/** A fake `EventTarget` that tests fire events on. */
function createTarget() {
  /** @type {Map<string, Set<(event: any) => void>>} */
  const listeners = new Map();
  return {
    /** @type {Map<string, AddEventListenerOptions | undefined>} */
    options: new Map(),
    /** @param {string} type @param {(event: any) => void} listener @param {AddEventListenerOptions} [options] */
    addEventListener(type, listener, options) {
      if (!listeners.has(type)) {
        listeners.set(type, new Set());
      }
      listeners.get(type)?.add(listener);
      this.options.set(type, options);
    },
    /** @param {string} type @param {(event: any) => void} listener */
    removeEventListener(type, listener) {
      listeners.get(type)?.delete(listener);
    },
    /** @param {string} type @param {object} [props] @returns {{ defaultPrevented: boolean }} */
    fire(type, props = {}) {
      const event = {
        timeStamp: 0,
        defaultPrevented: false,
        preventDefault() {
          this.defaultPrevented = true;
        },
        ...props,
      };
      for (const listener of listeners.get(type) ?? []) {
        listener(event);
      }
      return event;
    },
    listenerCount() {
      let count = 0;
      for (const set of listeners.values()) {
        count += set.size;
      }
      return count;
    },
  };
}

/**
 * @typedef {{ pressed: boolean, value: number }} Button
 * @typedef {{ index: number, mapping: string, timestamp: number, axes: number[], buttons: Button[] }} Pad
 */

/**
 * @param {{ focused?: boolean }} [options]
 */
function setup({ focused = true } = {}) {
  /** @type {number[]} */
  const captured = [];
  const canvas = Object.assign(createTarget(), {
    clientWidth: 640,
    clientHeight: 360,
    style: { touchAction: '' },
    /** @param {number} id */
    setPointerCapture(id) {
      captured.push(id);
    },
  });
  const window = Object.assign(createTarget(), { devicePixelRatio: 1 });
  const document = Object.assign(createTarget(), {
    visibilityState: focused ? 'visible' : 'hidden',
    /** @type {unknown} */
    pointerLockElement: null,
    hasFocus: () => focused,
  });
  /** @type {(Pad | null)[]} */
  let pads = [];
  let getGamepadsCalls = 0;
  /** @type {() => (Pad | null)[]} */
  let getGamepads = () => pads;
  const navigator = {
    getGamepads() {
      getGamepadsCalls++;
      return getGamepads();
    },
  };
  /** @type {{ callback: () => void, observed: unknown[], disconnected: boolean }[]} */
  const observers = [];
  class FakeResizeObserver {
    /** @param {() => void} callback */
    constructor(callback) {
      this.state = { callback, observed: /** @type {unknown[]} */ ([]), disconnected: false };
      observers.push(this.state);
    }
    /** @param {unknown} target */
    observe(target) {
      this.state.observed.push(target);
    }
    disconnect() {
      this.state.disconnected = true;
    }
  }

  const input = createInput({ canvas, window, document, navigator, now: () => 50, ResizeObserver: FakeResizeObserver }, 64);

  return {
    canvas,
    window,
    document,
    input,
    captured,
    observers,
    get getGamepadsCalls() {
      return getGamepadsCalls;
    },
    /** @param {(Pad | null)[]} value */
    setPads(value) {
      pads = value;
    },
    /** @param {() => (Pad | null)[]} value */
    setGetGamepads(value) {
      getGamepads = value;
    },
    /** Drains everything queued, relative to `frameTimeMs`. */
    drain(frameTimeMs = 100) {
      const target = createEventBuffer(64);
      return decode(target, input.drain(target, frameTimeMs));
    },
    /** Drains and drops the startup events. */
    started() {
      this.drain();
      return this;
    },
  };
}

/** @param {Decoded[]} events */
const kinds = (events) => events.map((e) => e.kind);

test('createInput_SendsResizeThenFocusStateFirst', () => {
  const { drain } = setup();

  const events = drain(100);

  assert.deepEqual(kinds(events), [Kind.Resize, Kind.FocusGained]);
  assert.deepEqual([events[0].x, events[0].y, events[0].z], [640, 360, 1]);
  assert.equal(events[0].time, Math.fround(-0.05));
});

test('createInput_NotFocused_SendsFocusLost', () => {
  const { drain } = setup({ focused: false });

  assert.deepEqual(kinds(drain()), [Kind.Resize, Kind.FocusLost]);
});

test('createInput_DisablesTouchGesturesAndObservesCanvasSize', () => {
  const { canvas, observers } = setup();

  assert.equal(canvas.style.touchAction, 'none');
  assert.equal(observers.length, 1);
  assert.deepEqual(observers[0].observed, [canvas]);
});

test('keydown_KnownKey_QueuesKeyDownAndPreventsDefault', () => {
  const { window, drain } = setup().started();

  const event = window.fire('keydown', { code: 'KeyW', timeStamp: 90 });

  assert.ok(event.defaultPrevented);
  assert.deepEqual(drain(100), [{ kind: Kind.KeyDown, detail: 0, code: 0x1a, id: 0, time: Math.fround(-0.01), x: 0, y: 0, z: 0 }]);
});

test('keydown_Repeat_IsDroppedButStillPrevented', () => {
  const { window, drain } = setup().started();

  const event = window.fire('keydown', { code: 'Space', repeat: true });

  assert.ok(event.defaultPrevented);
  assert.deepEqual(drain(), []);
});

test('keyup_KnownKey_QueuesKeyUp', () => {
  const { window, drain } = setup().started();

  window.fire('keyup', { code: 'ArrowLeft' });

  const [event] = drain();
  assert.equal(event.kind, Kind.KeyUp);
  assert.equal(event.code, 0x50);
});

test('keydown_UnknownCode_IsIgnoredAndNotPrevented', () => {
  const { window, drain } = setup().started();

  const event = window.fire('keydown', { code: 'IntlBackslash' });

  assert.equal(event.defaultPrevented, false);
  assert.deepEqual(drain(), []);
});

test('keydown_FunctionKeysAndShortcuts_AreQueuedButNotPrevented', () => {
  const { window, drain } = setup().started();

  const f5 = window.fire('keydown', { code: 'F5' });
  const ctrlR = window.fire('keydown', { code: 'KeyR', ctrlKey: true });
  const metaC = window.fire('keydown', { code: 'KeyC', metaKey: true });
  const shiftA = window.fire('keydown', { code: 'KeyA', shiftKey: true });

  assert.equal(f5.defaultPrevented, false);
  assert.equal(ctrlR.defaultPrevented, false);
  assert.equal(metaC.defaultPrevented, false);
  assert.ok(shiftA.defaultPrevented);
  assert.deepEqual(drain().map((e) => e.code), [0x3e, 0x15, 0x06, 0x04]);
});

/**
 * @param {object} props
 */
const mouse = (props) => ({ pointerId: 1, pointerType: 'mouse', button: -1, buttons: 0, offsetX: 0, offsetY: 0, ...props });

test('pointerdown_Mouse_QueuesPointerDownAndCapturesPointer', () => {
  const { canvas, captured, drain } = setup().started();

  canvas.fire('pointerdown', mouse({ button: 0, buttons: 1, offsetX: 12.5, offsetY: 40, timeStamp: 95 }));

  assert.deepEqual(drain(100), [
    { kind: Kind.PointerDown, detail: 0, code: PointerType.Mouse, id: 1, time: Math.fround(-0.005), x: 12.5, y: 40, z: 0 },
  ]);
  assert.deepEqual(captured, [1]);
});

test('pointer_ChordedButtons_QueueOneEventPerButton', () => {
  const { canvas, drain } = setup().started();

  canvas.fire('pointerdown', mouse({ button: 0, buttons: 1 }));
  canvas.fire('pointermove', mouse({ button: 2, buttons: 3, offsetX: 5, offsetY: 6 }));
  canvas.fire('pointermove', mouse({ button: 2, buttons: 1 }));
  canvas.fire('pointerup', mouse({ button: 0, buttons: 0 }));

  assert.deepEqual(drain().map((e) => [e.kind, e.detail]), [
    [Kind.PointerDown, 0],
    [Kind.PointerMove, 0],
    [Kind.PointerDown, 2],
    [Kind.PointerMove, 0],
    [Kind.PointerUp, 2],
    [Kind.PointerUp, 0],
  ]);
});

test('pointer_MiddleBackForwardButtons_UseButtonNumbers', () => {
  const { canvas, drain } = setup().started();

  canvas.fire('pointerdown', mouse({ button: 1, buttons: 4 }));
  canvas.fire('pointermove', mouse({ button: 3, buttons: 4 | 8 }));
  canvas.fire('pointermove', mouse({ button: 4, buttons: 4 | 8 | 16 }));

  assert.deepEqual(drain().filter((e) => e.kind === Kind.PointerDown).map((e) => e.detail), [1, 3, 4]);
});

test('pointer_TouchAndPen_ReportPointerType', () => {
  const { canvas, drain } = setup().started();

  canvas.fire('pointerdown', { pointerId: 7, pointerType: 'touch', button: 0, buttons: 1, offsetX: 1, offsetY: 2 });
  canvas.fire('pointerdown', { pointerId: 8, pointerType: 'pen', button: 0, buttons: 1, offsetX: 3, offsetY: 4 });

  assert.deepEqual(drain().map((e) => [e.id, e.code]), [[7, PointerType.Touch], [8, PointerType.Pen]]);
});

test('pointermove_Unlocked_QueuesPosition', () => {
  const { canvas, drain } = setup().started();

  canvas.fire('pointermove', mouse({ offsetX: 100, offsetY: 50, movementX: 3, movementY: 4 }));

  const [event] = drain();
  assert.deepEqual([event.kind, event.x, event.y], [Kind.PointerMove, 100, 50]);
});

test('pointerLock_LockedMouse_QueuesMotionInsteadOfMove', () => {
  const { canvas, document, drain } = setup().started();

  document.pointerLockElement = canvas;
  document.fire('pointerlockchange');
  canvas.fire('pointermove', mouse({ offsetX: 100, offsetY: 50, movementX: -3, movementY: 4 }));
  document.pointerLockElement = null;
  document.fire('pointerlockchange');
  canvas.fire('pointermove', mouse({ offsetX: 101, offsetY: 51, movementX: 1, movementY: 1 }));

  assert.deepEqual(drain().map((e) => [e.kind, e.detail, e.x, e.y]), [
    [Kind.PointerLockChanged, 1, 0, 0],
    [Kind.PointerMotion, 0, -3, 4],
    [Kind.PointerLockChanged, 0, 0, 0],
    [Kind.PointerMove, 0, 101, 51],
  ]);
});

test('pointerLock_OtherElementLocked_IsNotReported', () => {
  const { document, drain } = setup().started();

  document.pointerLockElement = {};
  document.fire('pointerlockchange');

  assert.deepEqual(drain(), []);
});

test('pointercancel_QueuesCancelAndForgetsButtons', () => {
  const { canvas, drain } = setup().started();

  canvas.fire('pointerdown', { pointerId: 3, pointerType: 'touch', button: 0, buttons: 1 });
  canvas.fire('pointercancel', { pointerId: 3, pointerType: 'touch' });
  canvas.fire('pointerdown', { pointerId: 3, pointerType: 'touch', button: 0, buttons: 1 });

  assert.deepEqual(kinds(drain()), [Kind.PointerDown, Kind.PointerCancel, Kind.PointerDown]);
});

test('wheel_QueuesPixelDeltasAndPreventsScrolling', () => {
  const { canvas, drain } = setup().started();

  const pixels = canvas.fire('wheel', { deltaMode: 0, deltaX: 1, deltaY: -40, deltaZ: 0 });
  canvas.fire('wheel', { deltaMode: 1, deltaX: 0, deltaY: 3, deltaZ: 0 });
  canvas.fire('wheel', { deltaMode: 2, deltaX: 0, deltaY: 1, deltaZ: 0 });

  assert.ok(pixels.defaultPrevented);
  assert.deepEqual(canvas.options.get('wheel'), { passive: false });
  assert.deepEqual(drain().map((e) => [e.kind, e.x, e.y]), [
    [Kind.Wheel, 1, -40],
    [Kind.Wheel, 0, 3 * WHEEL_LINE_PIXELS],
    [Kind.Wheel, 0, 360],
  ]);
});

test('contextmenu_IsPrevented', () => {
  const { canvas } = setup();

  assert.ok(canvas.fire('contextmenu').defaultPrevented);
});

test('focus_TransitionsAreQueuedOnce', () => {
  const { window, document, drain } = setup().started();

  window.fire('blur');
  window.fire('blur');
  window.fire('focus');
  document.visibilityState = 'hidden';
  document.fire('visibilitychange');
  window.fire('blur');

  assert.deepEqual(kinds(drain()), [Kind.FocusLost, Kind.FocusGained, Kind.FocusLost]);
});

test('resize_NewSize_QueuesResize', () => {
  const { canvas, observers, drain } = setup().started();

  observers[0].callback();
  canvas.clientWidth = 800;
  canvas.clientHeight = 600;
  observers[0].callback();

  assert.deepEqual(drain().map((e) => [e.kind, e.x, e.y, e.z]), [[Kind.Resize, 800, 600, 1]]);
});

test('drain_PixelRatioChanged_QueuesResize', () => {
  const { window, drain } = setup().started();

  window.devicePixelRatio = 2;

  assert.deepEqual(drain().map((e) => [e.kind, e.z]), [[Kind.Resize, 2]]);
  assert.deepEqual(drain(), []);
});

/**
 * @param {Partial<Pad>} [props]
 * @returns {Pad}
 */
const pad = (props) => ({
  index: 0,
  mapping: 'standard',
  timestamp: 80,
  axes: [0, 0],
  buttons: [{ pressed: false, value: 0 }, { pressed: false, value: 0 }],
  ...props,
});

test('gamepad_Connected_QueuesConnectedThenCurrentState', () => {
  const { setPads, drain } = setup().started();
  setPads([pad({ axes: [0.5, 0], buttons: [{ pressed: false, value: 0 }, { pressed: true, value: 1 }] })]);

  assert.deepEqual(drain(100), [
    { kind: Kind.GamepadConnected, detail: 1, code: 0, id: 0, time: Math.fround(-0.02), x: 0, y: 0, z: 0 },
    { kind: Kind.GamepadButton, detail: 1, code: 1, id: 0, time: Math.fround(-0.02), x: 1, y: 0, z: 0 },
    { kind: Kind.GamepadAxis, detail: 0, code: 0, id: 0, time: Math.fround(-0.02), x: 0.5, y: 0, z: 0 },
  ]);
});

test('gamepad_NonStandardMapping_ClearsStandardFlag', () => {
  const { setPads, drain } = setup().started();
  setPads([null, pad({ index: 1, mapping: '' })]);

  assert.deepEqual(drain().map((e) => [e.kind, e.detail, e.id]), [[Kind.GamepadConnected, 0, 1]]);
});

test('gamepad_ChangesBelowThreshold_AreNotQueued', () => {
  const { setPads, drain } = setup().started();
  setPads([pad()]);
  drain();

  setPads([pad({ axes: [GAMEPAD_THRESHOLD / 2, 0], buttons: [{ pressed: false, value: GAMEPAD_THRESHOLD / 2 }, { pressed: false, value: 0 }] })]);
  assert.deepEqual(drain(), []);

  setPads([pad({ axes: [0, -0.25], buttons: [{ pressed: false, value: 0.3 }, { pressed: false, value: 0 }] })]);
  assert.deepEqual(drain().map((e) => [e.kind, e.code, e.detail, e.x]), [
    [Kind.GamepadButton, 0, 0, Math.fround(0.3)],
    [Kind.GamepadAxis, 1, 0, -0.25],
  ]);
});

test('gamepad_ButtonReleased_QueuesButtonUp', () => {
  const { setPads, drain } = setup().started();
  setPads([pad({ buttons: [{ pressed: true, value: 1 }, { pressed: false, value: 0 }] })]);
  drain();

  setPads([pad()]);

  assert.deepEqual(drain().map((e) => [e.kind, e.code, e.detail, e.x]), [[Kind.GamepadButton, 0, 0, 0]]);
});

test('gamepad_Removed_QueuesDisconnected', () => {
  const { setPads, drain } = setup().started();
  setPads([pad()]);
  drain();

  setPads([null]);
  assert.deepEqual(drain().map((e) => [e.kind, e.id]), [[Kind.GamepadDisconnected, 0]]);

  setPads([]);
  assert.deepEqual(drain(), []);
});

test('gamepad_NoTimestamp_UsesFrameTime', () => {
  const { setPads, drain } = setup().started();
  setPads([pad({ timestamp: 0 })]);

  assert.equal(drain(100)[0].time, 0);
});

test('gamepad_GetGamepadsThrows_StopsPolling', () => {
  const state = setup().started();
  state.setGetGamepads(() => {
    throw new Error('blocked by permissions policy');
  });
  const callsBefore = state.getGamepadsCalls;

  assert.deepEqual(state.drain(), []);
  assert.deepEqual(state.drain(), []);
  assert.equal(state.getGamepadsCalls, callsBefore + 1);
});

test('detach_RemovesListenersAndStopsObserving', () => {
  const { canvas, window, document, observers, input, drain } = setup().started();

  input.detach();
  window.fire('keydown', { code: 'KeyA' });
  canvas.fire('pointerdown', mouse({ button: 0, buttons: 1 }));

  assert.equal(canvas.listenerCount() + window.listenerCount() + document.listenerCount(), 0);
  assert.ok(observers[0].disconnected);
  assert.deepEqual(drain(), []);
});

// --- Interop -------------------------------------------------------------------------------------------------------

test('drain_NotAttached_WritesNothing', () => {
  let writes = 0;
  const view = {
    byteLength: 4 * EVENT_SIZE,
    set() {
      writes++;
    },
  };

  assert.equal(drain(view, 0), 0);
  assert.equal(writes, 0);
});

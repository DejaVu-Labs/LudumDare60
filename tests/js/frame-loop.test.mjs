// @ts-check
import { test } from 'node:test';
import assert from 'node:assert/strict';

import { afterFirstFrame, createFrameLoop } from '../../src/engine/platforms/LudumDare60.Engine.Platform.Browser/wwwroot/frame-loop.js';

/** A manual stand-in for requestAnimationFrame: frames run only when the test calls `frame(timestamp)`. */
function createScheduler() {
  /** @type {Map<number, (timestamp: number) => void>} */
  const pending = new Map();
  let nextHandle = 1;

  return {
    /** @param {(timestamp: number) => void} callback */
    requestFrame(callback) {
      const handle = nextHandle++;
      pending.set(handle, callback);
      return handle;
    },
    /** @param {number} handle */
    cancelFrame(handle) {
      pending.delete(handle);
    },
    get pendingCount() {
      return pending.size;
    },
    /** Runs the callbacks scheduled so far, like the browser does at the start of a frame. */
    frame(/** @type {number} */ timestamp) {
      const callbacks = [...pending.values()];
      pending.clear();
      for (const callback of callbacks) {
        callback(timestamp);
      }
    },
  };
}

/**
 * @param {(dt: number, timestamp: number) => void} onFrame
 */
function start(onFrame) {
  const scheduler = createScheduler();
  const loop = createFrameLoop(onFrame, scheduler.requestFrame, scheduler.cancelFrame);
  return { scheduler, loop };
}

test('createFrameLoop_Start_SchedulesOneFrameWithoutCallingOnFrame', () => {
  /** @type {number[]} */
  const deltas = [];
  const { scheduler } = start((dt) => deltas.push(dt));

  assert.equal(scheduler.pendingCount, 1);
  assert.deepEqual(deltas, []);
});

test('createFrameLoop_FirstFrame_ReportsZeroDelta', () => {
  /** @type {number[]} */
  const deltas = [];
  const { scheduler } = start((dt) => deltas.push(dt));

  scheduler.frame(1234.5);

  assert.deepEqual(deltas, [0]);
});

test('createFrameLoop_Frames_ReportSecondsBetweenTimestamps', () => {
  /** @type {number[]} */
  const deltas = [];
  const { scheduler } = start((dt) => deltas.push(dt));

  scheduler.frame(1000);
  scheduler.frame(1016);
  scheduler.frame(1050);
  scheduler.frame(1050);

  assert.deepEqual(deltas, [0, 0.016, 0.034, 0]);
});

test('createFrameLoop_Frames_PassTheirTimestamp', () => {
  /** @type {number[]} */
  const timestamps = [];
  const { scheduler } = start((_, timestamp) => timestamps.push(timestamp));

  scheduler.frame(1000);
  scheduler.frame(1016.5);

  assert.deepEqual(timestamps, [1000, 1016.5]);
});

test('createFrameLoop_EachFrame_SchedulesExactlyOneNextFrame', () => {
  const { scheduler } = start(() => {});

  for (let i = 0; i < 5; i++) {
    scheduler.frame(i * 16);
    assert.equal(scheduler.pendingCount, 1);
  }
});

test('createFrameLoop_EachFrame_ReusesTheSameCallback', () => {
  /** @type {Set<unknown>} */
  const callbacks = new Set();
  const scheduler = createScheduler();
  createFrameLoop(
    () => {},
    (callback) => {
      callbacks.add(callback);
      return scheduler.requestFrame(callback);
    },
    scheduler.cancelFrame,
  );

  scheduler.frame(0);
  scheduler.frame(16);

  assert.equal(callbacks.size, 1);
});

test('stop_CancelsPendingFrameAndResolvesDone', async () => {
  let frames = 0;
  const { scheduler, loop } = start(() => frames++);
  scheduler.frame(0);

  loop.stop();
  scheduler.frame(16);

  assert.equal(scheduler.pendingCount, 0);
  assert.equal(frames, 1);
  await loop.done;
});

test('stop_CalledFromOnFrame_SchedulesNoFurtherFrames', async () => {
  /** @type {{ stop(): void } | undefined} */
  let self;
  let frames = 0;
  const { scheduler, loop } = start(() => {
    frames++;
    self?.stop();
  });
  self = loop;

  scheduler.frame(0);
  scheduler.frame(16);

  assert.equal(frames, 1);
  assert.equal(scheduler.pendingCount, 0);
  await loop.done;
});

test('stop_CalledTwice_IsIgnored', async () => {
  const { loop } = start(() => {});

  loop.stop();
  loop.stop();

  await loop.done;
});

test('createFrameLoop_OnFrameThrows_RejectsDoneAndStops', async () => {
  const failure = new Error('frame failure');
  let frames = 0;
  const { scheduler, loop } = start(() => {
    frames++;
    throw failure;
  });

  scheduler.frame(0);
  scheduler.frame(16);

  await assert.rejects(loop.done, (error) => error === failure);
  assert.equal(frames, 1);
  assert.equal(scheduler.pendingCount, 0);
});

test('stop_AfterOnFrameThrew_DoesNotResolveDone', async () => {
  const failure = new Error('frame failure');
  const { scheduler, loop } = start(() => {
    throw failure;
  });

  scheduler.frame(0);
  loop.stop();

  await assert.rejects(loop.done, (error) => error === failure);
});

test('afterFirstFrame_RunsHookOnceAfterFirstFrame', () => {
  /** @type {string[]} */
  const calls = [];
  const onFrame = afterFirstFrame(
    (dt) => calls.push(`frame ${dt}`),
    () => calls.push('first'),
  );

  onFrame(0, 0);
  onFrame(0.5, 500);

  assert.deepEqual(calls, ['frame 0', 'first', 'frame 0.5']);
});

test('afterFirstFrame_FirstFrameThrows_HookNotRun', () => {
  let hooked = false;
  const onFrame = afterFirstFrame(
    () => {
      throw new Error('frame failed');
    },
    () => {
      hooked = true;
    },
  );

  assert.throws(() => onFrame(0, 0), /frame failed/);
  assert.equal(hooked, false);
});

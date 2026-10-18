// @ts-check

import { getLoadLog, STEP } from './load-log.js';

/**
 * Frame loop of the browser platform. Each animation frame, the loop calls `onFrame(dt, timestamp)` once, with
 * `dt` = seconds since the previous frame. C# passes `onFrame` through `[JSImport]`, so a frame costs one JS -> .NET
 * call.
 */

/**
 * Schedules a callback for the next frame and returns a handle for `cancelFrame`.
 * @typedef {(callback: (timestamp: number) => void) => number} RequestFrame
 */

/**
 * Cancels a callback scheduled with `RequestFrame`.
 * @typedef {(handle: number) => void} CancelFrame
 */

/**
 * @typedef {object} FrameLoop
 * @property {Promise<void>} done Resolves after `stop()`. Rejects with the error thrown by `onFrame`.
 * @property {() => void} stop Stops the loop. The frame already scheduled never runs.
 */

/**
 * Starts a frame loop on the given scheduler. The first frame reports `dt = 0`.
 *
 * @param {(dt: number, timestamp: number) => void} onFrame Called once per frame with the seconds since the previous
 *   frame and the frame's timestamp in milliseconds (the `requestAnimationFrame` timestamp, on the
 *   `performance.now()` timeline).
 * @param {RequestFrame} requestFrame Usually `requestAnimationFrame`.
 * @param {CancelFrame} cancelFrame Usually `cancelAnimationFrame`.
 * @returns {FrameLoop}
 */
export function createFrameLoop(onFrame, requestFrame, cancelFrame) {
  /** @type {() => void} */
  let resolveDone = () => {};
  /** @type {(error: unknown) => void} */
  let rejectDone = () => {};
  /** @type {Promise<void>} */
  const done = new Promise((resolve, reject) => {
    resolveDone = resolve;
    rejectDone = reject;
  });

  let running = true;
  let handle = 0;
  let previousTimestamp = -1;

  // Created once: the callback runs every frame and must not allocate.
  /** @param {number} timestamp Frame start time in milliseconds. */
  const tick = (timestamp) => {
    if (!running) {
      return;
    }

    const dt = previousTimestamp < 0 ? 0 : (timestamp - previousTimestamp) / 1000;
    previousTimestamp = timestamp;

    try {
      onFrame(dt, timestamp);
    } catch (error) {
      running = false;
      rejectDone(error);
      return;
    }

    if (running) {
      handle = requestFrame(tick);
    }
  };

  handle = requestFrame(tick);

  return {
    done,
    stop() {
      if (!running) {
        return;
      }

      running = false;
      cancelFrame(handle);
      resolveDone();
    },
  };
}

/**
 * Wraps `onFrame` so that `onFirstFrame` runs once, after the first frame returns.
 *
 * @param {(dt: number, timestamp: number) => void} onFrame
 * @param {() => void} onFirstFrame
 * @returns {(dt: number, timestamp: number) => void}
 */
export function afterFirstFrame(onFrame, onFirstFrame) {
  let first = true;
  return (dt, timestamp) => {
    onFrame(dt, timestamp);
    if (first) {
      first = false;
      onFirstFrame();
    }
  };
}

/**
 * Runs the frame loop on `requestAnimationFrame` until `onFrame` throws, and logs the end of the first frame to the
 * load log. Bound from C# with `[JSImport]`.
 *
 * @param {(dt: number, timestamp: number) => void} onFrame
 * @returns {Promise<void>} Rejects with the error thrown by `onFrame`.
 */
export function run(onFrame) {
  return createFrameLoop(
    afterFirstFrame(onFrame, () => getLoadLog().step(STEP.FIRST_FRAME)),
    (callback) => globalThis.requestAnimationFrame(callback),
    (handle) => globalThis.cancelAnimationFrame(handle),
  ).done;
}

// @ts-check

/**
 * Frame loop of the browser platform. Each animation frame, the loop calls `onFrame(dt)` once, with `dt` = seconds
 * since the previous frame. C# passes `onFrame` through `[JSImport]`, so a frame costs one JS -> .NET call.
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
 * @param {(dt: number) => void} onFrame Called once per frame with the seconds since the previous frame.
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
      onFrame(dt);
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
 * Runs the frame loop on `requestAnimationFrame` until `onFrame` throws. Bound from C# with `[JSImport]`.
 *
 * @param {(dt: number) => void} onFrame
 * @returns {Promise<void>} Rejects with the error thrown by `onFrame`.
 */
export function run(onFrame) {
  return createFrameLoop(
    onFrame,
    (callback) => globalThis.requestAnimationFrame(callback),
    (handle) => globalThis.cancelAnimationFrame(handle),
  ).done;
}

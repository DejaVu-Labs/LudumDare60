// @ts-check

/**
 * Load timing of the browser build, for profiling startup on a real network. Every step is a `performance.mark`
 * (DevTools: Performance > Timings) and a `[load]` line in the console. Times are milliseconds since the page started
 * loading (`performance.now()`). File sizes and timings come from the Resource Timing API, so the downloads
 * themselves are not touched.
 */

/**
 * The part of a `PerformanceResourceTiming` entry the log reads.
 * @typedef {object} ResourceEntry
 * @property {string} name The URL.
 * @property {number} startTime
 * @property {number} responseEnd
 * @property {number} transferSize Bytes over the network, headers included; 0 when served from the cache.
 * @property {number} decodedBodySize
 */

/**
 * The part of `performance` the log uses, so tests can pass a fake clock.
 * @typedef {object} Clock
 * @property {() => number} now
 * @property {(name: string) => unknown} mark
 * @property {(type: 'resource') => readonly ResourceEntry[]} getEntriesByType
 */

/**
 * The part of `console` the log uses.
 * @typedef {object} LogSink
 * @property {(message: string) => void} info
 * @property {(message: string) => void} warn
 * @property {(message: string) => void} groupCollapsed
 * @property {() => void} groupEnd
 * @property {(rows: object[]) => void} table
 */

/**
 * One console line of the log, as listeners get it.
 * @typedef {object} LoadLogEntry
 * @property {'step' | 'retry' | 'downloads' | 'assets'} kind
 * @property {string} message The console line without `PREFIX` and the time.
 * @property {'info' | 'warning'} level
 * @property {number} time Milliseconds since the page started loading.
 */

/**
 * What the log has seen so far.
 * @typedef {object} LoadSnapshot
 * @property {Readonly<Record<string, number>>} steps Time of each step label, the first time it was logged.
 * @property {number} retries Download retries so far.
 * @property {{ start: number, end: number, files: number, bytes: number } | null} firstAssets The first asset batch,
 *   once its last read finished.
 */

/**
 * @typedef {object} LoadLog
 * @property {(label: string) => number} step Marks a startup step and logs it. Returns its time.
 * @property {(name: string, attempt: number, retries: number, delayMs: number, reason: string) => void} retry Logs
 *   a failed download that is about to be retried.
 * @property {(urlPart: string, label: string) => void} downloads Logs the files whose URL contains `urlPart`: one
 *   collapsed line with the count, the sizes and the end of the last download, and a table with one row per file.
 * @property {() => number} assetStarted Counts an asset read in. Returns its start time for `assetFinished`.
 * @property {(url: string, startTime: number, bytes: number) => void} assetFinished Counts an asset read out;
 *   `bytes` is negative for a failed read. When no read is left, logs the batch since the first one started.
 * @property {(listener: (entry: LoadLogEntry) => void) => void} subscribe Calls `listener` for every later line.
 * @property {() => LoadSnapshot} snapshot
 */

/** Step labels that other modules log and read back from `LoadSnapshot.steps`. */
export const STEP = Object.freeze({
  BOOT: 'boot',
  RUNTIME_IMPORTED: 'runtime script imported',
  RUNTIME_CREATED: 'runtime created, running Main',
  FIRST_FRAME: 'first frame',
});

/** Starts every console line, so `[load]` filters them. */
export const PREFIX = '[load]';

/**
 * @param {number} ms
 * @returns {string} `412 ms` below a second, `31.2 s` from there.
 */
export function formatMs(ms) {
  return ms < 1000 ? `${Math.round(ms)} ms` : `${(ms / 1000).toFixed(1)} s`;
}

/**
 * @param {number} bytes
 * @returns {string} `512 B`, `12.3 KB` or `2.51 MB`.
 */
export function formatBytes(bytes) {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(1)} KB`;
  }
  return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
}

/**
 * @param {string} url
 * @returns {string} The last path segment, without the query.
 */
function fileName(url) {
  const path = url.split(/[?#]/, 1)[0];
  return path.slice(path.lastIndexOf('/') + 1);
}

/**
 * @param {Clock} clock Usually `performance`.
 * @param {LogSink} sink Usually `console`.
 * @returns {LoadLog}
 */
export function createLoadLog(clock, sink) {
  let assetsInFlight = 0;
  let batchStart = 0;
  /** @type {{ file: string, start: number, duration: number, bytes: number }[]} */
  let batch = [];
  /** @type {((entry: LoadLogEntry) => void)[]} */
  const listeners = [];
  /** @type {Record<string, number>} */
  const steps = {};
  let retries = 0;
  /** @type {LoadSnapshot['firstAssets']} */
  let firstAssets = null;

  /**
   * @param {LoadLogEntry['kind']} kind
   * @param {string} message
   * @param {LoadLogEntry['level']} level
   * @param {number} time
   */
  function notify(kind, message, level, time) {
    for (const listener of listeners) {
      listener({ kind, message, level, time });
    }
  }

  return {
    step(label) {
      const time = clock.now();
      clock.mark(`${PREFIX} ${label}`);
      sink.info(`${PREFIX} ${formatMs(time)}  ${label}`);
      steps[label] ??= time;
      notify('step', label, 'info', time);
      return time;
    },

    retry(name, attempt, retryCount, delayMs, reason) {
      const time = clock.now();
      const message = `${name} failed (${reason}), retry ${attempt}/${retryCount} in ${formatMs(delayMs)}`;
      sink.warn(`${PREFIX} ${formatMs(time)}  ${message}`);
      retries++;
      notify('retry', message, 'warning', time);
    },

    downloads(urlPart, label) {
      const entries = clock.getEntriesByType('resource').filter((entry) => entry.name.includes(urlPart));
      if (entries.length === 0) {
        return;
      }
      let transferred = 0;
      let decoded = 0;
      let end = 0;
      for (const entry of entries) {
        transferred += entry.transferSize;
        decoded += entry.decodedBodySize;
        end = Math.max(end, entry.responseEnd);
      }
      const message = `${label}: ${entries.length} files, ${formatBytes(transferred)} transferred, ${formatBytes(decoded)} decoded`;
      sink.groupCollapsed(`${PREFIX} ${formatMs(end)}  ${message}`);
      sink.table(
        entries.map((entry) => ({
          file: fileName(entry.name),
          start: Math.round(entry.startTime),
          duration: Math.round(entry.responseEnd - entry.startTime),
          transferred: entry.transferSize,
          decoded: entry.decodedBodySize,
        })),
      );
      sink.groupEnd();
      notify('downloads', message, 'info', end);
    },

    assetStarted() {
      const time = clock.now();
      if (assetsInFlight === 0) {
        batchStart = time;
      }
      assetsInFlight++;
      return time;
    },

    assetFinished(url, startTime, bytes) {
      const time = clock.now();
      batch.push({ file: fileName(url), start: Math.round(startTime), duration: Math.round(time - startTime), bytes });
      assetsInFlight--;
      if (assetsInFlight > 0) {
        return;
      }
      let total = 0;
      let failed = 0;
      for (const row of batch) {
        if (row.bytes < 0) {
          failed++;
        } else {
          total += row.bytes;
        }
      }
      const failures = failed > 0 ? `, ${failed} failed` : '';
      const message = `assets: ${batch.length} files, ${formatBytes(total)}${failures}, in ${formatMs(time - batchStart)}`;
      sink.groupCollapsed(`${PREFIX} ${formatMs(time)}  ${message}`);
      sink.table(batch);
      sink.groupEnd();
      firstAssets ??= { start: batchStart, end: time, files: batch.length, bytes: total };
      batch = [];
      notify('assets', message, failed > 0 ? 'warning' : 'info', time);
    },

    subscribe(listener) {
      listeners.push(listener);
    },

    snapshot() {
      return { steps: { ...steps }, retries, firstAssets };
    },
  };
}

/** @type {LoadLog | undefined} */
let loadLog;

/** @returns {LoadLog} The page's log, on `performance` and `console`. */
export function getLoadLog() {
  loadLog ??= createLoadLog(globalThis.performance, globalThis.console);
  return loadLog;
}

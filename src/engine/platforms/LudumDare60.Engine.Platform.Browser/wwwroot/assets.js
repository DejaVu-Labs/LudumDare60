// @ts-check

import { browserWatchedFetch, createRetryingFetch, delay, RETRY_DELAYS_MS } from './boot.js';
import { getLoadLog } from './load-log.js';

/**
 * Asset reads of the browser platform. C# starts a read with `read(id, url, noCache)`, which fetches the whole file
 * and keeps its bytes under `id`, then copies them into a `byte[]` of the reported length with `take(id, span)`. A file
 * costs two interop calls. Reads are downloaded like the runtime's files: aborted after `STALL_MS` without data and
 * retried (`createRetryingFetch` in `boot.js`).
 */

/**
 * @typedef {(url: string, init: { cache: RequestCache }) => Promise<Response>} Fetch
 */

/**
 * The span view that `[JSImport]` passes for a `Span<byte>`; valid only during the call.
 * @typedef {object} MemoryView
 * @property {number} byteLength
 * @property {(source: Uint8Array, targetOffset?: number) => void} set
 */

/**
 * @typedef {object} AssetReader
 * @property {(id: number, url: string, noCache: boolean) => Promise<number>} read Fetches `url`, resolved against the
 *   page, and keeps the bytes under `id`. Resolves with the byte length, or with `-status` for an HTTP error status (no
 *   bytes are kept then). Rejects on a network error.
 * @property {(id: number, destination: MemoryView) => void} take Copies the bytes kept under `id` into
 *   `destination`, which must have exactly their length, and forgets them.
 * @property {number} pendingCount Number of reads whose bytes were not taken yet.
 */

/**
 * Hooks that time each read (see `LoadLog` in `load-log.js`).
 * @typedef {object} ReadTiming
 * @property {() => number} assetStarted Returns the read's start time.
 * @property {(url: string, startTime: number, bytes: number) => void} assetFinished `bytes` is negative for a failed
 *   read.
 */

/** @type {ReadTiming} */
const NO_TIMING = Object.freeze({
  assetStarted: () => 0,
  assetFinished: () => {},
});

/**
 * @param {Fetch} fetchFile Usually `fetch`.
 * @param {() => string} baseUri Usually the page's `document.baseURI`. Asset URLs are relative to the page, not to
 *   this module.
 * @param {ReadTiming} [timing] Usually the load log.
 * @returns {AssetReader}
 */
export function createAssetReader(fetchFile, baseUri, timing = NO_TIMING) {
  /** @type {Map<number, Uint8Array>} */
  const files = new Map();

  return {
    async read(id, url, noCache) {
      if (files.has(id)) {
        throw new Error(`Asset read ${id} is already pending.`);
      }
      const startTime = timing.assetStarted();
      let length = -1;
      try {
        const response = await fetchFile(new URL(url, baseUri()).href, { cache: noCache ? 'no-cache' : 'default' });
        if (!response.ok) {
          length = -response.status;
          return length;
        }
        const bytes = new Uint8Array(await response.arrayBuffer());
        files.set(id, bytes);
        length = bytes.byteLength;
        return length;
      } finally {
        timing.assetFinished(url, startTime, length);
      }
    },
    take(id, destination) {
      const bytes = files.get(id);
      if (bytes === undefined) {
        throw new Error(`No asset read ${id} to take.`);
      }
      files.delete(id);
      if (destination.byteLength !== bytes.byteLength) {
        throw new Error(`Asset read ${id} has ${bytes.byteLength} bytes, the destination ${destination.byteLength}.`);
      }
      destination.set(bytes, 0);
    },
    get pendingCount() {
      return files.size;
    },
  };
}

/**
 * @param {string} url
 * @returns {string} The last path segment, without the query.
 */
export function fileName(url) {
  const path = url.split(/[?#]/, 1)[0];
  return path.slice(path.lastIndexOf('/') + 1);
}

/** @type {AssetReader | undefined} */
let reader;

/** @returns {AssetReader} */
function getReader() {
  const log = getLoadLog();
  reader ??= createAssetReader(
    createRetryingFetch(browserWatchedFetch(), delay, (url, attempt, delayMs, reason) =>
      log.retry(fileName(url), attempt, RETRY_DELAYS_MS.length, delayMs, reason),
    ),
    () => globalThis.document.baseURI,
    log,
  );
  return reader;
}

/**
 * Bound from C# with `[JSImport]`. See `AssetReader.read`.
 *
 * @param {number} id
 * @param {string} url
 * @param {boolean} noCache Revalidate with the server instead of trusting the browser cache (Debug builds).
 * @returns {Promise<number>}
 */
export function read(id, url, noCache) {
  return getReader().read(id, url, noCache);
}

/**
 * Bound from C# with `[JSImport]`. See `AssetReader.take`.
 *
 * @param {number} id
 * @param {MemoryView} destination
 */
export function take(id, destination) {
  getReader().take(id, destination);
}

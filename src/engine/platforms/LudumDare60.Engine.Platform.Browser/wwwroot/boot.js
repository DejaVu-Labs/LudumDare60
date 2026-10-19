// @ts-check

import { getLoadLog, STEP } from './load-log.js';

/** @typedef {import('./reporting.js').Reporter} Reporter */

/**
 * Starts the .NET runtime and runs the app's `Main`. The host page's `main.js` calls `boot` with the folder that
 * holds the runtime (`dotnet.js` and the files it loads), so an app can ship several runtime builds side by side.
 * A page element with id `status` (optional) shows loading text until the runtime is ready, and an error message if
 * startup or the game fails.
 */

/**
 * Stands in for the error reporter when `reporting.js` does not load or start.
 *
 * @type {Pick<Reporter, 'captureError' | 'idle'>}
 */
export const NO_REPORTER = Object.freeze({ captureError() {}, idle: Promise.resolve() });

/** How long a repair waits for pending error reports before it reloads the page, in milliseconds. */
export const REPORT_WAIT_MS = 3000;

/**
 * Imports an optional module (error reporting, product analytics) and starts it, without the game depending on it:
 * a module that does not load (a content blocker stripping a file named like a tracker, a dropped connection) or a
 * start that throws gives `fallback` instead, with a console warning. The game starts either way.
 *
 * @template M, T
 * @param {() => Promise<M>} importModule Usually `() => import('./module.js')`.
 * @param {(module: M) => T} start
 * @param {T} fallback
 * @returns {Promise<T>}
 */
export function startOptional(importModule, start, fallback) {
  return Promise.resolve()
    .then(importModule)
    .then(start)
    .catch((error) => {
      console.warn('[boot] an optional module did not start; the game goes on without it:', error);
      return fallback;
    });
}

/** Shown in the status element when startup or the game fails. Details go to the console. */
export const ERROR_TEXT = 'Something went wrong. Reload the page to try again.';

/** @type {string | null} */
let errorText = null;

/**
 * Replaces `ERROR_TEXT` for the failure that follows. A module that knows why the game cannot go on (no WebGL2, a lost
 * GPU) sets a message the player can act on, then throws.
 *
 * @param {string | null} text `null` restores `ERROR_TEXT`.
 */
export function setErrorText(text) {
  errorText = text;
}

/** @returns {string} The text the status element shows on failure. */
export function getErrorText() {
  return errorText ?? ERROR_TEXT;
}

/**
 * The runtime's resource loader hook (`LoadBootResourceCallback` in `dotnet.d.ts`). It returns a URL string for the
 * runtime to fetch, a promise of the response, or `undefined` for the default download.
 * @typedef {(type: string, name: string, defaultUri: string, integrity: string) => string | Promise<Response> | undefined} LoadBootResource
 */

/**
 * @typedef {object} DotnetHostBuilder
 * @property {(loader: LoadBootResource) => DotnetHostBuilder} withResourceLoader
 * @property {() => Promise<{ runMain: () => Promise<number> }>} create
 */

/**
 * @typedef {(url: string, init: RequestInit) => Promise<Response>} FetchLike
 */

/**
 * Waits before each retry of a failed download, in milliseconds: six retries over about 31 s. The runtime's own retry
 * gives up after about 100 ms, which a reset connection on a slow network outlasts (itch.io's CDN, seen 2026-10-19).
 */
export const RETRY_DELAYS_MS = Object.freeze([1000, 2000, 4000, 8000, 8000, 8000]);

/**
 * A download that receives no bytes for this long is aborted and retried. On one phone's network, connections to
 * itch.io's CDN froze after about 16 KB and only failed at the browser's 120 s timeout, while a fresh request often
 * finished in under a second (Sentry traces, 2026-10-19).
 */
export const STALL_MS = 10_000;

/** Web Crypto names of the hash algorithms that `integrity` values use. */
const INTEGRITY_ALGORITHMS = Object.freeze({ sha256: 'SHA-256', sha384: 'SHA-384', sha512: 'SHA-512' });

/**
 * @typedef {object} WatchedFetchDeps
 * @property {(algorithm: string, bytes: Uint8Array) => Promise<ArrayBuffer>} digest Usually `crypto.subtle.digest`.
 * @property {(callback: () => void, ms: number) => unknown} setTimer Usually `setTimeout`.
 * @property {(timer: unknown) => void} clearTimer Usually `clearTimeout`.
 * @property {number} [stallMs] Defaults to `STALL_MS`.
 */

/**
 * @param {Uint8Array} bytes
 * @returns {string}
 */
function toBase64(bytes) {
  let binary = '';
  for (let i = 0; i < bytes.length; i += 0x8000) {
    binary += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
  }
  return btoa(binary);
}

/**
 * Wraps `fetchLike` so a request fails when no bytes arrive for `stallMs`, before the headers or between two chunks of
 * the body. The body is read to the end here, and `init.integrity` (SHA-256, -384 or -512) is checked against it here
 * too: with `integrity` on the request, `fetch` resolves only once the whole body is in, so a stall could not be seen.
 *
 * @param {FetchLike} fetchLike Usually `fetch`. Gets `init` without `integrity`, plus an abort `signal`.
 * @param {WatchedFetchDeps} deps
 * @returns {FetchLike} Resolves with a response that holds the whole body and the original status and
 *   `content-type`. Rejects with `TypeError` on a stall, a network error or a failed integrity check.
 */
export function createWatchedFetch(fetchLike, deps) {
  const stallMs = deps.stallMs ?? STALL_MS;
  return async (url, init) => {
    const { integrity, ...rest } = init;
    const controller = new AbortController();
    let stalled = false;
    /** @type {unknown} */
    let timer;
    const arm = () => {
      deps.clearTimer(timer);
      timer = deps.setTimer(() => {
        stalled = true;
        controller.abort();
      }, stallMs);
    };

    arm();
    try {
      const response = await fetchLike(url, { ...rest, signal: controller.signal });
      if (!response.ok || response.body === null) {
        return response;
      }
      /** @type {Uint8Array[]} */
      const chunks = [];
      let length = 0;
      const reader = response.body.getReader();
      for (;;) {
        arm();
        const { done, value } = await reader.read();
        if (done) {
          break;
        }
        chunks.push(value);
        length += value.byteLength;
      }
      const bytes = new Uint8Array(length);
      let offset = 0;
      for (const chunk of chunks) {
        bytes.set(chunk, offset);
        offset += chunk.byteLength;
      }

      if (integrity) {
        const dash = integrity.indexOf('-');
        const algorithm = INTEGRITY_ALGORITHMS[/** @type {keyof typeof INTEGRITY_ALGORITHMS} */ (integrity.slice(0, dash))];
        if (algorithm !== undefined) {
          const actual = `${integrity.slice(0, dash)}-${toBase64(new Uint8Array(await deps.digest(algorithm, bytes)))}`;
          if (actual !== integrity) {
            throw new TypeError(`integrity check failed (${actual})`);
          }
        }
      }

      /** @type {Record<string, string>} */
      const headers = {};
      const contentType = response.headers.get('content-type');
      if (contentType !== null) {
        headers['content-type'] = contentType;
      }
      return new Response(bytes, { status: response.status, statusText: response.statusText, headers });
    } catch (error) {
      if (stalled) {
        throw new TypeError(`no data for ${stallMs / 1000} s`);
      }
      throw error;
    } finally {
      deps.clearTimer(timer);
    }
  };
}

/**
 * @param {number} status
 * @returns {boolean} Whether a failed response may succeed when requested again.
 */
function isTransient(status) {
  return status === 408 || status === 429 || status >= 500;
}

/**
 * Fetches `url`, and again after each delay in `delaysMs` while the request fails with a network error or a
 * transient status (408, 429, 5xx). Other responses, including 404, are returned at once.
 *
 * Retries bypass the HTTP cache (`cache: 'reload'`). A failed integrity check is a network error, and the corrupt
 * body that failed it stays in the cache: a retry that read the cache would fail the same way, on every later visit
 * too (seen on itch.io from Chrome on Android, 2026-10-19).
 *
 * @param {string} url
 * @param {RequestInit} init
 * @param {FetchLike} fetchLike Usually `fetch`.
 * @param {(ms: number) => Promise<void>} sleep Usually `setTimeout`.
 * @param {readonly number[]} [delaysMs] One entry per retry.
 * @param {(attempt: number, delayMs: number, reason: string) => void} [onRetry] Called before each wait, with the
 *   1-based number of the retry that follows and why the request failed.
 * @returns {Promise<Response>} The last response. Rejects with the last network error.
 */
export async function fetchWithRetry(url, init, fetchLike, sleep, delaysMs = RETRY_DELAYS_MS, onRetry = () => {}) {
  for (let attempt = 0; ; attempt++) {
    const last = attempt >= delaysMs.length;
    let reason = '';
    try {
      const response = await fetchLike(url, attempt === 0 ? init : { ...init, cache: 'reload' });
      if (response.ok || last || !isTransient(response.status)) {
        return response;
      }
      reason = `HTTP ${response.status}`;
    } catch (error) {
      if (last) {
        throw error;
      }
      reason = String(error);
    }
    onRetry(attempt + 1, delaysMs[attempt], reason);
    await sleep(delaysMs[attempt]);
  }
}

/**
 * Returns a resource loader that downloads the runtime's files with `fetchWithRetry`. JS modules (`dotnetjs`) and the
 * boot manifest keep the default path: the runtime needs a URL for those, not a response.
 *
 * The runtime's default download sends `cache: 'no-cache'`, so every start revalidates every file. This loader uses
 * the normal HTTP cache instead: every runtime file name carries a content hash (`WasmFingerprintAssets`).
 *
 * @param {FetchLike} fetchLike
 * @param {(ms: number) => Promise<void>} sleep
 * @param {(name: string, attempt: number, delayMs: number, reason: string) => void} [onRetry] Called before each
 *   retry of the file `name` (see `fetchWithRetry`).
 * @returns {LoadBootResource}
 */
export function createResourceLoader(fetchLike, sleep, onRetry = () => {}) {
  return (type, name, defaultUri, integrity) => {
    if (type === 'dotnetjs' || type === 'manifest') {
      return undefined;
    }
    return fetchWithRetry(
      defaultUri,
      integrity ? { integrity } : {},
      fetchLike,
      sleep,
      RETRY_DELAYS_MS,
      (attempt, delayMs, reason) => onRetry(name, attempt, delayMs, reason),
    );
  };
}

/** Session storage key that marks a page reload done by `boot` to repair the runtime's cached modules. */
export const REPAIR_KEY = 'ld60.boot.repaired';

/**
 * @param {unknown} error Why startup failed.
 * @returns {boolean} Whether a JS module of the runtime failed to load: a failed integrity check (a corrupt cached
 *   copy) or a network error. The runtime imports its modules itself, so `createResourceLoader` cannot retry them.
 */
export function isModuleLoadFailure(error) {
  return error instanceof Error && error.message.includes('Failed to fetch dynamically imported module');
}

/**
 * Lists the runtime's JS modules from the page's import map: every target under `frameworkUrl`.
 *
 * @param {string | null} importMapJson The text of `<script type="importmap">`.
 * @param {string} frameworkUrl Absolute URL of the runtime folder, with a trailing `/`.
 * @param {string} baseUrl The page URL that import map targets are relative to.
 * @returns {string[]} Absolute URLs, each once.
 */
export function runtimeModuleUrls(importMapJson, frameworkUrl, baseUrl) {
  /** @type {unknown} */
  let map;
  try {
    map = JSON.parse(importMapJson ?? '');
  } catch {
    return [];
  }
  const imports = typeof map === 'object' && map !== null ? /** @type {{ imports?: unknown }} */ (map).imports : undefined;
  if (typeof imports !== 'object' || imports === null) {
    return [];
  }
  /** @type {Set<string>} */
  const urls = new Set();
  for (const target of Object.values(imports)) {
    if (typeof target === 'string') {
      const url = new URL(target, baseUrl).href;
      if (url.startsWith(frameworkUrl)) {
        urls.add(url);
      }
    }
  }
  return [...urls];
}

/**
 * Downloads `urls` again past the HTTP cache (`cache: 'reload'`), which replaces any corrupt cached copy. Failures
 * are ignored: the reload that follows reports them.
 *
 * @param {readonly string[]} urls
 * @param {FetchLike} fetchLike
 * @returns {Promise<void>}
 */
export async function refreshCache(urls, fetchLike) {
  await Promise.all(
    urls.map(async (url) => {
      try {
        const response = await fetchLike(url, { cache: 'reload' });
        await response.arrayBuffer();
      } catch {
        // The reload shows whether it helped.
      }
    }),
  );
}

/**
 * The part of `sessionStorage` that `repairOnce` uses. Its methods may throw (storage turned off).
 * @typedef {{ getItem(key: string): string | null, setItem(key: string, value: string): void }} RepairStore
 */

/**
 * Decides whether to repair the cache and reload after a failed start: only for a module load failure, and once per
 * browser tab, so a failure that a fresh download does not fix shows the error instead of reloading forever.
 *
 * @param {unknown} error
 * @param {RepairStore | null} store Usually `sessionStorage`; without one, never.
 * @returns {boolean} `true` after marking the repair in `store`.
 */
export function repairOnce(error, store) {
  if (store === null || !isModuleLoadFailure(error)) {
    return false;
  }
  try {
    if (store.getItem(REPAIR_KEY) !== null) {
      return false;
    }
    store.setItem(REPAIR_KEY, '1');
    return true;
  } catch {
    return false;
  }
}

/** @returns {RepairStore | null} */
function sessionStore() {
  try {
    return globalThis.sessionStorage;
  } catch {
    return null;
  }
}

/**
 * Downloads with `fetchWithRetry`, the default delays, and a report before each retry. Game assets use it
 * (`assets.js`), so a dropped connection costs a retry instead of a missing background.
 *
 * @param {FetchLike} fetchLike Usually `browserWatchedFetch()`.
 * @param {(ms: number) => Promise<void>} sleep
 * @param {(url: string, attempt: number, delayMs: number, reason: string) => void} onRetry
 * @returns {FetchLike}
 */
export function createRetryingFetch(fetchLike, sleep, onRetry) {
  return (url, init) =>
    fetchWithRetry(url, init, fetchLike, sleep, RETRY_DELAYS_MS, (attempt, delayMs, reason) =>
      onRetry(url, attempt, delayMs, reason),
    );
}

/** @returns {FetchLike} `fetch` behind `createWatchedFetch`, on the page's timers and Web Crypto. */
export function browserWatchedFetch() {
  return createWatchedFetch((url, init) => fetch(url, init), {
    digest: (algorithm, bytes) => crypto.subtle.digest(algorithm, bytes),
    setTimer: (callback, ms) => setTimeout(callback, ms),
    clearTimer: (timer) => clearTimeout(/** @type {number | undefined} */ (timer)),
  });
}

/**
 * @param {number} ms
 * @returns {Promise<void>}
 */
export function delay(ms) {
  return new Promise((resolve) => {
    setTimeout(resolve, ms);
  });
}

/**
 * @typedef {(url: string) => Promise<{ dotnet: DotnetHostBuilder }>} ImportRuntime
 */

/**
 * The part of an element that the status code touches, so tests can pass fakes.
 * @typedef {{ hidden: boolean, textContent: string | null }} StatusElement
 */

/**
 * Returns the absolute URL of `dotnet.js` in `frameworkRoot`.
 *
 * `import()` resolves relative URLs against the importing module (this file, under `_content/`), so the root is
 * resolved against the page instead. The result matches the page's import map keys, which redirect it to the
 * fingerprinted file.
 *
 * @param {string} frameworkRoot Runtime folder, relative to `baseUrl` or absolute. A trailing `/` is optional.
 * @param {string} baseUrl Usually `document.baseURI`.
 * @returns {string}
 */
export function resolveRuntimeUrl(frameworkRoot, baseUrl) {
  const root = frameworkRoot.endsWith('/') ? frameworkRoot : `${frameworkRoot}/`;
  return new URL('dotnet.js', new URL(root, baseUrl)).href;
}

/**
 * Imports the runtime from `runtimeUrl`, configures it, creates it and runs `Main`.
 *
 * @param {string} runtimeUrl Absolute URL of `dotnet.js`.
 * @param {ImportRuntime} importRuntime Usually `import()`.
 * @param {() => void} [onReady] Called once the runtime is downloaded and created, right before `Main` runs.
 * @param {(dotnet: DotnetHostBuilder) => DotnetHostBuilder} [configure] Applied to the builder before `create()`.
 * @returns {Promise<number>} The exit code of `Main`.
 */
export async function startRuntime(runtimeUrl, importRuntime, onReady = () => {}, configure = (dotnet) => dotnet) {
  const { dotnet } = await importRuntime(runtimeUrl);
  const { runMain } = await configure(dotnet).create();
  onReady();
  return runMain();
}

/**
 * Runs `start` with a status element: the element keeps its loading text until `start` reports ready, is hidden then,
 * and shows the error text if `start` fails, before or after ready (a game that throws in a frame ends `Main`).
 *
 * @template T
 * @param {(onReady: () => void) => Promise<T>} start Usually `startRuntime`.
 * @param {StatusElement | null} status Nothing is shown without one.
 * @param {() => string} [errorTextOf] The text to show on failure. Defaults to `getErrorText`.
 * @returns {Promise<T>} What `start` returns. Rejects with the error of `start`.
 */
export async function runWithStatus(start, status, errorTextOf = getErrorText) {
  try {
    return await start(() => {
      if (status !== null) {
        status.hidden = true;
      }
    });
  } catch (error) {
    if (status !== null) {
      status.textContent = errorTextOf();
      status.hidden = false;
    }
    throw error;
  }
}

/**
 * Starts the runtime from `frameworkRoot` (relative to the page) and runs `Main`, with loading and error text in the
 * element with id `statusId`, if the page has one. Runtime files are downloaded with retries (`createResourceLoader`).
 * Startup steps, retries and the runtime's downloads go to the load log (`load-log.js`). Errors and load timing go to
 * Sentry when `reportingConfig` holds a DSN (`reporting.js`). Product analytics go to GameAnalytics when
 * `analyticsConfig` holds the game's keys (`analytics.js`), with an error event when startup or the game fails and no
 * repair follows. If a runtime JS module
 * fails to load, `boot` downloads the runtime's modules again past the HTTP cache and reloads the page, once per tab
 * (`repairOnce`).
 *
 * @param {string} [frameworkRoot] Runtime folder. Defaults to the SDK's `./_framework/`.
 * @param {string} [statusId] Id of the status element.
 * @param {string} [reportingConfig] Reporting config file, relative to the page.
 * @param {string} [analyticsConfig] Analytics config file, relative to the page.
 * @returns {Promise<number>} The exit code of `Main`.
 */
export function boot(
  frameworkRoot = './_framework/',
  statusId = 'status',
  reportingConfig = './reporting.json',
  analyticsConfig = './analytics.json',
) {
  const log = getLoadLog();
  // Reporting and analytics are optional: blocked or broken, they never stop the game from starting.
  /** @type {Promise<Pick<Reporter, 'captureError' | 'idle'>>} */
  const reporterReady = startOptional(
    () => import('./reporting.js'),
    (module) => module.startReporting(new URL(reportingConfig, document.baseURI).href, log),
    NO_REPORTER,
  );
  const analyticsReady = startOptional(
    () => import('./analytics.js'),
    (module) => {
      module.startAnalytics(new URL(analyticsConfig, document.baseURI).href);
      return module;
    },
    null,
  );
  log.step(STEP.BOOT);
  return runWithStatus(
    (onReady) => {
      // "Loading..." stays until the game has drawn a frame: after the runtime starts, Main still imports the
      // platform's modules, and the canvas would be black meanwhile. From then on the game shows its own loading screen.
      log.subscribe((entry) => {
        if (entry.kind === 'step' && entry.message === STEP.FIRST_FRAME) {
          onReady();
        }
      });
      return startRuntime(
        resolveRuntimeUrl(frameworkRoot, document.baseURI),
        async (url) => {
          const runtime = await import(url);
          log.step(STEP.RUNTIME_IMPORTED);
          return runtime;
        },
        () => {
          log.downloads('/_framework/', 'runtime downloaded');
          log.step(STEP.RUNTIME_CREATED);
        },
        (dotnet) =>
          dotnet.withResourceLoader(
            createResourceLoader(
              browserWatchedFetch(),
              delay,
              (name, attempt, delayMs, reason) => log.retry(name, attempt, RETRY_DELAYS_MS.length, delayMs, reason),
            ),
          ),
      );
    },
    document.getElementById(statusId),
  ).catch(async (error) => {
    const reporter = await reporterReady;
    try {
      reporter.captureError(error, 'boot');
    } catch {
      // Reporting must never get in the way of the repair below.
    }
    if (repairOnce(error, sessionStore())) {
      const status = document.getElementById(statusId);
      if (status !== null) {
        status.textContent = 'Loading...';
      }
      log.step('repairing cached runtime modules, then reloading');
      const frameworkUrl = new URL(frameworkRoot.endsWith('/') ? frameworkRoot : `${frameworkRoot}/`, document.baseURI).href;
      const importMap = document.querySelector('script[type="importmap"]');
      await refreshCache(
        runtimeModuleUrls(importMap?.textContent ?? null, frameworkUrl, document.baseURI),
        (url, init) => fetch(url, init),
      );
      await Promise.race([reporter.idle, delay(REPORT_WAIT_MS)]);
      globalThis.location.reload();
    } else {
      try {
        (await analyticsReady)?.captureFatal(error);
      } catch {
        // Analytics must never take the error text down with it.
      }
    }
    throw error;
  });
}

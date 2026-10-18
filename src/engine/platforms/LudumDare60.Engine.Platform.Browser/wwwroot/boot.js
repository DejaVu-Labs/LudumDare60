// @ts-check

import { getLoadLog, STEP } from './load-log.js';
import { startReporting } from './reporting.js';

/**
 * Starts the .NET runtime and runs the app's `Main`. The host page's `main.js` calls `boot` with the folder that
 * holds the runtime (`dotnet.js` and the files it loads), so an app can ship several runtime builds side by side.
 * A page element with id `status` (optional) shows loading text until the runtime is ready, and an error message if
 * startup or the game fails.
 */

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
 * Waits before each retry of a failed download, in milliseconds: four retries over about 15 s. The runtime's own retry
 * gives up after about 100 ms, which a reset connection on a slow network outlasts (itch.io's CDN, seen 2026-10-19).
 */
export const RETRY_DELAYS_MS = Object.freeze([1000, 2000, 4000, 8000]);

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
      const response = await fetchLike(url, init);
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

/**
 * @param {number} ms
 * @returns {Promise<void>}
 */
function delay(ms) {
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
 * Sentry when `reportingConfig` holds a DSN (`reporting.js`).
 *
 * @param {string} [frameworkRoot] Runtime folder. Defaults to the SDK's `./_framework/`.
 * @param {string} [statusId] Id of the status element.
 * @param {string} [reportingConfig] Reporting config file, relative to the page.
 * @returns {Promise<number>} The exit code of `Main`.
 */
export function boot(frameworkRoot = './_framework/', statusId = 'status', reportingConfig = './reporting.json') {
  const log = getLoadLog();
  const reporter = startReporting(new URL(reportingConfig, document.baseURI).href, log);
  log.step(STEP.BOOT);
  return runWithStatus(
    (onReady) =>
      startRuntime(
        resolveRuntimeUrl(frameworkRoot, document.baseURI),
        async (url) => {
          const runtime = await import(url);
          log.step(STEP.RUNTIME_IMPORTED);
          return runtime;
        },
        () => {
          log.downloads('/_framework/', 'runtime downloaded');
          log.step(STEP.RUNTIME_CREATED);
          onReady();
        },
        (dotnet) =>
          dotnet.withResourceLoader(
            createResourceLoader(
              (url, init) => fetch(url, init),
              delay,
              (name, attempt, delayMs, reason) => log.retry(name, attempt, RETRY_DELAYS_MS.length, delayMs, reason),
            ),
          ),
      ),
    document.getElementById(statusId),
  ).catch((error) => {
    reporter.captureError(error, 'boot');
    throw error;
  });
}

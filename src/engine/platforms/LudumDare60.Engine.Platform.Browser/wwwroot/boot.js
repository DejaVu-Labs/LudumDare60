// @ts-check

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
 * @typedef {object} DotnetHostBuilder
 * @property {() => Promise<{ runMain: () => Promise<number> }>} create
 */

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
 * Imports the runtime from `runtimeUrl`, creates it and runs `Main`.
 *
 * @param {string} runtimeUrl Absolute URL of `dotnet.js`.
 * @param {ImportRuntime} importRuntime Usually `import()`.
 * @param {() => void} [onReady] Called once the runtime is downloaded and created, right before `Main` runs.
 * @returns {Promise<number>} The exit code of `Main`.
 */
export async function startRuntime(runtimeUrl, importRuntime, onReady = () => {}) {
  const { dotnet } = await importRuntime(runtimeUrl);
  const { runMain } = await dotnet.create();
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
 * element with id `statusId`, if the page has one.
 *
 * @param {string} [frameworkRoot] Runtime folder. Defaults to the SDK's `./_framework/`.
 * @param {string} [statusId] Id of the status element.
 * @returns {Promise<number>} The exit code of `Main`.
 */
export function boot(frameworkRoot = './_framework/', statusId = 'status') {
  return runWithStatus(
    (onReady) => startRuntime(resolveRuntimeUrl(frameworkRoot, document.baseURI), (url) => import(url), onReady),
    document.getElementById(statusId),
  );
}

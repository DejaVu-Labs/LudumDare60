// @ts-check

/**
 * Product analytics through the GameAnalytics SDK (`vendor/gameanalytics.js`). Analytics is off unless the page ships a
 * config file with the game's keys (`analytics.json`, see docs/tech/build-and-deploy.md#product-analytics); the SDK is
 * downloaded only then.
 *
 * Events come from .NET: `BrowserPlatform.Analytics` passes them to `addProgression` and `addDesign`. The SDK drops
 * events sent before its session starts (it waits for a reply from GameAnalytics), so this module queues them until
 * the SDK is ready. Custom dimension 01 is the kind of pointer the player last pressed with (`mouse`, `touch` or
 * `pen`). `boot.js` reports a game that stops on an error once, as a `Critical` error event.
 */

/**
 * @typedef {object} AnalyticsConfig
 * @property {string} gameKey The GameAnalytics game key: 32 letters and digits.
 * @property {string} secretKey The GameAnalytics secret key: 40 letters and digits.
 * @property {string} build Build version, at most 32 characters, e.g. `1.2.0` or `dev-42-abc1234`.
 */

/**
 * The part of the SDK's `gameanalytics.GameAnalytics` that this module calls.
 * @typedef {object} GameAnalyticsSdk
 * @property {(build: string) => void} configureBuild
 * @property {(values: string[]) => void} configureAvailableCustomDimensions01
 * @property {(gameKey: string, secretKey: string) => void} initialize
 * @property {(value: string) => void} setCustomDimension01
 * @property {(status: number, p1: string, p2?: string, p3?: string, score?: number) => void} addProgressionEvent
 * @property {(eventId: string, value?: number) => void} addDesignEvent
 * @property {(severity: number, message: string) => void} addErrorEvent
 * @property {(needsInitialized: boolean, warn: boolean) => boolean} [isSdkReady] Whether the session has started.
 *   Missing from the SDK's typings, but part of every 4.x and 5.x build; without it the SDK counts as ready.
 */

/**
 * @typedef {object} AnalyticsTimers
 * @property {(callback: () => void, ms: number) => unknown} setInterval
 * @property {(timer: unknown) => void} clearInterval
 */

/**
 * @typedef {object} Analytics
 * @property {(status: number, p1: string, p2?: string | null, p3?: string | null, score?: number) => void} progression
 * @property {(eventId: string, value?: number) => void} design
 * @property {(message: string) => void} fatal Sends one `Critical` error event per page; later calls do nothing.
 * @property {(pointerType: string) => void} setPointerType Sets custom dimension 01 when the type is in `POINTER_TYPES`.
 * @property {Promise<boolean>} started Resolves once the SDK is initialized (`true`), or analytics is off (`false`).
 */

/** `EGAProgressionStatus` values. */
export const PROGRESSION = Object.freeze({ START: 1, COMPLETE: 2, FAIL: 3 });

/** `EGAErrorSeverity.Critical`. */
export const SEVERITY_CRITICAL = 5;

/** Values of custom dimension 01: `PointerEvent.pointerType`. */
export const POINTER_TYPES = Object.freeze(['mouse', 'touch', 'pen']);

/** Events kept while the SDK starts. A session that never starts (an ad blocker) must not grow memory forever. */
export const MAX_QUEUED = 200;

/** How often the queue checks whether the SDK's session has started, in milliseconds. */
export const POLL_MS = 250;

/** The longest error message GameAnalytics takes. */
const MAX_ERROR_LENGTH = 8192;

/** One part of an event id, as GameAnalytics validates it. */
const ID_PART = /^[A-Za-z0-9\s\-_.()!?]{1,64}$/;

/**
 * @param {string} eventId
 * @returns {boolean} Whether GameAnalytics accepts `eventId` as a design event id: one to five valid parts joined by `:`.
 */
export function isValidDesignId(eventId) {
  if (typeof eventId !== 'string') {
    return false;
  }
  const parts = eventId.split(':');
  return parts.length <= 5 && parts.every((part) => ID_PART.test(part));
}

/**
 * @param {number} status
 * @param {string} p1
 * @param {string | null | undefined} p2
 * @param {string | null | undefined} p3
 * @returns {boolean} Whether GameAnalytics accepts the progression: a known status, then `p1`, `p1:p2` or `p1:p2:p3`.
 */
export function isValidProgression(status, p1, p2, p3) {
  if (status !== PROGRESSION.START && status !== PROGRESSION.COMPLETE && status !== PROGRESSION.FAIL) {
    return false;
  }
  if (typeof p1 !== 'string' || !ID_PART.test(p1)) {
    return false;
  }
  if (p2 == null) {
    return p3 == null;
  }
  return ID_PART.test(p2) && (p3 == null || ID_PART.test(p3));
}

/**
 * Validates a parsed `analytics.json`.
 *
 * @param {unknown} json
 * @returns {AnalyticsConfig | null} `null` when a key is missing or malformed, which turns analytics off.
 */
export function readConfig(json) {
  if (typeof json !== 'object' || json === null) {
    return null;
  }
  const { gameKey, secretKey, build } = /** @type {Record<string, unknown>} */ (json);
  if (typeof gameKey !== 'string' || !/^[A-Za-z0-9]{32}$/.test(gameKey)) {
    return null;
  }
  if (typeof secretKey !== 'string' || !/^[A-Za-z0-9]{40}$/.test(secretKey)) {
    return null;
  }
  return {
    gameKey,
    secretKey,
    build: typeof build === 'string' && build !== '' && build.length <= 32 ? build : 'unknown',
  };
}

/**
 * Fetches and validates the analytics config.
 *
 * @param {string} url
 * @param {(url: string, init: RequestInit) => Promise<Response>} fetchLike
 * @returns {Promise<AnalyticsConfig | null>} `null` when the file is missing, unreadable or has no valid keys.
 */
export async function loadConfig(url, fetchLike) {
  try {
    const response = await fetchLike(url, { cache: 'no-cache' });
    return response.ok ? readConfig(await response.json()) : null;
  } catch {
    return null;
  }
}

/**
 * Creates the page's analytics. The SDK is loaded only once `config` resolves to a config; with `null`, or when the
 * SDK fails to load, every event is dropped.
 *
 * @param {Promise<AnalyticsConfig | null>} config
 * @param {() => Promise<GameAnalyticsSdk>} loadSdk Usually imports `vendor/gameanalytics.js`.
 * @param {AnalyticsTimers} timers Usually the page's `setInterval` and `clearInterval`.
 * @returns {Analytics}
 */
export function createAnalytics(config, loadSdk, timers) {
  /** @type {GameAnalyticsSdk | null} */
  let sdk = null;
  let off = false;
  let fatalSent = false;
  /** @type {string | null} */
  let pointerType = null;
  /** @type {((sdk: GameAnalyticsSdk) => void)[]} */
  const queue = [];
  /** @type {unknown} */
  let timer = null;

  function isReady() {
    return sdk !== null && (typeof sdk.isSdkReady !== 'function' || sdk.isSdkReady(true, false));
  }

  function stopPolling() {
    if (timer !== null) {
      timers.clearInterval(timer);
      timer = null;
    }
  }

  function flush() {
    if (!isReady()) {
      if (timer === null && sdk !== null) {
        timer = timers.setInterval(flush, POLL_MS);
      }
      return;
    }
    stopPolling();
    for (const command of queue.splice(0)) {
      send(command);
    }
  }

  /** @param {(sdk: GameAnalyticsSdk) => void} command */
  function send(command) {
    try {
      command(/** @type {GameAnalyticsSdk} */ (sdk));
    } catch {
      // Analytics must never take the game down.
    }
  }

  /** @param {(sdk: GameAnalyticsSdk) => void} command */
  function run(command) {
    if (off) {
      return;
    }
    if (queue.length === 0 && isReady()) {
      send(command);
      return;
    }
    if (queue.length < MAX_QUEUED) {
      queue.push(command);
    }
    if (sdk !== null) {
      flush();
    }
  }

  function turnOff() {
    off = true;
    queue.length = 0;
    stopPolling();
  }

  const started = (async () => {
    const settings = await config.catch(() => null);
    if (settings === null) {
      turnOff();
      return false;
    }
    try {
      const loaded = await loadSdk();
      // Build and dimensions must be set before `initialize`.
      loaded.configureBuild(settings.build);
      loaded.configureAvailableCustomDimensions01([...POINTER_TYPES]);
      loaded.initialize(settings.gameKey, settings.secretKey);
      if (pointerType !== null) {
        loaded.setCustomDimension01(pointerType);
      }
      sdk = loaded;
    } catch (error) {
      console.warn('[analytics] GameAnalytics did not start:', error);
      turnOff();
      return false;
    }
    flush();
    return true;
  })();

  return {
    progression(status, p1, p2, p3, score) {
      if (!isValidProgression(status, p1, p2, p3)) {
        console.warn(`[analytics] invalid progression event dropped: ${status} ${p1}:${p2 ?? ''}:${p3 ?? ''}`);
        return;
      }
      run((ga) => ga.addProgressionEvent(status, p1, p2 ?? undefined, p3 ?? undefined, score));
    },

    design(eventId, value) {
      if (!isValidDesignId(eventId)) {
        console.warn(`[analytics] invalid design event id dropped: ${eventId}`);
        return;
      }
      run((ga) => ga.addDesignEvent(eventId, value));
    },

    fatal(message) {
      if (fatalSent) {
        return;
      }
      fatalSent = true;
      const text = String(message).slice(0, MAX_ERROR_LENGTH);
      run((ga) => ga.addErrorEvent(SEVERITY_CRITICAL, text));
    },

    setPointerType(type) {
      if (!POINTER_TYPES.includes(type) || type === pointerType) {
        return;
      }
      pointerType = type;
      if (sdk !== null && !off) {
        send((ga) => ga.setCustomDimension01(type));
      }
    },

    started,
  };
}

/** @type {Analytics | null} */
let pageAnalytics = null;

/**
 * Sends a progression event to the page's analytics (see `startAnalytics`). Bound from C# with `[JSImport]`
 * (`AnalyticsInterop.AddProgression`). Does nothing when analytics was not started.
 *
 * @param {number} status `PROGRESSION.START`, `COMPLETE` or `FAIL`.
 * @param {string} p1
 * @param {string | null} p2
 * @param {string | null} p3
 * @param {boolean} hasScore
 * @param {number} score Ignored without `hasScore`.
 */
export function addProgression(status, p1, p2, p3, hasScore, score) {
  pageAnalytics?.progression(status, p1, p2, p3, hasScore ? score : undefined);
}

/**
 * Sends a design event to the page's analytics. Bound from C# (`AnalyticsInterop.AddDesign`).
 *
 * @param {string} eventId
 * @param {boolean} hasValue
 * @param {number} value Ignored without `hasValue`.
 */
export function addDesign(eventId, hasValue, value) {
  pageAnalytics?.design(eventId, hasValue ? value : undefined);
}

/**
 * Reports that the game stopped on an error, once per page. `boot.js` calls it.
 *
 * @param {unknown} error
 */
export function captureFatal(error) {
  const text = error instanceof Error ? `${error.name}: ${error.message}` : String(error);
  pageAnalytics?.fatal(text);
}

/**
 * Starts analytics for the page: reads the config at `configUrl` and, when it has keys, imports the SDK and starts a
 * session. Pointer presses anywhere on the page set custom dimension 01.
 *
 * @param {string} configUrl Absolute URL of `analytics.json`.
 * @returns {Analytics}
 */
export function startAnalytics(configUrl) {
  const analytics = createAnalytics(
    loadConfig(configUrl, (url, init) => globalThis.fetch(url, init)),
    async () => (await import('./vendor/gameanalytics.js')).gameanalytics.GameAnalytics,
    {
      setInterval: (callback, ms) => globalThis.setInterval(callback, ms),
      clearInterval: (timer) => globalThis.clearInterval(/** @type {number | undefined} */ (timer)),
    },
  );
  globalThis.addEventListener(
    'pointerdown',
    (event) => analytics.setPointerType(/** @type {PointerEvent} */ (event).pointerType),
    { capture: true, passive: true },
  );
  pageAnalytics = analytics;
  return analytics;
}

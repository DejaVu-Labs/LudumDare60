// @ts-check

/**
 * Error and load-timing reports to Sentry, without the Sentry SDK: events are written in Sentry's envelope format and
 * posted with one `fetch` each. Reporting is off unless the page ships a config file with a DSN (`reporting.json`, see
 * docs/tech/build-and-deploy.md#error-and-load-reports).
 *
 * Errors: uncaught errors and unhandled promise rejections (a failed runtime download, a .NET `ManagedError`), and
 * .NET errors that `BrowserPlatform.ReportError` passes to `captureManaged` (failed asset loads, unhandled and
 * unobserved exceptions). At most `MAX_ERRORS` per page and each message once, with the load log's lines as
 * breadcrumbs.
 * Load timing: one `load` transaction per page, sent once the first frame ran and the first asset batch finished, or
 * on `pagehide` before that (status `cancelled`: the player left during loading). Switching tabs sends nothing.
 */

import { STEP } from './load-log.js';

/** @typedef {import('./load-log.js').LoadLog} LoadLog */
/** @typedef {import('./load-log.js').LoadLogEntry} LoadLogEntry */
/** @typedef {import('./load-log.js').LoadSnapshot} LoadSnapshot */
/** @typedef {import('./load-log.js').ResourceEntry} ResourceEntry */

/**
 * @typedef {object} ReportingConfig
 * @property {string} dsn Sentry project DSN: `https://<public key>@<host>/<project id>`.
 * @property {string} release Build version, e.g. `1.2.0` or `dev-42-abc1234`.
 * @property {string} environment `release` or `dev`.
 * @property {number} loadSampleRate Share of pages that send load timing, from 0 to 1.
 */

/**
 * @typedef {object} ParsedDsn
 * @property {string} envelopeUrl The project's envelope endpoint, with the public key in the query.
 */

/**
 * A stack frame in Sentry's format.
 * @typedef {{ function?: string, filename?: string, lineno?: number, colno?: number, in_app: boolean }} Frame
 */

/**
 * @typedef {object} ReporterDeps
 * @property {(url: string, body: string) => Promise<unknown>} send Posts an envelope. Failures are ignored.
 * @property {{ now(): number, timeOrigin: number, getEntriesByType(type: 'resource'): readonly ResourceEntry[] }} clock
 *   Usually `performance`.
 * @property {() => number} random A number in [0, 1), for ids and sampling. Usually `Math.random`.
 * @property {string} pageUrl
 * @property {string} userAgent
 */

/**
 * @typedef {object} Reporter
 * @property {(error: unknown, mechanism: string) => void} captureError Reports an uncaught JS error.
 * @property {(error: { type: string, value: string, frames: Frame[] }, mechanism: string, handled: boolean, extra?: Record<string, string>) => void} captureException
 *   Reports an error that is already described, with optional extra data shown on the event.
 * @property {(entry: LoadLogEntry) => void} addBreadcrumb
 * @property {(snapshot: LoadSnapshot, completed: boolean) => void} sendLoadTiming Sends once; later calls do nothing.
 * @property {Promise<void>} idle Resolves when every report so far was handed to `send` or dropped. For tests.
 */

/** Errors sent per page at most. A broken frame loop or a missing file must not use up the Sentry quota. */
export const MAX_ERRORS = 10;

/** Breadcrumbs kept for the next error. */
export const MAX_BREADCRUMBS = 30;

const SDK = Object.freeze({ name: 'ludumdare60.reporting', version: '1.0.0' });

/**
 * @param {string} dsn
 * @returns {ParsedDsn | null} `null` for anything that is not an HTTPS Sentry DSN.
 */
export function parseDsn(dsn) {
  let url;
  try {
    url = new URL(dsn);
  } catch {
    return null;
  }
  const segments = url.pathname.split('/').filter((segment) => segment !== '');
  const projectId = segments.pop();
  if (url.protocol !== 'https:' || url.username === '' || projectId === undefined || !/^\d+$/.test(projectId)) {
    return null;
  }
  const prefix = segments.length > 0 ? `/${segments.join('/')}` : '';
  const query = `sentry_key=${encodeURIComponent(url.username)}&sentry_version=7&sentry_client=${SDK.name}%2F${SDK.version}`;
  return { envelopeUrl: `https://${url.host}${prefix}/api/${projectId}/envelope/?${query}` };
}

/**
 * Validates a parsed `reporting.json`.
 *
 * @param {unknown} json
 * @returns {ReportingConfig | null} `null` when there is no usable DSN, which turns reporting off.
 */
export function readConfig(json) {
  if (typeof json !== 'object' || json === null) {
    return null;
  }
  const { dsn, release, environment, loadSampleRate } = /** @type {Record<string, unknown>} */ (json);
  if (typeof dsn !== 'string' || parseDsn(dsn) === null) {
    return null;
  }
  return {
    dsn,
    release: typeof release === 'string' && release !== '' ? release : 'unknown',
    environment: typeof environment === 'string' && environment !== '' ? environment : 'production',
    loadSampleRate:
      typeof loadSampleRate === 'number' && loadSampleRate >= 0 && loadSampleRate <= 1 ? loadSampleRate : 1,
  };
}

/**
 * @param {number} length Number of hex digits.
 * @param {() => number} random
 * @returns {string}
 */
export function randomHex(length, random) {
  let hex = '';
  for (let i = 0; i < length; i++) {
    hex += Math.floor(random() * 16).toString(16);
  }
  return hex;
}

const V8_FRAME = /^\s*at (?:async )?(?:(.+?) \()?(.+?):(\d+):(\d+)\)?$/;
const GECKO_FRAME = /^\s*(.*?)@(.+?):(\d+):(\d+)$/;
const DOTNET_FRAME = /^\s*at ([^\s(]+\(.*?\))(?: in (.+):line (\d+))?\s*$/;

/**
 * Namespaces of the .NET runtime and its libraries: their frames are not the game's code.
 * @param {string} method
 */
function isFrameworkMethod(method) {
  return method.startsWith('System.') || method.startsWith('Microsoft.') || method.startsWith('Interop.');
}

/**
 * Parses a Chrome, Firefox or Safari `error.stack`, or a .NET stack trace (`   at Type.Method(Args) in File:line N`),
 * which is also what a `ManagedError`'s stack ends with.
 *
 * @param {string | undefined} stack
 * @returns {Frame[]} Oldest call first, as Sentry expects. Lines that are not frames are skipped.
 */
export function parseStack(stack) {
  if (typeof stack !== 'string') {
    return [];
  }
  /** @type {Frame[]} */
  const frames = [];
  for (const line of stack.split('\n')) {
    const match = V8_FRAME.exec(line) ?? GECKO_FRAME.exec(line);
    if (match === null) {
      const dotnet = DOTNET_FRAME.exec(line);
      if (dotnet !== null) {
        /** @type {Frame} */
        const frame = { function: dotnet[1], in_app: !isFrameworkMethod(dotnet[1]) };
        if (dotnet[2] !== undefined) {
          frame.filename = dotnet[2];
          frame.lineno = Number(dotnet[3]);
        }
        frames.push(frame);
      }
      continue;
    }
    /** @type {Frame} */
    const frame = {
      filename: match[2],
      lineno: Number(match[3]),
      colno: Number(match[4]),
      in_app: !match[2].includes('/_framework/'),
    };
    if (match[1]) {
      frame.function = match[1];
    }
    frames.push(frame);
  }
  return frames.reverse();
}

/**
 * @param {unknown} error An `Error`, or anything else a promise was rejected with.
 * @returns {{ type: string, value: string, frames: Frame[] }}
 */
export function describeError(error) {
  if (error instanceof Error) {
    return { type: error.name || 'Error', value: error.message, frames: parseStack(error.stack) };
  }
  let value;
  try {
    value = typeof error === 'string' ? error : JSON.stringify(error) ?? String(error);
  } catch {
    value = String(error);
  }
  return { type: 'NonError', value, frames: [] };
}

/**
 * Builds a Sentry envelope: a header line, then an item header line and a payload line per item.
 *
 * @param {string} dsn
 * @param {number} sentAtMs Epoch milliseconds.
 * @param {string} eventId
 * @param {'event' | 'transaction'} type
 * @param {object} payload
 * @returns {string}
 */
export function envelope(dsn, sentAtMs, eventId, type, payload) {
  const header = { event_id: eventId, sent_at: new Date(sentAtMs).toISOString(), dsn, sdk: SDK };
  return `${JSON.stringify(header)}\n${JSON.stringify({ type })}\n${JSON.stringify(payload)}\n`;
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
 * Builds the `load` transaction: one span per startup phase the snapshot has both ends of, one span per runtime file,
 * and the step times as measurements.
 *
 * @param {LoadSnapshot} snapshot
 * @param {readonly ResourceEntry[]} resources The page's resource timing entries.
 * @param {number} endTime When the transaction ends, in milliseconds since the page started loading.
 * @param {boolean} completed `false` when the page was hidden before the game finished loading.
 * @param {number} timeOrigin `performance.timeOrigin`.
 * @param {() => number} random
 * @returns {object} The transaction payload, without the fields every event shares.
 */
export function loadTransaction(snapshot, resources, endTime, completed, timeOrigin, random) {
  const traceId = randomHex(32, random);
  const rootSpanId = randomHex(16, random);
  /** @param {number} ms */
  const at = (ms) => (timeOrigin + ms) / 1000;

  const runtimeFiles = resources.filter((entry) => entry.name.includes('/_framework/'));
  let downloadEnd = -1;
  let transferred = 0;
  for (const entry of runtimeFiles) {
    downloadEnd = Math.max(downloadEnd, entry.responseEnd);
    transferred += entry.transferSize;
  }

  const { steps, firstAssets } = snapshot;
  const imported = steps[STEP.RUNTIME_IMPORTED];
  /** @type {[string, string, number | undefined, number | undefined][]} */
  const phases = [
    ['browser', 'page and main.js', 0, steps[STEP.BOOT]],
    ['runtime.script', 'import dotnet.js', steps[STEP.BOOT], imported],
    ['runtime.download', 'runtime files', imported, downloadEnd >= 0 ? downloadEnd : undefined],
    ['runtime.create', 'compile and start the runtime', downloadEnd >= 0 ? downloadEnd : undefined, steps[STEP.RUNTIME_CREATED]],
    ['game.start', 'Main to the first frame', steps[STEP.RUNTIME_CREATED], steps[STEP.FIRST_FRAME]],
    ['game.assets', 'first asset batch', firstAssets?.start, firstAssets?.end],
  ];

  /** @type {object[]} */
  const spans = [];
  for (const [op, description, start, end] of phases) {
    if (start !== undefined && end !== undefined && end >= start) {
      spans.push({
        trace_id: traceId,
        span_id: randomHex(16, random),
        parent_span_id: rootSpanId,
        op,
        description,
        start_timestamp: at(start),
        timestamp: at(end),
      });
    }
  }
  for (const entry of runtimeFiles) {
    spans.push({
      trace_id: traceId,
      span_id: randomHex(16, random),
      parent_span_id: rootSpanId,
      op: 'resource.wasm',
      description: fileName(entry.name),
      start_timestamp: at(entry.startTime),
      timestamp: at(entry.responseEnd),
      data: {
        'http.response_transfer_size': entry.transferSize,
        'http.decoded_response_content_length': entry.decodedBodySize,
      },
    });
  }

  /** @type {Record<string, { value: number, unit: string }>} */
  const measurements = {
    retries: { value: snapshot.retries, unit: 'none' },
    runtime_transfer: { value: transferred, unit: 'byte' },
  };
  /** @type {[string, number | undefined][]} */
  const marks = [
    ['boot', steps[STEP.BOOT]],
    ['runtime_downloaded', downloadEnd >= 0 ? downloadEnd : undefined],
    ['runtime_created', steps[STEP.RUNTIME_CREATED]],
    ['first_frame', steps[STEP.FIRST_FRAME]],
    ['assets_loaded', firstAssets?.end],
  ];
  for (const [name, value] of marks) {
    if (value !== undefined) {
      measurements[name] = { value: Math.round(value), unit: 'millisecond' };
    }
  }

  return {
    type: 'transaction',
    transaction: 'load',
    transaction_info: { source: 'custom' },
    start_timestamp: at(0),
    timestamp: at(endTime),
    contexts: {
      trace: { trace_id: traceId, span_id: rootSpanId, op: 'pageload', status: completed ? 'ok' : 'cancelled' },
    },
    spans,
    measurements,
    tags: { 'load.completed': String(completed), 'load.retried': String(snapshot.retries > 0) },
  };
}

/**
 * Creates a reporter. Reports made before `config` resolves wait for it; with a `null` config they are dropped.
 *
 * @param {Promise<ReportingConfig | null>} config
 * @param {ReporterDeps} deps
 * @returns {Reporter}
 */
export function createReporter(config, deps) {
  let errorCount = 0;
  let loadSent = false;
  /** @type {Set<string>} */
  const seen = new Set();
  /** @type {object[]} */
  const breadcrumbs = [];
  /** @type {Promise<void>} */
  let idle = Promise.resolve();

  /**
   * Hands a payload to `send` once the config is known.
   * @param {'event' | 'transaction'} type
   * @param {number} time When it happened, in milliseconds since the page started loading.
   * @param {(config: ReportingConfig) => object | null} build
   */
  function deliver(type, time, build) {
    idle = idle.then(async () => {
      const settings = await config.catch(() => null);
      const dsn = settings === null ? null : parseDsn(settings.dsn);
      if (settings === null || dsn === null) {
        return;
      }
      const payload = build(settings);
      if (payload === null) {
        return;
      }
      const eventId = randomHex(32, deps.random);
      const event = {
        event_id: eventId,
        timestamp: (deps.clock.timeOrigin + time) / 1000,
        platform: 'javascript',
        release: settings.release,
        environment: settings.environment,
        sdk: SDK,
        request: { url: deps.pageUrl, headers: { 'User-Agent': deps.userAgent } },
        ...payload,
      };
      const sentAt = deps.clock.timeOrigin + deps.clock.now();
      try {
        await deps.send(dsn.envelopeUrl, envelope(settings.dsn, sentAt, eventId, type, event));
      } catch {
        // A blocked or failed report is not worth a console line of its own.
      }
    }).catch(() => {
      // A report that cannot even be built is dropped; the chain stays usable for the next one.
    });
  }

  /**
   * @param {{ type: string, value: string, frames: Frame[] }} error
   * @param {string} mechanism
   * @param {boolean} handled
   * @param {Record<string, string>} [extra]
   */
  function captureException({ type, value, frames }, mechanism, handled, extra) {
    const key = `${type}: ${value}`;
    if (errorCount >= MAX_ERRORS || seen.has(key)) {
      return;
    }
    errorCount++;
    seen.add(key);
    const crumbs = breadcrumbs.slice();
    deliver('event', deps.clock.now(), () => ({
      level: 'error',
      exception: {
        values: [
          {
            type,
            value,
            ...(frames.length > 0 ? { stacktrace: { frames } } : {}),
            mechanism: { type: mechanism, handled },
          },
        ],
      },
      breadcrumbs: { values: crumbs },
      ...(extra === undefined ? {} : { extra }),
    }));
  }

  return {
    captureError(error, mechanism) {
      captureException(describeError(error), mechanism, false);
    },

    captureException,

    addBreadcrumb(entry) {
      breadcrumbs.push({
        timestamp: (deps.clock.timeOrigin + entry.time) / 1000,
        category: `load.${entry.kind}`,
        message: entry.message,
        level: entry.level,
      });
      if (breadcrumbs.length > MAX_BREADCRUMBS) {
        breadcrumbs.shift();
      }
    },

    sendLoadTiming(snapshot, completed) {
      if (loadSent) {
        return;
      }
      loadSent = true;
      const endTime = deps.clock.now();
      const resources = deps.clock.getEntriesByType('resource');
      const sampled = deps.random();
      deliver('transaction', endTime, (settings) =>
        sampled < settings.loadSampleRate
          ? loadTransaction(snapshot, resources, endTime, completed, deps.clock.timeOrigin, deps.random)
          : null,
      );
    },

    get idle() {
      return idle;
    },
  };
}

/**
 * Fetches and validates the reporting config.
 *
 * @param {string} url
 * @param {(url: string, init: RequestInit) => Promise<Response>} fetchLike
 * @returns {Promise<ReportingConfig | null>} `null` when the file is missing, unreadable or has no DSN.
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
 * @param {LoadSnapshot} snapshot
 * @returns {boolean} Whether the game has drawn its first frame and finished its first asset batch.
 */
export function isLoaded(snapshot) {
  return snapshot.steps[STEP.FIRST_FRAME] !== undefined && snapshot.firstAssets !== null;
}

/** @type {Reporter | null} */
let pageReporter = null;

/**
 * Reports a .NET error to the page's reporter (see `startReporting`). Bound from C# with `[JSImport]`
 * (`ReportingInterop.Capture`). Does nothing when reporting was not started.
 *
 * @param {string} type Full name of the exception type.
 * @param {string} message
 * @param {string | null} stackTrace The managed stack trace, if any.
 * @param {string} details `Exception.ToString()`, with inner exceptions; sent as extra data.
 * @param {boolean} handled Whether the game went on after the error.
 */
export function captureManaged(type, message, stackTrace, details, handled) {
  pageReporter?.captureException(
    { type, value: message, frames: parseStack(stackTrace ?? undefined) },
    handled ? 'dotnet.handled' : 'dotnet.unhandled',
    handled,
    { dotnet: details },
  );
}

/**
 * Starts reporting for the page: reads the config at `configUrl`, listens for uncaught errors and unhandled
 * rejections, turns the load log's lines into breadcrumbs, and sends load timing once loading finishes or the page
 * goes away.
 *
 * @param {string} configUrl Absolute URL of `reporting.json`.
 * @param {LoadLog} log
 * @returns {Reporter}
 */
export function startReporting(configUrl, log) {
  const reporter = createReporter(
    loadConfig(configUrl, (url, init) => globalThis.fetch(url, init)),
    {
      send: (url, body) => globalThis.fetch(url, { method: 'POST', body, keepalive: true }),
      clock: globalThis.performance,
      random: Math.random,
      pageUrl: globalThis.location.href,
      userAgent: globalThis.navigator.userAgent,
    },
  );

  globalThis.addEventListener('error', (event) => {
    reporter.captureError(event.error ?? event.message, 'onerror');
  });
  globalThis.addEventListener('unhandledrejection', (event) => {
    reporter.captureError(event.reason, 'onunhandledrejection');
  });

  log.subscribe((entry) => {
    reporter.addBreadcrumb(entry);
    const snapshot = log.snapshot();
    if (isLoaded(snapshot)) {
      reporter.sendLoadTiming(snapshot, true);
    }
  });
  globalThis.addEventListener('pagehide', () => reporter.sendLoadTiming(log.snapshot(), false));

  pageReporter = reporter;
  return reporter;
}

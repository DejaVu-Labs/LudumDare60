// @ts-check
import { test } from 'node:test';
import assert from 'node:assert/strict';

import { STEP } from '../../src/engine/platforms/LudumDare60.Engine.Platform.Browser/wwwroot/load-log.js';
import {
  createReporter,
  describeError,
  envelope,
  isLoaded,
  loadConfig,
  loadTransaction,
  MAX_BREADCRUMBS,
  MAX_ERRORS,
  parseDsn,
  parseStack,
  randomHex,
  readConfig,
} from '../../src/engine/platforms/LudumDare60.Engine.Platform.Browser/wwwroot/reporting.js';

const DSN = 'https://abc123@o42.ingest.sentry.io/4507';
const ENVELOPE_URL =
  'https://o42.ingest.sentry.io/api/4507/envelope/?sentry_key=abc123&sentry_version=7&sentry_client=ludumdare60.reporting%2F1.0.0';
const TIME_ORIGIN = 1_790_000_000_000;

/** @returns {() => number} A repeating, predictable stand-in for `Math.random`. */
function fakeRandom() {
  let i = 0;
  return () => {
    i = (i + 1) % 16;
    return i / 16;
  };
}

/**
 * @param {import('../../src/engine/platforms/LudumDare60.Engine.Platform.Browser/wwwroot/reporting.js').ReportingConfig | null} config
 * @param {{ random?: () => number, resources?: import('../../src/engine/platforms/LudumDare60.Engine.Platform.Browser/wwwroot/load-log.js').ResourceEntry[] }} [options]
 */
function createTestReporter(config, options = {}) {
  /** @type {{ url: string, lines: any[] }[]} */
  const sent = [];
  const clock = {
    time: 0,
    timeOrigin: TIME_ORIGIN,
    now() {
      return clock.time;
    },
    getEntriesByType() {
      return options.resources ?? [];
    },
  };
  const reporter = createReporter(Promise.resolve(config), {
    send: async (url, body) => {
      sent.push({ url, lines: body.trimEnd().split('\n').map((line) => JSON.parse(line)) });
    },
    clock,
    random: options.random ?? fakeRandom(),
    pageUrl: 'https://html-classic.itch.zone/html/1/index.html',
    userAgent: 'TestAgent/1',
  });
  return { reporter, sent, clock };
}

/** @type {import('../../src/engine/platforms/LudumDare60.Engine.Platform.Browser/wwwroot/reporting.js').ReportingConfig} */
const config = { dsn: DSN, release: 'dev-7-abc1234', environment: 'dev', loadSampleRate: 1 };

test('parseDsn_SentryDsn_EnvelopeUrlWithKey', () => {
  assert.deepEqual(parseDsn(DSN), { envelopeUrl: ENVELOPE_URL });
});

test('parseDsn_PathPrefix_Kept', () => {
  assert.equal(
    parseDsn('https://k@sentry.example.com/relay/12')?.envelopeUrl.split('?')[0],
    'https://sentry.example.com/relay/api/12/envelope/',
  );
});

test('parseDsn_NotUsable_Null', () => {
  assert.equal(parseDsn('not a url'), null);
  assert.equal(parseDsn('http://k@sentry.example.com/12'), null);
  assert.equal(parseDsn('https://sentry.example.com/12'), null);
  assert.equal(parseDsn('https://k@sentry.example.com/'), null);
});

test('readConfig_FullConfig_Kept', () => {
  assert.deepEqual(readConfig({ dsn: DSN, release: '1.2.0', environment: 'release', loadSampleRate: 0.25 }), {
    dsn: DSN,
    release: '1.2.0',
    environment: 'release',
    loadSampleRate: 0.25,
  });
});

test('readConfig_OnlyDsn_Defaults', () => {
  assert.deepEqual(readConfig({ dsn: DSN }), { dsn: DSN, release: 'unknown', environment: 'production', loadSampleRate: 1 });
});

test('readConfig_NoDsn_Null', () => {
  assert.equal(readConfig({}), null);
  assert.equal(readConfig(null), null);
  assert.equal(readConfig({ dsn: 'nope' }), null);
});

test('readConfig_SampleRateOutOfRange_One', () => {
  assert.equal(readConfig({ dsn: DSN, loadSampleRate: 2 })?.loadSampleRate, 1);
});

test('loadConfig_Missing_Null', async () => {
  assert.equal(await loadConfig('https://x/reporting.json', async () => new Response(null, { status: 404 })), null);
});

test('loadConfig_NetworkError_Null', async () => {
  const failing = async () => {
    throw new TypeError('Failed to fetch');
  };
  assert.equal(await loadConfig('https://x/reporting.json', failing), null);
});

test('loadConfig_Valid_ReadsWithoutCache', async () => {
  /** @type {RequestInit | undefined} */
  let seen;
  const result = await loadConfig('https://x/reporting.json', async (_url, init) => {
    seen = init;
    return Response.json({ dsn: DSN });
  });

  assert.equal(result?.dsn, DSN);
  assert.deepEqual(seen, { cache: 'no-cache' });
});

test('randomHex_Length', () => {
  assert.match(randomHex(32, Math.random), /^[0-9a-f]{32}$/);
});

test('parseStack_Chrome_OldestFirst', () => {
  const stack = [
    'TypeError: Failed to fetch',
    '    at Object.k [as fetch_like] (https://x/_framework/dotnet.abc.js:4:4631)',
    '    at async start (https://x/_content/p/boot.abc.js:10:5)',
    '    at https://x/main.abc.js:3:1',
  ].join('\n');

  assert.deepEqual(parseStack(stack), [
    { filename: 'https://x/main.abc.js', lineno: 3, colno: 1, in_app: true },
    { function: 'start', filename: 'https://x/_content/p/boot.abc.js', lineno: 10, colno: 5, in_app: true },
    { function: 'Object.k [as fetch_like]', filename: 'https://x/_framework/dotnet.abc.js', lineno: 4, colno: 4631, in_app: false },
  ]);
});

test('parseStack_Firefox_OldestFirst', () => {
  const stack = 'draw@https://x/webgl2.js:20:7\n@https://x/main.js:3:1\n';

  assert.deepEqual(parseStack(stack), [
    { filename: 'https://x/main.js', lineno: 3, colno: 1, in_app: true },
    { function: 'draw', filename: 'https://x/webgl2.js', lineno: 20, colno: 7, in_app: true },
  ]);
});

test('parseStack_NoStack_Empty', () => {
  assert.deepEqual(parseStack(undefined), []);
});

test('describeError_Error_NameAndMessage', () => {
  const error = new RangeError('too far');
  error.stack = 'RangeError: too far\n    at f (https://x/a.js:1:2)';

  assert.deepEqual(describeError(error), {
    type: 'RangeError',
    value: 'too far',
    frames: [{ function: 'f', filename: 'https://x/a.js', lineno: 1, colno: 2, in_app: true }],
  });
});

test('describeError_NonError_Serialized', () => {
  assert.deepEqual(describeError('gone'), { type: 'NonError', value: 'gone', frames: [] });
  assert.deepEqual(describeError({ code: 3 }), { type: 'NonError', value: '{"code":3}', frames: [] });
  assert.deepEqual(describeError(undefined), { type: 'NonError', value: 'undefined', frames: [] });
});

test('envelope_ThreeJsonLines', () => {
  const lines = envelope(DSN, Date.UTC(2026, 9, 19), 'e'.repeat(32), 'event', { level: 'error' }).trimEnd().split('\n');

  assert.deepEqual(lines.map((line) => JSON.parse(line)), [
    { event_id: 'e'.repeat(32), sent_at: '2026-10-19T00:00:00.000Z', dsn: DSN, sdk: { name: 'ludumdare60.reporting', version: '1.0.0' } },
    { type: 'event' },
    { level: 'error' },
  ]);
});

test('captureError_Configured_SendsEventWithBreadcrumbs', async () => {
  const { reporter, sent, clock } = createTestReporter(config);
  reporter.addBreadcrumb({ kind: 'step', message: 'boot', level: 'info', time: 200 });
  clock.time = 1500;

  reporter.captureError(new TypeError('Failed to fetch'), 'onunhandledrejection');
  await reporter.idle;

  assert.equal(sent.length, 1);
  assert.equal(sent[0].url, ENVELOPE_URL);
  const [header, item, event] = sent[0].lines;
  assert.equal(header.event_id, event.event_id);
  assert.deepEqual(item, { type: 'event' });
  assert.equal(event.level, 'error');
  assert.equal(event.release, 'dev-7-abc1234');
  assert.equal(event.environment, 'dev');
  assert.equal(event.timestamp, (TIME_ORIGIN + 1500) / 1000);
  assert.deepEqual(event.request, { url: 'https://html-classic.itch.zone/html/1/index.html', headers: { 'User-Agent': 'TestAgent/1' } });
  assert.equal(event.exception.values[0].type, 'TypeError');
  assert.equal(event.exception.values[0].value, 'Failed to fetch');
  assert.deepEqual(event.exception.values[0].mechanism, { type: 'onunhandledrejection', handled: false });
  assert.deepEqual(event.breadcrumbs.values, [
    { timestamp: (TIME_ORIGIN + 200) / 1000, category: 'load.step', message: 'boot', level: 'info' },
  ]);
});

test('captureError_NoConfig_SendsNothing', async () => {
  const { reporter, sent } = createTestReporter(null);

  reporter.captureError(new Error('x'), 'onerror');
  await reporter.idle;

  assert.equal(sent.length, 0);
});

test('captureError_SameMessageTwice_SentOnce', async () => {
  const { reporter, sent } = createTestReporter(config);

  reporter.captureError(new TypeError('Failed to fetch'), 'onunhandledrejection');
  reporter.captureError(new TypeError('Failed to fetch'), 'onunhandledrejection');
  await reporter.idle;

  assert.equal(sent.length, 1);
});

test('captureError_ManyErrors_CappedAtMax', async () => {
  const { reporter, sent } = createTestReporter(config);

  for (let i = 0; i < MAX_ERRORS + 5; i++) {
    reporter.captureError(new Error(`error ${i}`), 'onerror');
  }
  await reporter.idle;

  assert.equal(sent.length, MAX_ERRORS);
});

test('captureError_SendFails_Ignored', async () => {
  const reporter = createReporter(Promise.resolve(config), {
    send: async () => {
      throw new TypeError('blocked');
    },
    clock: { now: () => 0, timeOrigin: TIME_ORIGIN, getEntriesByType: () => [] },
    random: Math.random,
    pageUrl: 'https://x/',
    userAgent: 'TestAgent/1',
  });

  reporter.captureError(new Error('x'), 'onerror');
  await reporter.idle;
});

test('addBreadcrumb_OverMax_DropsOldest', async () => {
  const { reporter, sent } = createTestReporter(config);
  for (let i = 0; i < MAX_BREADCRUMBS + 2; i++) {
    reporter.addBreadcrumb({ kind: 'step', message: `step ${i}`, level: 'info', time: i });
  }

  reporter.captureError(new Error('x'), 'onerror');
  await reporter.idle;

  const crumbs = sent[0].lines[2].breadcrumbs.values;
  assert.equal(crumbs.length, MAX_BREADCRUMBS);
  assert.equal(crumbs[0].message, 'step 2');
});

/** @type {import('../../src/engine/platforms/LudumDare60.Engine.Platform.Browser/wwwroot/load-log.js').LoadSnapshot} */
const loadedSnapshot = {
  steps: {
    [STEP.BOOT]: 200,
    [STEP.RUNTIME_IMPORTED]: 400,
    [STEP.RUNTIME_CREATED]: 21_000,
    [STEP.FIRST_FRAME]: 21_300,
  },
  retries: 2,
  firstAssets: { start: 21_100, end: 22_000, files: 3, bytes: 4096 },
};

const runtimeResources = [
  { name: 'https://x/_framework/dotnet.native.abc.wasm', startTime: 450, responseEnd: 20_000, transferSize: 2_600_000, decodedBodySize: 7_400_000 },
  { name: 'https://x/_framework/System.Linq.def.wasm', startTime: 460, responseEnd: 900, transferSize: 5000, decodedBodySize: 10_000 },
  { name: 'https://x/assets/gun.png?v=1', startTime: 21_100, responseEnd: 21_500, transferSize: 900, decodedBodySize: 900 },
];

test('loadTransaction_Loaded_PhasesFilesAndMeasurements', () => {
  const transaction = /** @type {any} */ (loadTransaction(loadedSnapshot, runtimeResources, 22_000, true, TIME_ORIGIN, fakeRandom()));

  assert.equal(transaction.type, 'transaction');
  assert.equal(transaction.transaction, 'load');
  assert.equal(transaction.start_timestamp, TIME_ORIGIN / 1000);
  assert.equal(transaction.timestamp, (TIME_ORIGIN + 22_000) / 1000);
  assert.equal(transaction.contexts.trace.status, 'ok');
  assert.equal(transaction.contexts.trace.op, 'pageload');
  assert.deepEqual(
    transaction.spans.map((/** @type {any} */ span) => [span.op, span.description, span.start_timestamp, span.timestamp]),
    [
      ['browser', 'page and main.js', TIME_ORIGIN / 1000, (TIME_ORIGIN + 200) / 1000],
      ['runtime.script', 'import dotnet.js', (TIME_ORIGIN + 200) / 1000, (TIME_ORIGIN + 400) / 1000],
      ['runtime.download', 'runtime files', (TIME_ORIGIN + 400) / 1000, (TIME_ORIGIN + 20_000) / 1000],
      ['runtime.create', 'compile and start the runtime', (TIME_ORIGIN + 20_000) / 1000, (TIME_ORIGIN + 21_000) / 1000],
      ['game.start', 'Main to the first frame', (TIME_ORIGIN + 21_000) / 1000, (TIME_ORIGIN + 21_300) / 1000],
      ['game.assets', 'first asset batch', (TIME_ORIGIN + 21_100) / 1000, (TIME_ORIGIN + 22_000) / 1000],
      ['resource.wasm', 'dotnet.native.abc.wasm', (TIME_ORIGIN + 450) / 1000, (TIME_ORIGIN + 20_000) / 1000],
      ['resource.wasm', 'System.Linq.def.wasm', (TIME_ORIGIN + 460) / 1000, (TIME_ORIGIN + 900) / 1000],
    ],
  );
  for (const span of transaction.spans) {
    assert.equal(span.trace_id, transaction.contexts.trace.trace_id);
    assert.equal(span.parent_span_id, transaction.contexts.trace.span_id);
  }
  assert.deepEqual(transaction.spans[6].data, {
    'http.response_transfer_size': 2_600_000,
    'http.decoded_response_content_length': 7_400_000,
  });
  assert.deepEqual(transaction.measurements, {
    retries: { value: 2, unit: 'none' },
    runtime_transfer: { value: 2_605_000, unit: 'byte' },
    boot: { value: 200, unit: 'millisecond' },
    runtime_downloaded: { value: 20_000, unit: 'millisecond' },
    runtime_created: { value: 21_000, unit: 'millisecond' },
    first_frame: { value: 21_300, unit: 'millisecond' },
    assets_loaded: { value: 22_000, unit: 'millisecond' },
  });
  assert.deepEqual(transaction.tags, { 'load.completed': 'true', 'load.retried': 'true' });
});

test('loadTransaction_LeftDuringDownload_OnlyKnownPhases', () => {
  const snapshot = { steps: { [STEP.BOOT]: 200, [STEP.RUNTIME_IMPORTED]: 400 }, retries: 0, firstAssets: null };

  const transaction = /** @type {any} */ (loadTransaction(snapshot, [], 9000, false, TIME_ORIGIN, fakeRandom()));

  assert.deepEqual(transaction.spans.map((/** @type {any} */ span) => span.op), ['browser', 'runtime.script']);
  assert.equal(transaction.contexts.trace.status, 'cancelled');
  assert.deepEqual(Object.keys(transaction.measurements), ['retries', 'runtime_transfer', 'boot']);
  assert.equal(transaction.tags['load.completed'], 'false');
});

test('sendLoadTiming_Configured_SendsTransactionOnce', async () => {
  const { reporter, sent, clock } = createTestReporter(config, { resources: runtimeResources });
  clock.time = 22_000;

  reporter.sendLoadTiming(loadedSnapshot, true);
  reporter.sendLoadTiming(loadedSnapshot, false);
  await reporter.idle;

  assert.equal(sent.length, 1);
  const [header, item, transaction] = sent[0].lines;
  assert.deepEqual(item, { type: 'transaction' });
  assert.equal(header.event_id, transaction.event_id);
  assert.equal(transaction.release, 'dev-7-abc1234');
  assert.equal(transaction.contexts.trace.status, 'ok');
  assert.equal(transaction.timestamp, (TIME_ORIGIN + 22_000) / 1000);
});

test('sendLoadTiming_NotSampled_SendsNothing', async () => {
  const { reporter, sent } = createTestReporter({ ...config, loadSampleRate: 0.5 }, { random: () => 0.75 });

  reporter.sendLoadTiming(loadedSnapshot, true);
  await reporter.idle;

  assert.equal(sent.length, 0);
});

test('isLoaded_NeedsFirstFrameAndFirstAssets', () => {
  assert.equal(isLoaded(loadedSnapshot), true);
  assert.equal(isLoaded({ ...loadedSnapshot, firstAssets: null }), false);
  assert.equal(isLoaded({ ...loadedSnapshot, steps: { [STEP.BOOT]: 1 } }), false);
});

// @ts-check
import { test } from 'node:test';
import assert from 'node:assert/strict';

import {
  createLoadLog,
  formatBytes,
  formatMs,
} from '../../src/engine/platforms/LudumDare60.Engine.Platform.Browser/wwwroot/load-log.js';

/**
 * A clock whose time the test sets, with recorded marks and fixed resource entries.
 * @param {import('../../src/engine/platforms/LudumDare60.Engine.Platform.Browser/wwwroot/load-log.js').ResourceEntry[]} [entries]
 */
function createClock(entries = []) {
  /** @type {string[]} */
  const marks = [];
  const clock = {
    time: 0,
    now() {
      return clock.time;
    },
    /** @param {string} name */
    mark(name) {
      marks.push(name);
    },
    getEntriesByType() {
      return entries;
    },
  };
  return { clock, marks };
}

/** A console stand-in that records every call as `[method, argument]`. */
function createSink() {
  /** @type {[string, unknown][]} */
  const calls = [];
  return {
    calls,
    sink: {
      /** @param {string} message */
      info: (message) => calls.push(['info', message]),
      /** @param {string} message */
      warn: (message) => calls.push(['warn', message]),
      /** @param {string} message */
      groupCollapsed: (message) => calls.push(['groupCollapsed', message]),
      groupEnd: () => calls.push(['groupEnd', undefined]),
      /** @param {object[]} rows */
      table: (rows) => calls.push(['table', rows]),
    },
  };
}

test('formatMs_BelowASecond_Milliseconds', () => {
  assert.equal(formatMs(412.4), '412 ms');
});

test('formatMs_FromASecond_SecondsWithOneDecimal', () => {
  assert.equal(formatMs(31_240), '31.2 s');
});

test('formatBytes_PicksUnit', () => {
  assert.equal(formatBytes(512), '512 B');
  assert.equal(formatBytes(12_595), '12.3 KB');
  assert.equal(formatBytes(2_626_916), '2.51 MB');
});

test('step_MarksAndLogsWithTime', () => {
  const { clock, marks } = createClock();
  const { sink, calls } = createSink();
  const log = createLoadLog(clock, sink);
  clock.time = 1500;

  const time = log.step('boot');

  assert.equal(time, 1500);
  assert.deepEqual(marks, ['[load] boot']);
  assert.deepEqual(calls, [['info', '[load] 1.5 s  boot']]);
});

test('retry_LogsWarningWithReasonAndDelay', () => {
  const { clock } = createClock();
  const { sink, calls } = createSink();
  clock.time = 800;

  createLoadLog(clock, sink).retry('dotnet.native.abc.wasm', 2, 4, 2000, 'TypeError: Failed to fetch');

  assert.deepEqual(calls, [
    ['warn', '[load] 800 ms  dotnet.native.abc.wasm failed (TypeError: Failed to fetch), retry 2/4 in 2.0 s'],
  ]);
});

test('downloads_MatchingEntries_SummaryAndTable', () => {
  const { clock } = createClock([
    { name: 'https://x/_framework/dotnet.native.abc.wasm', startTime: 100, responseEnd: 20_100, transferSize: 2_000_000, decodedBodySize: 7_000_000 },
    { name: 'https://x/_framework/System.Linq.def.wasm', startTime: 120, responseEnd: 600, transferSize: 5000, decodedBodySize: 10_000 },
    { name: 'https://x/assets/gun.png?v=1', startTime: 30_000, responseEnd: 31_000, transferSize: 1, decodedBodySize: 1 },
  ]);
  const { sink, calls } = createSink();

  createLoadLog(clock, sink).downloads('/_framework/', 'runtime downloaded');

  assert.deepEqual(calls, [
    ['groupCollapsed', '[load] 20.1 s  runtime downloaded: 2 files, 1.91 MB transferred, 6.69 MB decoded'],
    ['table', [
      { file: 'dotnet.native.abc.wasm', start: 100, duration: 20_000, transferred: 2_000_000, decoded: 7_000_000 },
      { file: 'System.Linq.def.wasm', start: 120, duration: 480, transferred: 5000, decoded: 10_000 },
    ]],
    ['groupEnd', undefined],
  ]);
});

test('downloads_NoMatchingEntries_LogsNothing', () => {
  const { clock } = createClock([]);
  const { sink, calls } = createSink();

  createLoadLog(clock, sink).downloads('/_framework/', 'runtime downloaded');

  assert.deepEqual(calls, []);
});

test('assetFinished_LastReadOfBatch_LogsBatchOnce', () => {
  const { clock } = createClock();
  const { sink, calls } = createSink();
  const log = createLoadLog(clock, sink);

  clock.time = 1000;
  const gun = log.assetStarted();
  clock.time = 1100;
  const missing = log.assetStarted();
  clock.time = 1300;
  log.assetFinished('assets/gun.png?v=1', gun, 2048);
  assert.deepEqual(calls, []);
  clock.time = 1500;
  log.assetFinished('assets/missing.ogg', missing, -404);

  assert.deepEqual(calls, [
    ['groupCollapsed', '[load] 1.5 s  assets: 2 files, 2.0 KB, 1 failed, in 500 ms'],
    ['table', [
      { file: 'gun.png', start: 1000, duration: 300, bytes: 2048 },
      { file: 'missing.ogg', start: 1100, duration: 400, bytes: -404 },
    ]],
    ['groupEnd', undefined],
  ]);
});

test('assetFinished_LaterBatch_StartsFresh', () => {
  const { clock } = createClock();
  const { sink, calls } = createSink();
  const log = createLoadLog(clock, sink);

  log.assetFinished('a.png', log.assetStarted(), 1);
  calls.length = 0;
  clock.time = 5000;
  const start = log.assetStarted();
  clock.time = 5200;
  log.assetFinished('b.png', start, 10);

  assert.deepEqual(calls[0], ['groupCollapsed', '[load] 5.2 s  assets: 1 files, 10 B, in 200 ms']);
  assert.deepEqual(calls[1], ['table', [{ file: 'b.png', start: 5000, duration: 200, bytes: 10 }]]);
});

test('subscribe_EveryLine_NotifiesWithKindAndLevel', () => {
  const { clock } = createClock([
    { name: 'https://x/_framework/a.wasm', startTime: 0, responseEnd: 700, transferSize: 10, decodedBodySize: 20 },
  ]);
  const { sink } = createSink();
  const log = createLoadLog(clock, sink);
  /** @type {unknown[]} */
  const entries = [];
  log.subscribe((entry) => entries.push(entry));

  clock.time = 100;
  log.step('boot');
  clock.time = 300;
  log.retry('a.wasm', 1, 4, 1000, 'HTTP 503');
  log.downloads('/_framework/', 'runtime downloaded');
  clock.time = 900;
  log.assetFinished('missing.png', log.assetStarted(), -404);

  assert.deepEqual(entries, [
    { kind: 'step', message: 'boot', level: 'info', time: 100 },
    { kind: 'retry', message: 'a.wasm failed (HTTP 503), retry 1/4 in 1.0 s', level: 'warning', time: 300 },
    { kind: 'downloads', message: 'runtime downloaded: 1 files, 10 B transferred, 20 B decoded', level: 'info', time: 700 },
    { kind: 'assets', message: 'assets: 1 files, 0 B, 1 failed, in 0 ms', level: 'warning', time: 900 },
  ]);
});

test('snapshot_KeepsFirstStepTimesRetriesAndFirstAssets', () => {
  const { clock } = createClock();
  const { sink } = createSink();
  const log = createLoadLog(clock, sink);

  clock.time = 100;
  log.step('boot');
  clock.time = 200;
  log.step('boot');
  log.retry('a.wasm', 1, 4, 1000, 'HTTP 503');
  const start = log.assetStarted();
  clock.time = 500;
  log.assetFinished('a.png', start, 64);
  log.assetFinished('b.png', log.assetStarted(), 8);

  assert.deepEqual(log.snapshot(), {
    steps: { boot: 100 },
    retries: 1,
    firstAssets: { start: 200, end: 500, files: 1, bytes: 64 },
  });
});

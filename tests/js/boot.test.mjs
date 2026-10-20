// @ts-check
import { test } from 'node:test';
import assert from 'node:assert/strict';

import { createHash } from 'node:crypto';

import {
  createResourceLoader,
  createRetryingFetch,
  createWatchedFetch,
  STALL_MS,
  ERROR_TEXT,
  fetchWithRetry,
  getErrorText,
  isModuleLoadFailure,
  refreshCache,
  REPAIR_COOLDOWN_MS,
  REPAIR_KEY,
  repairOnce,
  resolveRuntimeUrl,
  RETRY_DELAYS_MS,
  RETRY_PARAM,
  retryUrl,
  runtimeModuleUrls,
  runWithStatus,
  setErrorText,
  freshPageUrl,
  FRESH_PARAM,
  isMissingFile,
  startOptional,
  startRuntime,
  NO_REPORTER,
} from '../../src/engine/platforms/LudumDare60.Engine.Platform.Browser/wwwroot/boot.js';

const page = 'https://html-classic.itch.zone/html/123/index.html';

test('resolveRuntimeUrl_RelativeRoot_ResolvesAgainstPage', () => {
  assert.equal(resolveRuntimeUrl('./_framework/', page), 'https://html-classic.itch.zone/html/123/_framework/dotnet.js');
});

test('resolveRuntimeUrl_RootWithoutTrailingSlash_TreatedAsFolder', () => {
  assert.equal(resolveRuntimeUrl('./_framework', page), 'https://html-classic.itch.zone/html/123/_framework/dotnet.js');
});

test('resolveRuntimeUrl_NestedRoot_KeepsPath', () => {
  assert.equal(resolveRuntimeUrl('runtimes/st/', page), 'https://html-classic.itch.zone/html/123/runtimes/st/dotnet.js');
});

test('resolveRuntimeUrl_AbsoluteRoot_IgnoresPage', () => {
  assert.equal(resolveRuntimeUrl('https://cdn.example/rt/', page), 'https://cdn.example/rt/dotnet.js');
});

test('startRuntime_ImportsCreatesThenRunsMain', async () => {
  /** @type {string[]} */
  const calls = [];
  const exitCode = await startRuntime('https://example/_framework/dotnet.js', async (url) => {
    calls.push(`import ${url}`);
    return {
      dotnet: {
        async create() {
          calls.push('create');
          return {
            async runMain() {
              calls.push('runMain');
              return 3;
            },
          };
        },
      },
    };
  });

  assert.equal(exitCode, 3);
  assert.deepEqual(calls, ['import https://example/_framework/dotnet.js', 'create', 'runMain']);
});

test('startRuntime_CreateFails_Rejects', async () => {
  const failure = new Error('create failed');

  const run = startRuntime('https://example/_framework/dotnet.js', async () => ({
    dotnet: {
      async create() {
        throw failure;
      },
    },
  }));

  await assert.rejects(run, (error) => error === failure);
});

test('startRuntime_OnReady_RunsAfterCreateAndBeforeMain', async () => {
  /** @type {string[]} */
  const calls = [];

  await startRuntime(
    'https://example/_framework/dotnet.js',
    async () => ({
      dotnet: {
        async create() {
          calls.push('create');
          return {
            async runMain() {
              calls.push('runMain');
              return 0;
            },
          };
        },
      },
    }),
    () => calls.push('ready'),
  );

  assert.deepEqual(calls, ['create', 'ready', 'runMain']);
});

/** @returns {{ hidden: boolean, textContent: string | null }} */
const statusElement = () => ({ hidden: false, textContent: 'Loading...' });

test('runWithStatus_BeforeReady_KeepsLoadingText', async () => {
  const status = statusElement();
  /** @type {(value: number) => void} */
  let finish = () => {};

  const run = runWithStatus(() => new Promise((resolve) => (finish = resolve)), status);

  assert.deepEqual(status, { hidden: false, textContent: 'Loading...' });
  finish(0);
  await run;
});

test('runWithStatus_Ready_HidesStatus', async () => {
  const status = statusElement();

  const exitCode = await runWithStatus(async (onReady) => {
    onReady();
    return 7;
  }, status);

  assert.equal(exitCode, 7);
  assert.equal(status.hidden, true);
});

test('runWithStatus_FailsAfterReady_ShowsErrorAndRejects', async () => {
  const status = statusElement();
  const failure = new Error('frame threw');

  const run = runWithStatus(async (onReady) => {
    onReady();
    throw failure;
  }, status);

  await assert.rejects(run, (error) => error === failure);
  assert.deepEqual(status, { hidden: false, textContent: ERROR_TEXT });
});

test('runWithStatus_FailsBeforeReady_ShowsError', async () => {
  const status = statusElement();

  await assert.rejects(runWithStatus(async () => {
    throw new Error('download failed');
  }, status));

  assert.deepEqual(status, { hidden: false, textContent: ERROR_TEXT });
});

test('runWithStatus_NoStatusElement_StillRunsAndRejects', async () => {
  assert.equal(await runWithStatus(async (onReady) => {
    onReady();
    return 1;
  }, null), 1);
  await assert.rejects(runWithStatus(async () => {
    throw new Error('failed');
  }, null));
});

test('runWithStatus_FailsWithErrorTextSet_ShowsThatText', async () => {
  const status = statusElement();

  await assert.rejects(runWithStatus(async () => {
    throw new Error('no WebGL2');
  }, status, () => 'Needs WebGL2.'));

  assert.deepEqual(status, { hidden: false, textContent: 'Needs WebGL2.' });
});

test('setErrorText_ReplacesDefaultUntilReset', () => {
  assert.equal(getErrorText(), ERROR_TEXT);

  setErrorText('Lost the GPU.');
  try {
    assert.equal(getErrorText(), 'Lost the GPU.');
  } finally {
    setErrorText(null);
  }

  assert.equal(getErrorText(), ERROR_TEXT);
});

test('startRuntime_Configure_AppliedBeforeCreate', async () => {
  /** @type {string[]} */
  const calls = [];
  const builder = {
    withResourceLoader() {
      return builder;
    },
    async create() {
      calls.push('create');
      return {
        async runMain() {
          return 0;
        },
      };
    },
  };

  await startRuntime('https://example/_framework/dotnet.js', async () => ({ dotnet: builder }), () => {}, (dotnet) => {
    calls.push('configure');
    return dotnet;
  });

  assert.deepEqual(calls, ['configure', 'create']);
});

/**
 * A fetch that plays back `outcomes` in order: a number is a response status, an Error is thrown as a network error.
 * @param {(number | Error)[]} outcomes
 */
function scriptedFetch(outcomes) {
  /** @type {{ url: string, init: RequestInit }[]} */
  const requests = [];
  /** @type {(url: string, init: RequestInit) => Promise<Response>} */
  const fetchLike = async (url, init) => {
    requests.push({ url, init });
    const outcome = outcomes.shift();
    if (outcome === undefined) {
      throw new Error('unexpected request');
    }
    if (outcome instanceof Error) {
      throw outcome;
    }
    return new Response(null, { status: outcome });
  };
  return { fetchLike, requests };
}

function recordedSleep() {
  /** @type {number[]} */
  const waits = [];
  /** @type {(ms: number) => Promise<void>} */
  const sleep = async (ms) => {
    waits.push(ms);
  };
  return { sleep, waits };
}

test('fetchWithRetry_FirstAttemptOk_NoRetry', async () => {
  const { fetchLike, requests } = scriptedFetch([200]);
  const { sleep, waits } = recordedSleep();

  const response = await fetchWithRetry('a.wasm', {}, fetchLike, sleep);

  assert.equal(response.status, 200);
  assert.equal(requests.length, 1);
  assert.deepEqual(waits, []);
});

test('fetchWithRetry_NetworkErrorsThenOk_RetriesWithDelays', async () => {
  const { fetchLike, requests } = scriptedFetch([new TypeError('Failed to fetch'), new TypeError('Failed to fetch'), 200]);
  const { sleep, waits } = recordedSleep();

  const response = await fetchWithRetry('a.wasm', { integrity: 'sha256-x' }, fetchLike, sleep, [10, 20, 30]);

  assert.equal(response.status, 200);
  assert.deepEqual(waits, [10, 20]);
  assert.deepEqual(requests.map((r) => r.init), [
    { integrity: 'sha256-x' },
    { integrity: 'sha256-x', cache: 'no-store' },
    { integrity: 'sha256-x', cache: 'no-store' },
  ]);
  assert.deepEqual(requests.map((r) => r.url), ['a.wasm', `a.wasm?${RETRY_PARAM}=1`, `a.wasm?${RETRY_PARAM}=2`]);
});

test('fetchWithRetry_TransientStatusThenOk_Retries', async () => {
  const { fetchLike } = scriptedFetch([503, 429, 200]);
  const { sleep, waits } = recordedSleep();

  const response = await fetchWithRetry('a.wasm', {}, fetchLike, sleep, [10, 20]);

  assert.equal(response.status, 200);
  assert.deepEqual(waits, [10, 20]);
});

test('fetchWithRetry_NotFound_ReturnedWithoutRetry', async () => {
  const { fetchLike, requests } = scriptedFetch([404]);
  const { sleep, waits } = recordedSleep();

  const response = await fetchWithRetry('a.wasm', {}, fetchLike, sleep, [10]);

  assert.equal(response.status, 404);
  assert.equal(requests.length, 1);
  assert.deepEqual(waits, []);
});

test('fetchWithRetry_NetworkErrorEveryTime_RejectsWithLastError', async () => {
  const last = new TypeError('last');
  const { fetchLike, requests } = scriptedFetch([new TypeError('first'), new TypeError('second'), last]);
  const { sleep, waits } = recordedSleep();

  await assert.rejects(fetchWithRetry('a.wasm', {}, fetchLike, sleep, [10, 20]), (error) => error === last);
  assert.equal(requests.length, 3);
  assert.deepEqual(waits, [10, 20]);
});

test('fetchWithRetry_TransientStatusEveryTime_ReturnsLastResponse', async () => {
  const { fetchLike } = scriptedFetch([502, 503]);
  const { sleep } = recordedSleep();

  const response = await fetchWithRetry('a.wasm', {}, fetchLike, sleep, [10]);

  assert.equal(response.status, 503);
});

test('fetchWithRetry_DefaultDelays_OneRetryPerDelay', async () => {
  const errors = Array.from({ length: RETRY_DELAYS_MS.length + 1 }, () => new TypeError('Failed to fetch'));
  const { fetchLike, requests } = scriptedFetch(errors);
  const { sleep, waits } = recordedSleep();

  await assert.rejects(fetchWithRetry('a.wasm', {}, fetchLike, sleep));
  assert.equal(requests.length, RETRY_DELAYS_MS.length + 1);
  assert.deepEqual(waits, [...RETRY_DELAYS_MS]);
});

test('createResourceLoader_Assembly_FetchesDefaultUriWithIntegrity', async () => {
  const { fetchLike, requests } = scriptedFetch([200]);
  const { sleep } = recordedSleep();
  const load = createResourceLoader(fetchLike, sleep);

  const result = load('assembly', 'System.Linq.abc.wasm', './_framework/System.Linq.abc.wasm', 'sha256-x');

  assert.ok(result instanceof Promise);
  assert.equal((await result).status, 200);
  assert.deepEqual(requests, [{ url: './_framework/System.Linq.abc.wasm', init: { integrity: 'sha256-x' } }]);
});

test('createResourceLoader_NoIntegrity_FetchesWithoutIt', async () => {
  const { fetchLike, requests } = scriptedFetch([200]);
  const { sleep } = recordedSleep();
  const load = createResourceLoader(fetchLike, sleep);

  await load('dotnetwasm', 'dotnet.native.abc.wasm', './_framework/dotnet.native.abc.wasm', '');

  assert.deepEqual(requests[0].init, {});
});

test('createResourceLoader_JsModuleOrManifest_LeavesDefault', () => {
  const { fetchLike, requests } = scriptedFetch([]);
  const { sleep } = recordedSleep();
  const load = createResourceLoader(fetchLike, sleep);

  assert.equal(load('dotnetjs', 'dotnet.native.js', './_framework/dotnet.native.js', ''), undefined);
  assert.equal(load('manifest', 'dotnet.boot.js', './_framework/dotnet.boot.js', ''), undefined);
  assert.equal(requests.length, 0);
});

test('fetchWithRetry_OnRetry_ReportsAttemptDelayAndReason', async () => {
  const { fetchLike } = scriptedFetch([new TypeError('Failed to fetch'), 503, 200]);
  const { sleep } = recordedSleep();
  /** @type {unknown[][]} */
  const retries = [];

  await fetchWithRetry('a.wasm', {}, fetchLike, sleep, [10, 20], (...args) => retries.push(args));

  assert.deepEqual(retries, [
    [1, 10, 'TypeError: Failed to fetch'],
    [2, 20, 'HTTP 503'],
  ]);
});

test('createResourceLoader_OnRetry_ReportsFileName', async () => {
  const { fetchLike } = scriptedFetch([new TypeError('Failed to fetch'), 200]);
  const { sleep } = recordedSleep();
  /** @type {unknown[][]} */
  const retries = [];
  const load = createResourceLoader(fetchLike, sleep, (...args) => retries.push(args));

  await load('assembly', 'System.Linq.abc.wasm', './_framework/System.Linq.abc.wasm', '');

  assert.deepEqual(retries, [['System.Linq.abc.wasm', 1, RETRY_DELAYS_MS[0], 'TypeError: Failed to fetch']]);
});

const moduleFailure = new TypeError(
  'Failed to fetch dynamically imported module: https://x/_framework/dotnet.runtime.abc.js',
);

test('isModuleLoadFailure_DynamicImportError_True', () => {
  assert.equal(isModuleLoadFailure(moduleFailure), true);
});

test('isModuleLoadFailure_OtherErrors_False', () => {
  assert.equal(isModuleLoadFailure(new Error('WebGL2 is not available.')), false);
  assert.equal(isModuleLoadFailure('Failed to fetch dynamically imported module'), false);
});

test('runtimeModuleUrls_ImportMap_TargetsUnderFrameworkOnce', () => {
  const importMap = JSON.stringify({
    imports: {
      './_framework/dotnet.js': './_framework/dotnet.a1.js',
      './_framework/dotnet.native.js': './_framework/dotnet.native.b2.js',
      './_framework/dotnet.runtime.js': './_framework/dotnet.runtime.c3.js',
      './_framework/dotnet.runtime.c3.js': './_framework/dotnet.runtime.c3.js',
      './main.js': './main.d4.js',
      './_content/p/boot.js': './_content/p/boot.e5.js',
    },
  });

  assert.deepEqual(runtimeModuleUrls(importMap, `${new URL('_framework/', page)}`, page), [
    'https://html-classic.itch.zone/html/123/_framework/dotnet.a1.js',
    'https://html-classic.itch.zone/html/123/_framework/dotnet.native.b2.js',
    'https://html-classic.itch.zone/html/123/_framework/dotnet.runtime.c3.js',
  ]);
});

test('runtimeModuleUrls_MissingOrInvalidMap_Empty', () => {
  const framework = 'https://html-classic.itch.zone/html/123/_framework/';
  assert.deepEqual(runtimeModuleUrls(null, framework, page), []);
  assert.deepEqual(runtimeModuleUrls('{', framework, page), []);
  assert.deepEqual(runtimeModuleUrls('{"scopes":{}}', framework, page), []);
});

test('refreshCache_FetchesEachUrlPastCacheAndIgnoresFailures', async () => {
  /** @type {{ url: string, init: RequestInit }[]} */
  const requests = [];
  await refreshCache(['https://x/a.js', 'https://x/b.js'], async (url, init) => {
    requests.push({ url, init });
    if (url.endsWith('b.js')) {
      throw new TypeError('Failed to fetch');
    }
    return new Response('code');
  });

  assert.deepEqual(requests, [
    { url: 'https://x/a.js', init: { cache: 'reload' } },
    { url: 'https://x/b.js', init: { cache: 'reload' } },
  ]);
});

/** A sessionStorage stand-in. */
function createStore() {
  /** @type {Map<string, string>} */
  const items = new Map();
  return {
    items,
    /** @param {string} key */
    getItem: (key) => items.get(key) ?? null,
    /** @param {string} key @param {string} value */
    setItem: (key, value) => {
      items.set(key, value);
    },
  };
}

test('repairOnce_ModuleFailure_TrueOnceThenFalse', () => {
  const store = createStore();

  assert.equal(repairOnce(moduleFailure, store, false, 5000), true);
  assert.equal(store.items.get(REPAIR_KEY), '5000');
  assert.equal(repairOnce(moduleFailure, store, false, 6000), false);
});

test('repairOnce_OtherError_FalseAndNotMarked', () => {
  const store = createStore();

  assert.equal(repairOnce(new Error('WebGL2 is not available.'), store), false);
  assert.equal(store.items.size, 0);
});

test('repairOnce_NoStoreOrStoreThrows_False', () => {
  assert.equal(repairOnce(moduleFailure, null), false);
  const throwing = {
    getItem() {
      throw new Error('SecurityError');
    },
    setItem() {},
  };
  assert.equal(repairOnce(moduleFailure, throwing), false);
});

/** Timers the test fires by hand. */
function manualTimers() {
  /** @type {Map<number, () => void>} */
  const pending = new Map();
  let next = 1;
  let armed = 0;
  return {
    pending,
    get armed() {
      return armed;
    },
    /** @param {() => void} callback */
    setTimer: (callback) => {
      armed++;
      pending.set(next, callback);
      return next++;
    },
    /** @param {unknown} timer */
    clearTimer: (timer) => {
      pending.delete(/** @type {number} */ (timer));
    },
    /** Runs the one pending timer. */
    fire() {
      assert.equal(pending.size, 1, 'exactly one timer pending');
      const [[id, callback]] = pending;
      pending.delete(id);
      callback();
    },
  };
}

/** @param {Uint8Array} bytes */
const sha256 = (bytes) => `sha256-${createHash('sha256').update(bytes).digest('base64')}`;

/** @type {(algorithm: string, bytes: Uint8Array) => Promise<ArrayBuffer>} */
const digest = (algorithm, bytes) => globalThis.crypto.subtle.digest(algorithm, bytes);

/** @returns {Promise<void>} Lets pending promise callbacks run. */
const settle = () => new Promise((resolve) => setImmediate(resolve));

test('watchedFetch_FullBody_ChecksIntegrityAndKeepsContentType', async () => {
  const body = new Uint8Array([0, 97, 115, 109, 1, 0, 0, 0]);
  /** @type {RequestInit | undefined} */
  let seen;
  const timers = manualTimers();
  const watched = createWatchedFetch(async (_url, init) => {
    seen = init;
    return new Response(body, { headers: { 'content-type': 'application/wasm', 'content-encoding': 'gzip' } });
  }, { digest, ...timers });

  const response = await watched('https://x/_framework/a.wasm', { integrity: sha256(body), cache: 'reload' });

  assert.deepEqual(new Uint8Array(await response.arrayBuffer()), body);
  assert.equal(response.headers.get('content-type'), 'application/wasm');
  assert.equal(response.headers.get('content-encoding'), null);
  assert.equal(seen?.cache, 'reload');
  assert.equal('integrity' in (seen ?? {}), false);
  assert.ok(seen?.signal instanceof AbortSignal);
  assert.equal(timers.pending.size, 0);
});

test('watchedFetch_IntegrityMismatch_RejectsWithTypeError', async () => {
  const watched = createWatchedFetch(async () => new Response(new Uint8Array([1, 2, 3])), { digest, ...manualTimers() });

  await assert.rejects(
    watched('https://x/a.wasm', { integrity: sha256(new Uint8Array([9])) }),
    (error) => error instanceof TypeError && error.message.startsWith('integrity check failed'),
  );
});

test('watchedFetch_UnknownIntegrityAlgorithm_NotChecked', async () => {
  const watched = createWatchedFetch(async () => new Response('x'), { digest, ...manualTimers() });

  const response = await watched('https://x/a.wasm', { integrity: 'md5-abc' });

  assert.equal(await response.text(), 'x');
});

test('watchedFetch_ErrorStatus_ReturnedUnread', async () => {
  const watched = createWatchedFetch(async () => new Response('missing', { status: 404 }), { digest, ...manualTimers() });

  const response = await watched('https://x/a.wasm', {});

  assert.equal(response.status, 404);
  assert.equal(response.bodyUsed, false);
});

/**
 * A fetch whose response never comes, or whose body sends `chunks` and then hangs; aborting the signal fails it the
 * way a browser does.
 * @param {Uint8Array[] | null} chunks `null`: no response at all.
 */
function hangingFetch(chunks) {
  /** @type {(url: string, init: RequestInit) => Promise<Response>} */
  return (_url, init) =>
    new Promise((resolve, reject) => {
      const abortError = () => new DOMException('The operation was aborted.', 'AbortError');
      if (chunks === null) {
        init.signal?.addEventListener('abort', () => reject(abortError()));
        return;
      }
      const body = new ReadableStream({
        start(controller) {
          for (const chunk of chunks) {
            controller.enqueue(chunk);
          }
          init.signal?.addEventListener('abort', () => controller.error(abortError()));
        },
      });
      resolve(new Response(body));
    });
}

test('watchedFetch_NoResponse_RejectsAfterStall', async () => {
  const timers = manualTimers();
  const download = createWatchedFetch(hangingFetch(null), { digest, ...timers })('https://x/a.wasm', {});
  await settle();

  timers.fire();

  await assert.rejects(download, (error) => error instanceof TypeError && error.message === `no data for ${STALL_MS / 1000} s`);
});

test('watchedFetch_BodyStopsMidway_RejectsAfterStall', async () => {
  const timers = manualTimers();
  const download = createWatchedFetch(hangingFetch([new Uint8Array(16_000)]), { digest, ...timers, stallMs: 4000 })(
    'https://x/a.wasm',
    {},
  );
  await settle();

  timers.fire();

  await assert.rejects(download, (error) => error instanceof TypeError && error.message === 'no data for 4 s');
});

test('watchedFetch_EachChunk_RearmsTimer', async () => {
  const timers = manualTimers();
  const body = new ReadableStream({
    start(controller) {
      controller.enqueue(new Uint8Array([1]));
      controller.enqueue(new Uint8Array([2]));
      controller.enqueue(new Uint8Array([3]));
      controller.close();
    },
  });

  const response = await createWatchedFetch(async () => new Response(body), { digest, ...timers })('https://x/a.wasm', {});

  assert.deepEqual(new Uint8Array(await response.arrayBuffer()), new Uint8Array([1, 2, 3]));
  assert.ok(timers.armed >= 4, `armed ${timers.armed} times`);
  assert.equal(timers.pending.size, 0);
});

test('watchedFetch_NetworkError_PassedThrough', async () => {
  const failure = new TypeError('Failed to fetch');
  const watched = createWatchedFetch(async () => {
    throw failure;
  }, { digest, ...manualTimers() });

  await assert.rejects(watched('https://x/a.wasm', {}), (error) => error === failure);
});

test('createRetryingFetch_FailsThenOk_RetriesAndReportsUrl', async () => {
  const { fetchLike, requests } = scriptedFetch([new TypeError('no data for 10 s'), 200]);
  const { sleep, waits } = recordedSleep();
  /** @type {unknown[][]} */
  const retries = [];
  const retrying = createRetryingFetch(fetchLike, sleep, (...args) => retries.push(args));

  const response = await retrying('https://x/assets/bench.png?v=1', { cache: 'default' });

  assert.equal(response.status, 200);
  assert.deepEqual(requests.map((r) => r.init), [{ cache: 'default' }, { cache: 'no-store' }]);
  assert.equal(requests[1].url, `https://x/assets/bench.png?v=1&${RETRY_PARAM}=1`);
  assert.deepEqual(waits, [RETRY_DELAYS_MS[0]]);
  assert.deepEqual(retries, [['https://x/assets/bench.png?v=1', 1, RETRY_DELAYS_MS[0], 'TypeError: no data for 10 s']]);
});

test('createRetryingFetch_NotFound_NoRetry', async () => {
  const { fetchLike, requests } = scriptedFetch([404]);
  const { sleep } = recordedSleep();

  const response = await createRetryingFetch(fetchLike, sleep, () => {})('https://x/missing.png', { cache: 'default' });

  assert.equal(response.status, 404);
  assert.equal(requests.length, 1);
});

test('startOptional_ModuleLoadsAndStarts_ReturnsWhatStartReturns', async () => {
  const started = await startOptional(async () => ({ name: 'reporting' }), (module) => `${module.name} started`, 'fallback');

  assert.equal(started, 'reporting started');
});

test('startOptional_ModuleBlocked_ReturnsTheFallbackWithAWarning', async (t) => {
  const warn = t.mock.method(console, 'warn', () => {});

  const started = await startOptional(
    async () => {
      throw new TypeError('Failed to fetch dynamically imported module: analytics.js');
    },
    () => 'started',
    NO_REPORTER,
  );

  assert.equal(started, NO_REPORTER);
  assert.equal(warn.mock.callCount(), 1);
});

test('startOptional_StartThrows_ReturnsTheFallback', async (t) => {
  t.mock.method(console, 'warn', () => {});

  const started = await startOptional(
    async () => ({}),
    () => {
      throw new Error('no fetch');
    },
    null,
  );

  assert.equal(started, null);
});

test('NO_REPORTER_StandsInForReporting_DoesNothingAndIsIdle', async () => {
  NO_REPORTER.captureError(new Error('boom'), 'boot');

  assert.equal(await NO_REPORTER.idle, undefined);
});

test('createResourceLoader_ServerStillDoesNotHaveTheFile_ReportsItMissingAfterTheRetries', async () => {
  const { fetchLike, requests } = scriptedFetch(Array(RETRY_DELAYS_MS.length + 1).fill(404));
  const { sleep } = recordedSleep();
  /** @type {unknown[][]} */
  const missing = [];
  const load = createResourceLoader(fetchLike, sleep, () => {}, (...args) => missing.push(args));

  const response = await load('assembly', 'System.Private.CoreLib.old.wasm', './_framework/System.Private.CoreLib.old.wasm', 'sha256-x');

  assert.equal(response?.status, 404);
  assert.equal(requests.length, RETRY_DELAYS_MS.length + 1);
  assert.deepEqual(missing, [['System.Private.CoreLib.old.wasm', 404]]);
});

test('createResourceLoader_FileThere_ReportsNothingMissing', async () => {
  const { fetchLike } = scriptedFetch([200]);
  const { sleep } = recordedSleep();
  /** @type {unknown[][]} */
  const missing = [];
  const load = createResourceLoader(fetchLike, sleep, () => {}, (...args) => missing.push(args));

  await load('assembly', 'System.Linq.abc.wasm', './_framework/System.Linq.abc.wasm', 'sha256-x');

  assert.deepEqual(missing, []);
});

test('isMissingFile_NotFoundGoneOrS3Forbidden_True', () => {
  assert.deepEqual([403, 404, 410].map(isMissingFile), [true, true, true]);
  assert.deepEqual([200, 304, 500, 503].map(isMissingFile), [false, false, false, false]);
});

test('repairOnce_MissingRuntimeFile_TrueOnceThenFalse', () => {
  const store = createStore();
  const error = new Error('Failed to load System.Private.CoreLib');

  assert.equal(repairOnce(error, store, true), true);
  assert.equal(repairOnce(error, store, true), false);
});

test('repairOnce_AfterTheCooldown_RepairsAgain', () => {
  const store = createStore();

  assert.equal(repairOnce(moduleFailure, store, false, 1000), true);
  assert.equal(repairOnce(moduleFailure, store, false, 1000 + REPAIR_COOLDOWN_MS - 1), false);
  assert.equal(repairOnce(moduleFailure, store, false, 1000 + REPAIR_COOLDOWN_MS), true);
});

test('repairOnce_MarkFromAnOlderVersion_RepairsAgain', () => {
  const store = createStore();
  store.setItem(REPAIR_KEY, '1');

  assert.equal(repairOnce(moduleFailure, store, false, 10 * REPAIR_COOLDOWN_MS), true);
});

test('createResourceLoader_MissingThenThere_RetriesPastTheCacheAndReportsNothing', async () => {
  const { fetchLike, requests } = scriptedFetch([404, 200]);
  const { sleep } = recordedSleep();
  /** @type {unknown[][]} */
  const missing = [];
  const load = createResourceLoader(fetchLike, sleep, () => {}, (...args) => missing.push(args));

  const response = await load('assembly', 'System.Private.CoreLib.abc.wasm', './_framework/System.Private.CoreLib.abc.wasm', 'sha256-x');

  assert.equal(response?.status, 200);
  assert.equal(requests[1].init.cache, 'no-store');
  assert.deepEqual(missing, []);
});

test('freshPageUrl_PageWithParameters_SetsTheStampAndKeepsTheRest', () => {
  const fresh = new URL(freshPageUrl(`${page}?debug#top`, 1234));

  assert.equal(fresh.searchParams.get(FRESH_PARAM), '1234');
  assert.equal(fresh.searchParams.has('debug'), true);
  assert.equal(fresh.hash, '#top');
  assert.equal(fresh.pathname, new URL(page).pathname);
});

test('freshPageUrl_AlreadyFresh_ReplacesTheStamp', () => {
  const fresh = new URL(freshPageUrl(freshPageUrl(page, 1), 2));

  assert.deepEqual(fresh.searchParams.getAll(FRESH_PARAM), ['2']);
});

test('retryUrl_PlainQueriedRelativeOrHashed_AddsTheAttemptOnce', () => {
  assert.equal(retryUrl('./_framework/a.wasm', 1), `./_framework/a.wasm?${RETRY_PARAM}=1`);
  assert.equal(retryUrl('https://x/bench.png?v=abc', 2), `https://x/bench.png?v=abc&${RETRY_PARAM}=2`);
  assert.equal(retryUrl('https://x/a.js#top', 3), `https://x/a.js?${RETRY_PARAM}=3`);
});

// @ts-check
import { test } from 'node:test';
import assert from 'node:assert/strict';

import {
  createResourceLoader,
  ERROR_TEXT,
  fetchWithRetry,
  getErrorText,
  resolveRuntimeUrl,
  RETRY_DELAYS_MS,
  runWithStatus,
  setErrorText,
  startRuntime,
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
  assert.deepEqual(requests.map((r) => r.init), [{ integrity: 'sha256-x' }, { integrity: 'sha256-x' }, { integrity: 'sha256-x' }]);
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

test('fetchWithRetry_DefaultDelays_FourRetries', async () => {
  const errors = Array.from({ length: RETRY_DELAYS_MS.length + 1 }, () => new TypeError('Failed to fetch'));
  const { fetchLike, requests } = scriptedFetch(errors);
  const { sleep, waits } = recordedSleep();

  await assert.rejects(fetchWithRetry('a.wasm', {}, fetchLike, sleep));
  assert.equal(requests.length, 5);
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

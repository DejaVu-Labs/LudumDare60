// @ts-check
import { test } from 'node:test';
import assert from 'node:assert/strict';

import { ERROR_TEXT, resolveRuntimeUrl, runWithStatus, startRuntime } from '../../src/engine/platforms/LudumDare60.Engine.Platform.Browser/wwwroot/boot.js';

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

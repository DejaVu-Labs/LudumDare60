// @ts-check
import { test } from 'node:test';
import assert from 'node:assert/strict';

import { resolveRuntimeUrl, startRuntime } from '../../src/engine/platforms/LudumDare60.Engine.Platform.Browser/wwwroot/boot.js';

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

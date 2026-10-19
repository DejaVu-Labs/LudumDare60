// @ts-check
import { test } from 'node:test';
import assert from 'node:assert/strict';

import {
  createAnalytics,
  isValidDesignId,
  isValidProgression,
  loadConfig,
  MAX_QUEUED,
  POINTER_TYPES,
  PROGRESSION,
  readConfig,
  SEVERITY_CRITICAL,
} from '../../src/engine/platforms/LudumDare60.Engine.Platform.Browser/wwwroot/analytics.js';

const GAME_KEY = '0123456789abcdef0123456789ABCDEF';
const SECRET_KEY = '0123456789abcdef0123456789abcdef01234567';
const CONFIG = Object.freeze({ gameKey: GAME_KEY, secretKey: SECRET_KEY, build: 'dev-42-abc1234' });

/**
 * A stand-in for the SDK: records every call, and is ready once `ready` is set.
 */
function fakeSdk() {
  /** @type {unknown[][]} */
  const calls = [];
  const sdk = {
    ready: false,
    calls,
    /** @param {string} build */
    configureBuild: (build) => calls.push(['configureBuild', build]),
    /** @param {string[]} values */
    configureAvailableCustomDimensions01: (values) => calls.push(['configureAvailableCustomDimensions01', values]),
    /** @param {string} gameKey @param {string} secretKey */
    initialize: (gameKey, secretKey) => calls.push(['initialize', gameKey, secretKey]),
    /** @param {string} value */
    setCustomDimension01: (value) => calls.push(['setCustomDimension01', value]),
    /** @param {number} status @param {string} p1 @param {string} [p2] @param {string} [p3] @param {number} [score] */
    addProgressionEvent: (status, p1, p2, p3, score) => calls.push(['addProgressionEvent', status, p1, p2, p3, score]),
    /** @param {string} eventId @param {number} [value] */
    addDesignEvent: (eventId, value) => calls.push(['addDesignEvent', eventId, value]),
    /** @param {number} severity @param {string} message */
    addErrorEvent: (severity, message) => calls.push(['addErrorEvent', severity, message]),
    isSdkReady: () => sdk.ready,
  };
  return sdk;
}

/** Interval timers run by hand. */
function fakeTimers() {
  /** @type {Map<number, () => void>} */
  const active = new Map();
  let next = 1;
  return {
    active,
    /** @param {() => void} callback */
    setInterval: (callback) => {
      active.set(next, callback);
      return next++;
    },
    /** @param {unknown} timer */
    clearInterval: (timer) => {
      active.delete(/** @type {number} */ (timer));
    },
    tick() {
      for (const callback of [...active.values()]) {
        callback();
      }
    },
  };
}

/** @param {unknown[][]} calls @param {string} name */
function callsOf(calls, name) {
  return calls.filter((call) => call[0] === name);
}

test('isValidDesignId_GameAnalyticsRules_AcceptsOneToFivePartsOfAllowedCharacters', () => {
  assert.equal(isValidDesignId('Game:Ready'), true);
  assert.equal(isValidDesignId('Order:dog-walker:FirstPartOut'), true);
  assert.equal(isValidDesignId('a:b:c:d:e'), true);
  assert.equal(isValidDesignId('Odd (but) ok_1.2!?'), true);
  assert.equal(isValidDesignId('a:b:c:d:e:f'), false);
  assert.equal(isValidDesignId('Order::Start'), false);
  assert.equal(isValidDesignId(''), false);
  assert.equal(isValidDesignId('Order:café'), false);
  assert.equal(isValidDesignId('x'.repeat(65)), false);
  assert.equal(isValidDesignId('x'.repeat(64)), true);
});

test('isValidProgression_LevelsAndStatus_NeedsKnownStatusAndLevelsInOrder', () => {
  assert.equal(isValidProgression(PROGRESSION.START, 'Order', 'hoodie', null), true);
  assert.equal(isValidProgression(PROGRESSION.COMPLETE, 'Order', null, null), true);
  assert.equal(isValidProgression(PROGRESSION.FAIL, 'Order', 'hoodie', 'step'), true);
  assert.equal(isValidProgression(0, 'Order', 'hoodie', null), false);
  assert.equal(isValidProgression(4, 'Order', 'hoodie', null), false);
  assert.equal(isValidProgression(PROGRESSION.START, 'Order', null, 'step'), false);
  assert.equal(isValidProgression(PROGRESSION.START, '', null, null), false);
  assert.equal(isValidProgression(PROGRESSION.START, 'Order:hoodie', null, null), false);
});

test('readConfig_ValidKeys_ReturnsThemWithTheBuild', () => {
  assert.deepEqual(readConfig({ ...CONFIG }), CONFIG);
});

test('readConfig_MissingOrMalformedKeys_TurnsAnalyticsOff', () => {
  assert.equal(readConfig({}), null);
  assert.equal(readConfig(null), null);
  assert.equal(readConfig('keys'), null);
  assert.equal(readConfig({ gameKey: GAME_KEY }), null);
  assert.equal(readConfig({ gameKey: GAME_KEY.slice(1), secretKey: SECRET_KEY }), null);
  assert.equal(readConfig({ gameKey: GAME_KEY, secretKey: `${SECRET_KEY.slice(1)}-` }), null);
});

test('readConfig_MissingOrTooLongBuild_UsesUnknown', () => {
  assert.equal(readConfig({ gameKey: GAME_KEY, secretKey: SECRET_KEY })?.build, 'unknown');
  assert.equal(readConfig({ gameKey: GAME_KEY, secretKey: SECRET_KEY, build: 'x'.repeat(33) })?.build, 'unknown');
});

test('loadConfig_FailedOrMissingFile_ReturnsNull', async () => {
  assert.equal(await loadConfig('analytics.json', async () => new Response('', { status: 404 })), null);
  assert.equal(
    await loadConfig('analytics.json', async () => {
      throw new TypeError('Failed to fetch');
    }),
    null,
  );
  assert.equal(await loadConfig('analytics.json', async () => new Response('{not json')), null);
});

test('loadConfig_File_ReadsItWithoutTheCache', async () => {
  /** @type {RequestInit | undefined} */
  let seen;
  const config = await loadConfig('analytics.json', async (_url, init) => {
    seen = init;
    return new Response(JSON.stringify(CONFIG));
  });

  assert.deepEqual(config, CONFIG);
  assert.equal(seen?.cache, 'no-cache');
});

test('createAnalytics_Config_ConfiguresBuildAndDimensionsBeforeInitialize', async () => {
  const sdk = fakeSdk();
  const analytics = createAnalytics(Promise.resolve(CONFIG), async () => sdk, fakeTimers());

  assert.equal(await analytics.started, true);
  assert.deepEqual(sdk.calls, [
    ['configureBuild', 'dev-42-abc1234'],
    ['configureAvailableCustomDimensions01', [...POINTER_TYPES]],
    ['initialize', GAME_KEY, SECRET_KEY],
  ]);
});

test('createAnalytics_NoConfig_NeverLoadsTheSdkAndDropsEvents', async () => {
  let loaded = false;
  const analytics = createAnalytics(
    Promise.resolve(null),
    async () => {
      loaded = true;
      return fakeSdk();
    },
    fakeTimers(),
  );

  analytics.design('Game:Ready', 1);

  assert.equal(await analytics.started, false);
  assert.equal(loaded, false);
});

test('createAnalytics_SdkFailsToLoad_DropsEvents', async (t) => {
  t.mock.method(console, 'warn', () => {});
  const timers = fakeTimers();
  const analytics = createAnalytics(
    Promise.resolve(CONFIG),
    async () => {
      throw new TypeError('Failed to fetch dynamically imported module');
    },
    timers,
  );
  analytics.design('Game:Ready', 1);

  assert.equal(await analytics.started, false);
  assert.equal(timers.active.size, 0);
});

test('createAnalytics_SdkReadinessThrows_EventsWaitInsteadOfThrowingIntoTheGame', async () => {
  const sdk = fakeSdk();
  sdk.isSdkReady = () => {
    throw new Error('broken SDK');
  };
  const timers = fakeTimers();
  const analytics = createAnalytics(Promise.resolve(CONFIG), async () => sdk, timers);
  await analytics.started;

  analytics.design('Game:Ready', 1);
  analytics.progression(PROGRESSION.START, 'Order', 'hoodie', null);
  timers.tick();

  assert.equal(callsOf(sdk.calls, 'addDesignEvent').length, 0);
});

test('createAnalytics_EventsBeforeTheSession_QueuesThemUntilTheSdkIsReady', async () => {
  const sdk = fakeSdk();
  const timers = fakeTimers();
  const analytics = createAnalytics(Promise.resolve(CONFIG), async () => sdk, timers);

  analytics.design('Game:Ready', 2.5);
  analytics.progression(PROGRESSION.START, 'Order', 'hoodie', null);
  await analytics.started;
  timers.tick();
  const beforeReady = callsOf(sdk.calls, 'addDesignEvent').length;
  sdk.ready = true;
  timers.tick();

  assert.equal(beforeReady, 0);
  assert.deepEqual(callsOf(sdk.calls, 'addDesignEvent'), [['addDesignEvent', 'Game:Ready', 2.5]]);
  assert.deepEqual(callsOf(sdk.calls, 'addProgressionEvent'), [
    ['addProgressionEvent', PROGRESSION.START, 'Order', 'hoodie', undefined, undefined],
  ]);
  assert.equal(timers.active.size, 0, 'polling stops once the queue is empty');
});

test('createAnalytics_ReadySdk_SendsAtOnceInOrder', async () => {
  const sdk = fakeSdk();
  sdk.ready = true;
  const analytics = createAnalytics(Promise.resolve(CONFIG), async () => sdk, fakeTimers());
  await analytics.started;

  analytics.progression(PROGRESSION.COMPLETE, 'Order', 'hoodie', undefined, 74);
  analytics.design('Order:hoodie:Stalls', 0);
  analytics.design('Order:hoodie:Done');

  assert.deepEqual(sdk.calls.slice(3), [
    ['addProgressionEvent', PROGRESSION.COMPLETE, 'Order', 'hoodie', undefined, 74],
    ['addDesignEvent', 'Order:hoodie:Stalls', 0],
    ['addDesignEvent', 'Order:hoodie:Done', undefined],
  ]);
});

test('createAnalytics_InvalidEvents_DropsThemWithAWarning', async (t) => {
  const warn = t.mock.method(console, 'warn', () => {});
  const sdk = fakeSdk();
  sdk.ready = true;
  const analytics = createAnalytics(Promise.resolve(CONFIG), async () => sdk, fakeTimers());
  await analytics.started;

  analytics.design('Order:café:Start', 1);
  analytics.progression(0, 'Order', 'hoodie', null);

  assert.equal(callsOf(sdk.calls, 'addDesignEvent').length, 0);
  assert.equal(callsOf(sdk.calls, 'addProgressionEvent').length, 0);
  assert.equal(warn.mock.callCount(), 2);
});

test('createAnalytics_SessionNeverStarts_KeepsAtMostMaxQueued', async () => {
  const sdk = fakeSdk();
  const timers = fakeTimers();
  const analytics = createAnalytics(Promise.resolve(CONFIG), async () => sdk, timers);
  await analytics.started;

  for (let i = 0; i < MAX_QUEUED + 50; i++) {
    analytics.design('Game:Ready', i);
  }
  sdk.ready = true;
  timers.tick();

  const sent = callsOf(sdk.calls, 'addDesignEvent');
  assert.equal(sent.length, MAX_QUEUED);
  assert.deepEqual(sent.at(-1), ['addDesignEvent', 'Game:Ready', MAX_QUEUED - 1]);
});

test('createAnalytics_ThrowingSdk_KeepsGoing', async () => {
  const sdk = fakeSdk();
  sdk.ready = true;
  sdk.addDesignEvent = () => {
    throw new Error('boom');
  };
  const analytics = createAnalytics(Promise.resolve(CONFIG), async () => sdk, fakeTimers());
  await analytics.started;

  assert.doesNotThrow(() => analytics.design('Game:Ready', 1));
  analytics.progression(PROGRESSION.START, 'Order', 'hoodie', null);
  assert.equal(callsOf(sdk.calls, 'addProgressionEvent').length, 1);
});

test('createAnalytics_PointerTypeBeforeTheSdk_SetsTheDimensionAfterInitialize', async () => {
  const sdk = fakeSdk();
  const analytics = createAnalytics(Promise.resolve(CONFIG), async () => sdk, fakeTimers());

  analytics.setPointerType('touch');
  await analytics.started;

  assert.deepEqual(sdk.calls.at(-1), ['setCustomDimension01', 'touch']);
  assert.deepEqual(sdk.calls[2][0], 'initialize');
});

test('createAnalytics_PointerTypeChanges_SetsTheDimensionOncePerChangeAndIgnoresUnknownTypes', async () => {
  const sdk = fakeSdk();
  const analytics = createAnalytics(Promise.resolve(CONFIG), async () => sdk, fakeTimers());
  await analytics.started;

  analytics.setPointerType('mouse');
  analytics.setPointerType('mouse');
  analytics.setPointerType('');
  analytics.setPointerType('kinect');
  analytics.setPointerType('pen');

  assert.deepEqual(callsOf(sdk.calls, 'setCustomDimension01'), [
    ['setCustomDimension01', 'mouse'],
    ['setCustomDimension01', 'pen'],
  ]);
});

test('createAnalytics_Fatal_SendsOneCriticalErrorPerPage', async () => {
  const sdk = fakeSdk();
  sdk.ready = true;
  const analytics = createAnalytics(Promise.resolve(CONFIG), async () => sdk, fakeTimers());
  await analytics.started;

  analytics.fatal('ManagedError: assets/data/orders.json did not load.');
  analytics.fatal('TypeError: later');

  assert.deepEqual(callsOf(sdk.calls, 'addErrorEvent'), [
    ['addErrorEvent', SEVERITY_CRITICAL, 'ManagedError: assets/data/orders.json did not load.'],
  ]);
});

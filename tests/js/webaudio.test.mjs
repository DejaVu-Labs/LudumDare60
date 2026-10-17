// @ts-check
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

import {
  Bus,
  EVENT_SIZE,
  MAX_VOICES,
  MIN_FADE,
  PLAY,
  PLAY_LOOP,
  SET_BUS_VOLUME,
  SET_VOICE,
  SMOOTHING,
  SOUND_FAILED,
  SOUND_READY,
  STOP_VOICE,
  UNLOCK_EVENTS,
  attachLifecycle,
  createMixer,
  createSilentMixer,
} from '../../src/engine/platforms/LudumDare60.Engine.Platform.Browser/wwwroot/webaudio.js';

/** Shared with AudioTests.cs, which checks that C# records this frame as these bytes. */
const fixture = JSON.parse(readFileSync(new URL('../fixtures/audio-frame.json', import.meta.url), 'utf8'));

const NOW = 10;

/** An AudioParam that records its automation calls and jumps straight to every target. */
class FakeParam {
  /** @param {number} value */
  constructor(value) {
    this.value = value;
    /** @type {unknown[][]} */
    this.calls = [];
  }

  /** @param {number} time */
  cancelScheduledValues(time) {
    this.calls.push(['cancel', time]);
  }

  /** @param {number} value @param {number} time */
  setValueAtTime(value, time) {
    this.calls.push(['set', value, time]);
    this.value = value;
  }

  /** @param {number} value @param {number} time @param {number} constant */
  setTargetAtTime(value, time, constant) {
    this.calls.push(['target', value, time, constant]);
    this.value = value;
  }

  /** @param {number} value @param {number} time */
  linearRampToValueAtTime(value, time) {
    this.calls.push(['ramp', value, time]);
    this.value = value;
  }
}

/**
 * @typedef {object} FakeNode
 * @property {string} kind
 * @property {FakeNode[]} outputs
 * @property {boolean} disconnected
 * @property {FakeParam} [gain]
 * @property {FakeParam} [pan]
 * @property {FakeParam} [playbackRate]
 * @property {unknown} [buffer]
 * @property {boolean} [loop]
 * @property {boolean} [started]
 * @property {number} [stoppedAt]
 * @property {(() => void) | null} [onended]
 */

/** A stand-in for an AudioContext: nodes record their wiring, and decodes wait until the test settles them. */
function createFakeContext(state = 'running') {
  /** @type {FakeNode[]} */
  const sources = [];
  /** @type {{ bytes: ArrayBuffer, resolve: (buffer: unknown) => void, reject: (error: unknown) => void }[]} */
  const decodes = [];
  /** @type {string[]} */
  const calls = [];

  /** @param {string} kind @returns {FakeNode} */
  const node = (kind) => {
    /** @type {FakeNode & { connect(target: FakeNode): FakeNode, disconnect(): void }} */
    const created = {
      kind,
      outputs: [],
      disconnected: false,
      connect(target) {
        created.outputs.push(target);
        return target;
      },
      disconnect() {
        created.outputs = [];
        created.disconnected = true;
      },
    };
    return created;
  };

  const context = {
    state,
    currentTime: NOW,
    destination: node('destination'),
    createGain() {
      return Object.assign(node('gain'), { gain: new FakeParam(1) });
    },
    createStereoPanner() {
      return Object.assign(node('panner'), { pan: new FakeParam(0) });
    },
    createBufferSource() {
      const source = Object.assign(node('source'), {
        buffer: null,
        loop: false,
        started: false,
        stoppedAt: /** @type {number | undefined} */ (undefined),
        onended: /** @type {(() => void) | null} */ (null),
        playbackRate: new FakeParam(1),
        start() {
          source.started = true;
        },
        /** @param {number} [time] */
        stop(time) {
          source.stoppedAt = time ?? context.currentTime;
        },
      });
      sources.push(source);
      return source;
    },
    /** @param {ArrayBuffer} bytes */
    decodeAudioData(bytes) {
      return new Promise((resolve, reject) => decodes.push({ bytes, resolve, reject }));
    },
    resume() {
      calls.push('resume');
      context.state = 'running';
      return Promise.resolve();
    },
    suspend() {
      calls.push('suspend');
      context.state = 'suspended';
      return Promise.resolve();
    },
  };
  return {
    context,
    audioContext: /** @type {BaseAudioContext & { resume(): Promise<void>, suspend(): Promise<void> }} */ (
      /** @type {unknown} */ (context)
    ),
    sources,
    decodes,
    calls,
  };
}

/** Lets settled promises run their callbacks. */
const flush = () => new Promise((resolve) => setImmediate(resolve));

/** Writes commands the way `Audio` (C#) does: an int32 opcode, then int32 (`i`) or float32 (`f`) arguments. */
function encode(/** @type {[number, string, ...number[]][]} */ ...commands) {
  const words = commands.reduce((sum, [, layout]) => sum + 1 + layout.length, 0);
  const view = new DataView(new ArrayBuffer(words * 4));
  let offset = 0;
  for (const [opcode, layout, ...args] of commands) {
    view.setInt32(offset, opcode, true);
    offset += 4;
    for (let i = 0; i < layout.length; i++) {
      if (layout[i] === 'i') {
        view.setInt32(offset, args[i], true);
      } else {
        view.setFloat32(offset, args[i], true);
      }
      offset += 4;
    }
  }
  return new Uint8Array(view.buffer);
}

const play = (/** @type {number[]} */ ...a) => /** @type {[number, string, ...number[]]} */ ([PLAY, 'iifff', ...a]);
const playLoop = (/** @type {number[]} */ ...a) =>
  /** @type {[number, string, ...number[]]} */ ([PLAY_LOOP, 'iiiffff', ...a]);
const setVoice = (/** @type {number[]} */ ...a) => /** @type {[number, string, ...number[]]} */ ([SET_VOICE, 'ifff', ...a]);
const stopVoice = (/** @type {number[]} */ ...a) => /** @type {[number, string, ...number[]]} */ ([STOP_VOICE, 'if', ...a]);
const setBus = (/** @type {number[]} */ ...a) => /** @type {[number, string, ...number[]]} */ ([SET_BUS_VOLUME, 'if', ...a]);

/** @param {Uint8Array} bytes @param {number} count */
function readEvents(bytes, count) {
  const view = new DataView(bytes.buffer);
  return Array.from({ length: count }, (_, i) => [view.getInt32(i * EVENT_SIZE, true), view.getInt32(i * EVENT_SIZE + 4, true)]);
}

/**
 * A mixer on a fake context, with the buses found in the graph.
 * @param {string} [state]
 */
function setup(state) {
  const fake = createFakeContext(state);
  const warnings = /** @type {string[]} */ ([]);
  const mixer = createMixer(fake.audioContext, (message) => warnings.push(message));
  return { ...fake, mixer, warnings };
}

/**
 * Creates sounds and lets them finish decoding.
 * @param {ReturnType<typeof setup>} env
 * @param {number[]} ids
 */
async function readySounds(env, ...ids) {
  for (const id of ids) {
    env.mixer.createSound(id, new Uint8Array([1, 2, 3]));
    env.decodes[env.decodes.length - 1].resolve({ id });
  }
  await flush();
  env.mixer.submit(new Uint8Array(0), new Uint8Array(ids.length * EVENT_SIZE));
}

/**
 * The nodes after a source: its gain, its panner and the bus gain the panner feeds.
 * @param {FakeNode} source
 */
function chain(source) {
  const gain = source.outputs[0];
  const panner = gain.outputs[0];
  return { gain, panner, bus: panner.outputs[0] };
}

test('createMixer_StartsEmpty', () => {
  const env = setup();

  assert.equal(env.mixer.soundCount, 0);
  assert.equal(env.mixer.activeVoices, 0);
  assert.equal(env.sources.length, 0);
});

test('createSound_DecodesACopyAndReportsReadyOnNextSubmit', async () => {
  const env = setup();
  const data = new Uint8Array([7, 8, 9]);

  env.mixer.createSound(4, data);
  data[0] = 0;
  assert.deepEqual([...new Uint8Array(env.decodes[0].bytes)], [7, 8, 9]);

  const events = new Uint8Array(4 * EVENT_SIZE);
  assert.equal(env.mixer.submit(new Uint8Array(0), events), 0);

  env.decodes[0].resolve({});
  await flush();

  assert.equal(env.mixer.submit(new Uint8Array(0), events), 1);
  assert.deepEqual(readEvents(events, 1), [[SOUND_READY, 4]]);
  assert.equal(env.mixer.submit(new Uint8Array(0), events), 0);
});

test('createSound_DecodeFails_ReportsFailedAndWarns', async () => {
  const env = setup();

  env.mixer.createSound(2, new Uint8Array([1]));
  env.decodes[0].reject(new Error('Unable to decode audio data'));
  await flush();

  const events = new Uint8Array(EVENT_SIZE);
  assert.equal(env.mixer.submit(new Uint8Array(0), events), 1);
  assert.deepEqual(readEvents(events, 1), [[SOUND_FAILED, 2]]);
  assert.equal(env.warnings.length, 1);
  assert.match(env.warnings[0], /Sound 2 could not be decoded: Unable to decode audio data/);
});

test('createSound_DestroyedWhileDecoding_ReportsNothing', async () => {
  const env = setup();

  env.mixer.createSound(0, new Uint8Array([1]));
  env.mixer.destroySound(0);
  env.mixer.createSound(0, new Uint8Array([2]));
  env.decodes[0].resolve({ old: true });
  await flush();

  assert.equal(env.mixer.submit(new Uint8Array(0), new Uint8Array(EVENT_SIZE)), 0);
  assert.equal(env.mixer.soundCount, 1);
});

test('createSound_IdInUse_Throws', () => {
  const env = setup();
  env.mixer.createSound(0, new Uint8Array([1]));

  assert.throws(() => env.mixer.createSound(0, new Uint8Array([1])), /Sound 0 already exists/);
});

test('submit_EventsBeyondBuffer_StayQueued', async () => {
  const env = setup();
  for (let id = 0; id < 3; id++) {
    env.mixer.createSound(id, new Uint8Array([1]));
    env.decodes[id].resolve({});
  }
  await flush();

  const events = new Uint8Array(2 * EVENT_SIZE + 3);
  assert.equal(env.mixer.submit(new Uint8Array(0), events), 2);
  assert.deepEqual(readEvents(events, 2), [[SOUND_READY, 0], [SOUND_READY, 1]]);
  assert.equal(env.mixer.submit(new Uint8Array(0), events), 1);
  assert.deepEqual(readEvents(events, 1), [[SOUND_READY, 2]]);
});

test('play_ReadySound_StartsOneShotThroughGainAndPanner', async () => {
  const env = setup();
  await readySounds(env, 0);

  env.mixer.submit(encode(play(0, Bus.Sfx, 0.5, 2, -0.25)), new Uint8Array(0));

  const [source] = env.sources;
  assert.deepEqual(source.buffer, { id: 0 });
  assert.equal(source.loop, false);
  assert.equal(source.started, true);
  assert.equal(source.playbackRate?.value, 2);
  const { gain, panner, bus } = chain(source);
  assert.equal(gain.gain?.value, 0.5);
  assert.equal(panner.pan?.value, -0.25);
  // Sfx bus -> Master -> speakers.
  assert.equal(bus.outputs[0].outputs[0], env.context.destination);
  assert.equal(env.mixer.activeVoices, 1);

  source.onended?.();

  assert.equal(env.mixer.activeVoices, 0);
  assert.equal(source.disconnected, true);
  assert.equal(gain.disconnected, true);
  assert.equal(panner.disconnected, true);
});

test('play_EachBus_GoesToItsOwnGain', async () => {
  const env = setup();
  await readySounds(env, 0);

  env.mixer.submit(encode(play(0, Bus.Sfx, 1, 1, 0), play(0, Bus.Ambience, 1, 1, 0), play(0, Bus.Music, 1, 1, 0)), new Uint8Array(0));

  const targets = env.sources.map((source) => chain(source).bus);
  assert.equal(new Set(targets).size, 3);
  const master = targets[0].outputs[0];
  assert.ok(targets.every((bus) => bus.outputs[0] === master));
});

test('play_ContextNotRunning_IsDropped', async () => {
  const env = setup('suspended');
  await readySounds(env, 0);

  env.mixer.submit(encode(play(0, Bus.Sfx, 1, 1, 0)), new Uint8Array(0));

  assert.equal(env.sources.length, 0);
});

test('play_SoundStillDecoding_IsDropped', () => {
  const env = setup();
  env.mixer.createSound(0, new Uint8Array([1]));

  env.mixer.submit(encode(play(0, Bus.Sfx, 1, 1, 0)), new Uint8Array(0));

  assert.equal(env.sources.length, 0);
});

test('play_AtVoiceLimit_DropsOneShotsButNotMusic', async () => {
  const env = setup();
  await readySounds(env, 0);
  const plays = Array.from({ length: MAX_VOICES + 1 }, () => play(0, Bus.Sfx, 1, 1, 0));

  env.mixer.submit(encode(...plays, play(0, Bus.Music, 1, 1, 0)), new Uint8Array(0));

  assert.equal(env.sources.length, MAX_VOICES + 1);
  assert.equal(env.mixer.activeVoices, MAX_VOICES);

  env.sources[0].onended?.();
  env.mixer.submit(encode(play(0, Bus.Ambience, 1, 1, 0)), new Uint8Array(0));

  assert.equal(env.sources.length, MAX_VOICES + 2);
});

test('play_MasterOrUnknownBus_Throws', async () => {
  const env = setup();
  await readySounds(env, 0);

  assert.throws(() => env.mixer.submit(encode(play(0, Bus.Master, 1, 1, 0)), new Uint8Array(0)), /bus 0/);
  assert.throws(() => env.mixer.submit(encode(play(0, 7, 1, 1, 0)), new Uint8Array(0)), /bus 7/);
});

test('play_UnknownSound_Throws', () => {
  const env = setup();

  assert.throws(() => env.mixer.submit(encode(play(5, Bus.Sfx, 1, 1, 0)), new Uint8Array(0)), /Sound 5 does not exist/);
});

test('playLoop_ReadySound_StartsLoopWithFadeIn', async () => {
  const env = setup();
  await readySounds(env, 0);

  env.mixer.submit(encode(playLoop(3, 0, Bus.Ambience, 0.5, 0.75, 0.25, 2)), new Uint8Array(0));

  const [source] = env.sources;
  assert.equal(source.loop, true);
  assert.equal(source.started, true);
  assert.equal(source.playbackRate?.value, 0.75);
  const { gain, panner } = chain(source);
  assert.equal(panner.pan?.value, 0.25);
  assert.deepEqual(gain.gain?.calls, [['set', 0, NOW], ['ramp', 0.5, NOW + 2]]);
  assert.equal(env.mixer.activeVoices, 1);
});

test('playLoop_NoFade_StartsAtVolume', async () => {
  const env = setup();
  await readySounds(env, 0);

  env.mixer.submit(encode(playLoop(0, 0, Bus.Sfx, 0.5, 1, 0, 0)), new Uint8Array(0));

  assert.deepEqual(chain(env.sources[0]).gain.gain?.calls, [['set', 0.5, NOW]]);
});

test('playLoop_SoundStillDecoding_StartsWhenReadyWithLatestSettings', async () => {
  const env = setup();
  env.mixer.createSound(0, new Uint8Array([1]));

  env.mixer.submit(encode(playLoop(1, 0, Bus.Music, 1, 1, 0, 0), setVoice(1, 0.25, 1.5, -1)), new Uint8Array(0));
  assert.equal(env.sources.length, 0);
  assert.equal(env.mixer.activeVoices, 0);

  env.decodes[0].resolve({});
  await flush();

  const [source] = env.sources;
  assert.equal(source.loop, true);
  assert.equal(source.playbackRate?.value, 1.5);
  assert.equal(chain(source).gain.gain?.value, 0.25);
  assert.equal(chain(source).panner.pan?.value, -1);
});

test('playLoop_WhileContextSuspended_StillStarts', async () => {
  const env = setup('suspended');
  await readySounds(env, 0);

  env.mixer.submit(encode(playLoop(0, 0, Bus.Ambience, 1, 1, 0, 0)), new Uint8Array(0));

  assert.equal(env.sources[0].started, true);
});

test('playLoop_AtVoiceLimit_StillStarts', async () => {
  const env = setup();
  await readySounds(env, 0);
  const plays = Array.from({ length: MAX_VOICES }, () => play(0, Bus.Sfx, 1, 1, 0));

  env.mixer.submit(encode(...plays, playLoop(0, 0, Bus.Ambience, 1, 1, 0, 0)), new Uint8Array(0));

  assert.equal(env.sources.length, MAX_VOICES + 1);
  assert.equal(env.mixer.activeVoices, MAX_VOICES + 1);
});

test('playLoop_IdInUse_Throws', async () => {
  const env = setup();
  await readySounds(env, 0);

  assert.throws(
    () => env.mixer.submit(encode(playLoop(0, 0, Bus.Sfx, 1, 1, 0, 0), playLoop(0, 0, Bus.Sfx, 1, 1, 0, 0)), new Uint8Array(0)),
    /Voice 0 already plays/,
  );
});

test('setVoice_GlidesVolumePitchAndPan', async () => {
  const env = setup();
  await readySounds(env, 0);
  env.mixer.submit(encode(playLoop(0, 0, Bus.Sfx, 1, 1, 0, 0)), new Uint8Array(0));
  const [source] = env.sources;
  const { gain, panner } = chain(source);
  /** @param {FakeParam | undefined} param */
  const lastGlide = (param) => param?.calls.slice(-3);

  env.mixer.submit(encode(setVoice(0, 0.25, 0.5, 1)), new Uint8Array(0));

  assert.deepEqual(lastGlide(gain.gain), [['cancel', NOW], ['set', 1, NOW], ['target', 0.25, NOW, SMOOTHING]]);
  assert.deepEqual(lastGlide(source.playbackRate), [['cancel', NOW], ['set', 1, NOW], ['target', 0.5, NOW, SMOOTHING]]);
  assert.deepEqual(lastGlide(panner.pan), [['cancel', NOW], ['set', 0, NOW], ['target', 1, NOW, SMOOTHING]]);
});

test('setVoice_UnknownVoice_Throws', () => {
  const env = setup();

  assert.throws(() => env.mixer.submit(encode(setVoice(4, 1, 1, 0)), new Uint8Array(0)), /Voice 4 does not exist/);
});

test('stopVoice_FadesOutStopsAndFreesId', async () => {
  const env = setup();
  await readySounds(env, 0);
  env.mixer.submit(encode(playLoop(0, 0, Bus.Sfx, 0.5, 1, 0, 0)), new Uint8Array(0));
  const [source] = env.sources;
  const { gain, panner } = chain(source);

  env.mixer.submit(encode(stopVoice(0, 2), playLoop(0, 0, Bus.Sfx, 1, 1, 0, 0)), new Uint8Array(0));

  assert.deepEqual(gain.gain?.calls.slice(-3), [['cancel', NOW], ['set', 0.5, NOW], ['ramp', 0, NOW + 2]]);
  assert.equal(source.stoppedAt, NOW + 2);
  assert.equal(env.sources.length, 2);
  assert.equal(env.mixer.activeVoices, 1);

  source.onended?.();

  assert.equal(gain.disconnected, true);
  assert.equal(panner.disconnected, true);
});

test('stopVoice_NoFade_FadesForMinFade', async () => {
  const env = setup();
  await readySounds(env, 0);
  env.mixer.submit(encode(playLoop(0, 0, Bus.Sfx, 1, 1, 0, 0), stopVoice(0, 0)), new Uint8Array(0));

  assert.equal(env.sources[0].stoppedAt, NOW + MIN_FADE);
});

test('stopVoice_LoopStillDecoding_DisconnectsAndNeverStarts', async () => {
  const env = setup();
  env.mixer.createSound(0, new Uint8Array([1]));
  env.mixer.submit(encode(playLoop(0, 0, Bus.Sfx, 1, 1, 0, 0), stopVoice(0, 1)), new Uint8Array(0));

  env.decodes[0].resolve({});
  await flush();

  assert.equal(env.sources.length, 0);
  assert.equal(env.mixer.activeVoices, 0);
});

test('destroySound_StopsItsLoopsAndOneShots', async () => {
  const env = setup();
  await readySounds(env, 0, 1);
  env.mixer.submit(
    encode(playLoop(0, 0, Bus.Ambience, 1, 1, 0, 0), play(0, Bus.Sfx, 1, 1, 0), playLoop(1, 1, Bus.Ambience, 1, 1, 0, 0)),
    new Uint8Array(0),
  );
  const [loop, shot, other] = env.sources;

  env.mixer.destroySound(0);

  assert.equal(loop.stoppedAt, NOW + MIN_FADE);
  assert.equal(shot.stoppedAt, NOW);
  assert.equal(other.stoppedAt, undefined);
  assert.equal(env.mixer.activeVoices, 1);
  assert.equal(env.mixer.soundCount, 1);
  shot.onended?.();
  assert.equal(env.mixer.activeVoices, 1);
  assert.throws(() => env.mixer.submit(encode(setVoice(0, 1, 1, 0)), new Uint8Array(0)), /Voice 0 does not exist/);
});

test('destroySound_Unknown_Throws', () => {
  const env = setup();

  assert.throws(() => env.mixer.destroySound(3), /Sound 3 does not exist/);
});

test('setBusVolume_GlidesBusGain', async () => {
  const env = setup();
  await readySounds(env, 0);
  env.mixer.submit(encode(play(0, Bus.Music, 1, 1, 0)), new Uint8Array(0));
  const musicBus = chain(env.sources[0]).bus;
  const master = musicBus.outputs[0];

  env.mixer.submit(encode(setBus(Bus.Music, 0.5), setBus(Bus.Master, 0)), new Uint8Array(0));

  assert.deepEqual(musicBus.gain?.calls, [['cancel', NOW], ['set', 1, NOW], ['target', 0.5, NOW, SMOOTHING]]);
  assert.deepEqual(master.gain?.calls, [['cancel', NOW], ['set', 1, NOW], ['target', 0, NOW, SMOOTHING]]);
});

test('setBusVolume_UnknownBus_Throws', () => {
  const env = setup();

  assert.throws(() => env.mixer.submit(encode(setBus(4, 1)), new Uint8Array(0)), /Unknown audio bus 4/);
});

test('submit_UnknownOpcode_Throws', () => {
  const env = setup();

  assert.throws(() => env.mixer.submit(encode([9, '']), new Uint8Array(0)), /Unknown audio command 9 at byte 0/);
});

test('submit_SharedFixture_RunsTheFrame', async () => {
  const env = setup();
  await readySounds(env, ...fixture.sounds);
  const commands = Uint8Array.from(Buffer.from(fixture.commandsHex, 'hex'));

  env.mixer.submit(commands, new Uint8Array(0));

  const [shot, ambience, music] = env.sources;
  // Play(sound 0, Sfx, 0.75, 1.25, -0.5)
  assert.equal(shot.loop, false);
  assert.equal(shot.playbackRate?.value, 1.25);
  assert.equal(chain(shot).gain.gain?.value, 0.75);
  assert.equal(chain(shot).panner.pan?.value, -0.5);
  // PlayLoop(voice 0, sound 1, Ambience, 0.5, 1, 0, fade-in 0.25), SetVoice(0, 0.25, 0.5, 1), Stop(0, 0.5)
  assert.equal(ambience.loop, true);
  assert.deepEqual(ambience.buffer, { id: 1 });
  assert.deepEqual(chain(ambience).gain.gain?.calls, [
    ['set', 0, NOW],
    ['ramp', 0.5, NOW + 0.25],
    ['cancel', NOW],
    ['set', 0.5, NOW],
    ['target', 0.25, NOW, SMOOTHING],
    ['cancel', NOW],
    ['set', 0.25, NOW],
    ['ramp', 0, NOW + 0.5],
  ]);
  assert.equal(ambience.playbackRate?.value, 0.5);
  assert.equal(chain(ambience).panner.pan?.value, 1);
  assert.equal(ambience.stoppedAt, NOW + 0.5);
  // PlayMusic(sound 1, crossfade 2) -> PlayLoop(voice 1, sound 1, Music, 1, 1, 0, 2), after SetBusVolume(Music, 0.5)
  assert.equal(music.loop, true);
  assert.deepEqual(chain(music).gain.gain?.calls, [['set', 0, NOW], ['ramp', 1, NOW + 2]]);
  assert.equal(chain(music).bus.gain?.value, 0.5);
  assert.notEqual(chain(music).bus, chain(shot).bus);
  assert.notEqual(chain(ambience).bus, chain(shot).bus);
  // The one-shot counts; the stopped loop and the music do not.
  assert.equal(env.mixer.activeVoices, 1);
});

/** A target that keeps its listeners, like `window` or `document`. */
function createTarget(visibilityState = 'visible') {
  /** @type {Map<string, () => void>} */
  const listeners = new Map();
  return {
    visibilityState,
    listeners,
    /** @param {string} type @param {() => void} listener */
    addEventListener(type, listener) {
      listeners.set(type, listener);
    },
    /** @param {string} type */
    removeEventListener(type) {
      listeners.delete(type);
    },
    /** @param {string} type */
    fire(type) {
      listeners.get(type)?.();
    },
  };
}

test('attachLifecycle_FirstGesture_ResumesContext', () => {
  const fake = createFakeContext('suspended');
  const window = createTarget();
  const document = createTarget();
  const lifecycle = attachLifecycle(fake.audioContext, window, document);

  assert.deepEqual([...window.listeners.keys()], [...UNLOCK_EVENTS]);
  assert.equal(lifecycle.isUnlocked(), false);

  window.fire('pointerdown');

  assert.deepEqual(fake.calls, ['resume']);
  assert.equal(lifecycle.isUnlocked(), true);

  window.fire('keydown');

  assert.deepEqual(fake.calls, ['resume']);
});

test('attachLifecycle_HiddenPage_SuspendsAndVisibleResumesOnlyAfterUnlock', () => {
  const fake = createFakeContext('suspended');
  const window = createTarget();
  const document = createTarget();
  attachLifecycle(fake.audioContext, window, document);

  document.visibilityState = 'hidden';
  document.fire('visibilitychange');
  document.visibilityState = 'visible';
  document.fire('visibilitychange');

  assert.deepEqual(fake.calls, ['suspend']);

  window.fire('touchend');
  document.visibilityState = 'hidden';
  document.fire('visibilitychange');
  document.visibilityState = 'visible';
  document.fire('visibilitychange');

  assert.deepEqual(fake.calls, ['suspend', 'resume', 'suspend', 'resume']);
});

test('attachLifecycle_RunningContext_IsUnlockedFromTheStart', () => {
  const lifecycle = attachLifecycle(createFakeContext('running').audioContext, createTarget(), createTarget());

  assert.equal(lifecycle.isUnlocked(), true);
});

test('attachLifecycle_Detach_RemovesEveryListener', () => {
  const window = createTarget();
  const document = createTarget();

  attachLifecycle(createFakeContext().audioContext, window, document).detach();

  assert.equal(window.listeners.size, 0);
  assert.equal(document.listeners.size, 0);
});

test('createSilentMixer_EverySoundFailsAndCommandsAreDropped', () => {
  const mixer = createSilentMixer();

  mixer.createSound(3, new Uint8Array([1]));
  const events = new Uint8Array(EVENT_SIZE);

  assert.equal(mixer.submit(encode(play(3, Bus.Sfx, 1, 1, 0)), events), 1);
  assert.deepEqual(readEvents(events, 1), [[SOUND_FAILED, 3]]);
  assert.equal(mixer.soundCount, 1);
  mixer.destroySound(3);
  assert.equal(mixer.soundCount, 0);
});

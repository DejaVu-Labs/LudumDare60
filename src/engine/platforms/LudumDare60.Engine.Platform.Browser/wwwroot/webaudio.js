// @ts-check

/**
 * Web Audio backend of the browser platform. C# records a frame of audio commands and passes them in one `submit`
 * call, which also returns the decode results since the previous frame. Sounds are created and destroyed with their
 * own calls, outside frames, because the file bytes would not fit in the stream.
 *
 * The graph: every voice is `AudioBufferSourceNode -> GainNode (volume) -> StereoPannerNode (pan) -> bus`, and the
 * Sfx, Ambience and Music bus gains feed the Master gain, which feeds the speakers.
 *
 * The command layout is documented on `AudioCommandKind` (C#), the event layout on `AudioEvent` (C#).
 */

/** Opcodes of the command stream. Values match `AudioCommandKind` (C#). */
export const PLAY = 1;
export const PLAY_LOOP = 2;
export const SET_VOICE = 3;
export const STOP_VOICE = 4;
export const SET_BUS_VOLUME = 5;

/** Event kinds. Values match `AudioEventKind` (C#). */
export const SOUND_READY = 1;
export const SOUND_FAILED = 2;

/** Bytes per event (`AudioEvent.Size`). */
export const EVENT_SIZE = 8;

/** Matches `AudioBus` (C#). */
export const Bus = Object.freeze({ Master: 0, Sfx: 1, Ambience: 2, Music: 3 });

/** Voices that may play at once on Sfx and Ambience. Matches `AudioLimits.MaxVoices` (C#). */
export const MAX_VOICES = 32;

/** Time constant of volume, pitch and pan changes, in seconds: a change is about 95% done after 3 of them. */
export const SMOOTHING = 0.01 / 3;

/** Shortest fade-out, in seconds: stopping a sound at full volume clicks. */
export const MIN_FADE = 0.01;

/** Events with the first user gesture that browsers accept for starting audio. */
export const UNLOCK_EVENTS = Object.freeze(['pointerdown', 'keydown', 'touchend']);

/**
 * @typedef {object} Sound
 * @property {AudioBuffer | null} buffer Null while decoding, and for good if decoding failed.
 */

/**
 * @typedef {object} Voice A loop.
 * @property {number} soundId
 * @property {GainNode} gain
 * @property {StereoPannerNode} panner
 * @property {AudioBufferSourceNode | null} source Null until the sound is decoded.
 * @property {number} volume
 * @property {number} pitch
 * @property {number} fadeIn
 * @property {boolean} counted Counts against `MAX_VOICES` (not on the Music bus).
 */

/**
 * @typedef {object} OneShot
 * @property {number} soundId
 * @property {AudioBufferSourceNode} source
 * @property {boolean} counted
 * @property {boolean} done
 */

/**
 * @typedef {object} Mixer
 * @property {(id: number, data: Uint8Array) => void} createSound Copies the file and starts decoding it.
 * @property {(id: number) => void} destroySound Stops the sound's voices and forgets it.
 * @property {(commands: Uint8Array, events: Uint8Array) => number} submit Runs one frame; returns the number of
 *   events written to `events`. Both arrays are used during the call only.
 * @property {number} activeVoices Voices counting against `MAX_VOICES`.
 * @property {number} soundCount
 */

/**
 * Creates the voices and buses on an audio context.
 *
 * @param {BaseAudioContext} context
 * @param {(message: string) => void} [warn] Reports sounds that fail to decode.
 * @returns {Mixer}
 */
export function createMixer(context, warn = (message) => console.warn(message)) {
  const master = context.createGain();
  master.connect(context.destination);
  /** @type {GainNode[]} Indexed by `Bus`. */
  const buses = [master];
  for (let bus = Bus.Sfx; bus <= Bus.Music; bus++) {
    const gain = context.createGain();
    gain.connect(master);
    buses.push(gain);
  }

  /** @type {Map<number, Sound>} */
  const sounds = new Map();
  /** @type {Map<number, Voice>} */
  const voices = new Map();
  /** @type {Set<OneShot>} */
  const oneShots = new Set();
  /** @type {number[]} Pairs of event kind and sound id, oldest first. */
  const events = [];
  let activeVoices = 0;

  /** @param {number} bus */
  function getBus(bus) {
    const gain = buses[bus];
    if (bus === Bus.Master || gain === undefined) {
      throw new Error(`Voices cannot play on bus ${bus}.`);
    }
    return gain;
  }

  /** @param {number} id */
  function getSound(id) {
    const sound = sounds.get(id);
    if (sound === undefined) {
      throw new Error(`Sound ${id} does not exist.`);
    }
    return sound;
  }

  /** @param {number} id */
  function getVoice(id) {
    const voice = voices.get(id);
    if (voice === undefined) {
      throw new Error(`Voice ${id} does not exist.`);
    }
    return voice;
  }

  /**
   * Moves an audio parameter smoothly to `value`, starting from wherever it is now (even in the middle of a fade).
   * @param {AudioParam} param
   * @param {number} value
   */
  function glide(param, value) {
    const now = context.currentTime;
    param.cancelScheduledValues(now);
    param.setValueAtTime(param.value, now);
    param.setTargetAtTime(value, now, SMOOTHING);
  }

  /** @param {Voice} voice */
  function startLoop(voice) {
    const sound = getSound(voice.soundId);
    if (sound.buffer === null) {
      return;
    }
    const source = context.createBufferSource();
    source.buffer = sound.buffer;
    source.loop = true;
    source.playbackRate.value = voice.pitch;
    source.connect(voice.gain);
    const now = context.currentTime;
    if (voice.fadeIn > 0) {
      voice.gain.gain.setValueAtTime(0, now);
      voice.gain.gain.linearRampToValueAtTime(voice.volume, now + voice.fadeIn);
    } else {
      voice.gain.gain.setValueAtTime(voice.volume, now);
    }
    source.start();
    voice.source = source;
  }

  /**
   * @param {Voice} voice
   * @param {number} fade Seconds.
   */
  function stopLoop(voice, fade) {
    if (voice.counted) {
      activeVoices--;
    }
    const source = voice.source;
    if (source === null) {
      voice.gain.disconnect();
      voice.panner.disconnect();
      return;
    }
    const now = context.currentTime;
    const end = now + Math.max(fade, MIN_FADE);
    const param = voice.gain.gain;
    param.cancelScheduledValues(now);
    param.setValueAtTime(param.value, now);
    param.linearRampToValueAtTime(0, end);
    source.onended = () => {
      source.disconnect();
      voice.gain.disconnect();
      voice.panner.disconnect();
    };
    source.stop(end);
  }

  /** @param {OneShot} shot */
  function finishOneShot(shot) {
    if (shot.done) {
      return;
    }
    shot.done = true;
    oneShots.delete(shot);
    if (shot.counted) {
      activeVoices--;
    }
  }

  /**
   * @param {number} soundId
   * @param {number} bus
   * @param {number} volume
   * @param {number} pitch
   * @param {number} pan
   */
  function play(soundId, bus, volume, pitch, pan) {
    const output = getBus(bus);
    const sound = getSound(soundId);
    const counted = bus !== Bus.Music;
    // Before the first gesture a suspended context would queue the sound and play it late.
    if (context.state !== 'running' || sound.buffer === null || (counted && activeVoices >= MAX_VOICES)) {
      return;
    }
    const source = context.createBufferSource();
    const gain = context.createGain();
    const panner = context.createStereoPanner();
    source.buffer = sound.buffer;
    source.playbackRate.value = pitch;
    gain.gain.value = volume;
    panner.pan.value = pan;
    source.connect(gain);
    gain.connect(panner);
    panner.connect(output);
    /** @type {OneShot} */
    const shot = { soundId, source, counted, done: false };
    source.onended = () => {
      finishOneShot(shot);
      source.disconnect();
      gain.disconnect();
      panner.disconnect();
    };
    oneShots.add(shot);
    if (counted) {
      activeVoices++;
    }
    source.start();
  }

  return {
    createSound(id, data) {
      if (sounds.has(id)) {
        throw new Error(`Sound ${id} already exists.`);
      }
      /** @type {Sound} */
      const sound = { buffer: null };
      sounds.set(id, sound);
      // decodeAudioData detaches its argument, so it gets its own copy of the file.
      const bytes = data.slice().buffer;
      context.decodeAudioData(bytes).then(
        (buffer) => {
          if (sounds.get(id) !== sound) {
            return;
          }
          sound.buffer = buffer;
          events.push(SOUND_READY, id);
          for (const voice of voices.values()) {
            if (voice.soundId === id && voice.source === null) {
              startLoop(voice);
            }
          }
        },
        (error) => {
          if (sounds.get(id) !== sound) {
            return;
          }
          warn(`Sound ${id} could not be decoded: ${error instanceof Error ? error.message : String(error)}`);
          events.push(SOUND_FAILED, id);
        },
      );
    },

    destroySound(id) {
      getSound(id);
      for (const [voiceId, voice] of voices) {
        if (voice.soundId === id) {
          voices.delete(voiceId);
          stopLoop(voice, 0);
        }
      }
      for (const shot of oneShots) {
        if (shot.soundId === id) {
          finishOneShot(shot);
          shot.source.stop();
        }
      }
      sounds.delete(id);
    },

    submit(commands, eventBytes) {
      const view = new DataView(commands.buffer, commands.byteOffset, commands.byteLength);
      let offset = 0;
      while (offset < view.byteLength) {
        const opcode = view.getInt32(offset, true);
        switch (opcode) {
          case PLAY: {
            play(
              view.getInt32(offset + 4, true),
              view.getInt32(offset + 8, true),
              view.getFloat32(offset + 12, true),
              view.getFloat32(offset + 16, true),
              view.getFloat32(offset + 20, true),
            );
            offset += 24;
            break;
          }
          case PLAY_LOOP: {
            const id = view.getInt32(offset + 4, true);
            const soundId = view.getInt32(offset + 8, true);
            const bus = view.getInt32(offset + 12, true);
            if (voices.has(id)) {
              throw new Error(`Voice ${id} already plays.`);
            }
            const output = getBus(bus);
            getSound(soundId);
            const gain = context.createGain();
            const panner = context.createStereoPanner();
            gain.gain.value = 0;
            panner.pan.value = view.getFloat32(offset + 24, true);
            gain.connect(panner);
            panner.connect(output);
            /** @type {Voice} */
            const voice = {
              soundId,
              gain,
              panner,
              source: null,
              volume: view.getFloat32(offset + 16, true),
              pitch: view.getFloat32(offset + 20, true),
              fadeIn: view.getFloat32(offset + 28, true),
              counted: bus !== Bus.Music,
            };
            voices.set(id, voice);
            if (voice.counted) {
              activeVoices++;
            }
            startLoop(voice);
            offset += 32;
            break;
          }
          case SET_VOICE: {
            const voice = getVoice(view.getInt32(offset + 4, true));
            voice.volume = view.getFloat32(offset + 8, true);
            voice.pitch = view.getFloat32(offset + 12, true);
            glide(voice.panner.pan, view.getFloat32(offset + 16, true));
            if (voice.source !== null) {
              glide(voice.gain.gain, voice.volume);
              glide(voice.source.playbackRate, voice.pitch);
            }
            offset += 20;
            break;
          }
          case STOP_VOICE: {
            const id = view.getInt32(offset + 4, true);
            const voice = getVoice(id);
            voices.delete(id);
            stopLoop(voice, view.getFloat32(offset + 8, true));
            offset += 12;
            break;
          }
          case SET_BUS_VOLUME: {
            const bus = view.getInt32(offset + 4, true);
            const gain = buses[bus];
            if (gain === undefined) {
              throw new Error(`Unknown audio bus ${bus}.`);
            }
            glide(gain.gain, view.getFloat32(offset + 8, true));
            offset += 12;
            break;
          }
          default:
            throw new Error(`Unknown audio command ${opcode} at byte ${offset}.`);
        }
      }

      const count = Math.min(events.length / 2, Math.floor(eventBytes.byteLength / EVENT_SIZE));
      const out = new DataView(eventBytes.buffer, eventBytes.byteOffset, eventBytes.byteLength);
      for (let i = 0; i < count; i++) {
        out.setInt32(i * EVENT_SIZE, events[2 * i], true);
        out.setInt32(i * EVENT_SIZE + 4, events[2 * i + 1], true);
      }
      events.splice(0, 2 * count);
      return count;
    },

    get activeVoices() {
      return activeVoices;
    },

    get soundCount() {
      return sounds.size;
    },
  };
}

/**
 * The part of a page the audio lifecycle listens to, so tests can pass plain objects.
 * @typedef {object} LifecycleTarget
 * @property {(type: string, listener: () => void, options?: AddEventListenerOptions) => void} addEventListener
 * @property {(type: string, listener: () => void, options?: EventListenerOptions) => void} removeEventListener
 */

/**
 * @typedef {object} Lifecycle
 * @property {() => boolean} isUnlocked Whether a user gesture has started audio.
 * @property {() => void} detach Removes every listener.
 */

/**
 * Starts the context on the first user gesture (browsers keep it suspended until then) and suspends it while the page
 * is hidden, so loops do not play in a background tab whose frames have stopped.
 *
 * @param {BaseAudioContext & { resume(): Promise<void>, suspend(): Promise<void> }} context
 * @param {LifecycleTarget} window Receives the gesture events.
 * @param {LifecycleTarget & { visibilityState: string }} document Receives `visibilitychange`.
 * @returns {Lifecycle}
 */
export function attachLifecycle(context, window, document) {
  let unlocked = context.state === 'running';

  /** @param {Promise<void>} promise */
  const ignore = (promise) => {
    promise.catch(() => {});
  };

  const onGesture = () => {
    unlocked = true;
    if (context.state !== 'running' && document.visibilityState !== 'hidden') {
      ignore(context.resume());
    }
  };

  const onVisibility = () => {
    if (document.visibilityState === 'hidden') {
      ignore(context.suspend());
    } else if (unlocked) {
      ignore(context.resume());
    }
  };

  for (const type of UNLOCK_EVENTS) {
    window.addEventListener(type, onGesture, { capture: true });
  }
  document.addEventListener('visibilitychange', onVisibility);

  return {
    isUnlocked: () => unlocked,
    detach() {
      for (const type of UNLOCK_EVENTS) {
        window.removeEventListener(type, onGesture, { capture: true });
      }
      document.removeEventListener('visibilitychange', onVisibility);
    },
  };
}

/**
 * A mixer for a browser without Web Audio: every sound fails to decode, and commands are dropped.
 * @returns {Mixer}
 */
export function createSilentMixer() {
  /** @type {number[]} */
  const events = [];
  const sounds = new Set();
  return {
    createSound(id) {
      sounds.add(id);
      events.push(SOUND_FAILED, id);
    },
    destroySound(id) {
      sounds.delete(id);
    },
    submit(_, eventBytes) {
      const count = Math.min(events.length / 2, Math.floor(eventBytes.byteLength / EVENT_SIZE));
      const out = new DataView(eventBytes.buffer, eventBytes.byteOffset, eventBytes.byteLength);
      for (let i = 0; i < count; i++) {
        out.setInt32(i * EVENT_SIZE, events[2 * i], true);
        out.setInt32(i * EVENT_SIZE + 4, events[2 * i + 1], true);
      }
      events.splice(0, 2 * count);
      return count;
    },
    activeVoices: 0,
    get soundCount() {
      return sounds.size;
    },
  };
}

/**
 * The span view that `[JSImport]` passes for a `Span<byte>`; valid only during the call.
 * @typedef {object} MemoryView
 * @property {() => Uint8Array} _unsafe_create_view A view on WASM memory, without a copy.
 */

/** @type {Mixer | undefined} */
let mixer;

/** @returns {Mixer} */
function getMixer() {
  if (mixer === undefined) {
    throw new Error('Web Audio is not initialized.');
  }
  return mixer;
}

/**
 * Creates the audio context, its buses and the unlock and visibility listeners. Bound from C# with `[JSImport]`.
 * Calling it again keeps the existing mixer. Without Web Audio the game runs silent.
 */
export function init() {
  if (mixer !== undefined) {
    return;
  }
  if (typeof globalThis.AudioContext !== 'function') {
    console.warn('This browser has no Web Audio; the game runs without sound.');
    mixer = createSilentMixer();
    return;
  }
  const context = new AudioContext();
  attachLifecycle(context, globalThis.window, globalThis.document);
  mixer = createMixer(context);
}

/**
 * Bound from C# with `[JSImport]`.
 * @param {number} id
 * @param {MemoryView} data
 */
export function createSound(id, data) {
  getMixer().createSound(id, data._unsafe_create_view());
}

/**
 * Bound from C# with `[JSImport]`.
 * @param {number} id
 */
export function destroySound(id) {
  getMixer().destroySound(id);
}

/**
 * Runs one frame. Bound from C# with `[JSImport]`.
 * @param {MemoryView} commands
 * @param {MemoryView} events
 * @returns {number} Events written.
 */
export function submit(commands, events) {
  return getMixer().submit(commands._unsafe_create_view(), events._unsafe_create_view());
}

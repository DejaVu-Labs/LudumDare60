// @ts-check
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

import {
  CONTEXT_LOST_TEXT,
  INSTANCE_SIZE,
  createRenderer,
} from '../../src/engine/platforms/LudumDare60.Engine.Platform.Browser/wwwroot/webgl2.js';

/** Shared with SpriteBatchTests.cs, which checks that C# records this frame as these bytes. */
const fixture = JSON.parse(readFileSync(new URL('../fixtures/render-frame.json', import.meta.url), 'utf8'));

/**
 * A stand-in for a WebGL2 context. Upper-case properties are the constant's name, `create*` methods return a new
 * object, the status queries succeed, and every other method is recorded as `[name, ...args]`.
 *
 * @param {{ lost?: boolean }} [options]
 */
function createFakeGl(options = {}) {
  /** @type {unknown[][]} */
  const calls = [];
  let nextObject = 1;
  const gl = new Proxy(
    {},
    {
      get(_, name) {
        if (typeof name !== 'string') {
          return undefined;
        }
        if (/^[A-Z0-9_]+$/.test(name)) {
          return name;
        }
        if (name === 'isContextLost') {
          return () => options.lost === true;
        }
        if (name === 'getShaderParameter' || name === 'getProgramParameter') {
          return () => true;
        }
        if (name === 'getUniformLocation') {
          return (/** @type {unknown} */ _program, /** @type {string} */ uniform) => uniform;
        }
        if (name.startsWith('create')) {
          return () => ({ kind: name.slice('create'.length), n: nextObject++ });
        }
        return (/** @type {unknown[]} */ ...args) => {
          calls.push([name, ...args]);
        };
      },
    },
  );
  return { gl: /** @type {WebGL2RenderingContext} */ (/** @type {unknown} */ (gl)), calls };
}

/** A surface that counts size writes, like a canvas whose size changes reset it. */
function createSurface() {
  let width = 300;
  let height = 150;
  const surface = {
    writes: 0,
    get width() {
      return width;
    },
    set width(value) {
      width = value;
      surface.writes++;
    },
    get height() {
      return height;
    },
    set height(value) {
      height = value;
      surface.writes++;
    },
  };
  return surface;
}

/** @param {string} hex */
function bytes(hex) {
  return Uint8Array.from(Buffer.from(hex, 'hex'));
}

function setUp(options = {}) {
  const { gl, calls } = createFakeGl(options);
  const surface = createSurface();
  const renderer = createRenderer(gl, surface);
  for (const texture of fixture.textures) {
    renderer.createTexture(texture.id, texture.width, texture.height, new Uint8Array(texture.width * texture.height * 4));
  }
  calls.length = 0;
  return { calls, surface, renderer };
}

/** @param {unknown[][]} calls @param {string} name */
function named(calls, name) {
  return calls.filter((call) => call[0] === name);
}

/**
 * @param {number} opcode
 * @param {number[]} args
 */
function command(opcode, ...args) {
  return new Uint8Array(Int32Array.from([opcode, ...args]).buffer);
}

const fixtureCommands = bytes(fixture.commandsHex);
const fixtureInstances = bytes(fixture.instancesHex);

test('createRenderer_SetsUpProgramAndPremultipliedBlending', () => {
  const { gl, calls } = createFakeGl();

  createRenderer(gl, createSurface());

  assert.equal(named(calls, 'linkProgram').length, 1);
  assert.deepEqual(named(calls, 'blendFunc'), [['blendFunc', 'ONE', 'ONE_MINUS_SRC_ALPHA']]);
  assert.deepEqual(named(calls, 'vertexAttribDivisor').map((call) => call[1]), [0, 1, 2, 3, 4, 5]);
});

test('submit_FixtureFrame_SizesSurfaceAndTarget', () => {
  const { calls, surface, renderer } = setUp();

  renderer.submit(fixtureCommands, fixtureInstances);

  assert.equal(surface.width, 1400);
  assert.equal(surface.height, 800);
  assert.deepEqual(named(calls, 'texStorage2D'), [['texStorage2D', 'TEXTURE_2D', 1, 'RGBA8', 640, 360]]);
});

test('submit_FixtureFrame_ClearsTargetWithColor', () => {
  const { calls, renderer } = setUp();

  renderer.submit(fixtureCommands, fixtureInstances);

  const [r, g, b, a] = fixture.clear;
  assert.deepEqual(named(calls, 'clearColor')[0], ['clearColor', r / 255, g / 255, b / 255, a / 255]);
});

test('submit_FixtureFrame_UploadsInstancesOnce', () => {
  const { calls, renderer } = setUp();

  renderer.submit(fixtureCommands, fixtureInstances);

  assert.deepEqual(named(calls, 'bufferData'), [['bufferData', 'ARRAY_BUFFER', 120, 'DYNAMIC_DRAW']]);
  assert.deepEqual(named(calls, 'bufferSubData'), [['bufferSubData', 'ARRAY_BUFFER', 0, fixtureInstances]]);
});

test('submit_FixtureFrame_DrawsEachRunFromItsFirstInstance', () => {
  const { calls, renderer } = setUp();

  renderer.submit(fixtureCommands, fixtureInstances);

  const draws = named(calls, 'drawArraysInstanced');
  assert.deepEqual(
    draws,
    fixture.draws.map((/** @type {{ count: number }} */ draw) => ['drawArraysInstanced', 'TRIANGLE_STRIP', 0, 4, draw.count]),
  );
  const positionPointers = named(calls, 'vertexAttribPointer').filter((call) => call[1] === 0);
  assert.deepEqual(
    positionPointers.map((call) => call[6]),
    fixture.draws.map((/** @type {{ first: number }} */ draw) => draw.first * INSTANCE_SIZE),
  );
  const textureSizes = named(calls, 'uniform2f').filter((call) => call[1] === 'u_textureSize');
  assert.deepEqual(textureSizes.map((call) => [call[2], call[3]]), [[32, 32], [16, 8]]);
});

test('submit_FixtureFrame_AttributeLayoutMatchesSpriteInstance', () => {
  const { calls, renderer } = setUp();

  renderer.submit(fixtureCommands, fixtureInstances);

  const firstDraw = named(calls, 'vertexAttribPointer').slice(0, 6);
  assert.deepEqual(firstDraw, [
    ['vertexAttribPointer', 0, 2, 'FLOAT', false, 40, 0],
    ['vertexAttribPointer', 1, 2, 'FLOAT', false, 40, 8],
    ['vertexAttribPointer', 2, 2, 'FLOAT', false, 40, 16],
    ['vertexAttribPointer', 3, 1, 'FLOAT', false, 40, 24],
    ['vertexAttribPointer', 4, 4, 'UNSIGNED_BYTE', true, 40, 28],
    ['vertexAttribPointer', 5, 4, 'UNSIGNED_SHORT', false, 40, 32],
  ]);
});

test('submit_FixtureFrame_PresentBlitsIntoViewportCountedFromBottom', () => {
  const { calls, renderer } = setUp();

  renderer.submit(fixtureCommands, fixtureInstances);

  // 1400x800 surface, 1280x720 screen at (60, 40) from the top: 40 rows of bar below it as well.
  assert.deepEqual(named(calls, 'blitFramebuffer'), [
    ['blitFramebuffer', 0, 0, 640, 360, 60, 40, 1340, 760, 'COLOR_BUFFER_BIT', 'NEAREST'],
  ]);
  const lastClear = named(calls, 'clearColor').at(-1);
  assert.deepEqual(lastClear, ['clearColor', 0, 0, 0, 1]);
});

test('submit_SecondFrameSameSizes_KeepsSurfaceTargetAndBuffer', () => {
  const { calls, surface, renderer } = setUp();
  renderer.submit(fixtureCommands, fixtureInstances);
  const writes = surface.writes;

  renderer.submit(fixtureCommands, fixtureInstances);

  assert.equal(surface.writes, writes);
  assert.equal(named(calls, 'texStorage2D').length, 1);
  assert.equal(named(calls, 'bufferData').length, 1);
});

test('submit_VirtualSizeChanges_RecreatesTarget', () => {
  const { calls, renderer } = setUp();
  renderer.submit(command(1, 640, 360, 320, 180, 0, 0, 640, 360), new Uint8Array(0));

  renderer.submit(command(1, 640, 360, 640, 360, 0, 0, 640, 360), new Uint8Array(0));

  assert.deepEqual(named(calls, 'texStorage2D').map((call) => [call[4], call[5]]), [[320, 180], [640, 360]]);
  assert.equal(named(calls, 'deleteFramebuffer').length, 2);
});

test('submit_ClearBeforeSetSurface_Throws', () => {
  const { renderer } = setUp();

  assert.throws(() => renderer.submit(command(2, 0), new Uint8Array(0)), /SetSurface must be the first/);
});

test('submit_UnknownOpcode_Throws', () => {
  const { renderer } = setUp();

  assert.throws(() => renderer.submit(command(99), new Uint8Array(0)), /Unknown render command 99 at byte 0/);
});

test('submit_DrawWithUnknownTexture_Throws', () => {
  const { renderer } = setUp();
  const frame = new Uint8Array([...fixtureCommands.subarray(0, 36), ...command(3, 7, 0, 1)]);

  assert.throws(() => renderer.submit(frame, fixtureInstances), /texture 7, which does not exist/);
});

test('submit_DrawPastInstances_Throws', () => {
  const { renderer } = setUp();
  const frame = new Uint8Array([...fixtureCommands.subarray(0, 36), ...command(3, 0, 2, 2)]);

  assert.throws(() => renderer.submit(frame, fixtureInstances), /instances 2\.\.4 of 3/);
});

test('submit_ContextLost_ThrowsWithPlayerText', () => {
  const { renderer } = setUp({ lost: true });

  assert.throws(() => renderer.submit(fixtureCommands, fixtureInstances), { message: CONTEXT_LOST_TEXT });
});

test('createTexture_UploadsNearestClampedRgba8', () => {
  const { calls, renderer } = setUp();
  const pixels = new Uint8Array(2 * 3 * 4);

  renderer.createTexture(5, 2, 3, pixels);

  assert.deepEqual(named(calls, 'texImage2D'), [
    ['texImage2D', 'TEXTURE_2D', 0, 'RGBA8', 2, 3, 0, 'RGBA', 'UNSIGNED_BYTE', pixels],
  ]);
  assert.deepEqual(
    named(calls, 'texParameteri').map((call) => call[3]),
    ['NEAREST', 'NEAREST', 'CLAMP_TO_EDGE', 'CLAMP_TO_EDGE'],
  );
  assert.equal(renderer.textureCount, 3);
});

test('createTexture_WrongByteCount_Throws', () => {
  const { renderer } = setUp();

  assert.throws(() => renderer.createTexture(5, 2, 2, new Uint8Array(15)), /needs 16 bytes, not 15/);
});

test('createTexture_ExistingId_Throws', () => {
  const { renderer } = setUp();

  assert.throws(() => renderer.createTexture(0, 1, 1, new Uint8Array(4)), /Texture 0 already exists/);
});

test('updateTexture_UploadsTheRowsWithTexSubImage2D', () => {
  const { calls, renderer } = setUp();
  renderer.createTexture(5, 2, 3, new Uint8Array(2 * 3 * 4));
  calls.length = 0;
  const rows = new Uint8Array(2 * 2 * 4);

  renderer.updateTexture(5, 1, 2, rows);

  assert.deepEqual(named(calls, 'texSubImage2D'), [
    ['texSubImage2D', 'TEXTURE_2D', 0, 0, 1, 2, 2, 'RGBA', 'UNSIGNED_BYTE', rows],
  ]);
  assert.equal(named(calls, 'bindTexture').length, 1);
});

test('updateTexture_RowsOutside_Throws', () => {
  const { renderer } = setUp();
  renderer.createTexture(5, 2, 3, new Uint8Array(2 * 3 * 4));

  assert.throws(() => renderer.updateTexture(5, 2, 2, new Uint8Array(16)), /Rows 2\+2 lie outside texture 5/);
  assert.throws(() => renderer.updateTexture(5, 0, 0, new Uint8Array(0)), /Rows 0\+0 lie outside texture 5/);
});

test('updateTexture_WrongByteCount_Throws', () => {
  const { renderer } = setUp();
  renderer.createTexture(5, 2, 3, new Uint8Array(2 * 3 * 4));

  assert.throws(() => renderer.updateTexture(5, 0, 1, new Uint8Array(4)), /1 rows of texture 5 need 8 bytes, not 4/);
});

test('updateTexture_UnknownId_Throws', () => {
  const { renderer } = setUp();

  assert.throws(() => renderer.updateTexture(9, 0, 1, new Uint8Array(4)), /Texture 9 does not exist/);
});

test('destroyTexture_FreesIdForReuse', () => {
  const { calls, renderer } = setUp();

  renderer.destroyTexture(1);
  renderer.createTexture(1, 1, 1, new Uint8Array(4));

  assert.equal(named(calls, 'deleteTexture').length, 1);
  assert.equal(renderer.textureCount, 2);
});

test('destroyTexture_UnknownId_Throws', () => {
  const { renderer } = setUp();

  assert.throws(() => renderer.destroyTexture(9), /Texture 9 does not exist/);
});

// @ts-check

import { setErrorText } from './boot.js';
import { spriteFragment, spriteVertex } from './shaders/sprite.glsl.js';

/**
 * WebGL2 backend of the browser platform. C# records a frame as a command stream plus an array of sprite instances
 * and passes both in one `submit` call. Textures are created and destroyed with their own calls, outside frames.
 *
 * The scene is drawn into a texture of the virtual screen size (for example 640x360), then `Present` scales it onto
 * the canvas with nearest filtering into the rectangle C# computed (`PixelViewport`), with black bars around it.
 *
 * The command layout is documented on `RenderCommandKind` (C#), the instance layout on `SpriteInstance` (C#).
 */

/** Opcodes of the command stream. Values match `RenderCommandKind` (C#). */
export const SET_SURFACE = 1;
export const CLEAR = 2;
export const DRAW_SPRITES = 3;
export const PRESENT = 4;

/** Bytes per sprite instance (`SpriteInstance.Size`). */
export const INSTANCE_SIZE = 40;

/** Shown in the page's status element when the browser has no WebGL2. */
export const UNSUPPORTED_TEXT = 'This game needs WebGL2, which this browser does not support or has turned off.';

/** Shown in the page's status element when the browser takes the GPU away (driver reset, too many contexts). */
export const CONTEXT_LOST_TEXT = 'The graphics context was lost. Reload the page to continue.';

/**
 * Context attributes: an opaque canvas without multisampling or depth. Pixel art needs neither, and `blitFramebuffer`
 * into a multisampled canvas fails.
 * @type {WebGLContextAttributes}
 */
export const CONTEXT_ATTRIBUTES = Object.freeze({
  alpha: false,
  antialias: false,
  depth: false,
  stencil: false,
  premultipliedAlpha: false,
  preserveDrawingBuffer: false,
});

/**
 * The part of a canvas the renderer touches, so tests can pass a plain object.
 * @typedef {{ width: number, height: number }} Surface
 */

/**
 * @typedef {object} Renderer
 * @property {(id: number, width: number, height: number, pixels: Uint8Array) => void} createTexture Uploads RGBA8
 *   pixels, rows from the top, straight alpha.
 * @property {(id: number) => void} destroyTexture
 * @property {(commands: Uint8Array, instances: Uint8Array) => void} submit Runs one frame. Both arrays are read during
 *   the call only.
 * @property {number} textureCount Live textures, render target excluded.
 */

/**
 * Creates the sprite renderer on a WebGL2 context.
 *
 * @param {WebGL2RenderingContext} gl
 * @param {Surface} surface The canvas: `SetSurface` sets its size.
 * @returns {Renderer}
 */
export function createRenderer(gl, surface) {
  const program = createProgram(gl, spriteVertex, spriteFragment);
  const screenSizeLocation = gl.getUniformLocation(program, 'u_screenSize');
  const textureSizeLocation = gl.getUniformLocation(program, 'u_textureSize');
  gl.useProgram(program);
  gl.uniform1i(gl.getUniformLocation(program, 'u_texture'), 0);

  const vao = gl.createVertexArray();
  const instanceBuffer = gl.createBuffer();
  let instanceBufferSize = 0;
  gl.bindVertexArray(vao);
  gl.bindBuffer(gl.ARRAY_BUFFER, instanceBuffer);
  for (let location = 0; location <= 5; location++) {
    gl.enableVertexAttribArray(location);
    gl.vertexAttribDivisor(location, 1);
  }
  gl.bindVertexArray(null);

  gl.pixelStorei(gl.UNPACK_ALIGNMENT, 1);
  gl.enable(gl.BLEND);
  gl.blendFunc(gl.ONE, gl.ONE_MINUS_SRC_ALPHA);

  /** @type {(WebGLTexture | undefined)[]} */
  const textures = [];
  /** @type {number[]} Width and height of texture `id` at `2 * id` and `2 * id + 1`. */
  const textureSizes = [];
  let textureCount = 0;

  /** The virtual screen: a texture and the framebuffer that draws into it. */
  /** @type {WebGLTexture | null} */
  let targetTexture = null;
  /** @type {WebGLFramebuffer | null} */
  let target = null;
  let targetWidth = 0;
  let targetHeight = 0;
  // Where Present puts the virtual screen, in device pixels from the top-left corner.
  let viewX = 0;
  let viewY = 0;
  let viewWidth = 0;
  let viewHeight = 0;

  /**
   * @param {number} width
   * @param {number} height
   */
  function ensureTarget(width, height) {
    if (target !== null && width === targetWidth && height === targetHeight) {
      return;
    }
    gl.deleteFramebuffer(target);
    gl.deleteTexture(targetTexture);
    targetTexture = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, targetTexture);
    gl.texStorage2D(gl.TEXTURE_2D, 1, gl.RGBA8, width, height);
    setNearest(gl);
    target = gl.createFramebuffer();
    gl.bindFramebuffer(gl.FRAMEBUFFER, target);
    gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, targetTexture, 0);
    targetWidth = width;
    targetHeight = height;
  }

  function bindTarget() {
    if (target === null) {
      throw new Error('SetSurface must be the first render command.');
    }
    gl.bindFramebuffer(gl.FRAMEBUFFER, target);
    gl.viewport(0, 0, targetWidth, targetHeight);
  }

  /** @param {number} first Index of the first instance of the draw. */
  function pointInstances(first) {
    const base = first * INSTANCE_SIZE;
    gl.vertexAttribPointer(0, 2, gl.FLOAT, false, INSTANCE_SIZE, base);
    gl.vertexAttribPointer(1, 2, gl.FLOAT, false, INSTANCE_SIZE, base + 8);
    gl.vertexAttribPointer(2, 2, gl.FLOAT, false, INSTANCE_SIZE, base + 16);
    gl.vertexAttribPointer(3, 1, gl.FLOAT, false, INSTANCE_SIZE, base + 24);
    gl.vertexAttribPointer(4, 4, gl.UNSIGNED_BYTE, true, INSTANCE_SIZE, base + 28);
    gl.vertexAttribPointer(5, 4, gl.UNSIGNED_SHORT, false, INSTANCE_SIZE, base + 32);
  }

  return {
    createTexture(id, width, height, pixels) {
      if (textures[id] !== undefined) {
        throw new Error(`Texture ${id} already exists.`);
      }
      if (pixels.byteLength !== width * height * 4) {
        throw new Error(`A ${width}x${height} texture needs ${width * height * 4} bytes, not ${pixels.byteLength}.`);
      }
      const texture = gl.createTexture();
      gl.bindTexture(gl.TEXTURE_2D, texture);
      gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, width, height, 0, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
      setNearest(gl);
      textures[id] = texture ?? undefined;
      textureSizes[2 * id] = width;
      textureSizes[2 * id + 1] = height;
      textureCount++;
    },

    destroyTexture(id) {
      const texture = textures[id];
      if (texture === undefined) {
        throw new Error(`Texture ${id} does not exist.`);
      }
      gl.deleteTexture(texture);
      textures[id] = undefined;
      textureCount--;
    },

    submit(commands, instances) {
      if (gl.isContextLost()) {
        throw new Error(CONTEXT_LOST_TEXT);
      }

      const instanceCount = Math.floor(instances.byteLength / INSTANCE_SIZE);
      if (instances.byteLength > 0) {
        gl.bindBuffer(gl.ARRAY_BUFFER, instanceBuffer);
        if (instances.byteLength > instanceBufferSize) {
          instanceBufferSize = Math.max(instances.byteLength, 2 * instanceBufferSize);
          gl.bufferData(gl.ARRAY_BUFFER, instanceBufferSize, gl.DYNAMIC_DRAW);
        }
        gl.bufferSubData(gl.ARRAY_BUFFER, 0, instances);
      }

      const view = new DataView(commands.buffer, commands.byteOffset, commands.byteLength);
      let offset = 0;
      while (offset < view.byteLength) {
        const opcode = view.getInt32(offset, true);
        switch (opcode) {
          case SET_SURFACE: {
            const width = view.getInt32(offset + 4, true);
            const height = view.getInt32(offset + 8, true);
            if (surface.width !== width) {
              surface.width = width;
            }
            if (surface.height !== height) {
              surface.height = height;
            }
            ensureTarget(view.getInt32(offset + 12, true), view.getInt32(offset + 16, true));
            viewX = view.getInt32(offset + 20, true);
            viewY = view.getInt32(offset + 24, true);
            viewWidth = view.getInt32(offset + 28, true);
            viewHeight = view.getInt32(offset + 32, true);
            offset += 36;
            break;
          }
          case CLEAR: {
            bindTarget();
            gl.clearColor(
              view.getUint8(offset + 4) / 255,
              view.getUint8(offset + 5) / 255,
              view.getUint8(offset + 6) / 255,
              view.getUint8(offset + 7) / 255,
            );
            gl.clear(gl.COLOR_BUFFER_BIT);
            offset += 8;
            break;
          }
          case DRAW_SPRITES: {
            const id = view.getInt32(offset + 4, true);
            const first = view.getInt32(offset + 8, true);
            const count = view.getInt32(offset + 12, true);
            const texture = textures[id];
            if (texture === undefined) {
              throw new Error(`DrawSprites uses texture ${id}, which does not exist.`);
            }
            if (first < 0 || count < 1 || first + count > instanceCount) {
              throw new Error(`DrawSprites reads instances ${first}..${first + count} of ${instanceCount}.`);
            }
            bindTarget();
            gl.useProgram(program);
            gl.uniform2f(screenSizeLocation, targetWidth, targetHeight);
            gl.uniform2f(textureSizeLocation, textureSizes[2 * id], textureSizes[2 * id + 1]);
            gl.activeTexture(gl.TEXTURE0);
            gl.bindTexture(gl.TEXTURE_2D, texture);
            gl.bindVertexArray(vao);
            gl.bindBuffer(gl.ARRAY_BUFFER, instanceBuffer);
            pointInstances(first);
            gl.drawArraysInstanced(gl.TRIANGLE_STRIP, 0, 4, count);
            gl.bindVertexArray(null);
            offset += 16;
            break;
          }
          case PRESENT: {
            if (target === null) {
              throw new Error('SetSurface must be the first render command.');
            }
            gl.bindFramebuffer(gl.FRAMEBUFFER, null);
            gl.viewport(0, 0, surface.width, surface.height);
            gl.clearColor(0, 0, 0, 1);
            gl.clear(gl.COLOR_BUFFER_BIT);
            gl.bindFramebuffer(gl.READ_FRAMEBUFFER, target);
            // GL counts rows from the bottom; the viewport rectangle counts them from the top.
            const bottom = surface.height - viewY - viewHeight;
            gl.blitFramebuffer(
              0, 0, targetWidth, targetHeight,
              viewX, bottom, viewX + viewWidth, bottom + viewHeight,
              gl.COLOR_BUFFER_BIT, gl.NEAREST,
            );
            gl.bindFramebuffer(gl.READ_FRAMEBUFFER, null);
            offset += 4;
            break;
          }
          default:
            throw new Error(`Unknown render command ${opcode} at byte ${offset}.`);
        }
      }
    },

    get textureCount() {
      return textureCount;
    },
  };
}

/**
 * @param {WebGL2RenderingContext} gl
 * @param {string} vertexSource
 * @param {string} fragmentSource
 * @returns {WebGLProgram}
 */
export function createProgram(gl, vertexSource, fragmentSource) {
  const program = gl.createProgram();
  const vertex = compileShader(gl, gl.VERTEX_SHADER, vertexSource);
  const fragment = compileShader(gl, gl.FRAGMENT_SHADER, fragmentSource);
  gl.attachShader(program, vertex);
  gl.attachShader(program, fragment);
  gl.linkProgram(program);
  if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
    throw new Error(`Linking the sprite shaders failed: ${gl.getProgramInfoLog(program)}`);
  }
  gl.deleteShader(vertex);
  gl.deleteShader(fragment);
  return program;
}

/**
 * @param {WebGL2RenderingContext} gl
 * @param {number} type
 * @param {string} source
 * @returns {WebGLShader}
 */
function compileShader(gl, type, source) {
  const shader = gl.createShader(type);
  if (shader === null) {
    throw new Error('Creating a shader failed.');
  }
  gl.shaderSource(shader, source);
  gl.compileShader(shader);
  if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
    throw new Error(`Compiling a sprite shader failed: ${gl.getShaderInfoLog(shader)}`);
  }
  return shader;
}

/**
 * Nearest filtering and clamped edges for the texture bound to `TEXTURE_2D`.
 * @param {WebGL2RenderingContext} gl
 */
function setNearest(gl) {
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
}

/**
 * The span view that `[JSImport]` passes for a `Span<byte>`; valid only during the call.
 * @typedef {object} MemoryView
 * @property {() => Uint8Array} _unsafe_create_view A view on WASM memory, without a copy.
 */

/** @type {{ canvasId: string, renderer: Renderer } | undefined} */
let active;

/** @returns {Renderer} */
function getRenderer() {
  if (active === undefined) {
    throw new Error('The WebGL2 renderer is not initialized.');
  }
  return active.renderer;
}

/**
 * Creates the WebGL2 context on the canvas with id `canvasId` and the renderer on it. Bound from C# with `[JSImport]`.
 * Calling it again with the same canvas keeps the existing renderer and its textures.
 *
 * @param {string} canvasId
 */
export function init(canvasId) {
  if (active !== undefined) {
    if (active.canvasId !== canvasId) {
      throw new Error(`The renderer already draws to canvas "${active.canvasId}".`);
    }
    return;
  }
  const canvas = document.getElementById(canvasId);
  if (!(canvas instanceof HTMLCanvasElement)) {
    throw new Error(`The page has no canvas with id "${canvasId}".`);
  }
  const gl = canvas.getContext('webgl2', CONTEXT_ATTRIBUTES);
  if (gl === null) {
    setErrorText(UNSUPPORTED_TEXT);
    throw new Error('WebGL2 is not available.');
  }
  // No restore: the next submit throws, the game stops, and boot.js shows the text.
  canvas.addEventListener('webglcontextlost', () => setErrorText(CONTEXT_LOST_TEXT));
  active = { canvasId, renderer: createRenderer(gl, canvas) };
}

/**
 * Bound from C# with `[JSImport]`.
 * @param {number} id
 * @param {number} width
 * @param {number} height
 * @param {MemoryView} pixels
 */
export function createTexture(id, width, height, pixels) {
  getRenderer().createTexture(id, width, height, pixels._unsafe_create_view());
}

/**
 * Bound from C# with `[JSImport]`.
 * @param {number} id
 */
export function destroyTexture(id) {
  getRenderer().destroyTexture(id);
}

/**
 * Runs one frame. Bound from C# with `[JSImport]`.
 * @param {MemoryView} commands
 * @param {MemoryView} instances
 */
export function submit(commands, instances) {
  getRenderer().submit(commands._unsafe_create_view(), instances._unsafe_create_view());
}

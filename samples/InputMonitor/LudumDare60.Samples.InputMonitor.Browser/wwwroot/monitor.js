// @ts-check

/**
 * Page side of the input monitor sample: shows the report text and locks the pointer on double-click, so
 * `PointerMotion` and `PointerLockChanged` can be tried. Locking belongs to a future platform command channel; the
 * sample page does it itself until then.
 */

const decoder = new TextDecoder();

/** @type {HTMLElement | null} */
let output = null;

// Optional chaining keeps the module importable outside a page (Node tests).
const canvas = globalThis.document?.getElementById('screen');
canvas?.addEventListener('dblclick', () => {
  canvas.requestPointerLock();
});

/**
 * Replaces the monitor text. Bound from C# with `[JSImport]`.
 *
 * @param {{ slice: () => Uint8Array }} utf8 Span view, valid only during the call.
 */
export function show(utf8) {
  output ??= globalThis.document?.getElementById('monitor') ?? null;
  if (output !== null) {
    output.textContent = decoder.decode(utf8.slice());
  }
}

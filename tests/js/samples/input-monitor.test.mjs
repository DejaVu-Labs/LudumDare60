// @ts-check
import { test } from 'node:test';
import assert from 'node:assert/strict';

/** A fake page with the monitor element; set before the module is imported. */
const monitor = { textContent: '' };
/** @type {Record<string, unknown>} */
const elements = { monitor };
Object.defineProperty(globalThis, 'document', {
  configurable: true,
  value: { getElementById: (/** @type {string} */ id) => elements[id] ?? null },
});

const { show } = await import('../../../samples/InputMonitor/LudumDare60.Samples.InputMonitor.Browser/wwwroot/monitor.js');

/** Mimics the span view `[JSImport]` passes: `slice()` copies the bytes out. */
const view = (/** @type {string} */ text) => {
  const bytes = new TextEncoder().encode(text);
  return { slice: () => bytes.slice() };
};

test('show_DecodesUtf8IntoMonitorElement', () => {
  show(view('Keys       W ShiftLeft\n→ ok'));

  assert.equal(monitor.textContent, 'Keys       W ShiftLeft\n→ ok');
});

test('show_ReplacesPreviousText', () => {
  show(view('first'));
  show(view('second'));

  assert.equal(monitor.textContent, 'second');
});

// @ts-check
import { test } from 'node:test';
import assert from 'node:assert/strict';

import { createAssetReader } from '../../src/engine/platforms/LudumDare60.Engine.Platform.Browser/wwwroot/assets.js';

const page = 'https://html-classic.itch.zone/html/123/index.html?v=456';

/**
 * A fetch stand-in that serves `files` by absolute URL and records every request.
 * @param {Record<string, Uint8Array>} files
 */
function createFetch(files) {
  /** @type {{ url: string, cache: RequestCache }[]} */
  const requests = [];
  /** @type {(url: string, init: { cache: RequestCache }) => Promise<Response>} */
  const fetchFile = async (url, init) => {
    requests.push({ url, cache: init.cache });
    const body = files[url];
    return body === undefined ? new Response(null, { status: 404 }) : new Response(body);
  };
  return { fetchFile, requests };
}

test('read_RelativeUrl_ResolvesAgainstPageNotModule', async () => {
  const { fetchFile, requests } = createFetch({});
  const reader = createAssetReader(fetchFile, () => page);

  await reader.read(1, 'assets/sounds/click.ogg?v=abc', false);

  assert.equal(requests[0].url, 'https://html-classic.itch.zone/html/123/assets/sounds/click.ogg?v=abc');
});

test('read_NoCacheFlag_PicksCacheMode', async () => {
  const { fetchFile, requests } = createFetch({});
  const reader = createAssetReader(fetchFile, () => page);

  await reader.read(1, 'a.bin', false);
  await reader.read(2, 'a.bin', true);

  assert.deepEqual(
    requests.map((request) => request.cache),
    ['default', 'no-cache'],
  );
});

test('read_Found_ResolvesLengthAndTakeCopiesBytes', async () => {
  const { fetchFile } = createFetch({ 'https://html-classic.itch.zone/html/123/a.bin': new Uint8Array([1, 2, 3]) });
  const reader = createAssetReader(fetchFile, () => page);

  const length = await reader.read(7, 'a.bin', false);
  const destination = new Uint8Array(length);
  reader.take(7, destination);

  assert.equal(length, 3);
  assert.deepEqual([...destination], [1, 2, 3]);
  assert.equal(reader.pendingCount, 0);
});

test('read_EmptyFile_ResolvesZeroAndCanBeTaken', async () => {
  const { fetchFile } = createFetch({ 'https://html-classic.itch.zone/html/123/empty': new Uint8Array(0) });
  const reader = createAssetReader(fetchFile, () => page);

  assert.equal(await reader.read(1, 'empty', false), 0);
  reader.take(1, new Uint8Array(0));
  assert.equal(reader.pendingCount, 0);
});

test('read_HttpErrorStatus_ResolvesMinusStatusAndKeepsNothing', async () => {
  const { fetchFile } = createFetch({});
  const reader = createAssetReader(fetchFile, () => page);

  assert.equal(await reader.read(1, 'missing.png', false), -404);
  assert.equal(reader.pendingCount, 0);
  assert.throws(() => reader.take(1, new Uint8Array(0)));
});

test('read_NetworkError_RejectsAndKeepsNothing', async () => {
  const failure = new TypeError('Failed to fetch');
  const reader = createAssetReader(
    () => Promise.reject(failure),
    () => page,
  );

  await assert.rejects(reader.read(1, 'a.bin', false), (error) => error === failure);
  assert.equal(reader.pendingCount, 0);
});

test('read_IdStillPending_Rejects', async () => {
  const { fetchFile } = createFetch({ 'https://html-classic.itch.zone/html/123/a.bin': new Uint8Array([1]) });
  const reader = createAssetReader(fetchFile, () => page);
  await reader.read(1, 'a.bin', false);

  await assert.rejects(reader.read(1, 'a.bin', false));
});

test('take_UnknownId_Throws', () => {
  const reader = createAssetReader(createFetch({}).fetchFile, () => page);

  assert.throws(() => reader.take(42, new Uint8Array(0)), /No asset read 42/);
});

test('take_WrongDestinationLength_ThrowsAndForgetsTheRead', async () => {
  const { fetchFile } = createFetch({ 'https://html-classic.itch.zone/html/123/a.bin': new Uint8Array([1, 2]) });
  const reader = createAssetReader(fetchFile, () => page);
  await reader.read(1, 'a.bin', false);

  assert.throws(() => reader.take(1, new Uint8Array(1)), /2 bytes/);
  assert.equal(reader.pendingCount, 0);
});

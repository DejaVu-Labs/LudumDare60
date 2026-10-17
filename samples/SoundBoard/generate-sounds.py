#!/usr/bin/env python3
"""Synthesizes the SoundBoard sample's test sounds, so the sample needs no recordings.

Effects are written as 16-bit mono WAV, the ambience loop and the music tracks as OGG Vorbis (the formats the game
ships, see docs/tech/architecture.md#audio). Loops are rendered with wrap-around, so they repeat without a seam.

OGG encoding calls the system's libvorbisenc and libogg through ctypes (Debian/Ubuntu: libvorbisenc2, libogg0).
Only the Python standard library is needed. Output is deterministic: the noise generator has a fixed seed.

    python3 samples/SoundBoard/generate-sounds.py
"""

import ctypes
import ctypes.util
import math
import random
import struct
import wave
from pathlib import Path

OUT = Path(__file__).parent / "LudumDare60.Samples.SoundBoard.Browser" / "wwwroot" / "assets"
SFX_RATE = 22050
LOOP_RATE = 44100

rng = random.Random(60)


def silence(seconds, rate):
    return [0.0] * int(seconds * rate)


def envelope(t, attack, decay):
    """A short linear attack, then an exponential decay with time constant `decay`."""
    if t < attack:
        return t / attack
    return math.exp(-(t - attack) / decay)


def click(rate=SFX_RATE):
    out = silence(0.06, rate)
    previous = 0.0
    for i in range(len(out)):
        noise = rng.uniform(-1, 1)
        # A first difference keeps the highs: a dry, tight click.
        out[i] = (noise - previous) * 0.5 * envelope(i / rate, 0.0005, 0.008)
        previous = noise
    return out


def clink(rate=SFX_RATE):
    out = silence(0.6, rate)
    partials = [(2093, 1.0, 0.18), (3390, 0.6, 0.12), (5650, 0.35, 0.07), (7920, 0.2, 0.04)]
    for i in range(len(out)):
        t = i / rate
        out[i] = sum(a * math.sin(2 * math.pi * f * t) * envelope(t, 0.001, d) for f, a, d in partials) * 0.4
    return out


def squeak(rate=SFX_RATE):
    length = 0.28
    out = silence(length, rate)
    phase = 0.0
    for i in range(len(out)):
        t = i / rate
        frequency = 900 + 500 * (t / length) + 40 * math.sin(2 * math.pi * 23 * t)
        phase += 2 * math.pi * frequency / rate
        shape = math.sin(math.pi * t / length)
        # A little odd-harmonic edge makes it read as friction, not a whistle.
        out[i] = (math.sin(phase) + 0.3 * math.sin(3 * phase)) * shape * 0.35
    return out


def ratchet(rate=SFX_RATE):
    out = silence(0.22, rate)
    for n in range(4):
        start = int((0.012 + n * 0.045) * rate)
        tick = click(rate)
        for i, s in enumerate(tick):
            if start + i < len(out):
                out[start + i] += s * (1.0 - 0.15 * n)
    return out


def drop(rate=SFX_RATE):
    out = silence(0.18, rate)
    phase = 0.0
    for i in range(len(out)):
        t = i / rate
        frequency = 400 + 900 * math.exp(-t / 0.02)
        phase += 2 * math.pi * frequency / rate
        out[i] = math.sin(phase) * envelope(t, 0.002, 0.04) * 0.6
    return out


def spring(rate=SFX_RATE):
    out = silence(0.7, rate)
    phase = 0.0
    for i in range(len(out)):
        t = i / rate
        frequency = 180 * (1 + 0.35 * math.sin(2 * math.pi * 11 * t) * math.exp(-t / 0.3))
        phase += 2 * math.pi * frequency / rate
        out[i] = (math.sin(phase) + 0.4 * math.sin(2.01 * phase)) * envelope(t, 0.003, 0.18) * 0.45
    return out


def dry_fire(rate=SFX_RATE):
    out = silence(0.25, rate)
    tick = click(rate)
    for i in range(len(out)):
        t = i / rate
        body = math.sin(2 * math.pi * 110 * t) * envelope(t, 0.001, 0.05) * 0.5
        out[i] = body + (tick[i] * 1.3 if i < len(tick) else 0.0)
    return out


def rain(rate=LOOP_RATE, seconds=6.0):
    """Low-passed noise with scattered droplets, wrapped so it loops without a seam."""
    n = int(seconds * rate)
    out = [0.0] * n
    low = 0.0
    # Two passes: the first warms the filter up, so the start matches the end.
    for _ in range(2):
        for i in range(n):
            low += 0.08 * (rng.uniform(-1, 1) - low)
            out[i] = low * 0.9
    for _ in range(int(seconds * 40)):
        start = rng.randrange(n)
        pitch = rng.uniform(2500, 6000)
        gain = rng.uniform(0.05, 0.18)
        for i in range(int(0.02 * rate)):
            t = i / rate
            out[(start + i) % n] += math.sin(2 * math.pi * pitch * t) * math.exp(-t / 0.004) * gain
    return out


def note_into(out, rate, start, frequency, length, gain, harmonics):
    """Adds one plucked note, wrapping its tail around to the start of the loop."""
    n = len(out)
    begin = int(start * rate)
    for i in range(int(length * rate)):
        t = i / rate
        value = sum(a * math.sin(2 * math.pi * frequency * k * t) for k, a in harmonics)
        out[(begin + i) % n] += value * envelope(t, 0.005, length / 4) * gain


def midi(note):
    return 440.0 * 2 ** ((note - 69) / 12)


def track(rate, chords, voice, bass_voice, beats_per_chord=8, bpm=120):
    beat = 60 / bpm
    out = [0.0] * int(len(chords) * beats_per_chord * beat * rate)
    for c, chord in enumerate(chords):
        bar = c * beats_per_chord * beat
        note_into(out, rate, bar, midi(chord[0] - 12), beat * 4, 0.35, bass_voice)
        note_into(out, rate, bar + beat * 4, midi(chord[0] - 12), beat * 4, 0.3, bass_voice)
        for step in range(beats_per_chord * 2):
            pitch = chord[step % len(chord)] + (12 if step % 8 >= 4 else 0)
            note_into(out, rate, bar + step * beat / 2, midi(pitch), beat * 1.5, 0.12, voice)
    peak = max(abs(s) for s in out)
    return [s * 0.8 / peak for s in out]


def track_a(rate=LOOP_RATE):
    # C minor, A flat, E flat, G: a calm workbench loop.
    return track(rate, [[60, 63, 67], [56, 60, 63], [63, 67, 70], [55, 59, 62]],
                 voice=[(1, 1.0), (2, 0.25), (3, 0.1)], bass_voice=[(1, 1.0), (2, 0.4)])


def track_b(rate=LOOP_RATE):
    # F major, D minor, B flat, C: brighter, with a hollow, square-ish lead.
    return track(rate, [[65, 69, 72], [62, 65, 69], [58, 62, 65], [60, 64, 67]],
                 voice=[(1, 1.0), (3, 0.33), (5, 0.2)], bass_voice=[(1, 1.0), (3, 0.2)], bpm=132)


def normalized(samples, peak=0.9):
    top = max(abs(s) for s in samples) or 1.0
    return [s * peak / top for s in samples]


def write_wav(path, samples, rate):
    path.parent.mkdir(parents=True, exist_ok=True)
    frames = b"".join(struct.pack("<h", int(max(-1.0, min(1.0, s)) * 32767)) for s in samples)
    with wave.open(str(path), "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(rate)
        f.writeframes(frames)


class OggPage(ctypes.Structure):
    _fields_ = [("header", ctypes.c_void_p), ("header_len", ctypes.c_long),
                ("body", ctypes.c_void_p), ("body_len", ctypes.c_long)]


class OggPacket(ctypes.Structure):
    _fields_ = [("packet", ctypes.c_void_p), ("bytes", ctypes.c_long), ("b_o_s", ctypes.c_long),
                ("e_o_s", ctypes.c_long), ("granulepos", ctypes.c_int64), ("packetno", ctypes.c_int64)]


def load(name, soname):
    path = ctypes.util.find_library(name) or soname
    return ctypes.CDLL(path)


def write_ogg(path, samples, rate, quality=0.4):
    """Encodes mono float samples as OGG Vorbis (VBR) through libvorbisenc."""
    ogg = load("ogg", "libogg.so.0")
    vorbis = load("vorbis", "libvorbis.so.0")
    vorbisenc = load("vorbisenc", "libvorbisenc.so.2")
    vorbis.vorbis_analysis_buffer.restype = ctypes.POINTER(ctypes.POINTER(ctypes.c_float))
    vorbisenc.vorbis_encode_init_vbr.argtypes = [ctypes.c_void_p, ctypes.c_long, ctypes.c_long, ctypes.c_float]

    # The library structs are only passed by pointer: opaque buffers far larger than any of them are enough.
    def opaque():
        return ctypes.create_string_buffer(64 * 1024)

    info, comment, dsp, block, stream = opaque(), opaque(), opaque(), opaque(), opaque()
    page, packet = OggPage(), OggPacket()
    headers = [OggPacket(), OggPacket(), OggPacket()]

    vorbis.vorbis_info_init(info)
    if vorbisenc.vorbis_encode_init_vbr(info, 1, rate, quality) != 0:
        raise RuntimeError("vorbis_encode_init_vbr failed")
    vorbis.vorbis_comment_init(comment)
    vorbis.vorbis_comment_add_tag(comment, b"ENCODER", b"generate-sounds.py")
    vorbis.vorbis_analysis_init(dsp, info)
    vorbis.vorbis_block_init(dsp, block)
    ogg.ogg_stream_init(stream, 60)

    out = bytearray()

    def write_pages(flush):
        step = ogg.ogg_stream_flush if flush else ogg.ogg_stream_pageout
        while step(stream, ctypes.byref(page)) != 0:
            out.extend(ctypes.string_at(page.header, page.header_len))
            out.extend(ctypes.string_at(page.body, page.body_len))

    vorbis.vorbis_analysis_headerout(dsp, comment, *(ctypes.byref(h) for h in headers))
    for header in headers:
        ogg.ogg_stream_packetin(stream, ctypes.byref(header))
    write_pages(flush=True)

    def drain():
        while vorbis.vorbis_analysis_blockout(dsp, block) == 1:
            vorbis.vorbis_analysis(block, None)
            vorbis.vorbis_bitrate_addblock(block)
            while vorbis.vorbis_bitrate_flushpacket(dsp, ctypes.byref(packet)) == 1:
                ogg.ogg_stream_packetin(stream, ctypes.byref(packet))
                write_pages(flush=False)

    chunk = 1024
    for start in range(0, len(samples), chunk):
        part = samples[start:start + chunk]
        buffer = vorbis.vorbis_analysis_buffer(dsp, len(part))
        channel = buffer[0]
        for i, s in enumerate(part):
            channel[i] = s
        vorbis.vorbis_analysis_wrote(dsp, len(part))
        drain()
    vorbis.vorbis_analysis_wrote(dsp, 0)
    drain()
    write_pages(flush=True)

    ogg.ogg_stream_clear(stream)
    vorbis.vorbis_block_clear(block)
    vorbis.vorbis_dsp_clear(dsp)
    vorbis.vorbis_comment_clear(comment)
    vorbis.vorbis_info_clear(info)

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(bytes(out))


def main():
    effects = {
        "click": click, "clink": clink, "squeak": squeak, "ratchet": ratchet,
        "drop": drop, "spring": spring, "dry-fire": dry_fire,
    }
    for name, make in effects.items():
        write_wav(OUT / "sfx" / f"{name}.wav", normalized(make()), SFX_RATE)
    write_ogg(OUT / "ambience" / "rain.ogg", normalized(rain(), 0.5), LOOP_RATE)
    write_ogg(OUT / "music" / "track-a.ogg", track_a(), LOOP_RATE)
    write_ogg(OUT / "music" / "track-b.ogg", track_b(), LOOP_RATE)
    for path in sorted(OUT.rglob("*.*")):
        print(f"{path.relative_to(OUT)}  {path.stat().st_size} bytes")


if __name__ == "__main__":
    main()

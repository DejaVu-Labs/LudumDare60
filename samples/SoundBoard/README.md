# Sound board

A browser sample that plays every kind of sound the engine supports, so a browser and its speakers can be checked by
ear: one-shot effects with a random pitch and pointer pan, a rain loop that follows the pointer, two music tracks
with a crossfade, and a master mute.

| Key | Does |
| --- | --- |
| 1-7 | Effects: click, clink, squeak, ratchet, drop, spring, dry fire. The pointer's x position pans them. |
| L | Rain loop on and off, with a 1 s fade. The pointer's x position pans it, and its y position sets the volume. |
| Q, W | Music track A or B, with a 2 s crossfade. |
| S | Stops the music, with a 2 s fade. |
| M | Mutes and unmutes the master bus. |

Audio starts with the first click or key press. Each effect has a pad on screen that lights up when it plays. A pad
is dark while its file decodes and red if decoding failed (for example Ogg in Safari before 18.4).

| Project | Contents |
| --- | --- |
| `LudumDare60.Samples.SoundBoard` | `SoundBoardGame : IGame` and `SoundPaths`. Plain `net10.0`, tested in `tests/LudumDare60.Samples.SoundBoard.Tests`. |
| `LudumDare60.Samples.SoundBoard.Browser` | Entry point, page with the key legend, and the sound files in `wwwroot/assets/`. |

```bash
dotnet run --project samples/SoundBoard/LudumDare60.Samples.SoundBoard.Browser
```

The sounds are synthesized, not recorded. `generate-sounds.py` writes the effects as 16-bit mono WAV and the loops
as OGG Vorbis (through the system's libvorbisenc and libogg, with only the Python standard library). The loops wrap
around, so they repeat without a seam. Run it again after changing a sound:

```bash
python3 samples/SoundBoard/generate-sounds.py
```

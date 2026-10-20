Six Chambers
============

A Ludum Dare 60 game (theme: Old Tech). Strip, clean and fix old revolvers for the customers of a small
gunsmith's shop, one order at a time.


Running the game
----------------

1. Unpack the whole folder anywhere. Keep the "assets" folder and libSDL3.so next to the game.
2. Start it from a terminal in that folder:

       ./LudumDare60.Game.Desktop

   If the file is not executable (some archive tools drop the flag), run first:

       chmod +x LudumDare60.Game.Desktop

The itch.io app sets everything up by itself. Nothing else needs to be installed: the game brings its own
.NET runtime and SDL3.


Requirements
------------

- 64-bit x86 Linux with glibc 2.28 or newer (Ubuntu 20.04, Debian 10, Fedora 29 and later).
- Wayland or X11.
- A Vulkan driver: Mesa (RADV for AMD, ANV for Intel) or the NVIDIA driver. On Debian and Ubuntu:

       sudo apt install libvulkan1 mesa-vulkan-drivers

- Sound is optional: PipeWire, PulseAudio or ALSA. Without an audio device the game runs silent.

ChromeOS (Linux development environment, Crostini) works too: the game picks the right settings by itself.


Controls
--------

- Mouse, touchpad or touchscreen: take parts, drag them, turn screws, use the brush and the oil can.
- F11 or Alt+Enter: switch between fullscreen and a window.
- Alt+F4: quit.

The game starts fullscreen.


Troubleshooting
---------------

Errors are printed to the terminal the game was started from.

"Creating the Vulkan GPU device failed" or "Claiming the window for the GPU failed":
  Install a Vulkan driver (see Requirements). "vulkaninfo --summary" (package vulkan-tools) shows whether
  Vulkan works.

The window opens in the wrong place or on X11 instead of Wayland:
  Pick the video driver by hand:

      SDL_VIDEO_DRIVER=wayland ./LudumDare60.Game.Desktop
      SDL_VIDEO_DRIVER=x11 ./LudumDare60.Game.Desktop

The browser version is on the same itch.io page, if the desktop one does not run.

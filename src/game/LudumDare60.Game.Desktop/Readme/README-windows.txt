Six Chambers
============

A Ludum Dare 60 game (theme: Old Tech). Strip, clean and fix old revolvers for the customers of a small
gunsmith's shop, one order at a time.


Running the game
----------------

1. Unpack the whole folder anywhere. Keep the "assets" folder and SDL3.dll next to the .exe.
2. Start LudumDare60.Game.Desktop.exe.

Windows may say "Windows protected your PC", because the game is not signed. Click "More info", then
"Run anyway".

Nothing needs to be installed: the game brings its own .NET runtime.


Requirements
------------

- Windows 10 or 11, 64-bit.
- A graphics card with Vulkan support and an up-to-date driver (most cards from 2016 on).
- Sound is optional: without an audio device the game runs silent.


Controls
--------

- Mouse: take parts, drag them, turn screws, use the brush and the oil can.
- F11 or Alt+Enter: switch between fullscreen and a window.
- Alt+F4: quit.

The game starts fullscreen.


Troubleshooting
---------------

The game closes at once or shows nothing:
  Update the graphics driver from the card maker's site (NVIDIA, AMD or Intel); the driver that comes with
  Windows often has no Vulkan. To see the error, open a command prompt in the game folder and run
  LudumDare60.Game.Desktop.exe from there: errors are printed to the window.

The browser version is on the same itch.io page, if the desktop one does not run.

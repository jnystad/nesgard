# NESgard

Just another NES emulator written in C#, .NET 5.

I wouldn't recommend anyone to use this apart from curiosity. Use Mesen or something for a proper NES experience.

## Core features

The core of the emulator is platform agnostic, and could theoretically be wrapped in other platform specific or platform agnostic host programs.

CPU, PPU, APU and controllers are mostly feature complete, but with some known limitations, like only support for NTSC ROMs at the moment. Some more seldomly used parts may be untested (like APU DMC).

The ROM loader supports loading iNES format .nes files.

Some common mappers implemented
- Mapper 0, NROM
- Mapper 1, MMC1
- Mapper 2, UxROM
- Mapper 4, MMC3
- Mapper 66, GxROM

## Windows (WinForms) host features

WinForms host program supporting Windows only.

Support for Xbox controller (or compatible controllers, like the 8BitDo Lite Gamepad in Xbox mode). The controller must be connected during load. There is no option for button mapping.

> Note: Build a Release build for best performance.

## Debugger tools

A PPU viewer to view the current state of the PPU VRAM, including scroll.

Logs the CPU trace during boot.

## Known issues

Vertical (partial) scrolling is not accurate in all games, causing offset or missing graphics in some cases.

Some games lock up.

Sound has some delay.

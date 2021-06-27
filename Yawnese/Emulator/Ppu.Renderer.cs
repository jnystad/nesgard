using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Yawnese.Emulator
{
    partial class Ppu
    {
        void DrawPixel()
        {
            if (scanline >= 240) return;

            int color = 0;

            if (mask.HasFlag(PpuMask.RenderBackground))
            {
                int nameTable = (address & 0x0C00) >> 10;

                if (cycles > 8 || mask.HasFlag(PpuMask.RenderBackgroundColumn1))
                    color = GetBgPixel(fineX);
            }


            if (mask.HasFlag(PpuMask.RenderSprites))
            {
                int bank = control.HasFlag(PpuControl.SpritePatternAddr) ? 0x1000 : 0;

                if (cycles == 1)
                    LoadSprites(bank);

                var sprite = GetSpriteColor(color);
                if (sprite != 0)
                    color = sprite;
            }

            var m = mask.HasFlag(PpuMask.Greyscale) ? 0x30 : 0xFF;
            var rgb = (byte)(vram[0x3F00 + color] & m);

            SetPixel(cycles - 1, scanline, GetColor(rgb));
        }

        struct Tile
        {
            public byte highByte;
            public byte lowByte;
            public byte paletteOffset;
        }

        Tile prevTile;
        Tile currentTile;
        Tile nextTile;

        int tileHighShift;
        int tileLowShift;

        void LoadTile()
        {
            switch (cycles & 0x7)
            {
                case 1:
                    prevTile = currentTile;
                    currentTile = nextTile;

                    tileHighShift |= currentTile.highByte;
                    tileLowShift |= currentTile.lowByte;

                    var bank = control.HasFlag(PpuControl.BgPatternAddr) ? 0x1000 : 0;
                    var tileIdx = vram[MirrorVramAddr((ushort)(0x2000 | (address & 0xFFF)))];
                    var tileAddr = (ushort)(bank | (tileIdx << 4) | (address >> 12));
                    nextTile = new Tile
                    {
                        lowByte = rom.mapper.ChrRead(tileAddr),
                        highByte = rom.mapper.ChrRead((ushort)(tileAddr + 8))
                    };

                    break;

                case 3:
                    nextTile.paletteOffset = PaletteOffset();
                    break;
            }
        }

        byte PaletteOffset()
        {
            var shift = ((address >> 4) & 0x04) | (address & 0x02);
            var b = 0x23C0 | (address & 0x0C00) | ((address >> 4) & 0x38) | ((address >> 2) & 0x07);
            return (byte)(((vram[MirrorVramAddr((ushort)b)] >> shift) & 0x3) << 2);
        }

        int GetBgPixel(int x)
        {
            var value = (((tileLowShift << x) & 0x8000) >> 15) | (((tileHighShift << x) & 0x8000) >> 14);
            return value == 0 ? 0 : ((x + ((cycles - 1) & 0x7) < 8) ? prevTile : currentTile).paletteOffset + value;
        }

        struct Sprite
        {
            public byte flags;
            public byte x;
            public byte y;
            public byte paletteOffset;
            public byte lower;
            public byte upper;
        }

        Sprite[] sprites = new Sprite[8];
        int spriteCount = 0;

        void LoadSprites(int bank)
        {
            var largeSprites = control.HasFlag(PpuControl.SpriteSize);
            spriteCount = 0;
            for (var i = 0; i < 256 && spriteCount < 8; i += 4)
            {
                sprites[spriteCount].y = (byte)(oamData[i] + 1);

                if (sprites[spriteCount].y > scanline || sprites[spriteCount].y + (largeSprites ? 16 : 8) <= scanline)
                    continue;

                sprites[spriteCount].x = oamData[i + 3];
                var tileAttr = oamData[i + 2];
                var tileIdx = oamData[i + 1];

                var flipV = (tileAttr & 0x80) != 0;

                var y = scanline - sprites[spriteCount].y;
                if (flipV)
                    y = (largeSprites ? 15 : 7) - y;

                var offset = 0;
                if (largeSprites)
                    offset = (((tileIdx & 1) == 0 ? 0 : 0x1000) | ((tileIdx & 0xFE) << 4)) + (y >= 8 ? y + 8 : y);
                else
                    offset = (bank | (tileIdx << 4)) + y;

                sprites[spriteCount].flags = (byte)((i == 0 ? 1 : 0) | (tileAttr & 0x40) | (tileAttr & 0x20));
                sprites[spriteCount].paletteOffset = (byte)(0x10 + (tileAttr & 0b11) * 4);
                sprites[spriteCount].lower = rom.mapper.ChrRead((ushort)offset);
                sprites[spriteCount].upper = rom.mapper.ChrRead((ushort)(offset + 8));

                spriteCount++;

                if (spriteCount == 8)
                    break;
            }
        }

        int GetSpriteColor(int bg)
        {
            var largeSprites = control.HasFlag(PpuControl.SpriteSize);
            for (var i = 0; i < spriteCount; ++i)
            {
                var x = cycles - sprites[i].x - 1;

                if (x < 0 || x > 7)
                    continue;

                if ((sprites[i].flags & 0x40) != 0)
                    x = 7 - x;

                var b1 = (sprites[i].lower >> (7 - x)) & 1;
                var b2 = (sprites[i].upper >> (7 - x)) & 1;
                if (b1 + b2 == 0)
                    continue;

                var rgb = (b2 << 1) | b1;

                if (!sprite0HitThisFrame && (sprites[i].flags & 1) == 1 && mask.HasFlag(PpuMask.RenderBackground) && (cycles > 8 || mask.HasFlag(PpuMask.RenderBackgroundColumn1)) && cycles < 256)
                {
                    status |= PpuStatus.Sprite0Hit;
                    sprite0HitThisFrame = true;
                }

                if (rgb == 0 || (bg != 0 && (sprites[i].flags & 0x20) != 0))
                    return bg;

                return sprites[i].paletteOffset + rgb;
            }
            return bg;
        }

        int GetColor(byte paletteIdx)
        {
            var rgb = NES_PALETTE[paletteIdx];

            // TODO Precalculate 8 palettes
            var emphasis = ((byte)mask) & 0xE0;
            if (emphasis != 0 && (paletteIdx & 0xF) < 0xD)
            {
                if (emphasis == 0xE0 || (emphasis & 0x80) == 0)
                    rgb = (rgb & 0xFFFF00) | ((rgb & 0xFF) * 5 / 6);
                if (emphasis == 0xE0 || (emphasis & 0x40) == 0)
                    rgb = (rgb & 0xFF00FF) | (((rgb >> 8) & 0xFF) * 5 / 6) << 8;
                if (emphasis == 0xE0 || (emphasis & 0x20) == 0)
                    rgb = (rgb & 0x00FFFF) | (((rgb >> 16) & 0xFF) * 5 / 6) << 16;
            }

            return rgb;
        }

        void SetPixel(int x, int y, int color)
        {
            var offset = (y * 256 + x) * 3;
            buffer[currentBuffer][offset + 0] = (byte)(color >> 0);
            buffer[currentBuffer][offset + 1] = (byte)(color >> 8);
            buffer[currentBuffer][offset + 2] = (byte)(color >> 16);
        }

        void BackgroundPalette(int nameTable, int column, int row, ref byte[] palette)
        {
            var m = mask.HasFlag(PpuMask.Greyscale) ? 0x30 : 0xFF;
            var attr = vram[0x23C0 + nameTable * 0x400 + row / 4 * 8 + column / 4];
            int select = 0;
            switch (row % 4 / 2, column % 4 / 2)
            {
                case (0, 0):
                    select = attr & 0b11;
                    break;
                case (0, 1):
                    select = (attr >> 2) & 0b11;
                    break;
                case (1, 0):
                    select = (attr >> 4) & 0b11;
                    break;
                case (1, 1):
                    select = (attr >> 6) & 0b11;
                    break;
            }
            var idx = 0x3F01 + select * 4;
            palette[0] = ((byte)(vram[0x3F00] & m));
            palette[1] = ((byte)(vram[idx] & m));
            palette[2] = ((byte)(vram[idx + 1] & m));
            palette[3] = ((byte)(vram[idx + 2] & m));
        }

        public void GetBackgroundBuffers(Bitmap img)
        {
            int bufferSize = 256 * 240 * 3;
            byte[] buffer = new byte[bufferSize * 4];
            var palette = new byte[4];
            var rgbs = new int[4];
            int bank = control.HasFlag(PpuControl.BgPatternAddr) ? 0x1000 : 0;

            int rgb, offset;
            byte tile, lower, upper;

            for (var part = 0; part < 4; ++part)
            {
                int xOff = 256 * (part % 2);
                int yOff = 240 * (part / 2);

                var nameTable = 0;
                switch (rom.mapper.Mirroring)
                {
                    case Mirroring.Horizontal:
                        nameTable = part / 2;
                        break;

                    case Mirroring.Vertical:
                        nameTable = part % 2;
                        break;

                    case Mirroring.ScreenAOnly:
                        nameTable = 0;
                        break;

                    case Mirroring.ScreenBOnly:
                        nameTable = 1;
                        break;
                }
                for (var row = 0; row < 30; ++row)
                {
                    for (var col = 0; col < 32; ++col)
                    {
                        BackgroundPalette(nameTable, col, row, ref palette);
                        rgbs[0] = GetColor(palette[0]);
                        rgbs[1] = GetColor(palette[1]);
                        rgbs[2] = GetColor(palette[2]);
                        rgbs[3] = GetColor(palette[3]);

                        tile = vram[0x2000 + nameTable * 0x400 + row * 32 + col];

                        for (var y = 0; y < 8; ++y)
                        {
                            lower = rom.mapper.ChrRead((ushort)(bank + tile * 16 + y));
                            upper = rom.mapper.ChrRead((ushort)(bank + tile * 16 + y + 8));

                            for (var x = 7; x >= 0; --x)
                            {
                                rgb = (rgbs[((upper & 1) << 1) | (lower & 1)]);
                                lower >>= 1;
                                upper >>= 1;

                                offset = ((yOff + row * 8 + y) * 512 + xOff + col * 8 + x) * 3;
                                buffer[offset + 0] = (byte)rgb;
                                buffer[offset + 1] = (byte)(rgb >> 8);
                                buffer[offset + 2] = (byte)(rgb >> 16);
                            }
                        }
                    }
                }
            }

            var imgLock = img.LockBits(new Rectangle(0, 0, img.Width, img.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            Marshal.Copy(buffer, 0, imgLock.Scan0, buffer.Length);
            img.UnlockBits(imgLock);
        }

        public string GetStatusText()
        {
            int nameTable = (control.HasFlag(PpuControl.NameTable2) ? 2 : 0) + (control.HasFlag(PpuControl.NameTable1) ? 1 : 0);
            return string.Format(
                "Control: {0:X2}, Mask: {1:X2}, Scroll: {2},{3}, Name table: {4}, Mirror: {5}",
                (int)control, (int)mask, fineX, 0, nameTable, rom.mapper.Mirroring
            );
        }

        static readonly int[] NES_PALETTE = new[] {
            0x808080, 0x003DA6, 0x0012B0, 0x440096, 0xA1005E, 0xC70028, 0xBA0600, 0x8C1700, 0x5C2F00, 0x104500, 0x054A00, 0x00472E, 0x004166, 0x000000, 0x050505, 0x050505,
            0xC7C7C7, 0x0077FF, 0x2155FF, 0x8237FA, 0xEB2FB5, 0xFF2950, 0xFF2200, 0xD64A00, 0xC46200, 0x358000, 0x058F00, 0x008A55, 0x0099CC, 0x212121, 0x090909, 0x090909,
            0xFFFFFF, 0x0FD7FF, 0x69A2FF, 0xD480FF, 0xFF45F3, 0xFF618B, 0xFF8833, 0xFF9C12, 0xFABC20, 0x9FE30E, 0x2BF035, 0x0CF0A4, 0x05FBFF, 0x5E5E5E, 0x0D0D0D, 0x0D0D0D,
            0xFFFFFF, 0xA6FCFF, 0xB3ECFF, 0xDAABEB, 0xFFA8F9, 0xFFABB3, 0xFFD2B0, 0xFFEFA6, 0xFFF79C, 0xD7E895, 0xA6EDAF, 0xA2F2DA, 0x99FFFC, 0xDDDDDD, 0x111111, 0x111111,
        };
    }
}
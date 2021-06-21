using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Yawnese.Emulator
{
    partial class Ppu
    {
        private byte[] palette = new byte[4];

        void RenderScanline()
        {
            if (scanline >= 240) return;

            if (mask.HasFlag(PpuMask.RenderBackground))
            {
                int nameTable = (control.HasFlag(PpuControl.NameTable2) ? 2 : 0) + (control.HasFlag(PpuControl.NameTable1) ? 1 : 0);
                int secondaryNameTable = 0;
                int bank = control.HasFlag(PpuControl.BgPatternAddr) ? 0x1000 : 0;

                switch ((nameTable, rom.mapper.Mirroring))
                {
                    case (_, Mirroring.ScreenAOnly):
                        nameTable = 0;
                        secondaryNameTable = 0;
                        break;

                    case (_, Mirroring.ScreenBOnly):
                        nameTable = 1;
                        secondaryNameTable = 1;
                        break;

                    case (0, _):
                    case (1, Mirroring.Horizontal):
                    case (2, Mirroring.Vertical):
                        nameTable = 0;
                        secondaryNameTable = 1;
                        break;

                    case (1, Mirroring.Vertical):
                    case (2, Mirroring.Horizontal):
                    case (3, _):
                        nameTable = 1;
                        secondaryNameTable = 0;
                        break;
                }

                for (int x = 0; x < 256; ++x)
                {
                    if (x < 8 && !mask.HasFlag(PpuMask.RenderBackgroundColumn1))
                        continue;

                    if (scroll.scroll_y == 0)
                    {
                        if (x < 256 - scroll.scroll_x)
                            RenderBackground(nameTable, bank, x + scroll.scroll_x, scanline, -scroll.scroll_x, 0);
                        else
                            RenderBackground(secondaryNameTable, bank, x + scroll.scroll_x - 256, scanline, 256 - scroll.scroll_x, 0);
                    }
                    else
                    {
                        if (scanline < 240 - scroll.scroll_y)
                            RenderBackground(nameTable, bank, x, scanline + scroll.scroll_y, 0, -scroll.scroll_y);
                        else
                            RenderBackground(secondaryNameTable, bank, x, scanline + scroll.scroll_y - 240, 0, 240 - scroll.scroll_y);
                    }
                }
            }

            if (mask.HasFlag(PpuMask.RenderSprites))
            {
                int bank = control.HasFlag(PpuControl.SpritePatternAddr) ? 0x1000 : 0;
                for (var i = 252; i >= 0; i -= 4)
                {
                    RenderSprite(bank, i, scanline);
                }
            }
        }

        void RenderBackground(int nameTable, int bank, int x, int y, int offsetX, int offsetY)
        {
            int row = y / 8;
            int col = x / 8;

            var tile = vram[0x2000 + nameTable * 0x400 + row * 32 + col];
            BackgroundPalette(nameTable, col, row, ref palette);

            var offset = y % 8;
            var lower = rom.mapper.ChrRead((ushort)(bank + tile * 16 + offset));
            var upper = rom.mapper.ChrRead((ushort)(bank + tile * 16 + offset + 8));

            var shift = 7 - (x % 8);
            var b1 = (lower >> shift) & 1;
            var b2 = (upper >> shift) & 1;
            var value = (b2 << 1) | b1;

            var rgb = GetColor(palette[value]);
            SetPixel(offsetX + x, offsetY + y, rgb, value != 0);
        }

        void RenderSprite(int bank, int i, int scanline)
        {
            var largeSprites = control.HasFlag(PpuControl.SpriteSize);

            var tileAttr = oamData[i + 2];
            var tileIdx = oamData[i + 1];
            var tileY = oamData[i];
            var tileX = oamData[i + 3];

            var flipH = (tileAttr & 0x40) != 0;
            var flipV = (tileAttr & 0x80) != 0;
            var behind = (tileAttr & 0x20) != 0;

            if (tileY > scanline || tileY + (largeSprites ? 16 : 8) <= scanline)
                return;

            var paletteIdx = tileAttr & 0b11;
            SpritePalette(paletteIdx, ref palette);

            var y = scanline - tileY;
            if (flipV)
                y = (largeSprites ? 15 : 7) - y;

            var offset = 0;
            if (largeSprites)
                offset = (((tileIdx & 1) == 0 ? 0 : 0x1000) | ((tileIdx & 0xFE) << 4)) + (y >= 8 ? y + 8 : y);
            else
                offset = (bank | (tileIdx << 4)) + y;

            var lower = rom.mapper.ChrRead((ushort)offset);
            var upper = rom.mapper.ChrRead((ushort)(offset + 8));

            for (var x = 7; x >= 0; --x)
            {
                var b1 = lower & 1;
                var b2 = upper & 1;
                lower >>= 1;
                upper >>= 1;

                var renderX = tileX + (flipH ? 7 - x : x);

                if (b1 + b2 == 0) continue;
                int rgb = GetColor(palette[(b2 << 1) | b1]);

                if (!spriteHitThisFrame && i == 0 && mask.HasFlag(PpuMask.RenderBackground) && (renderX > 7 || mask.HasFlag(PpuMask.RenderBackgroundColumn1)) && renderX < 255)
                {
                    status |= PpuStatus.SpriteHit;
                    spriteHitThisFrame = true;
                }

                SetPixel(renderX, scanline, rgb, !behind);
            }
        }

        void SpritePalette(int paletteIdx, ref byte[] palette)
        {
            var m = mask.HasFlag(PpuMask.Greyscale) ? 0x30 : 0xFF;
            var idx = 0x3F11 + paletteIdx * 4;
            palette[0] = 0;
            palette[1] = ((byte)(vram[idx] & m));
            palette[2] = ((byte)(vram[idx + 1] & m));
            palette[3] = ((byte)(vram[idx + 2] & m));
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int GetColor(byte paletteIdx)
        {
            var rgb = NES_PALETTE[paletteIdx];
            if ((paletteIdx & 0xF) < 0xD)
            {
                var eR = mask.HasFlag(PpuMask.EmphasisR);
                var eG = mask.HasFlag(PpuMask.EmphasisG);
                var eB = mask.HasFlag(PpuMask.EmphasisB);
                var any = eR || eG || eB;
                var all = eR && eG && eB;
                if (all || (any && !eB))
                    rgb = (rgb & 0xFFFF00) | ((rgb & 0xFF) * 5 / 6);
                if (all || (any && !eG))
                    rgb = (rgb & 0xFF00FF) | (((rgb >> 8) & 0xFF) * 5 / 6) << 8;
                if (all || (any && !eR))
                    rgb = (rgb & 0x00FFFF) | (((rgb >> 16) & 0xFF) * 5 / 6) << 16;
            }
            return rgb;
        }

        void SetPixel(int x, int y, int color, bool priority)
        {
            if (x < 0 || x >= 256 || y < 0 || y >= 240) return;

            var offset = (y * 256 + x) * 3;

            if (!priority && bufferPriority[y * 256 + x]) return;

            buffer[0][offset + 0] = (byte)(color >> 0);
            buffer[0][offset + 1] = (byte)(color >> 8);
            buffer[0][offset + 2] = (byte)(color >> 16);

            bufferPriority[y * 256 + x] = priority;
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
                (int)control, (int)mask, scroll.scroll_x, scroll.scroll_y, nameTable, rom.mapper.Mirroring
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
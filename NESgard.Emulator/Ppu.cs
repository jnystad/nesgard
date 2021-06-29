using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace NESgard.Emulator
{
    public partial class Ppu
    {
        Cartridge rom;

        byte[] vram;

        byte read_buffer;

        public int scanline;

        public int cycles;

        public ulong frameCount;

        public bool nmi = false;

        public PpuStatus status;

        private bool sprite0HitThisFrame = false;

        public PpuControl control;

        public PpuMask mask;

        public byte oamAddress;

        public byte[] oamData;

        bool writeLatch = false;

        public int fineX = 0;
        int frameScrollX = 0;
        int frameScrollY = 0;

        private int tmpAddress = 0;

        public ushort address;

        private int[][] buffer;
        private int currentBuffer;

        private byte openBus;

        public Ppu(Cartridge rom)
        {
            this.rom = rom;

            vram = new byte[0x4000];
            oamData = new byte[256];

            buffer = new[]
            {
                new int[256 * 240],
                new int[256 * 240],
            };
        }

        public void GetImage(Bitmap img)
        {
            var data = buffer[(currentBuffer + 1) % 2];
            var imgLock = img.LockBits(new Rectangle(0, 0, img.Width, img.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            Marshal.Copy(data, 0, imgLock.Scan0, data.Length);
            img.UnlockBits(imgLock);
        }

        public void Reset()
        {
            cycles = 0;
            scanline = -1;
            frameCount = 0;
            nmi = false;
            status = 0;
            control = 0;
            mask = 0;
            openBus = 0;
            writeLatch = false;
        }

        public PpuResult Tick()
        {
            var r1 = Cycle();
            var r2 = Cycle();
            var r3 = Cycle();
            if (r1 != PpuResult.None)
                return r1;
            if (r2 != PpuResult.None)
                return r2;
            return r3;
        }

        protected PpuResult Cycle()
        {
            ++cycles;

            if (IsRenderingEnabled)
            {
                if (cycles <= 256)
                {
                    LoadTile();

                    if ((cycles & 0x7) == 0)
                        IncrementScrollX();
                    if (cycles == 256)
                        IncrementScrollY();

                    if (scanline != -1)
                    {
                        DrawPixel();
                        tileHighShift <<= 1;
                        tileLowShift <<= 1;
                    }
                }
                else if (cycles == 257)
                {
                    address = (ushort)((address & ~0x041F) | (tmpAddress & 0x041F));

                    if (scanline == 239)
                    {
                        frameScrollX = ((address & 0x001F) << 3) | fineX | ((address & 0x0400) != 0 ? 0x0100 : 0);
                        frameScrollY = (((address & 0x03E0) >> 2) | ((address & 0x7000) >> 12)) + ((address & 0x0800) != 0 ? 240 : 0);
                    }
                }
                else if (cycles >= 280 && cycles <= 304 && scanline == -1)
                {
                    address = (ushort)((address & ~0x7BE0) | (tmpAddress & 0x7BE0));
                }
                else if ((cycles >= 321 && cycles <= 336))
                {
                    if (cycles == 328 || cycles == 336)
                    {
                        LoadTile();
                        tileHighShift <<= 8;
                        tileLowShift <<= 8;
                        IncrementScrollX();
                    }
                    else
                        LoadTile();
                }

            }
            if (cycles >= 257 && cycles <= 320)
            {
                if (IsRenderingEnabled)
                    oamAddress = 0;
            }
            else if (cycles == 339 && (frameCount & 1) == 1 && scanline == -1 && rom.header.ntsc)
            {
                // Skip one cycle every odd frame for NTSC ROMs
                cycles = 340;
            }

            if (cycles == 1 && scanline == -1)
            {
                status &= ~PpuStatus.Vblank;
                nmi = false;
            }

            if (cycles == 341)
            {
                status &= ~PpuStatus.Sprite0Hit;

                cycles = 0;
                ++scanline;

                if (scanline == 240)
                {
                    currentBuffer = currentBuffer == 1 ? 0 : 1;
                    status |= PpuStatus.Vblank;
                    if (control.HasFlag(PpuControl.GenerateNMI))
                    {
                        nmi = true;
                        return PpuResult.Nmi;
                    }
                }

                if (scanline == 261)
                {
                    scanline = -1;
                    sprite0HitThisFrame = false;

                    frameCount++;
                    return PpuResult.EndOfFrame;
                }
                return PpuResult.Scanline;
            }
            return PpuResult.None;
        }

        void IncrementScrollX()
        {
            var addr = address;
            if ((addr & 0x001F) == 31)
                addr = (ushort)((addr & ~0x001F) ^ 0x0400);
            else
                ++addr;
            address = addr;
        }

        void IncrementScrollY()
        {
            if ((address & 0x7000) != 0x7000)
            {
                address += 0x1000;
            }
            else
            {
                address &= 0x8FFF;
                int y = (address & 0x03E0) >> 5;
                if (y == 29)
                {
                    y = 0;
                    address ^= 0x0800;
                }
                else if (y == 31)
                    y = 0;
                else
                    ++y;
                address = (ushort)((address & ~0x03E0) | (y << 5));
            }
        }

        protected bool IsRenderingEnabled
        {
            get
            {
                if (scanline >= 240) return false;
                return mask.HasFlag(PpuMask.RenderBackground) || mask.HasFlag(PpuMask.RenderSprites);
            }
        }

        public bool PollNmi()
        {
            var result = nmi;
            nmi = false;
            return result;
        }

        public byte Read(ushort addr)
        {
            byte result;
            switch (addr % 8)
            {
                case 0:
                case 1:
                case 3:
                case 5:
                case 6:
                    result = openBus;
                    break;
                case 2:
                    result = (byte)(ReadStatus() | (openBus & 0b11111));
                    break;
                case 4:
                    result = ReadOamData();
                    break;
                case 7:
                    result = (byte)(ReadData() | ((address >> 8) == 0x3F ? openBus & 0b11000000 : 0));
                    break;
                default:
                    throw new Exception(string.Format("Invalid PPU read address {0:X4}", addr));
            }

            openBus = result;
            return result;
        }

        public void Write(ushort addr, byte data)
        {
            openBus = data;
            switch (addr % 8)
            {
                case 0:
                    WriteControl(data); break;
                case 1:
                    mask = (PpuMask)data;
                    break;
                case 2:
                    throw new Exception(string.Format("Write read-only PPU address {0:X4}", addr));
                case 3:
                    WriteOamAddress(data);
                    break;
                case 4:
                    WriteOamData(data);
                    break;
                case 5:
                    WriteScroll(data);
                    break;
                case 6:
                    WriteAddress(data);
                    break;
                case 7:
                    WriteData(data);
                    break;
            }
        }

        void WriteControl(byte data)
        {
            var was_nmi_set = control.HasFlag(PpuControl.GenerateNMI);
            control = (PpuControl)data;
            tmpAddress = (ushort)(((data & 0x3) << 10) | (tmpAddress & 0xF3FF));
            if (!was_nmi_set && control.HasFlag(PpuControl.GenerateNMI) && status.HasFlag(PpuStatus.Vblank))
            {
                nmi = true;
            }
        }

        void WriteOamAddress(byte data)
        {
            oamAddress = data;
        }

        public byte ReadOamData()
        {
            if (oamAddress % 4 == 2)
                return (byte)(oamData[oamAddress] & 0b11100011);
            return oamData[oamAddress];
        }

        public void WriteOamData(byte data)
        {
            oamData[oamAddress] = data;
            oamAddress = (byte)(oamAddress + 1);
        }

        void WriteScroll(byte data)
        {
            if (writeLatch)
            {
                tmpAddress = (tmpAddress & ~0x73E0) | ((data & 0xF8) << 2) | ((data & 0x7) << 12);
            }
            else
            {
                fineX = data & 0x7;
                tmpAddress = (tmpAddress & 0xFFE0) | (data >> 3);
            }
            writeLatch = !writeLatch;
        }

        public void WriteAddress(byte data)
        {
            if (writeLatch)
            {
                tmpAddress = (tmpAddress & 0xFF00) | data;
                address = (ushort)tmpAddress;
            }
            else
            {
                tmpAddress = (ushort)(((data << 8) | (tmpAddress & 0xFF)) & 0x3FFF);
            }
            writeLatch = !writeLatch;
        }

        public void IncrementAddress()
        {
            if (control.HasFlag(PpuControl.VramAddIncrement))
                address = (ushort)((address + 32) & 0x3FFF);
            else
                address = (ushort)((address + 1) & 0x3FFF);
        }

        byte ReadStatus()
        {
            var data = (byte)status;
            status &= ~PpuStatus.Vblank;
            writeLatch = false;
            return data;
        }

        byte ReadData()
        {
            var addr = (ushort)(address & 0x3FFF);
            IncrementAddress();
            switch (addr)
            {
                case ushort a when (a <= 0x1FFF):
                    {
                        var data = read_buffer;
                        read_buffer = rom.mapper.ChrRead(addr);
                        return data;
                    }
                case ushort a when (a <= 0x2FFF):
                    {
                        var data = read_buffer;
                        read_buffer = vram[MirrorVramAddr(addr)];
                        return data;
                    }

                case ushort a when (a <= 0x3FFF):
                    switch (addr & 0x1F)
                    {
                        case 0x10:
                        case 0x14:
                        case 0x18:
                        case 0x1C:
                            return vram[addr - 0x10];

                        default:
                            return vram[addr & 0x3F1F];
                    }

                default:
                    throw new Exception(string.Format("Read data addr {0:X4}", addr));
            }
        }

        void WriteData(byte data)
        {
            var addr = (ushort)(address & 0x3FFF);
            IncrementAddress();

            switch (addr)
            {
                case var a when (a <= 0x1FFF):
                    rom.mapper.ChrWrite(addr, data);
                    break;

                case var a when (a <= 0x2FFF):
                    vram[MirrorVramAddr(addr)] = data;
                    break;

                case var a when (a <= 0x3FFF):
                    switch (addr & 0x1F)
                    {
                        case 0x10:
                        case 0x14:
                        case 0x18:
                        case 0x1C:
                            vram[addr - 0x10] = data;
                            break;

                        default:
                            vram[addr & 0x3F1F] = data;
                            break;
                    }
                    break;

                default:
                    throw new Exception(string.Format("Write data addr {0:X4}", addr));
            }
        }

        ushort MirrorVramAddr(ushort addr)
        {
            switch (rom.mapper.Mirroring)
            {
                case (Mirroring.ScreenAOnly):
                    return (ushort)(addr & 0x23FF);

                case (Mirroring.ScreenBOnly):
                    return (ushort)((addr & 0x23FF) | 0x0400);

                case (Mirroring.Vertical):
                    return (ushort)(addr & 0x27FF);

                case (Mirroring.Horizontal):
                    return (ushort)(((addr >> 1) & 0x0400) + (addr & 0x23FF));

                default:
                    return addr;
            }
        }
    }
}
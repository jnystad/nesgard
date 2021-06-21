using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Yawnese.Emulator
{
    public partial class Ppu
    {
        public class PpuAddress
        {
            bool hibyte = true;

            public ushort address = 0;

            Ppu ppu;

            public PpuAddress(Ppu ppu)
            {
                this.ppu = ppu;
            }

            public void Write(byte data)
            {
                if (hibyte)
                    address = (ushort)((data << 8) | (address & 0xFF));
                else
                    address = (ushort)((address & 0xFF00) | data);
                hibyte = !hibyte;
            }

            public void Increment()
            {
                if (ppu.control.HasFlag(PpuControl.VramAddIncrement))
                    address += 32;
                else
                    address += 1;
                address &= 0x3FFF;
            }

            public void ResetLatch()
            {
                hibyte = true;
            }
        }

        public class PpuScroll
        {
            bool latch = false;

            public int scroll_y = 0;

            public int scroll_x = 0;

            public void Write(byte data)
            {
                if (latch)
                    scroll_y = data;
                else
                    scroll_x = data;
                latch = !latch;
            }

            public void ResetLatch()
            {
                latch = false;
            }
        }

        Cartridge rom;

        public byte[] vram;

        byte read_buffer;

        public ushort scanline;

        public ulong cycles;

        public ulong frameCount;

        public bool nmi = false;

        public PpuStatus status;

        private bool spriteHitThisFrame = false;

        public PpuControl control;

        public PpuMask mask;

        public PpuScroll scroll = new PpuScroll();

        public PpuAddress address;

        public byte oamAddress;

        public byte[] oamData;

        private byte[][] buffer;

        private bool[] bufferPriority;

        private byte openBus;

        public Ppu(Cartridge rom)
        {
            this.rom = rom;

            vram = new byte[0x4000];
            oamData = new byte[256];
            address = new PpuAddress(this);

            buffer = new[]
            {
                new byte[256 * 240 * 3],
                new byte[256 * 240 * 3],
            };
            bufferPriority = new bool[256 * 240];
        }

        public void GetImage(Bitmap img)
        {
            var data = buffer[1];
            var imgLock = img.LockBits(new Rectangle(0, 0, img.Width, img.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            Marshal.Copy(data, 0, imgLock.Scan0, data.Length);
            img.UnlockBits(imgLock);
        }

        public void Reset()
        {
            cycles = 0;
            scanline = 0;
            frameCount = 0;
            nmi = false;
            status = 0;
            control = 0;
            mask = 0;
            openBus = 0;

            address.ResetLatch();
            scroll.ResetLatch();
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

            if (cycles == 339 && (frameCount & 1) == 1 && scanline == 262 && rom.header.ntsc)
            {
                // Skip one cycle every odd frame for NTSC ROMs
                cycles = 340;
            }

            if (cycles >= 341)
            {
                status &= ~PpuStatus.SpriteHit;

                RenderScanline();

                cycles -= 341;
                scanline += 1;

                if (scanline == 241)
                {
                    for (var i = 0; i < buffer[0].Length; ++i)
                        buffer[1][i] = buffer[0][i];
                    for (var i = 0; i < bufferPriority.Length; ++i)
                        bufferPriority[i] = false;
                    status |= PpuStatus.Vblank;
                    if (control.HasFlag(PpuControl.GenerateNMI))
                    {
                        nmi = true;
                        return PpuResult.Nmi;
                    }
                }

                if (scanline == 262)
                {
                    scanline = 0;
                    spriteHitThisFrame = false;
                    status &= ~PpuStatus.Vblank;
                    nmi = false;

                    frameCount++;
                    return PpuResult.EndOfFrame;
                }
                return PpuResult.Scanline;
            }
            return PpuResult.None;
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
                    result = (byte)(ReadData() | ((address.address >> 8) == 0x3F ? openBus & 0b11000000 : 0));
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
                    scroll.Write(data);
                    break;
                case 6:
                    address.Write(data);
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

        byte ReadStatus()
        {
            var data = (byte)status;
            status &= ~PpuStatus.Vblank;
            scroll.ResetLatch();
            address.ResetLatch();
            return data;
        }

        byte ReadData()
        {
            var addr = address.address;
            address.Increment();
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
            var addr = address.address;
            address.Increment();

            switch (addr)
            {
                case ushort a when (a <= 0x1FFF):
                    rom.mapper.ChrWrite(addr, data);
                    break;

                case ushort a when (a <= 0x2FFF):
                    vram[MirrorVramAddr(addr)] = data;
                    break;

                case ushort a when (a <= 0x3FFF):
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
            var name_table = ((addr & 0x2fff) - 0x2000) / 0x400;
            switch (rom.mapper.Mirroring, name_table)
            {
                case (Mirroring.ScreenAOnly, _):
                    return (ushort)(addr & 0x23FF);

                case (Mirroring.ScreenBOnly, _):
                    return (ushort)((addr & 0x23FF) | 0x0400);

                case (Mirroring.Vertical, 2):
                case (Mirroring.Vertical, 3):
                case (Mirroring.Horizontal, 3):
                    return (ushort)(addr & 0x27FF);

                case (Mirroring.Horizontal, 1):
                case (Mirroring.Horizontal, 2):
                    return (ushort)(addr & 0x2CFF);

                default:
                    return addr;
            }
        }
    }
}
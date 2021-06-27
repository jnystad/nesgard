using System;

namespace Yawnese.Emulator.Mappers
{
    public class MMC3 : BaseMapper
    {
        byte bankSelect;
        bool prgRomSwap;
        bool chrRomSwap;
        byte[] bankPage;
        byte prgRamProtect;
        byte irqLatch;
        bool irqReload;
        byte irqCounter;
        bool irqEnabled;

        byte[] prgRam;

        public MMC3(Cartridge cartridge) : base(cartridge)
        {
            prgRam = new byte[0x2000];
            bankPage = new byte[8];
        }

        public override byte PrgRead(ushort addr)
        {
            switch (addr)
            {
                case var a when (a >= 0x6000 && a <= 0x7FFF):
                    if ((prgRamProtect & 0x80) == 0)
                        return prgRam[addr - 0x6000];
                    return 0;

                case var a when (a >= 0x8000 && a <= 0xFFFF):
                    {
                        switch (addr & 0xE000)
                        {
                            case 0x8000:
                                return prgRom[PrgAddressOf(prgRomSwap ? -2 : bankPage[6]) + addr - 0x8000];
                            case 0xA000:
                                return prgRom[PrgAddressOf(bankPage[7]) + addr - 0xA000];
                            case 0xC000:
                                return prgRom[PrgAddressOf(prgRomSwap ? bankPage[6] : -2) + addr - 0xC000];
                            case 0xE000:
                                return prgRom[PrgAddressOf(-1) + addr - 0xE000];
                            default:
                                throw new Exception("Read invalid PRG ROM address");
                        }
                    }

                default:
                    throw new Exception("Read invalid PRG ROM address");
            }
        }

        public override void PrgWrite(ushort addr, byte data)
        {
            switch (addr)
            {
                case var a when (a >= 0x6000 && a <= 0x7FFF):
                    if ((prgRamProtect & 0xC0) == 0)
                        prgRam[addr - 0x6000] = data;
                    break;

                case var a when (a >= 0x8000 && a <= 0xFFFF):
                    switch (addr & 0xE001)
                    {
                        case 0x8000:
                            bankSelect = (byte)(data & 0b111);
                            prgRomSwap = (data & 0x40) != 0;
                            chrRomSwap = (data & 0x80) != 0;
                            break;
                        case 0x8001:
                            bankPage[bankSelect] = data;
                            break;
                        case 0xA000:
                            Mirroring = (data & 1) == 0 ? Mirroring.Vertical : Mirroring.Horizontal;
                            break;
                        case 0xA001:
                            prgRamProtect = data;
                            break;
                        case 0xC000:
                            irqLatch = data;
                            break;
                        case 0xC001:
                            irqReload = true;
                            break;
                        case 0xE000:
                            irqEnabled = false;
                            interrupt = false;
                            break;
                        case 0xE001:
                            irqEnabled = true;
                            break;
                    }
                    break;
            }
        }

        public override byte ChrRead(ushort addr)
        {
            switch (addr & 0x1C00)
            {
                case 0x0000:
                    return chrRom[ChrAddressOf(!chrRomSwap ? bankPage[0] & 0xFE : bankPage[2]) + addr];
                case 0x0400:
                    return chrRom[ChrAddressOf(!chrRomSwap ? bankPage[0] | 1 : bankPage[3]) + addr - 0x0400];
                case 0x0800:
                    return chrRom[ChrAddressOf(!chrRomSwap ? bankPage[1] & 0xFE : bankPage[4]) + addr - 0x0800];
                case 0x0C00:
                    return chrRom[ChrAddressOf(!chrRomSwap ? bankPage[1] | +1 : bankPage[5]) + addr - 0x0C00];
                case 0x1000:
                    return chrRom[ChrAddressOf(!chrRomSwap ? bankPage[2] : bankPage[0] & 0xFE) + addr - 0x1000];
                case 0x1400:
                    return chrRom[ChrAddressOf(!chrRomSwap ? bankPage[3] : bankPage[0] | 1) + addr - 0x1400];
                case 0x1800:
                    return chrRom[ChrAddressOf(!chrRomSwap ? bankPage[4] : bankPage[1] & 0xFE) + addr - 0x1800];
                case 0x1C00:
                    return chrRom[ChrAddressOf(!chrRomSwap ? bankPage[5] : bankPage[1] | 1) + addr - 0x1C00];
                default:
                    throw new Exception("Invalid CHR address read");
            }
        }

        public override void PpuRise()
        {
            if (irqReload || irqCounter == 0)
            {
                if (irqEnabled)
                    interrupt = true;
                irqCounter = irqLatch;
            }
            irqCounter--;
        }

        private int PrgAddressOf(int page)
        {
            if (page < 0)
            {
                return prgRom.Length + page * 0x2000;
            }
            return page * 0x2000;
        }

        private int ChrAddressOf(int page)
        {
            return page * 0x400;
        }
    }
}
using System;

namespace NESgard.Emulator.Mappers
{
    public class MMC1 : BaseMapper
    {
        byte reg8 = 0x0C;
        byte regA;
        byte regC;
        byte regE;

        byte shift;
        byte shiftWrites = 0;

        byte[] prgRam;

        public MMC1(Cartridge cartridge) : base(cartridge)
        {
            prgRam = new byte[0x2000];
        }

        public override byte PrgRead(ushort addr)
        {
            switch (addr)
            {
                case var a when (a >= 0x6000 && a <= 0x7FFF):
                    if ((regE & 0x10) == 0)
                        return prgRam[addr - 0x6000];
                    return 0;

                case var a when (a >= 0x8000 && a <= 0xFFFF):
                    {
                        var offset = addr - 0x8000;
                        if ((reg8 & 0x8) == 0)
                        {
                            offset += (regE & 0xE) * 0x4000;
                        }
                        else if ((reg8 & 0x4) == 0)
                        {
                            if (addr >= 0xC000)
                                offset += (regE & 0xF) * 0x4000 - 0x4000;
                        }
                        else
                        {
                            if (addr < 0xC000)
                                offset += (regE & 0xF) * 0x4000;
                            else
                                offset += prgRom.Length - 0x8000;
                        }
                        return prgRom[offset];
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
                    if ((regE & 0x10) == 0)
                        prgRam[addr - 0x6000] = data;
                    break;

                case var a when (a >= 0x8000 && a <= 0xFFFF):

                    if ((data & 0x80) != 0)
                    {
                        shift = 0;
                        shiftWrites = 0;
                    }
                    else
                    {
                        shift = (byte)((shift >> 1) | ((data & 0b1) << 4));
                        shiftWrites++;

                        if (shiftWrites == 5)
                        {
                            switch (addr & 0xE000)
                            {
                                case 0x8000: reg8 = shift; UpdateMirroring(); break;
                                case 0xA000: regA = shift; break;
                                case 0xC000: regC = shift; break;
                                case 0xE000: regE = shift; break;
                            }
                            shiftWrites = 0;
                            shift = 0;
                        }
                    }
                    break;
            }
        }

        public override byte ChrRead(ushort addr)
        {
            if ((reg8 & 0x10) == 0)
            {
                var offset = ((regA & 0x1E) * 0x1000);
                return chrRom[offset + addr];
            }
            else if (addr < 0x1000)
            {
                var offset = ((regA & 0x1F) * 0x1000);
                return chrRom[offset + addr];
            }
            else
            {
                var offset = ((regC & 0x1F) * 0x1000);
                return chrRom[offset + addr - 0x1000];
            }
        }

        private void UpdateMirroring()
        {
            switch (reg8 & 0x3)
            {
                case 0:
                    Mirroring = Mirroring.ScreenAOnly;
                    break;
                case 1:
                    Mirroring = Mirroring.ScreenBOnly;
                    break;
                case 2:
                    Mirroring = Mirroring.Vertical;
                    break;
                case 3:
                    Mirroring = Mirroring.Horizontal;
                    break;
            }
        }
    }
}
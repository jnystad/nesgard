using System;
using System.Linq;

namespace NESgard.Emulator.Mappers
{
    public class GxROM : BaseMapper
    {
        int prgBankOffset;
        int chrBankOffset;

        public GxROM(Cartridge cartridge) : base(cartridge) { }

        public override byte PrgRead(ushort addr)
        {
            switch (addr)
            {
                case var a when (a >= 0x8000 && a <= 0xFFFF):
                    {
                        var offset = prgBankOffset + addr - 0x8000;
                        return prgRom[offset];
                    }

                default:
                    throw new Exception("Read invalid PRG ROM address");
            }
        }

        public override void PrgWrite(ushort addr, byte data)
        {
            chrBankOffset = (addr & 0b11) * 0x2000;
            prgBankOffset = ((addr >> 4) & 0b11) * 0x8000;
        }

        public override byte ChrRead(ushort addr)
        {
            return chrRom[chrBankOffset + addr];
        }
    }
}
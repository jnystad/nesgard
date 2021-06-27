using System;

namespace Yawnese.Emulator.Mappers
{
    public class UxROM : BaseMapper
    {
        int prgBankOffset;

        public UxROM(Cartridge cartridge) : base(cartridge) { }

        public override byte PrgRead(ushort addr)
        {
            switch (addr)
            {
                case var a when (a >= 0x8000 && a <= 0xBFFF):
                    {
                        var offset = prgBankOffset + addr - 0x8000;
                        return prgRom[offset];
                    }

                case var a when (a >= 0xC000 && a <= 0xFFFF):
                    {
                        var offset = (prgRom.Length - 0x4000) + addr - 0xC000;
                        return prgRom[offset];
                    }

                default:
                    throw new Exception("Read invalid PRG ROM address");
            }
        }

        public override void PrgWrite(ushort addr, byte data)
        {
            prgBankOffset = (addr & 0b111) * 0x4000;
        }
    }
}
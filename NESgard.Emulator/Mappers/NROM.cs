using System;

namespace NESgard.Emulator.Mappers
{
    public class NROM : BaseMapper
    {
        public NROM(Cartridge cartridge) : base(cartridge) { }

        public override byte PrgRead(ushort addr)
        {
            switch (addr)
            {
                case var a when (a >= 0x8000 && a <= 0xBFFF):
                    {
                        var offset = addr - 0x8000;
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
    }
}
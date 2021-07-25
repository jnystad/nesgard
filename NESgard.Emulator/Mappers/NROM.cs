using System;

namespace NESgard.Emulator.Mappers
{
    public class NROM : BaseMapper
    {
        byte[] prgRam;

        public NROM(Cartridge cartridge) : base(cartridge)
        {
            prgRam = new byte[0x2000];
        }

        public override byte PrgRead(ushort addr)
        {
            switch (addr)
            {
                case var a when (a >= 0x6000 && a <= 0x7FFF):
                    return prgRam[addr - 0x6000];

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

        public override void PrgWrite(ushort addr, byte data)
        {
            switch (addr)
            {
                case var a when (a >= 0x6000 && a <= 0x7FFF):
                    prgRam[addr - 0x6000] = data;
                    break;

                default:
                    base.PrgWrite(addr, data);
                    break;
            }
        }
    }
}
using System;
using System.Linq;

namespace Yawnese.Emulator.Mappers
{
    public class Mapper0 : IMapper
    {
        byte[] prgRom;
        int prgRomPages;
        byte[] chrRom;

        public Mapper0(Cartridge cartridge, byte[] prg_rom, byte[] chr_rom)
        {
            prgRom = prg_rom;
            prgRomPages = cartridge.header.prg_rom_pages;
            chrRom = chr_rom;
        }

        public byte PrgRead(ushort addr)
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
                        var offset = (prgRomPages - 1) * 16384 + addr - 0xC000;
                        return prgRom[offset];
                    }

                default:
                    throw new Exception("Read invalid PRG ROM address");
            }
        }

        public byte ChrRead(ushort addr)
        {
            return chrRom[addr];
        }

        public void Write(ushort addr, byte data)
        {
            throw new Exception(string.Format("Write to ROM {0:X4}", addr));
        }
    }
}
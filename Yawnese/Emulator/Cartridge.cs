using System;
using System.IO;
using Yawnese.Emulator.Mappers;

namespace Yawnese.Emulator
{
    public class Cartridge
    {
        public struct Header
        {
            public byte[] raw;
            public bool preamble;
            public int prg_rom_pages;
            public int prg_ram_pages;
            public int chr_rom_pages;
            public Mirroring mirroring;
            public bool persistent;
            public bool trainer;
            public bool four_screen_vram;
            public byte mapper;
            public bool vs_unisystem;
            public bool playchoice_10;
            public bool nes_2_0;
            public bool ntsc;

            public Header(byte[] data)
            {
                raw = data;
                preamble = data[0] == 0x4E && data[1] == 0x45 && data[2] == 0x53 && data[3] == 0x1A;
                prg_rom_pages = data[4];
                chr_rom_pages = data[5];
                mirroring = (data[6] & 0b1) != 0 ? Mirroring.Vertical : Mirroring.Horizontal;
                persistent = (data[6] & 0b10) != 0;
                trainer = (data[6] & 0b100) != 0;
                four_screen_vram = (data[6] & 0b1000) != 0;
                mapper = (byte)((data[6] >> 4) | (data[7] & 0xF0));
                vs_unisystem = (data[7] & 0b1) != 0;
                playchoice_10 = (data[7] & 0b10) != 0;
                nes_2_0 = (data[7] & 0b1100) == 0b1000;
                prg_ram_pages = data[8];
                ntsc = (data[10] & 0b1) == 0;
            }
        }

        public Header header;

        public IMapper mapper;

        public Cartridge(string file)
        {
            using (var fs = File.OpenRead(file))
            {
                var headerRaw = new byte[16];
                fs.Read(headerRaw, 0, 16);

                header = new Header(headerRaw);

                var trainer = new byte[256];
                if (header.trainer)
                    fs.Read(trainer, 0, 256);

                var prg_rom = new byte[16384 * header.prg_rom_pages];
                fs.Read(prg_rom, 0, prg_rom.Length);

                var chr_rom = new byte[8192 * Math.Max(1, header.chr_rom_pages)];
                fs.Read(chr_rom, 0, chr_rom.Length);

                switch (header.mapper)
                {
                    case 0:
                        mapper = new Mapper0(this, prg_rom, chr_rom);
                        break;

                    default:
                        throw new Exception(string.Format("Unsupported mapper {0}", header.mapper));
                }

            }
        }
    }
}
using System;
using System.IO;
using System.Text;
using NESgard.Emulator.Mappers;

namespace NESgard.Emulator
{
    public class Cartridge
    {
        public enum CartridgeType
        {
            Nes2_0,
            iNes,
            OldiNes,
        }

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
            public ushort mapper;
            public bool vs_unisystem;
            public bool playchoice_10;
            public CartridgeType type;
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

                if ((data[7] & 0b1100) == 0b1000)
                {
                    type = CartridgeType.Nes2_0;
                }
                else if ((data[7] & 0x0C) == 0)
                {
                    type = CartridgeType.iNes;
                }
                else
                {
                    type = CartridgeType.OldiNes;
                }

                switch (type)
                {
                    case CartridgeType.Nes2_0:
                        mapper = (ushort)(((data[8] & 0x0F) << 8) | (data[7] & 0xF0) | (data[6] >> 4));
                        break;

                    case CartridgeType.OldiNes:
                        mapper = (ushort)(data[6] >> 4);
                        break;

                    default:
                    case CartridgeType.iNes:
                        mapper = (ushort)((data[7] & 0xF0) | (data[6] >> 4));
                        break;
                }

                vs_unisystem = (data[7] & 0b1) != 0;
                playchoice_10 = (data[7] & 0b10) != 0;
                prg_ram_pages = data[8];
                ntsc = (data[10] & 0b1) == 0;
            }

            public override string ToString()
            {
                var sb = new StringBuilder();

                sb.AppendFormat("ROM type: {0}\n", type);
                sb.AppendFormat("PRG ROM pages: {0}\n", prg_rom_pages);
                sb.AppendFormat("CHR ROM pages: {0}\n", chr_rom_pages);
                sb.AppendFormat("Mapper: {0}\n", mapper);
                sb.AppendFormat("Persistence: {0}\n", persistent);
                sb.AppendFormat("Mirroring: {0} 4s: {1}\n", mirroring, four_screen_vram);
                sb.AppendFormat("Trainer: {0}\n", trainer);
                sb.AppendFormat("NTSC: {0}", ntsc);

                return sb.ToString();
            }
        }

        public struct CartridgeData
        {
            public byte[] prgRom;
            public byte[] chrRom;
        }

        public Header header;

        public IMapper mapper;

        public CartridgeData data;

        public Cartridge(string file)
        {
            using (var fs = File.OpenRead(file))
            {
                var headerRaw = new byte[16];
                fs.Read(headerRaw, 0, 16);

                header = new Header(headerRaw);

                Console.WriteLine(header.ToString());

                var trainer = new byte[256];
                if (header.trainer)
                    fs.Read(trainer, 0, 256);

                var prgRom = new byte[16384 * header.prg_rom_pages];
                fs.Read(prgRom, 0, prgRom.Length);

                var chrRom = new byte[8192 * header.chr_rom_pages];
                fs.Read(chrRom, 0, chrRom.Length);

                data = new CartridgeData
                {
                    prgRom = prgRom,
                    chrRom = chrRom,
                };

                mapper = GetMapper(header.mapper);
            }
        }

        private IMapper GetMapper(ushort mapper)
        {
            switch (mapper)
            {
                case 0: return new NROM(this);
                case 1: return new MMC1(this);
                case 2: return new UxROM(this);
                case 4: return new MMC3(this);
                case 66: return new GxROM(this);

                default:
                    throw new Exception(string.Format("Unsupported mapper {0}", header.mapper));
            }

        }
    }
}
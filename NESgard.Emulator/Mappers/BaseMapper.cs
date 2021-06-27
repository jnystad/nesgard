using System;

namespace NESgard.Emulator
{
    public class BaseMapper : IMapper
    {
        protected byte[] prgRom;
        protected byte[] chrRom;

        protected bool isChrRam = false;
        protected bool interrupt = false;

        public Mirroring Mirroring { get; protected set; }

        public BaseMapper(Cartridge cartridge)
        {
            Mirroring = cartridge.header.mirroring;

            prgRom = cartridge.data.prgRom;
            chrRom = cartridge.data.chrRom;

            if (cartridge.header.chr_rom_pages == 0)
            {
                chrRom = new byte[0x2000];
                isChrRam = true;
            }
        }

        public virtual byte ChrRead(ushort addr)
        {
            return chrRom[addr];
        }

        public virtual void ChrWrite(ushort addr, byte data)
        {
            if (!isChrRam)
                throw new Exception("Attempted to write to CHR ROM");
            chrRom[addr] = data;
        }

        public virtual byte PrgRead(ushort addr)
        {
            return prgRom[addr - 0x8000];
        }

        public virtual void PrgWrite(ushort addr, byte data)
        {
            throw new Exception("Attempted to write PRG ROM");
        }

        public virtual void PpuRise() { }

        public virtual bool HasInterrupt()
        {
            var result = interrupt;
            interrupt = false;
            return result;
        }
    }
}
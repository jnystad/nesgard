using System;

namespace Yawnese.Emulator
{
    public class Bus
    {
        public Ppu ppu;

        public Cartridge rom;

        public IMapper mapper;

        public byte[] ram;

        public bool endOfFrame = false;

        public Controller controller1;

        public Controller controller2;

        public int stallCycles = 0;

        public Bus(Cartridge rom)
        {
            this.rom = rom;
            mapper = rom.mapper;
            ram = new byte[2048];
            ppu = new Ppu(rom);

            controller1 = new Controller();
            controller2 = new Controller();
        }

        public void Reset()
        {
            endOfFrame = false;
            ppu.Reset();
        }

        public void Tick()
        {
            var result = ppu.Tick();
            switch (result)
            {
                case PpuResult.EndOfFrame:
                    endOfFrame = true;
                    break;
            }
        }

        public bool PollNMI() { return ppu.PollNmi(); }

        public byte Read(ushort addr)
        {
            switch (addr)
            {
                case var a when (a <= 0x1FFF):
                    return ram[addr % 0x0800];
                case 0x4014:
                    throw new Exception(string.Format("Read write-only address {0:X4}", addr));
                case var a when (a >= 0x2000 & a <= 0x3FFF):
                    return ppu.Read(addr);
                case var a when (a >= 0x4000 && a <= 0x4013):
                case 0x4015:
                    return 0; // apu.Read(addr);
                case 0x4016:
                    return controller1.Read();
                case 0x4017:
                    return controller2.Read();
                case var a when (a >= 0x8000 && a <= 0xFFFF):
                    return mapper.PrgRead(addr);
                default:
                    throw new Exception(string.Format("Read invalid address {0:X4}", addr));
            }
        }

        public void Write(ushort addr, byte data)
        {
            switch (addr)
            {

                case var a when (a <= 0x1FFF):
                    ram[addr % 0x0800] = data;
                    break;

                case var a when (a >= 0x2000 && a <= 0x3FFF):
                    ppu.Write(addr, data);
                    break;

                case var a when (a >= 0x4000 && a <= 0x4013):
                case 0x4015:
                case 0x4017:
                    // apu.Write(addr, data);
                    break;

                case 0x4014:
                    OamDma(data);
                    break;

                case 0x4016:
                    controller1.Write(data);
                    controller2.Write(data);
                    break;

                case var a when (a >= 0x4020 && a <= 0xFFFF):
                    mapper.Write(addr, data);
                    break;

                default:
                    throw new Exception(string.Format("Write invalid address {0:X4}", addr));
            }
        }

        void OamDma(byte bank)
        {
            stallCycles = 513;
            for (int i = 0; i < 256; ++i)
            {
                var data = Read((ushort)((bank << 8) | i));
                ppu.WriteOamData(data);
            }
        }
    }
}
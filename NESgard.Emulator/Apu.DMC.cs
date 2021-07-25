/*
 * Source: http://www.slack.net/~ant/nes-emu/apu_ref.txt
 */

using System;

namespace NESgard.Emulator
{
    class DMC : ApuChannel
    {
        public byte frequency;
        public bool loop;
        public bool irqEnable;
        public ushort sampleAddress;
        public ushort sampleLength;

        byte shiftRegister;
        byte bitCount;
        public ushort addressCounter;
        public ushort lengthCounter;
        byte counter;

        public override void Reset()
        {
            lengthCounter = 0;
            base.Reset();
        }

        public void Resume()
        {
            if (lengthCounter == 0)
            {
                addressCounter = sampleAddress;
                lengthCounter = sampleLength;
            }
        }

        public void ClockTimer(Apu apu)
        {
            if (Enabled)
                ReadByte(apu);

            base.ClockTimer();
        }

        protected override void UpdateOutput()
        {
            Output = 0;
            if (bitCount > 0)
            {
                if ((shiftRegister & 1) == 1)
                {
                    if (counter <= 125)
                        counter += 2;
                }
                else
                {
                    if (counter > 2)
                    {
                        counter -= 2;
                    }
                }
                shiftRegister >>= 1;
                --bitCount;

                Output = counter;
            }
        }

        void ReadByte(Apu apu)
        {
            shiftRegister = apu.bus.Read(addressCounter);
            bitCount = 8;
            apu.bus.stallCycles += 4;
            ++addressCounter;
            if (addressCounter == 0)
                addressCounter = 0x8000;
            --lengthCounter;

            if (lengthCounter == 0 && irqEnable)
                apu.Interrupt();

            if (lengthCounter == 0 && loop)
            {
                addressCounter = sampleAddress;
                lengthCounter = sampleLength;
            }
        }

        public DmcWriteResult Write(int offset, byte data)
        {
            switch (offset)
            {
                case 0:
                    frequency = (byte)(data & 0x0F);
                    timerPeriod = DMC_FREQUENCY_PERIODS[frequency];
                    loop = (data & 0x40) != 0;
                    irqEnable = (data & 0x80) != 0;

                    if (!irqEnable)
                    {
                        return DmcWriteResult.ClearIrq;
                    }
                    break;

                case 1:
                    counter = (byte)(data & 0x7F);
                    break;

                case 2:
                    sampleAddress = (ushort)(0xC000 | (data << 6));
                    break;

                case 3:
                    sampleLength = (ushort)((data << 4) | 1);
                    break;
            }
            return DmcWriteResult.None;
        }

        public enum DmcWriteResult
        {
            None,
            ClearIrq
        }

        static readonly byte[] DMC_FREQUENCY_PERIODS = new byte[]
        {
            214, 190, 170, 160, 143, 127, 113, 107,
            95, 80, 71, 64, 53, 42, 36, 27
        };
    }
}

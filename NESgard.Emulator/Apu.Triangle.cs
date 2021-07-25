/*
 * Source: http://www.slack.net/~ant/nes-emu/apu_ref.txt
 */

namespace NESgard.Emulator
{
    public class Triangle : ApuLengthCounter
    {
        byte linearCounterLoad;
        bool linearCounterControl;

        byte linearCounter;

        bool wasLinearReset;

        int sequenceCounter;

        public void ClockLinear()
        {
            if (wasLinearReset)
            {
                linearCounter = linearCounterLoad;
            }
            else if (linearCounter > 0)
            {
                --linearCounter;
            }
            if (!linearCounterControl)
                wasLinearReset = false;
        }

        protected override void UpdateOutput()
        {
            Output = 0;

            if (linearCounter > 0 && LengthCounter > 0)
            {
                Output = TRIANGLE_SEQUENCE[sequenceCounter];
                sequenceCounter = (sequenceCounter + 1) % 32;
            }
        }

        public void Write(int offset, byte data)
        {
            switch (offset)
            {
                case 0:
                    linearCounterLoad = (byte)(data & 0x7F);
                    linearCounterControl = (data & 0x80) != 0;
                    SetLengthCounterHalt(linearCounterControl);
                    break;

                case 1:
                    break;

                case 2:
                    timerPeriod = (ushort)((timerPeriod & 0x0700) | data);
                    break;

                case 3:
                    timerPeriod = (ushort)((timerPeriod & 0x00FF) | ((data & 0x7) << 8));
                    SetLengthCounter(data >> 3);
                    wasLinearReset = true;
                    break;
            }
        }

        static readonly byte[] TRIANGLE_SEQUENCE = new byte[]
        {
            0xF, 0xE, 0xD, 0xC, 0xB, 0xA, 0x9, 0x8, 0x7, 0x6, 0x5, 0x4, 0x3, 0x2, 0x1, 0x0,
            0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0xA, 0xB, 0xC, 0xD, 0xE, 0xF
        };
    }
}

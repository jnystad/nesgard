/*
 * Source: http://www.slack.net/~ant/nes-emu/apu_ref.txt
 */

namespace NESgard.Emulator
{
    class Noise : ApuEnvelope
    {
        public byte period;
        public bool loopNoise;

        int shiftRegister;

        public override void Reset()
        {
            shiftRegister = 1;
            base.Reset();
        }

        protected override void UpdateOutput()
        {
            var shift = loopNoise ? 6 : 1;
            var b1 = shiftRegister & 1;
            var b2 = (shiftRegister >> shift) & 1;
            shiftRegister >>= 1;
            shiftRegister |= (b1 ^ b2) << 14;

            if (!Enabled || LengthCounter == 0 || (shiftRegister & 1) == 1)
                Output = 0;
            else
                Output = GetVolume();
        }

        public void Write(int offset, byte data)
        {
            switch (offset)
            {
                case 0:
                    SetEnvelope(data);
                    SetLengthCounterHalt((data & 0x20) != 0);
                    break;

                case 1:
                    break;

                case 2:
                    period = (byte)(data & 0x0F);
                    timerPeriod = TIMER_PERIODS[period];
                    loopNoise = (data & 0x80) != 0;
                    break;

                case 3:
                    ResetEnvelope();
                    SetLengthCounter(data >> 3);
                    break;
            }
        }

        static readonly ushort[] TIMER_PERIODS = new ushort[]
        {
            0x004, 0x008, 0x010, 0x020, 0x040, 0x060, 0x080, 0x0A0,
            0x0CA, 0x0FE, 0x17C, 0x1FC, 0x2FA, 0x3F8, 0x7F2, 0xFE4
        };
    }
}

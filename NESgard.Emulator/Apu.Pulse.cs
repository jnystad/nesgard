/*
 * Source: http://www.slack.net/~ant/nes-emu/apu_ref.txt
 */

namespace NESgard.Emulator
{
    class Pulse : ApuEnvelope
    {
        bool isPulse2;

        byte dutyMode;
        byte sweepShift;
        bool sweepNegate;
        byte sweepPeriod;
        ushort sweepTargetPeriod;
        bool sweepEnabled;
        ushort realPeriod;

        byte dutyCounter;
        bool wasSweepUpdated;
        byte sweepCounter;


        public Pulse(bool isPulse2)
        {
            this.isPulse2 = isPulse2;
        }

        public void ClockSweep()
        {
            --sweepCounter;
            if (sweepCounter == 0)
            {
                if (sweepEnabled && sweepShift > 0)
                    SetPeriod(sweepTargetPeriod);
                sweepCounter = sweepPeriod;
            }

            if (wasSweepUpdated)
            {
                sweepCounter = sweepPeriod;
                wasSweepUpdated = false;
            }

        }

        protected override void UpdateOutput()
        {
            dutyCounter = (byte)((dutyCounter + 1) % 8);

            var v = GetVolume();

            if (DUTY_SEQUENCES[dutyMode][dutyCounter] == 0)
                v = 0;

            if (realPeriod < 8 || (!sweepNegate && timerPeriod > 0x7FF))
                v = 0;

            Output = v;
        }

        void SetPeriod(ushort period)
        {
            realPeriod = period;
            timerPeriod = (ushort)(realPeriod * 2 + 1);
            UpdateTargetPeriod();
        }

        void UpdateTargetPeriod()
        {
            ushort shiftResult = (ushort)(realPeriod >> sweepShift);
            if (sweepNegate)
            {
                sweepTargetPeriod = (ushort)(realPeriod - shiftResult);
                if (isPulse2)
                {
                    --sweepTargetPeriod;
                }
            }
            else
            {
                sweepTargetPeriod = (ushort)(realPeriod + shiftResult);
            }
        }

        public void Write(int offset, byte data)
        {
            switch (offset)
            {
                case 0:
                    SetEnvelope(data);
                    SetLengthCounterHalt((data & 0x20) != 0);
                    dutyMode = (byte)((data & 0xC0) >> 6);
                    break;

                case 1:
                    sweepShift = (byte)(data & 0x07);
                    sweepNegate = (data & 0x08) != 0;
                    sweepPeriod = (byte)(((data & 0x70) >> 4) + 1);
                    sweepEnabled = (data & 0x80) != 0;
                    wasSweepUpdated = true;
                    UpdateTargetPeriod();
                    break;

                case 2:
                    SetPeriod((ushort)((realPeriod & 0x0700) | data));
                    break;

                case 3:
                    SetPeriod((ushort)((realPeriod & 0x00FF) | ((data & 0x7) << 8)));
                    SetLengthCounter(data >> 3);
                    ResetEnvelope();
                    dutyCounter = 0;
                    break;
            }
        }

        static readonly byte[][] DUTY_SEQUENCES = new byte[][]
        {
            new byte[] { 0, 1, 0, 0, 0, 0, 0, 0 },
            new byte[] { 0, 1, 1, 0, 0, 0, 0, 0 },
            new byte[] { 0, 1, 1, 1, 1, 0, 0, 0 },
            new byte[] { 1, 0, 0, 1, 1, 1, 1, 1 },
        };
    }
}

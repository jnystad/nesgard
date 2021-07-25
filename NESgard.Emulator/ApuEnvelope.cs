namespace NESgard.Emulator
{
    public abstract class ApuEnvelope : ApuLengthCounter
    {
        byte volume;
        bool constantVolume;

        bool wasReset;

        byte divider;
        byte counter;

        protected void SetEnvelope(byte data)
        {
            constantVolume = (data & 0x10) != 0;
            volume = (byte)(data & 0xF);
        }

        protected void ResetEnvelope()
        {
            wasReset = true;
        }

        protected byte GetVolume()
        {
            if (LengthCounter == 0)
                return 0;

            if (constantVolume)
                return volume;

            return counter;
        }

        public void ClockEnvelope()
        {
            if (wasReset)
            {
                counter = 15;
                divider = volume;
                wasReset = false;
            }
            else if (divider > 0)
                --divider;
            else
            {
                if (counter > 0)
                    --counter;
                else if (lengthCounterHalt)
                    counter = 15;
                divider = volume;
            }
        }
    }
}
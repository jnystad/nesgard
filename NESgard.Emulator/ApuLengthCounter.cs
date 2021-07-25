namespace NESgard.Emulator
{
    public abstract class ApuLengthCounter : ApuChannel
    {
        public byte LengthCounter { get; set; }
        protected bool lengthCounterHalt;

        public override void Reset()
        {
            LengthCounter = 0;
            lengthCounterHalt = false;
            base.Reset();
        }

        protected void SetLengthCounter(int index)
        {
            LengthCounter = LENGTH_TABLE[index];
        }

        protected void SetLengthCounterHalt(bool halt)
        {
            lengthCounterHalt = halt;
        }

        public void ClockLength()
        {
            if (LengthCounter > 0 && !lengthCounterHalt)
                --LengthCounter;
        }

        static readonly byte[] LENGTH_TABLE = new byte[]
        {
            0x0A, 0xFE, 0x14, 0x02, 0x28, 0x04, 0x50, 0x06,
            0xA0, 0x08, 0x3C, 0x0A, 0x0E, 0x0C, 0x1A, 0x0E,
            0x0C, 0x10, 0x18, 0x12, 0x30, 0x14, 0x60, 0x16,
            0xC0, 0x18, 0x48, 0x1A, 0x10, 0x1C, 0x20, 0x1E
        };
    }
}
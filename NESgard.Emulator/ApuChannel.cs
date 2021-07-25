namespace NESgard.Emulator
{
    public abstract class ApuChannel
    {
        public bool Enabled { get; set; }
        public byte Output { get; protected set; }
        protected ushort timerPeriod;
        ushort timerCounter;

        public virtual void Reset()
        {
            Enabled = false;
            timerCounter = 0;
            timerPeriod = 0;
        }

        public void ClockTimer()
        {
            if (timerCounter == 0)
            {
                timerCounter = timerPeriod;

                UpdateOutput();

                if (!Enabled)
                    Output = 0;
            }
            else
                --timerCounter;
        }

        protected abstract void UpdateOutput();
    }
}
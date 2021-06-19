
using System;

namespace Yawnese.Emulator
{

    public class Controller
    {
        bool strobe;
        byte buttonIndex;
        ControllerButton status;
        ControllerButton cachedStatus;

        public Controller() { }

        public byte Read()
        {
            if (buttonIndex > 7)
                return 1;

            var result = ((int)cachedStatus & (1 << buttonIndex)) >> buttonIndex;

            if (!strobe && buttonIndex < 8)
                buttonIndex++;

            return (byte)result;
        }

        public void Write(byte data)
        {
            strobe = (data & 1) == 1;
            if (strobe)
            {
                cachedStatus = status;
                buttonIndex = 0;
            }
        }

        public void Update(ControllerButton button, bool pressed)
        {
            if (pressed)
            {
                status |= button;
            }
            else
            {
                status &= ~button;
            }
        }
    }
}
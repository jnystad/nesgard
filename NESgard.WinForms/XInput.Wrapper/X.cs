// Extracted from XInput.Wrapper by Nikolai Voronin
// http://github.com/nikvoronin/xinput.wrapper
// Version 0.5 (Sep 8, 2020)
// Under the MIT License (MIT)
//

using System.Collections.Generic;
using System.Linq;

namespace XInput.Wrapper
{
    public static partial class X
    {
        public static readonly IReadOnlyList<Gamepad> Gamepads;
        public static readonly Gamepad Gamepad1;
        public static readonly Gamepad Gamepad2;
        public static readonly Gamepad Gamepad3;
        public static readonly Gamepad Gamepad4;
        static X()
        {
            Gamepad1 = new Gamepad(0);
            Gamepad2 = new Gamepad(1);
            Gamepad3 = new Gamepad(2);
            Gamepad4 = new Gamepad(3);

            Gamepads = new List<Gamepad>() { Gamepad1, Gamepad2, Gamepad3, Gamepad4 };
        }

        public static IEnumerable<Gamepad> AvailableGamepads
        {
            get
            {
                var gpads = Gamepads.Where(gp =>
                {
                    gp.UpdateConnectionState();
                    return gp.Available;
                });

                return gpads;
            }
        }

        /// <summary>
        /// Tests availability of the XInput_1.4 subsystem. 
        /// </summary>
        public static bool Available
        {
            get
            {
                try
                {
                    Native.XINPUT_STATE state = new Native.XINPUT_STATE();
                    Native.XInputGetState(0, ref state);
                }
                catch
                {
                    return false;
                }

                return true;
            }
        }
    }
}
using System;

namespace XInput.Wrapper
{
    public static partial class X
    {
        public sealed partial class Gamepad
        {
            [Flags]
            public enum ButtonFlags : uint
            {
                None = 0x0000,
                Up = 0x0001,
                Down = 0x0002,
                Left = 0x0004,
                Right = 0x0008,
                Start = 0x0010,
                Back = 0x0020,
                LStick = 0x0040,
                RStick = 0x0080,
                LThumb = 0x0040,
                RThumb = 0x0080,
                LBumper = 0x0100,
                RBumper = 0x0200,
                LTopShoulder = 0x0100,
                RTopShoulder = 0x0200,
                A = 0x1000,
                B = 0x2000,
                X = 0x4000,
                Y = 0x8000,
            };
        }
    }
}

using System.Runtime.InteropServices;

namespace XInput.Wrapper
{
    public static partial class X
    {
        private const string XINPUT1_4_DLL = "xinput1_4.dll";

        public static class Native
        {
            [DllImport(XINPUT1_4_DLL)]
            public static extern uint XInputGetState(
                uint dwUserIndex,
                ref XINPUT_STATE pState
                );

            [DllImport(XINPUT1_4_DLL)]
            public static extern void XInputEnable(
                bool enable
                );

            [StructLayout(LayoutKind.Explicit)]
            public struct XINPUT_STATE
            {
                [FieldOffset(0)]
                public uint dwPacketNumber;

                [FieldOffset(4)]
                public XINPUT_GAMEPAD Gamepad;
            }

            [StructLayout(LayoutKind.Explicit)]
            public struct XINPUT_GAMEPAD
            {
                [MarshalAs(UnmanagedType.I2)]
                [FieldOffset(0)]
                public ushort wButtons;

                [MarshalAs(UnmanagedType.I1)]
                [FieldOffset(2)]
                public byte bLeftTrigger;

                [MarshalAs(UnmanagedType.I1)]
                [FieldOffset(3)]
                public byte bRightTrigger;

                [MarshalAs(UnmanagedType.I2)]
                [FieldOffset(4)]
                public short sThumbLX;

                [MarshalAs(UnmanagedType.I2)]
                [FieldOffset(6)]
                public short sThumbLY;

                [MarshalAs(UnmanagedType.I2)]
                [FieldOffset(8)]
                public short sThumbRX;

                [MarshalAs(UnmanagedType.I2)]
                [FieldOffset(10)]
                public short sThumbRY;
            }
        }
    }
}

using System;

namespace XInput.Wrapper
{
    public static partial class X
    {
        public sealed partial class Gamepad
        {
            public readonly uint Index; // User index or controller index, zero based in a range of [0..3]

            /// <summary>
            /// Each controller displays which ID it is using by lighting up a quadrant on the "ring of light" in the center of the controller. A dwUserIndex value of 0 corresponds to the top-left quadrant; the numbering proceeds around the ring in clockwise order.
            /// </summary>
            public Quadrant UserQuadrant => (Quadrant)Index;

            public uint PacketNumber { get { return _internalState.dwPacketNumber; } }

            public ButtonFlags ButtonsState = ButtonFlags.None;

            private Native.XINPUT_STATE _internalState = new Native.XINPUT_STATE();

            internal Gamepad(uint index)
            {
                if (index < 0 || index > 3)
                    throw new ArgumentOutOfRangeException("index", index, "The XInput API supports up to four controllers. The index must be in range of [0..3]");

                Index = index;
            }

            public bool Available => Connected;
            public bool Connected { get; internal set; }
            public static bool Enable { set { Native.XInputEnable(value); } }

            public bool UpdateConnectionState()
            {
                bool isChanged = false;
                uint result = Native.XInputGetState(Index, ref _internalState);

                if (Connected != (result == 0))
                {
                    isChanged = true;
                    Connected = (result == 0);
                }

                return isChanged;
            }

            /// <summary>
            /// Update gamepad data
            /// </summary>
            /// <returns>TRUE - if state has been changed (button pressed, gamepad dis|connected, etc)</returns>
            public bool Update()
            {
                var lastButtonsState = ButtonsState;

                uint prevPacketNumber = _internalState.dwPacketNumber;
                bool isChanged = UpdateConnectionState();

                if (prevPacketNumber != _internalState.dwPacketNumber)
                {
                    isChanged = true;
                }

                if (Connected)
                {
                    ButtonsState = (ButtonFlags)_internalState.Gamepad.wButtons;
                }

                return isChanged;
            }

            public enum Quadrant { TopLeft = 0, TopRight = 1, BottomRight = 2, BottomLeft = 3 }
        }
    }
}

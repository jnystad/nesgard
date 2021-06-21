using System;

namespace Yawnese.Emulator
{
    [Flags]
    public enum ControllerButton
    {
        RIGHT = 0b10000000,
        LEFT = 0b01000000,
        DOWN = 0b00100000,
        UP = 0b00010000,
        START = 0b00001000,
        SELECT = 0b00000100,
        BUTTON_B = 0b00000010,
        BUTTON_A = 0b00000001,
    }

    [Flags]
    public enum CpuStatus
    {
        Carry = 0b00000001,
        Zero = 0b00000010,
        InterruptDisable = 0b00000100,
        DecimalMode = 0b00001000,
        Push = 0b00010000,
        Break = 0b00100000,
        Overflow = 0b01000000,
        Negative = 0b10000000,
    }

    public enum Mirroring
    {
        Horizontal,
        Vertical,
        ScreenAOnly,
        ScreenBOnly,
    }

    [Flags]
    public enum PpuControl
    {
        NameTable1 = 0b00000001,
        NameTable2 = 0b00000010,
        VramAddIncrement = 0b00000100,
        SpritePatternAddr = 0b00001000,
        BgPatternAddr = 0b00010000,
        SpriteSize = 0b00100000,
        MasterSlaveSelect = 0b01000000,
        GenerateNMI = 0b10000000,
    }

    public enum PpuMask
    {
        Greyscale = 0b00000001,
        RenderBackgroundColumn1 = 0b00000010,
        RenderSpritesColumn1 = 0b00000100,
        RenderBackground = 0b00001000,
        RenderSprites = 0b00010000,
        EmphasisR = 0b00100000,
        EmphasisG = 0b01000000,
        EmphasisB = 0b10000000,
    }

    public enum PpuResult
    {
        None = 0,
        Scanline,
        Nmi,
        EndOfFrame,
    }

    [Flags]
    public enum PpuStatus
    {
        SpriteOverflow = 0b00100000,
        SpriteHit = 0b01000000,
        Vblank = 0b10000000,
    }
}
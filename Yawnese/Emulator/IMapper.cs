namespace Yawnese.Emulator
{
    public interface IMapper
    {
        byte ChrRead(ushort addr);
        byte PrgRead(ushort addr);
        void Write(ushort addr, byte data);
    }
}
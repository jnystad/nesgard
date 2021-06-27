namespace Yawnese.Emulator
{
    public interface IMapper
    {
        byte ChrRead(ushort addr);
        void ChrWrite(ushort addr, byte data);
        byte PrgRead(ushort addr);
        void PrgWrite(ushort addr, byte data);

        void PpuRise();
        bool HasInterrupt();

        Mirroring Mirroring { get; }
    }
}
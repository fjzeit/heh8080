namespace Heh8080.Core;

/// <summary>
/// Memory interface for the 8080 CPU.
/// </summary>
public interface IMemory
{
    byte Read(ushort address);
    void Write(ushort address, byte value);
}

namespace Heh8080.Core;

/// <summary>
/// I/O bus interface for the 8080 CPU.
/// </summary>
public interface IIoBus
{
    byte In(byte port);
    void Out(byte port, byte value);
}

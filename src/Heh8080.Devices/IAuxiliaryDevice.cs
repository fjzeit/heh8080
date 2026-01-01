namespace Heh8080.Devices;

/// <summary>
/// Auxiliary I/O device interface (reader/punch, serial port, pipe).
/// </summary>
public interface IAuxiliaryDevice
{
    /// <summary>
    /// Check if input data is available.
    /// </summary>
    bool IsInputReady { get; }

    /// <summary>
    /// Check if device has reached end of file.
    /// </summary>
    bool IsEof { get; }

    /// <summary>
    /// Read a byte from the auxiliary device.
    /// </summary>
    byte ReadByte();

    /// <summary>
    /// Write a byte to the auxiliary device.
    /// </summary>
    void WriteByte(byte value);

    /// <summary>
    /// Signal end of file on output.
    /// </summary>
    void SetEof();
}

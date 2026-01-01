namespace Heh8080.Devices;

/// <summary>
/// Printer device interface for line printer output.
/// </summary>
public interface IPrinterDevice
{
    /// <summary>
    /// Check if the printer is ready to receive data.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Write a character to the printer.
    /// </summary>
    void WriteChar(byte c);
}

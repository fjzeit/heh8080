namespace Heh8080.Devices;

/// <summary>
/// Null printer device that discards output.
/// </summary>
public sealed class NullPrinterDevice : IPrinterDevice
{
    public bool IsReady => true;

    public void WriteChar(byte c)
    {
        // Discard output
    }
}

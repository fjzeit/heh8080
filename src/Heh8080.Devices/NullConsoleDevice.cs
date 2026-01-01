namespace Heh8080.Devices;

/// <summary>
/// Null console device that discards output and returns no input.
/// Useful for headless testing.
/// </summary>
public sealed class NullConsoleDevice : IConsoleDevice
{
    public bool IsInputReady => false;

    public byte ReadChar() => 0x00;

    public void WriteChar(byte c)
    {
        // Discard output
    }
}

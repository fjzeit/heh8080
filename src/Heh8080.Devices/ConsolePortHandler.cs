using Heh8080.Core;

namespace Heh8080.Devices;

/// <summary>
/// Console port handler (ports 0-1).
/// Port 0: Status (IN) - FFh if input ready, 00h if not
/// Port 1: Data (IN/OUT) - Read/write character
/// </summary>
public sealed class ConsolePortHandler : IIoDevice
{
    private readonly IConsoleDevice _console;

    public ConsolePortHandler(IConsoleDevice console)
    {
        _console = console;
    }

    public byte In(byte port)
    {
        return port switch
        {
            0 => _console.IsInputReady ? (byte)0xFF : (byte)0x00,
            1 => _console.ReadChar(),
            _ => 0xFF
        };
    }

    public void Out(byte port, byte value)
    {
        if (port == 1)
        {
            _console.WriteChar(value);
        }
    }

    /// <summary>
    /// Register this handler with the I/O bus.
    /// </summary>
    public void Register(IoBus bus)
    {
        bus.Register(this, 0, 1);
    }
}

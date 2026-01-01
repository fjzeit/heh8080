using Heh8080.Core;

namespace Heh8080.Devices;

/// <summary>
/// Console port handler (ports 0-1).
/// Port 0: Status (IN) - FFh if input ready, 00h if not
/// Port 1: Data (IN/OUT) - Read/write character
///
/// Includes idle detection: when status is polled repeatedly with no input,
/// yields CPU time to avoid spinning at 100%.
/// </summary>
public sealed class ConsolePortHandler : IIoDevice
{
    private readonly IConsoleDevice _console;
    private int _idlePollCount;
    private const int IdleThreshold = 100;

    public ConsolePortHandler(IConsoleDevice console)
    {
        _console = console;
    }

    public byte In(byte port)
    {
        switch (port)
        {
            case 0:
                if (_console.IsInputReady)
                {
                    _idlePollCount = 0;
                    return 0xFF;
                }
                else
                {
                    _idlePollCount++;
                    if (_idlePollCount >= IdleThreshold)
                    {
                        _idlePollCount = 0;
                        Thread.Sleep(1); // Yield CPU when idle
                    }
                    return 0x00;
                }

            case 1:
                _idlePollCount = 0;
                return _console.ReadChar();

            default:
                return 0xFF;
        }
    }

    public void Out(byte port, byte value)
    {
        if (port == 1)
        {
            _idlePollCount = 0;
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

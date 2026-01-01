using Heh8080.Core;

namespace Heh8080.Devices;

/// <summary>
/// Printer port handler (ports 2-3).
/// Port 2: Status (IN) - Always FFh (ready)
/// Port 3: Data (IN/OUT) - Read returns 1Ah (EOF), write sends to printer
/// </summary>
public sealed class PrinterPortHandler : IIoDevice
{
    private readonly IPrinterDevice? _printer;

    public PrinterPortHandler(IPrinterDevice? printer = null)
    {
        _printer = printer;
    }

    public byte In(byte port)
    {
        return port switch
        {
            2 => 0xFF, // Always ready
            3 => 0x1A, // EOF
            _ => 0xFF
        };
    }

    public void Out(byte port, byte value)
    {
        if (port == 3)
        {
            _printer?.WriteChar(value);
        }
    }

    /// <summary>
    /// Register this handler with the I/O bus.
    /// </summary>
    public void Register(IoBus bus)
    {
        bus.Register(this, 2, 3);
    }
}

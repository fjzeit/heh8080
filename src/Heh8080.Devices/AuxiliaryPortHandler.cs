using Heh8080.Core;

namespace Heh8080.Devices;

/// <summary>
/// Auxiliary I/O port handler (ports 4-5).
/// Port 4: Status (IN) - FFh if input ready, 00h if not; OUT sets EOF flag
/// Port 5: Data (IN/OUT) - Read/write byte
/// </summary>
public sealed class AuxiliaryPortHandler : IIoDevice
{
    private readonly IAuxiliaryDevice? _auxiliary;

    public AuxiliaryPortHandler(IAuxiliaryDevice? auxiliary = null)
    {
        _auxiliary = auxiliary;
    }

    public byte In(byte port)
    {
        if (_auxiliary == null)
        {
            return port == 4 ? (byte)0x00 : (byte)0x1A; // Not ready, EOF
        }

        return port switch
        {
            4 => _auxiliary.IsInputReady ? (byte)0xFF : (byte)0x00,
            5 => _auxiliary.ReadByte(),
            _ => 0xFF
        };
    }

    public void Out(byte port, byte value)
    {
        if (_auxiliary == null) return;

        switch (port)
        {
            case 4:
                if (value != 0)
                    _auxiliary.SetEof();
                break;
            case 5:
                _auxiliary.WriteByte(value);
                break;
        }
    }

    /// <summary>
    /// Register this handler with the I/O bus.
    /// </summary>
    public void Register(IoBus bus)
    {
        bus.Register(this, 4, 5);
    }
}

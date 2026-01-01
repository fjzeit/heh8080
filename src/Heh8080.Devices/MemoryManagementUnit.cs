using Heh8080.Core;

namespace Heh8080.Devices;

/// <summary>
/// Memory Management Unit port handler (ports 20-23).
///
/// Ports:
///   20: Bank count (IN) / Initialize banks (OUT)
///   21: Current bank (IN/OUT)
///   22: Segment size in pages (IN/OUT) - must set before init
///   23: Write protect status (IN/OUT)
///
/// Default: 192 pages (48KB) banked, 64 pages (16KB) common.
/// </summary>
public sealed class MemoryManagementUnit : IIoDevice
{
    private readonly Memory _memory;
    private int _segmentSizePages = 192; // Default 48KB

    public MemoryManagementUnit(Memory memory)
    {
        _memory = memory;
    }

    public byte In(byte port)
    {
        return port switch
        {
            20 => (byte)_memory.BankCount,
            21 => (byte)_memory.CurrentBank,
            22 => (byte)_segmentSizePages,
            23 => 0x00, // Write protect status - not tracked separately
            _ => 0xFF
        };
    }

    public void Out(byte port, byte value)
    {
        switch (port)
        {
            case 20: // Initialize banks
                _memory.SetSegmentSize(_segmentSizePages);
                _memory.InitializeBanks(value);
                break;
            case 21: // Select bank
                _memory.SelectBank(value);
                break;
            case 22: // Set segment size (before init)
                _segmentSizePages = value;
                break;
            case 23: // Write protect
                _memory.SetWriteProtect(value != 0);
                break;
        }
    }

    /// <summary>
    /// Register this handler with the I/O bus.
    /// </summary>
    public void Register(IoBus bus)
    {
        bus.Register(this, 20, 23);
    }
}

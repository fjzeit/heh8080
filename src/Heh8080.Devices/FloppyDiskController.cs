using Heh8080.Core;

namespace Heh8080.Devices;

/// <summary>
/// Floppy Disk Controller port handler (ports 10-17).
///
/// Ports:
///   10: Drive select (0-15, A-P)
///   11: Track number (0-255)
///   12: Sector number low byte
///   13: Command (OUT: 0=read, 1=write; IN: 00h)
///   14: Status (IN only)
///   15: DMA address low byte
///   16: DMA address high byte
///   17: Sector number high byte
///
/// Standard 8" floppy: 77 tracks, 26 sectors/track, 128 bytes/sector = 256KB
/// </summary>
public sealed class FloppyDiskController : IIoDevice
{
    private readonly IDiskImageProvider _diskProvider;
    private readonly IMemory _memory;

    // Registers
    private byte _drive;
    private byte _track;
    private ushort _sector;
    private ushort _dmaAddress;
    private byte _status;

    // Constants
    public const int MaxTracks = 77;
    public const int SectorsPerTrack = 26;
    public const int BytesPerSector = 128;

    // Status codes
    public const byte StatusOk = 0;
    public const byte StatusInvalidDrive = 1;
    public const byte StatusInvalidTrack = 2;
    public const byte StatusInvalidSector = 3;
    public const byte StatusSeekError = 4;
    public const byte StatusReadError = 5;
    public const byte StatusWriteError = 6;
    public const byte StatusInvalidCommand = 7;

    public FloppyDiskController(IDiskImageProvider diskProvider, IMemory memory)
    {
        _diskProvider = diskProvider;
        _memory = memory;
    }

    public byte CurrentDrive => _drive;
    public byte CurrentTrack => _track;
    public ushort CurrentSector => _sector;
    public ushort DmaAddress => _dmaAddress;
    public byte Status => _status;

    public byte In(byte port)
    {
        return port switch
        {
            10 => _drive,
            11 => _track,
            12 => (byte)(_sector & 0xFF),
            13 => 0x00,
            14 => _status,
            15 => (byte)(_dmaAddress & 0xFF),
            16 => (byte)((_dmaAddress >> 8) & 0xFF),
            17 => (byte)((_sector >> 8) & 0xFF),
            _ => 0xFF
        };
    }

    public void Out(byte port, byte value)
    {
        switch (port)
        {
            case 10: // Drive select
                _drive = value;
                break;
            case 11: // Track
                _track = value;
                break;
            case 12: // Sector low
                _sector = (ushort)((_sector & 0xFF00) | value);
                break;
            case 13: // Command
                ExecuteCommand(value);
                break;
            case 15: // DMA low
                _dmaAddress = (ushort)((_dmaAddress & 0xFF00) | value);
                break;
            case 16: // DMA high
                _dmaAddress = (ushort)((_dmaAddress & 0x00FF) | (value << 8));
                break;
            case 17: // Sector high
                _sector = (ushort)((_sector & 0x00FF) | (value << 8));
                break;
        }
    }

    private void ExecuteCommand(byte command)
    {
        // Validate parameters
        if (!_diskProvider.IsMounted(_drive))
        {
            _status = StatusInvalidDrive;
            return;
        }

        if (_track >= MaxTracks)
        {
            _status = StatusInvalidTrack;
            return;
        }

        if (_sector < 1 || _sector > SectorsPerTrack)
        {
            _status = StatusInvalidSector;
            return;
        }

        switch (command)
        {
            case 0: // Read sector
                ReadSector();
                break;
            case 1: // Write sector
                WriteSector();
                break;
            default:
                _status = StatusInvalidCommand;
                break;
        }
    }

    private void ReadSector()
    {
        Span<byte> buffer = stackalloc byte[BytesPerSector];

        if (_diskProvider.ReadSector(_drive, _track, _sector, buffer))
        {
            // Transfer to memory at DMA address
            for (int i = 0; i < BytesPerSector; i++)
            {
                _memory.Write((ushort)(_dmaAddress + i), buffer[i]);
            }
            _status = StatusOk;
        }
        else
        {
            _status = StatusReadError;
        }
    }

    private void WriteSector()
    {
        if (_diskProvider.IsReadOnly(_drive))
        {
            _status = StatusWriteError;
            return;
        }

        Span<byte> buffer = stackalloc byte[BytesPerSector];

        // Transfer from memory at DMA address
        for (int i = 0; i < BytesPerSector; i++)
        {
            buffer[i] = _memory.Read((ushort)(_dmaAddress + i));
        }

        if (_diskProvider.WriteSector(_drive, _track, _sector, buffer))
        {
            _status = StatusOk;
        }
        else
        {
            _status = StatusWriteError;
        }
    }

    /// <summary>
    /// Register this handler with the I/O bus.
    /// </summary>
    public void Register(IoBus bus)
    {
        bus.Register(this, 10, 17);
    }
}

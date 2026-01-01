using Heh8080.Core;

namespace Heh8080.Devices;

/// <summary>
/// Hardware control device (port 160).
/// Port is locked until magic byte 0xAA is written.
///
/// Control bits (after unlock):
///   Bit 4: Switch to 8080 mode
///   Bit 5: Switch to Z80 mode
///   Bit 6: Reset CPU and MMU, reboot
///   Bit 7: Halt emulation
/// </summary>
public sealed class HardwareControlDevice : IIoDevice
{
    private const byte UnlockMagic = 0xAA;

    private bool _unlocked;
    private Action? _resetCallback;
    private Action? _haltCallback;

    /// <summary>
    /// Set the callback to invoke on reset request.
    /// </summary>
    public void SetResetCallback(Action callback)
    {
        _resetCallback = callback;
    }

    /// <summary>
    /// Set the callback to invoke on halt request.
    /// </summary>
    public void SetHaltCallback(Action callback)
    {
        _haltCallback = callback;
    }

    public byte In(byte port)
    {
        // Returns lock status: 0 = locked, 1 = unlocked
        return _unlocked ? (byte)0x01 : (byte)0x00;
    }

    public void Out(byte port, byte value)
    {
        if (port != 160)
            return;

        if (!_unlocked)
        {
            // Check for unlock magic
            if (value == UnlockMagic)
            {
                _unlocked = true;
            }
            return;
        }

        // Locked again after any command
        _unlocked = false;

        // Process control bits
        if ((value & 0x80) != 0) // Bit 7: Halt
        {
            _haltCallback?.Invoke();
        }
        else if ((value & 0x40) != 0) // Bit 6: Reset
        {
            _resetCallback?.Invoke();
        }
        // Bits 4-5: CPU mode switching (8080/Z80) - not implemented, we're 8080 only
    }

    /// <summary>
    /// Register this handler with the I/O bus.
    /// </summary>
    public void Register(IoBus bus)
    {
        bus.Register(this, 160);
    }
}

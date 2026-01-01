using Heh8080.Core;

namespace Heh8080.Devices;

/// <summary>
/// Timer device port handler (port 27).
/// Generates maskable interrupt every 10ms when enabled.
///
/// Port 27:
///   IN: Returns current timer state (1=enabled, 0=disabled)
///   OUT: 1=enable, 0=disable
/// </summary>
public sealed class TimerDevice : IIoDevice
{
    private bool _enabled;
    private Action? _interruptCallback;

    /// <summary>
    /// Gets whether the timer is enabled.
    /// </summary>
    public bool IsEnabled => _enabled;

    /// <summary>
    /// Set the callback to invoke when timer fires.
    /// The callback should trigger an interrupt on the CPU.
    /// </summary>
    public void SetInterruptCallback(Action callback)
    {
        _interruptCallback = callback;
    }

    /// <summary>
    /// Called by the emulator's timer system every 10ms.
    /// </summary>
    public void Tick()
    {
        if (_enabled)
        {
            _interruptCallback?.Invoke();
        }
    }

    public byte In(byte port)
    {
        return port == 27 ? (_enabled ? (byte)0x01 : (byte)0x00) : (byte)0xFF;
    }

    public void Out(byte port, byte value)
    {
        if (port == 27)
        {
            _enabled = value != 0;
        }
    }

    /// <summary>
    /// Register this handler with the I/O bus.
    /// </summary>
    public void Register(IoBus bus)
    {
        bus.Register(this, 27);
    }
}

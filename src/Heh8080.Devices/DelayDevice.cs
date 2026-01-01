using Heh8080.Core;

namespace Heh8080.Devices;

/// <summary>
/// Delay device port handler (port 28).
/// Used for busy-wait detection and timing.
///
/// Port 28:
///   IN: Returns 00h
///   OUT: Delay n × 10ms
/// </summary>
public sealed class DelayDevice : IIoDevice
{
    private Action<int>? _delayCallback;

    /// <summary>
    /// Set the callback to invoke when delay is requested.
    /// Parameter is delay in 10ms units.
    /// </summary>
    public void SetDelayCallback(Action<int> callback)
    {
        _delayCallback = callback;
    }

    public byte In(byte port)
    {
        return 0x00;
    }

    public void Out(byte port, byte value)
    {
        if (port == 28 && value > 0)
        {
            _delayCallback?.Invoke(value);
        }
    }

    /// <summary>
    /// Register this handler with the I/O bus.
    /// </summary>
    public void Register(IoBus bus)
    {
        bus.Register(this, 28);
    }
}

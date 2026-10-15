using System.Collections.Concurrent;
using Heh8080.Core;

namespace Heh8080.Tests;

public class EmulatorThreadingTests
{
    private const int InCallTarget = 10_000;

    [Fact]
    public async Task Start_FromThreadWithSynchronizationContext_RunsNoInstructionsOnThatThread()
    {
        using var uiThread = new PumpingSynchronizationContext();
        var device = new ThreadRecordingDevice(uiThread.ManagedThreadId);
        using var emulator = new Emulator(CpuType.ZilogZ80);
        emulator.IoBus.Register(device, 0x00);
        // IN A,(0) ; JP 0000
        emulator.Load(0x0000, [0xDB, 0x00, 0xC3, 0x00, 0x00]);

        uiThread.Post(_ => emulator.Start(), null);
        await device.TargetReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await emulator.StopAsync();

        Assert.InRange(device.Calls, InCallTarget, int.MaxValue);
        Assert.Equal(0, device.CallsOnUiThread);
    }

    private sealed class ThreadRecordingDevice(int uiThreadId) : IIoDevice
    {
        private int _calls;
        private int _callsOnUiThread;

        public TaskCompletionSource TargetReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls => Volatile.Read(ref _calls);
        public int CallsOnUiThread => Volatile.Read(ref _callsOnUiThread);

        public byte In(byte port)
        {
            if (Environment.CurrentManagedThreadId == uiThreadId)
                Interlocked.Increment(ref _callsOnUiThread);
            if (Interlocked.Increment(ref _calls) == InCallTarget)
                TargetReached.TrySetResult();
            return 0;
        }

        public void Out(byte port, byte value) { }
    }

    private sealed class PumpingSynchronizationContext : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();
        private readonly Thread _thread;

        public PumpingSynchronizationContext()
        {
            _thread = new Thread(Pump) { IsBackground = true };
            _thread.Start();
        }

        public int ManagedThreadId => _thread.ManagedThreadId;

        public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

        public void Dispose()
        {
            _queue.CompleteAdding();
            _thread.Join();
            _queue.Dispose();
        }

        private void Pump()
        {
            SetSynchronizationContext(this);
            foreach (var (callback, state) in _queue.GetConsumingEnumerable())
                callback(state);
        }
    }
}

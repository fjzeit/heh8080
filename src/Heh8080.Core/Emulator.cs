namespace Heh8080.Core;

/// <summary>
/// CPU type selection.
/// </summary>
public enum CpuType
{
    /// <summary>Intel 8080 (1974)</summary>
    Intel8080,

    /// <summary>Zilog Z80 (1976)</summary>
    ZilogZ80
}

/// <summary>
/// Main emulator orchestrator. Manages CPU execution on a background thread
/// with device coordination via the I/O bus.
/// </summary>
public sealed class Emulator : IDisposable
{
    private const int BatchSize = 5000;

    public ICpu Cpu { get; }
    public CpuType CpuType { get; }
    public Memory Memory { get; }
    public IoBus IoBus { get; }

    public bool IsRunning { get; private set; }
    public long InstructionCount { get; private set; }

    public event Action? Started;
    public event Action? Stopped;
    public event Action<Exception>? Error;

    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private readonly object _lock = new();

    public Emulator(CpuType cpuType = CpuType.ZilogZ80)
    {
        CpuType = cpuType;
        Memory = new Memory();
        IoBus = new IoBus();
        Cpu = cpuType switch
        {
            CpuType.Intel8080 => new Cpu8080(Memory, IoBus),
            CpuType.ZilogZ80 => new CpuZ80(Memory, IoBus),
            _ => throw new ArgumentOutOfRangeException(nameof(cpuType))
        };
    }

    /// <summary>
    /// Load binary data into memory at the specified address.
    /// </summary>
    public void Load(ushort address, ReadOnlySpan<byte> data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            Memory.Write((ushort)(address + i), data[i]);
        }
    }

    /// <summary>
    /// Start CPU execution on a background thread.
    /// </summary>
    public void Start()
    {
        lock (_lock)
        {
            if (IsRunning) return;

            _cts = new CancellationTokenSource();
            IsRunning = true;
            // Use async RunLoop that yields periodically (required for single-threaded WASM)
            _runTask = RunLoopAsync(_cts.Token);
        }
        Started?.Invoke();
    }

    /// <summary>
    /// Stop CPU execution.
    /// </summary>
    public async Task StopAsync()
    {
        Task? runTask;
        lock (_lock)
        {
            if (!IsRunning) return;
            _cts?.Cancel();
            runTask = _runTask;
        }

        if (runTask != null)
        {
            try
            {
                await runTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        lock (_lock)
        {
            _cts?.Dispose();
            _cts = null;
            _runTask = null;
        }
    }

    /// <summary>
    /// Reset CPU to initial state.
    /// </summary>
    public void Reset()
    {
        Cpu.Reset();
        InstructionCount = 0;
    }

    /// <summary>
    /// Execute a single instruction (for debugging).
    /// </summary>
    public int Step()
    {
        int cycles = Cpu.Step();
        InstructionCount++;
        return cycles;
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && !Cpu.Halted)
            {
                // Run a batch of instructions
                for (int i = 0; i < BatchSize && !ct.IsCancellationRequested && !Cpu.Halted; i++)
                {
                    Cpu.Step();
                    InstructionCount++;
                }

                // Delay to allow UI updates (critical for single-threaded WASM)
                // Task.Yield() doesn't work in single-threaded WASM - need actual delay
                await Task.Delay(1);
            }
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex);
        }
        finally
        {
            IsRunning = false;
            Stopped?.Invoke();
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _runTask?.Wait(TimeSpan.FromSeconds(1));
    }
}

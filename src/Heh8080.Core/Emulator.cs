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

    // Debug infrastructure
    private readonly TraceBuffer _traceBuffer = new();
    private readonly HashSet<ushort> _breakpoints = new();
    private volatile bool _traceEnabled;
    private volatile bool _breakpointHit;
    private ushort _hitAddress;

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

    #region Debug Infrastructure

    /// <summary>
    /// Enable or disable instruction tracing.
    /// </summary>
    public bool TraceEnabled
    {
        get => _traceEnabled;
        set => _traceEnabled = value;
    }

    /// <summary>
    /// Get the trace buffer.
    /// </summary>
    public TraceBuffer TraceBuffer => _traceBuffer;

    /// <summary>
    /// Get active breakpoints.
    /// </summary>
    public IReadOnlyCollection<ushort> Breakpoints => _breakpoints;

    /// <summary>
    /// True if execution stopped at a breakpoint.
    /// </summary>
    public bool BreakpointHit => _breakpointHit;

    /// <summary>
    /// Address where breakpoint was hit.
    /// </summary>
    public ushort HitAddress => _hitAddress;

    /// <summary>
    /// Set a breakpoint at the specified address.
    /// </summary>
    public void SetBreakpoint(ushort address)
    {
        lock (_lock) { _breakpoints.Add(address); }
    }

    /// <summary>
    /// Clear a breakpoint at the specified address.
    /// </summary>
    public void ClearBreakpoint(ushort address)
    {
        lock (_lock) { _breakpoints.Remove(address); }
    }

    /// <summary>
    /// Clear all breakpoints.
    /// </summary>
    public void ClearAllBreakpoints()
    {
        lock (_lock) { _breakpoints.Clear(); }
    }

    /// <summary>
    /// Clear the breakpoint hit flag to allow resuming execution.
    /// </summary>
    public void ClearHit()
    {
        _breakpointHit = false;
    }

    #endregion

    private async Task RunLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && !Cpu.Halted && !_breakpointHit)
            {
                // Run a batch of instructions
                for (int i = 0; i < BatchSize && !ct.IsCancellationRequested && !Cpu.Halted; i++)
                {
                    // Breakpoint check (before instruction)
                    if (_breakpoints.Count > 0 && _breakpoints.Contains(Cpu.PC))
                    {
                        _hitAddress = Cpu.PC;
                        _breakpointHit = true;
                        return;
                    }

                    // Trace capture (before instruction, if enabled)
                    if (_traceEnabled)
                    {
                        var state = Cpu.GetTraceState();
                        _traceBuffer.Add(new TraceEntry(state, Memory));
                    }

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

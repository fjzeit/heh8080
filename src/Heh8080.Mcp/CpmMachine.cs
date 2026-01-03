using System.Text;
using Heh8080.Core;
using Heh8080.Devices;
using Heh8080.Terminal;

namespace Heh8080.Mcp;

/// <summary>
/// Headless CP/M machine for MCP server use.
/// Wraps emulator, terminal, and disk controller without UI dependencies.
/// </summary>
public class CpmMachine : IDisposable
{
    private readonly Emulator _emulator;
    private readonly Adm3aTerminal _terminal;
    private readonly FileDiskImageProvider _diskProvider;
    private readonly FloppyDiskController _fdc;
    private readonly ConsolePortHandler _console;
    private readonly MemoryManagementUnit _mmu;
    private readonly TimerDevice _timer;
    private readonly DelayDevice _delay;
    private readonly HardwareControlDevice _hwControl;
    private Timer? _interruptTimer;
    private bool _disposed;

    public CpmMachine(string? diskAPath = null)
    {
        _emulator = new Emulator(CpuType.Intel8080);
        _terminal = new Adm3aTerminal();
        _diskProvider = new FileDiskImageProvider();

        // Create and register devices
        _console = new ConsolePortHandler(_terminal);
        _console.Register(_emulator.IoBus);

        _fdc = new FloppyDiskController(_diskProvider, _emulator.Memory);
        _fdc.Register(_emulator.IoBus);

        _mmu = new MemoryManagementUnit(_emulator.Memory);
        _mmu.Register(_emulator.IoBus);

        _timer = new TimerDevice();
        _timer.Register(_emulator.IoBus);
        _timer.SetInterruptCallback(OnTimerInterrupt);

        _delay = new DelayDevice();
        _delay.Register(_emulator.IoBus);
        _delay.SetDelayCallback(OnDelayRequested);

        _hwControl = new HardwareControlDevice();
        _hwControl.Register(_emulator.IoBus);

        // Mount disk A if provided
        if (!string.IsNullOrEmpty(diskAPath) && File.Exists(diskAPath))
        {
            _diskProvider.Mount(0, diskAPath);
        }
    }

    public Adm3aTerminal Terminal => _terminal;
    public IMemory Memory => _emulator.Memory;
    public IDiskImageProvider DiskProvider => _diskProvider;
    public bool IsRunning => _emulator.IsRunning;

    /// <summary>
    /// Boot from disk A.
    /// </summary>
    public bool Boot()
    {
        if (!_diskProvider.IsMounted(0))
            return false;

        // Load boot sector (track 0, sector 1) to 0x0000
        Span<byte> bootSector = stackalloc byte[128];
        if (!_diskProvider.ReadSector(0, 0, 1, bootSector))
            return false;

        _emulator.Load(0x0000, bootSector);
        _emulator.Cpu.PC = 0x0000;
        _emulator.Cpu.SP = 0xFFFF;

        StartInterruptTimer();
        _emulator.Start();
        return true;
    }

    /// <summary>
    /// Stop the emulator.
    /// </summary>
    public async Task StopAsync()
    {
        StopInterruptTimer();
        await _emulator.StopAsync();
    }

    /// <summary>
    /// Reset the machine.
    /// </summary>
    public async Task ResetAsync()
    {
        await _emulator.StopAsync();
        StopInterruptTimer();
        _emulator.Reset();
        _terminal.Buffer.Clear();
    }

    /// <summary>
    /// Send input text to the terminal.
    /// </summary>
    public void SendInput(string text)
    {
        _terminal.QueueInput(text);
    }

    /// <summary>
    /// Read the current screen contents as a string.
    /// </summary>
    public string ReadScreen()
    {
        var buffer = _terminal.Buffer;
        var sb = new StringBuilder();

        for (int y = 0; y < TerminalBuffer.Height; y++)
        {
            for (int x = 0; x < TerminalBuffer.Width; x++)
            {
                char c = buffer[x, y].Character;
                sb.Append(c == '\0' ? ' ' : c);
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Wait for specific text to appear on screen.
    /// </summary>
    public async Task<bool> WaitForTextAsync(string pattern, int timeoutMs = 5000)
    {
        var cts = new CancellationTokenSource(timeoutMs);
        var tcs = new TaskCompletionSource<bool>();

        void OnContentChanged()
        {
            if (ReadScreen().Contains(pattern))
            {
                tcs.TrySetResult(true);
            }
        }

        // Check immediately
        if (ReadScreen().Contains(pattern))
            return true;

        _terminal.ContentChanged += OnContentChanged;
        cts.Token.Register(() => tcs.TrySetResult(false));

        try
        {
            return await tcs.Task;
        }
        finally
        {
            _terminal.ContentChanged -= OnContentChanged;
        }
    }

    /// <summary>
    /// Read bytes from memory.
    /// </summary>
    public byte[] PeekMemory(int address, int length)
    {
        var bytes = new byte[length];
        for (int i = 0; i < length && address + i < 0x10000; i++)
        {
            bytes[i] = _emulator.Memory.Read((ushort)(address + i));
        }
        return bytes;
    }

    /// <summary>
    /// Write bytes to memory.
    /// </summary>
    public void PokeMemory(int address, byte[] data)
    {
        for (int i = 0; i < data.Length && address + i < 0x10000; i++)
        {
            _emulator.Memory.Write((ushort)(address + i), data[i]);
        }
    }

    /// <summary>
    /// Get machine status.
    /// </summary>
    public string GetStatus()
    {
        var cpu = _emulator.Cpu;
        return $"Running: {_emulator.IsRunning}, PC: {cpu.PC:X4}, SP: {cpu.SP:X4}, Halted: {cpu.Halted}";
    }

    #region Debug Methods

    /// <summary>
    /// Get the CPU for direct register access.
    /// </summary>
    public ICpu Cpu => _emulator.Cpu;

    /// <summary>
    /// Get the emulator for advanced access.
    /// </summary>
    public Emulator Emulator => _emulator;

    /// <summary>
    /// Enable instruction tracing.
    /// </summary>
    public void EnableTrace() => _emulator.TraceEnabled = true;

    /// <summary>
    /// Disable instruction tracing.
    /// </summary>
    public void DisableTrace() => _emulator.TraceEnabled = false;

    /// <summary>
    /// Get trace entries.
    /// </summary>
    public TraceEntry[] GetTraceEntries() => _emulator.TraceBuffer.GetEntries();

    /// <summary>
    /// Clear trace buffer.
    /// </summary>
    public void ClearTrace() => _emulator.TraceBuffer.Clear();

    /// <summary>
    /// Set a breakpoint at the specified address.
    /// </summary>
    public void SetBreakpoint(ushort address) => _emulator.SetBreakpoint(address);

    /// <summary>
    /// Clear a breakpoint at the specified address.
    /// </summary>
    public void ClearBreakpoint(ushort address) => _emulator.ClearBreakpoint(address);

    /// <summary>
    /// Clear all breakpoints.
    /// </summary>
    public void ClearAllBreakpoints() => _emulator.ClearAllBreakpoints();

    /// <summary>
    /// Get all active breakpoints.
    /// </summary>
    public IReadOnlyCollection<ushort> GetBreakpoints() => _emulator.Breakpoints;

    /// <summary>
    /// Check if execution is stopped at a breakpoint.
    /// </summary>
    public bool BreakpointHit => _emulator.BreakpointHit;

    /// <summary>
    /// Get the address where breakpoint was hit.
    /// </summary>
    public ushort HitAddress => _emulator.HitAddress;

    /// <summary>
    /// Continue execution after hitting a breakpoint.
    /// </summary>
    public void Continue()
    {
        _emulator.ClearHit();
        StartInterruptTimer();
        _emulator.Start();
    }

    /// <summary>
    /// Execute a single instruction.
    /// </summary>
    public int SingleStep() => _emulator.Step();

    #endregion

    private void StartInterruptTimer()
    {
        _interruptTimer = new Timer(
            _ => _timer.Tick(),
            null,
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(10));
    }

    private void StopInterruptTimer()
    {
        _interruptTimer?.Dispose();
        _interruptTimer = null;
    }

    private void OnTimerInterrupt()
    {
        if (_emulator.IsRunning && _emulator.Cpu.InterruptsEnabled)
        {
            _emulator.Cpu.Interrupt(7);
        }
    }

    private void OnDelayRequested(int units)
    {
        Thread.Sleep(units * 10);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopInterruptTimer();
        _emulator.StopAsync().Wait(TimeSpan.FromSeconds(1));
        _emulator.Dispose();
        _diskProvider.Dispose();
    }
}

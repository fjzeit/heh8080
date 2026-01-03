using System.ComponentModel;
using System.Text;
using Heh8080.Core;
using ModelContextProtocol.Server;

namespace Heh8080.Mcp;

/// <summary>
/// MCP tools for interacting with a CP/M machine.
/// </summary>
[McpServerToolType]
public class CpmTools
{
    private readonly CpmMachine _machine;

    public CpmTools(CpmMachine machine)
    {
        _machine = machine;
    }

    [McpServerTool]
    [Description("Send text input to the CP/M console. Use \\r for Enter key.")]
    public string SendInput(string text)
    {
        // Convert escaped sequences
        text = text.Replace("\\r", "\r").Replace("\\n", "\n");
        _machine.SendInput(text);
        return $"Sent {text.Length} characters";
    }

    [McpServerTool]
    [Description("Read the current terminal screen contents (80x24 characters).")]
    public string ReadScreen()
    {
        return _machine.ReadScreen();
    }

    [McpServerTool]
    [Description("Wait for specific text to appear on the terminal screen.")]
    public async Task<string> WaitForText(
        [Description("The text pattern to wait for")] string pattern,
        [Description("Timeout in milliseconds (default 5000)")] int timeoutMs = 5000)
    {
        bool found = await _machine.WaitForTextAsync(pattern, timeoutMs);
        return found ? $"Found: {pattern}" : $"Timeout waiting for: {pattern}";
    }

    [McpServerTool]
    [Description("Read bytes from emulator memory. Returns hex-encoded bytes.")]
    public string PeekMemory(
        [Description("Memory address (0-65535)")] int address,
        [Description("Number of bytes to read (max 256)")] int length = 16)
    {
        length = Math.Clamp(length, 1, 256);
        address = Math.Clamp(address, 0, 0xFFFF);

        var bytes = _machine.PeekMemory(address, length);
        return Convert.ToHexString(bytes);
    }

    [McpServerTool]
    [Description("Write bytes to emulator memory. Data should be hex-encoded.")]
    public string PokeMemory(
        [Description("Memory address (0-65535)")] int address,
        [Description("Hex-encoded bytes to write")] string hexData)
    {
        address = Math.Clamp(address, 0, 0xFFFF);

        try
        {
            var data = Convert.FromHexString(hexData);
            _machine.PokeMemory(address, data);
            return $"Wrote {data.Length} bytes at {address:X4}";
        }
        catch (FormatException)
        {
            return "Error: Invalid hex data";
        }
    }

    [McpServerTool]
    [Description("Get current machine status including CPU registers.")]
    public string Status()
    {
        return _machine.GetStatus();
    }

    [McpServerTool]
    [Description("Reset the CP/M machine.")]
    public async Task<string> Reset()
    {
        await _machine.ResetAsync();

        if (_machine.Boot())
        {
            return "Machine reset and rebooted";
        }
        return "Machine reset (no boot disk)";
    }

    [McpServerTool]
    [Description("Mount a disk image file to a drive.")]
    public string MountDisk(
        [Description("Drive number (0=A, 1=B, 2=C, 3=D)")] int drive,
        [Description("Path to disk image file")] string path)
    {
        if (drive < 0 || drive > 3)
            return "Error: Drive must be 0-3";

        if (!File.Exists(path))
            return $"Error: File not found: {path}";

        _machine.DiskProvider.Mount(drive, path);
        return $"Mounted {path} as drive {(char)('A' + drive)}:";
    }

    [McpServerTool]
    [Description("Get information about mounted disk drives.")]
    public string DiskInfo()
    {
        var lines = new List<string>();
        for (int i = 0; i < 4; i++)
        {
            char drive = (char)('A' + i);
            bool mounted = _machine.DiskProvider.IsMounted(i);
            lines.Add($"{drive}: {(mounted ? "Mounted" : "Empty")}");
        }
        return string.Join("\n", lines);
    }

    [McpServerTool]
    [Description("Refresh disk to see external changes made by cpmtools. Reopens file handle.")]
    public string RefreshDisk(
        [Description("Drive number (0=A, 1=B, 2=C, 3=D)")] int drive)
    {
        if (drive < 0 || drive > 3)
            return "Error: Drive must be 0-3";

        if (!_machine.DiskProvider.IsMounted(drive))
            return $"Error: Drive {(char)('A' + drive)}: not mounted";

        if (_machine.DiskProvider.Refresh(drive))
            return $"Drive {(char)('A' + drive)}: refreshed";
        else
            return $"Error: Failed to refresh drive {(char)('A' + drive)}:";
    }

    #region Debug Tools

    [McpServerTool]
    [Description("Get full CPU state including all registers and flags.")]
    public string GetCpuState()
    {
        var cpu = _machine.Cpu;
        var state = cpu.GetTraceState();

        return $"PC:{state.PC:X4} SP:{state.SP:X4} " +
               $"A:{state.A:X2} B:{state.B:X2} C:{state.C:X2} " +
               $"D:{state.D:X2} E:{state.E:X2} H:{state.H:X2} L:{state.L:X2} " +
               $"F:{state.Flags:X2} IE:{(cpu.InterruptsEnabled ? 1 : 0)} Halted:{(cpu.Halted ? 1 : 0)}";
    }

    [McpServerTool]
    [Description("Get instruction trace buffer (last N instructions executed). Returns hex format.")]
    public string GetTrace(
        [Description("Number of entries to return (default 32, max 256)")] int count = 32)
    {
        count = Math.Clamp(count, 1, 256);
        var entries = _machine.GetTraceEntries();

        if (entries.Length == 0)
            return "Trace buffer empty. Enable tracing with EnableTrace first.";

        var sb = new StringBuilder();
        sb.AppendLine("PC   OP      A  B  C  D  E  H  L  SP   F");

        foreach (var e in entries.TakeLast(count))
        {
            sb.AppendLine($"{e.PC:X4} {e.Opcode:X2}{e.Op1:X2}{e.Op2:X2} " +
                          $"{e.A:X2} {e.B:X2} {e.C:X2} {e.D:X2} {e.E:X2} {e.H:X2} {e.L:X2} " +
                          $"{e.SP:X4} {e.Flags:X2}");
        }

        return sb.ToString();
    }

    [McpServerTool]
    [Description("Enable instruction tracing.")]
    public string EnableTrace()
    {
        _machine.EnableTrace();
        return "Trace enabled. Use GetTrace to view captured instructions.";
    }

    [McpServerTool]
    [Description("Disable instruction tracing.")]
    public string DisableTrace()
    {
        _machine.DisableTrace();
        return "Trace disabled.";
    }

    [McpServerTool]
    [Description("Clear the trace buffer.")]
    public string ClearTrace()
    {
        _machine.ClearTrace();
        return "Trace buffer cleared.";
    }

    [McpServerTool]
    [Description("Set a breakpoint at the specified address. Execution stops before the instruction at this address.")]
    public string SetBreakpoint(
        [Description("Memory address for breakpoint (0-65535)")] int address)
    {
        address = Math.Clamp(address, 0, 0xFFFF);
        _machine.SetBreakpoint((ushort)address);
        return $"Breakpoint set at {address:X4}";
    }

    [McpServerTool]
    [Description("Clear a breakpoint at the specified address.")]
    public string ClearBreakpoint(
        [Description("Memory address of breakpoint to clear (0-65535)")] int address)
    {
        address = Math.Clamp(address, 0, 0xFFFF);
        _machine.ClearBreakpoint((ushort)address);
        return $"Breakpoint cleared at {address:X4}";
    }

    [McpServerTool]
    [Description("List all active breakpoints.")]
    public string ListBreakpoints()
    {
        var bps = _machine.GetBreakpoints();
        if (bps.Count == 0)
            return "No breakpoints set.";
        return "Breakpoints: " + string.Join(", ", bps.OrderBy(x => x).Select(x => $"{x:X4}"));
    }

    [McpServerTool]
    [Description("Execute a single instruction and return CPU state. Machine must be stopped first.")]
    public string Step()
    {
        if (_machine.IsRunning)
            return "Error: Machine is running. Use StopMachine first.";

        _machine.SingleStep();
        return GetCpuState();
    }

    [McpServerTool]
    [Description("Stop the running machine.")]
    public async Task<string> StopMachine()
    {
        await _machine.StopAsync();
        return "Machine stopped. " + GetCpuState();
    }

    [McpServerTool]
    [Description("Continue execution after hitting a breakpoint.")]
    public string Continue()
    {
        if (!_machine.BreakpointHit)
            return "Not stopped at breakpoint.";

        _machine.Continue();
        return $"Execution resumed from {_machine.HitAddress:X4}";
    }

    #endregion
}

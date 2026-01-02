using System.ComponentModel;
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
}

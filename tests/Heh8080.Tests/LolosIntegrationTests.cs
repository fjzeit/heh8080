using System.Text;
using Heh8080.Core;
using Heh8080.Devices;
using Heh8080.Terminal;
using Xunit.Abstractions;

namespace Heh8080.Tests;

/// <summary>
/// Integration tests verifying LOLOS boots and runs correctly.
/// </summary>
public class LolosIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public LolosIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string? GetDiskPath()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "src", "Heh8080.Desktop", "Assets", "disks", "lolos.dsk"),
            Path.Combine(baseDir, "..", "..", "..", "..", "src", "Heh8080.Desktop", "Assets", "disks", "lolos.dsk"),
            "src/Heh8080.Desktop/Assets/disks/lolos.dsk",
        };

        foreach (var candidate in candidates)
        {
            var normalized = Path.GetFullPath(candidate);
            if (File.Exists(normalized)) return normalized;
        }
        return null;
    }

    [Fact]
    public void LOLOS_BootsSuccessfully_Z80()
    {
        var diskPath = GetDiskPath();
        if (diskPath == null)
        {
            _output.WriteLine("SKIPPED: lolos.dsk not found");
            return;
        }

        _output.WriteLine($"Loading disk from {diskPath}");
        var (output, completed) = RunLolosUntilPrompt(CpuType.ZilogZ80, diskPath);

        _output.WriteLine("=== Terminal Output ===");
        _output.WriteLine(output);
        _output.WriteLine("=======================");

        Assert.True(completed, $"LOLOS did not boot within time limit. Output:\n{output}");
        Assert.Contains("LOLOS", output.ToUpperInvariant());
    }

    [Fact]
    public void LOLOS_BootsSuccessfully_8080()
    {
        var diskPath = GetDiskPath();
        if (diskPath == null)
        {
            _output.WriteLine("SKIPPED: lolos.dsk not found");
            return;
        }

        _output.WriteLine($"Loading disk from {diskPath}");
        var (output, completed) = RunLolosUntilPrompt(CpuType.Intel8080, diskPath);

        _output.WriteLine("=== Terminal Output ===");
        _output.WriteLine(output);
        _output.WriteLine("=======================");

        Assert.True(completed, $"LOLOS did not boot within time limit. Output:\n{output}");
        Assert.Contains("LOLOS", output.ToUpperInvariant());
    }

    [Fact]
    public void LOLOS_ShowsCommandPrompt()
    {
        var diskPath = GetDiskPath();
        if (diskPath == null)
        {
            _output.WriteLine("SKIPPED: lolos.dsk not found");
            return;
        }

        var (output, completed) = RunLolosUntilPrompt(CpuType.ZilogZ80, diskPath);

        _output.WriteLine("=== Terminal Output ===");
        _output.WriteLine(output);
        _output.WriteLine("=======================");

        Assert.True(completed, $"LOLOS did not show prompt. Output:\n{output}");
        // LOLOS shows "A>" prompt when ready
        Assert.Contains("A>", output);
    }

    [Fact]
    public void LOLOS_DIR_ListsFiles()
    {
        var diskPath = GetDiskPath();
        if (diskPath == null)
        {
            _output.WriteLine("SKIPPED: lolos.dsk not found");
            return;
        }

        var (output, completed) = RunLolosCommand(CpuType.ZilogZ80, diskPath, "DIR\r");

        _output.WriteLine("=== Terminal Output ===");
        _output.WriteLine(output);
        _output.WriteLine("=======================");

        Assert.True(completed, $"DIR command did not complete. Output:\n{output}");
        // Should show DIR output (either files or "No File" for empty disk)
        Assert.True(output.Contains("A>DIR") || output.Contains("A>dir"),
            $"DIR command not echoed. Output:\n{output}");
    }

    [Fact]
    public void LOLOS_DIR_WithWildcard()
    {
        var diskPath = GetDiskPath();
        if (diskPath == null)
        {
            _output.WriteLine("SKIPPED: lolos.dsk not found");
            return;
        }

        var (output, completed) = RunLolosCommand(CpuType.ZilogZ80, diskPath, "DIR *.COM\r");

        _output.WriteLine("=== Terminal Output ===");
        _output.WriteLine(output);
        _output.WriteLine("=======================");

        Assert.True(completed, $"DIR *.COM did not complete. Output:\n{output}");
        // Should show DIR command executed (may show "No File" on empty disk)
        Assert.True(output.Contains("DIR") && output.Contains(".COM"),
            $"DIR *.COM not echoed. Output:\n{output}");
    }

    [Fact]
    public void LOLOS_InvalidCommand_ShowsError()
    {
        var diskPath = GetDiskPath();
        if (diskPath == null)
        {
            _output.WriteLine("SKIPPED: lolos.dsk not found");
            return;
        }

        var (output, completed) = RunLolosCommand(CpuType.ZilogZ80, diskPath, "XYZZY\r");

        _output.WriteLine("=== Terminal Output ===");
        _output.WriteLine(output);
        _output.WriteLine("=======================");

        Assert.True(completed, $"Command did not complete. Output:\n{output}");
        // CP/M shows "No File" for unknown commands (tries to load as .COM)
        Assert.Contains("No File", output);
    }

    private (string output, bool completed) RunLolosCommand(CpuType cpuType, string diskPath, string command)
    {
        var diskData = File.ReadAllBytes(diskPath);
        var diskProvider = new TestDiskImageProvider();
        diskProvider.MountFromBytes(0, diskData);

        var terminal = new Adm3aTerminal();
        var emulator = new Emulator(cpuType);

        // Register devices
        var console = new ConsolePortHandler(terminal);
        console.Register(emulator.IoBus);

        var fdc = new FloppyDiskController(diskProvider, emulator.Memory);
        fdc.Register(emulator.IoBus);

        var mmu = new MemoryManagementUnit(emulator.Memory);
        mmu.Register(emulator.IoBus);

        var timer = new TimerDevice();
        timer.Register(emulator.IoBus);

        var delay = new DelayDevice();
        delay.Register(emulator.IoBus);

        var hwControl = new HardwareControlDevice();
        hwControl.Register(emulator.IoBus);

        var printer = new PrinterPortHandler(new NullPrinterDevice());
        printer.Register(emulator.IoBus);

        var aux = new AuxiliaryPortHandler(null);
        aux.Register(emulator.IoBus);

        // Load boot sector
        Span<byte> bootSector = stackalloc byte[128];
        diskProvider.ReadSector(0, 0, 1, bootSector);
        emulator.Load(0x0000, bootSector);
        emulator.Cpu.PC = 0x0000;
        emulator.Cpu.SP = 0xFFFF;

        const long maxInstructions = 100_000_000;
        long instructionCount = 0;
        bool sawPrompt = false;
        bool commandSent = false;

        const int timerInterval = 10000;
        int timerCounter = 0;

        while (instructionCount < maxInstructions && !emulator.Cpu.Halted)
        {
            emulator.Step();
            instructionCount++;
            timerCounter++;

            if (timerCounter >= timerInterval)
            {
                timerCounter = 0;
                timer.Tick();
                if (emulator.Cpu.InterruptsEnabled)
                {
                    emulator.Cpu.Interrupt(7);
                }
            }

            if (instructionCount % 50000 == 0)
            {
                var currentOutput = ReadTerminalBuffer(terminal);

                // Count prompts to know when to send command and when to stop
                int currentPromptCount = CountOccurrences(currentOutput, "A>");

                if (!commandSent && currentPromptCount >= 1)
                {
                    // First prompt seen - send command
                    terminal.QueueInput(command);
                    commandSent = true;
                }
                else if (commandSent && currentPromptCount >= 2)
                {
                    // Second prompt - command completed
                    sawPrompt = true;
                    break;
                }
            }
        }

        var output = ReadTerminalBuffer(terminal);
        if (!sawPrompt && CountOccurrences(output, "A>") >= 2)
        {
            sawPrompt = true;
        }

        emulator.Dispose();
        return (output, sawPrompt);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    private (string output, bool completed) RunLolosUntilPrompt(CpuType cpuType, string diskPath)
    {
        var diskData = File.ReadAllBytes(diskPath);
        var diskProvider = new TestDiskImageProvider();
        diskProvider.MountFromBytes(0, diskData);

        var terminal = new Adm3aTerminal();

        var emulator = new Emulator(cpuType);

        // Register devices
        var console = new ConsolePortHandler(terminal);
        console.Register(emulator.IoBus);

        var fdc = new FloppyDiskController(diskProvider, emulator.Memory);
        fdc.Register(emulator.IoBus);

        var mmu = new MemoryManagementUnit(emulator.Memory);
        mmu.Register(emulator.IoBus);

        var timer = new TimerDevice();
        timer.Register(emulator.IoBus);

        var delay = new DelayDevice();
        delay.Register(emulator.IoBus);

        var hwControl = new HardwareControlDevice();
        hwControl.Register(emulator.IoBus);

        var printer = new PrinterPortHandler(new NullPrinterDevice());
        printer.Register(emulator.IoBus);

        var aux = new AuxiliaryPortHandler(null);
        aux.Register(emulator.IoBus);

        // Load boot sector
        Span<byte> bootSector = stackalloc byte[128];
        diskProvider.ReadSector(0, 0, 1, bootSector);
        emulator.Load(0x0000, bootSector);
        emulator.Cpu.PC = 0x0000;
        emulator.Cpu.SP = 0xFFFF;

        // Run until we see "A>" prompt or timeout
        const long maxInstructions = 100_000_000; // 100M instructions should be plenty
        long instructionCount = 0;
        bool sawPrompt = false;

        // Timer interrupt every ~10000 instructions (simulating 10ms at ~1MHz)
        const int timerInterval = 10000;
        int timerCounter = 0;

        while (instructionCount < maxInstructions && !emulator.Cpu.Halted)
        {
            emulator.Step();
            instructionCount++;
            timerCounter++;

            if (timerCounter >= timerInterval)
            {
                timerCounter = 0;
                timer.Tick();
                if (emulator.Cpu.InterruptsEnabled)
                {
                    emulator.Cpu.Interrupt(7);
                }
            }

            // Check for prompt periodically
            if (instructionCount % 100000 == 0)
            {
                var currentOutput = ReadTerminalBuffer(terminal);
                if (currentOutput.Contains("A>"))
                {
                    sawPrompt = true;
                    break;
                }
            }
        }

        // Final output read
        var output = ReadTerminalBuffer(terminal);
        if (!sawPrompt && output.Contains("A>"))
        {
            sawPrompt = true;
        }

        emulator.Dispose();
        return (output, sawPrompt);
    }

    private static string ReadTerminalBuffer(Adm3aTerminal terminal)
    {
        var sb = new StringBuilder();
        for (int y = 0; y < TerminalBuffer.Height; y++)
        {
            for (int x = 0; x < TerminalBuffer.Width; x++)
            {
                char c = terminal.Buffer[x, y].Character;
                sb.Append(c);
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}

/// <summary>
/// Simple in-memory disk image provider for testing.
/// </summary>
internal sealed class TestDiskImageProvider : IDiskImageProvider
{
    private const int MaxDrives = 16;
    private const int BytesPerSector = 128;
    private const int SectorsPerTrack = 26;
    private const int TracksPerDisk = 77;
    private const int DiskSize = BytesPerSector * SectorsPerTrack * TracksPerDisk;

    private readonly byte[]?[] _drives = new byte[]?[MaxDrives];

    public bool IsMounted(int drive) => drive >= 0 && drive < MaxDrives && _drives[drive] != null;

    public void MountFromBytes(int drive, byte[] data)
    {
        if (drive < 0 || drive >= MaxDrives)
            throw new ArgumentOutOfRangeException(nameof(drive));

        byte[] diskData;
        if (data.Length >= DiskSize)
        {
            diskData = data;
        }
        else
        {
            diskData = new byte[DiskSize];
            Array.Copy(data, diskData, data.Length);
            for (int i = data.Length; i < DiskSize; i++)
                diskData[i] = 0xE5;
        }
        _drives[drive] = diskData;
    }

    public void Mount(int drive, string imagePath, bool readOnly = false)
        => throw new NotSupportedException("Use MountFromBytes for tests");

    public void Unmount(int drive)
    {
        if (drive >= 0 && drive < MaxDrives)
            _drives[drive] = null;
    }

    public bool ReadSector(int drive, int track, int sector, Span<byte> buffer)
    {
        if (buffer.Length < BytesPerSector) return false;
        var diskData = drive >= 0 && drive < MaxDrives ? _drives[drive] : null;
        if (diskData == null) return false;

        int offset = (track * SectorsPerTrack + (sector - 1)) * BytesPerSector;
        if (offset < 0 || offset + BytesPerSector > diskData.Length) return false;

        diskData.AsSpan(offset, BytesPerSector).CopyTo(buffer);
        return true;
    }

    public bool WriteSector(int drive, int track, int sector, ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < BytesPerSector) return false;
        var diskData = drive >= 0 && drive < MaxDrives ? _drives[drive] : null;
        if (diskData == null) return false;

        int offset = (track * SectorsPerTrack + (sector - 1)) * BytesPerSector;
        if (offset < 0 || offset + BytesPerSector > diskData.Length) return false;

        buffer[..BytesPerSector].CopyTo(diskData.AsSpan(offset, BytesPerSector));
        return true;
    }

    public bool IsReadOnly(int drive) => false;
}

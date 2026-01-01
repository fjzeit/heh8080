using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Heh8080.Core;
using Heh8080.Devices;
using Heh8080.Terminal;

namespace Heh8080.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly Emulator _emulator;
    private readonly FileDiskImageProvider _diskProvider;
    private readonly Adm3aTerminal _terminal;

    // Devices
    private readonly ConsolePortHandler _console;
    private readonly FloppyDiskController _fdc;
    private readonly MemoryManagementUnit _mmu;
    private readonly TimerDevice _timer;
    private readonly DelayDevice _delay;
    private readonly HardwareControlDevice _hwControl;
    private readonly PrinterPortHandler _printer;
    private readonly AuxiliaryPortHandler _aux;

    // Timer for 10ms interrupts
    private System.Threading.Timer? _interruptTimer;

    // Temp file for extracted disk image
    private string? _extractedDiskPath;

    [ObservableProperty]
    private string _statusText = "Initializing...";

    [ObservableProperty]
    private string? _driveAPath;

    [ObservableProperty]
    private string? _driveBPath;

    [ObservableProperty]
    private string? _driveCPath;

    [ObservableProperty]
    private string? _driveDPath;

    public Adm3aTerminal Terminal => _terminal;
    public bool IsRunning => _emulator.IsRunning;

    public MainViewModel()
    {
        // Initialize emulator
        _emulator = new Emulator();
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
        _hwControl.SetResetCallback(OnResetRequested);
        _hwControl.SetHaltCallback(OnHaltRequested);

        _printer = new PrinterPortHandler(new NullPrinterDevice());
        _printer.Register(_emulator.IoBus);

        _aux = new AuxiliaryPortHandler(null);
        _aux.Register(_emulator.IoBus);

        // Wire up emulator events
        _emulator.Started += () => Dispatcher.UIThread.Post(() => StatusText = "Running");
        _emulator.Stopped += () => Dispatcher.UIThread.Post(() => StatusText = "Stopped");
        _emulator.Error += ex => Dispatcher.UIThread.Post(() => StatusText = $"Error: {ex.Message}");

        // Auto-boot
        AutoBoot();
    }

    private void AutoBoot()
    {
        try
        {
            // Try to extract and mount bundled LOLOS disk
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "Heh8080.Desktop.Assets.disks.lolos.dsk";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                // Extract to temp file
                _extractedDiskPath = Path.Combine(Path.GetTempPath(), "heh8080_lolos.dsk");
                using (var fs = File.Create(_extractedDiskPath))
                {
                    stream.CopyTo(fs);
                }

                // Mount to drive A:
                _diskProvider.Mount(0, _extractedDiskPath, readOnly: false);
                DriveAPath = "lolos.img";

                // Load boot sector (track 0, sector 1) to 0x0000
                Span<byte> bootSector = stackalloc byte[128];
                if (_diskProvider.ReadSector(0, 0, 1, bootSector))
                {
                    _emulator.Load(0x0000, bootSector);
                    _emulator.Cpu.PC = 0x0000;
                    _emulator.Cpu.SP = 0xFFFF;

                    // Start the interrupt timer and emulator
                    StartInterruptTimer();
                    _emulator.Start();
                    StatusText = "LOLOS booting...";
                    return;
                }
            }

            // No bundled disk or boot failed - show welcome message
            StatusText = "Ready - mount a disk to begin";
            WriteToTerminal("heh8080 - Intel 8080 Emulator\r\n");
            WriteToTerminal("FJM-3A Terminal Ready\r\n");
            WriteToTerminal("\r\nNo boot disk found. Click the FJM-3A logo\r\n");
            WriteToTerminal("to mount a disk image.\r\n");
        }
        catch (Exception ex)
        {
            StatusText = $"Boot failed: {ex.Message}";
            WriteToTerminal($"Boot error: {ex.Message}\r\n");
        }
    }

    private void WriteToTerminal(string text)
    {
        foreach (char c in text)
            _terminal.WriteChar((byte)c);
    }

    private void StartInterruptTimer()
    {
        _interruptTimer = new System.Threading.Timer(
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
            _emulator.Cpu.Interrupt(7); // RST 7 for timer
        }
    }

    private void OnDelayRequested(int units)
    {
        // Delay n × 10ms - just sleep on the CPU thread
        Thread.Sleep(units * 10);
    }

    private void OnResetRequested()
    {
        Dispatcher.UIThread.Post(async () => await Reset());
    }

    private void OnHaltRequested()
    {
        Dispatcher.UIThread.Post(async () =>
        {
            await _emulator.StopAsync();
            StopInterruptTimer();
            StatusText = "Halted";
        });
    }

    [RelayCommand]
    private async Task Reset()
    {
        await _emulator.StopAsync();
        StopInterruptTimer();
        _emulator.Reset();
        _terminal.Buffer.Clear();

        // Reload boot sector if disk is mounted
        if (_diskProvider.IsMounted(0))
        {
            Span<byte> bootSector = stackalloc byte[128];
            if (_diskProvider.ReadSector(0, 0, 1, bootSector))
            {
                _emulator.Load(0x0000, bootSector);
                _emulator.Cpu.PC = 0x0000;
                _emulator.Cpu.SP = 0xFFFF;
                StartInterruptTimer();
                _emulator.Start();
                StatusText = "Reset - rebooting...";
                return;
            }
        }

        StatusText = "Reset";
    }

    [RelayCommand]
    private async Task MountDisk(int drive)
    {
        // This will be called from ConfigDialog with TopLevel reference
        // For now, just update status - actual file picker is in ConfigDialog
        StatusText = $"Mount disk to {(char)('A' + drive)}:";
    }

    public async Task MountDiskFromPath(int drive, string path)
    {
        try
        {
            bool wasRunning = _emulator.IsRunning;
            if (wasRunning)
            {
                await _emulator.StopAsync();
                StopInterruptTimer();
            }

            _diskProvider.Mount(drive, path, readOnly: false);
            UpdateDriveStatus(drive, Path.GetFileName(path));
            StatusText = $"Mounted {Path.GetFileName(path)} to {(char)('A' + drive)}:";

            // If mounting to A: and not running, boot from it
            if (drive == 0 && !wasRunning)
            {
                Span<byte> bootSector = stackalloc byte[128];
                if (_diskProvider.ReadSector(0, 0, 1, bootSector))
                {
                    _emulator.Reset();
                    _terminal.Buffer.Clear();
                    _emulator.Load(0x0000, bootSector);
                    _emulator.Cpu.PC = 0x0000;
                    _emulator.Cpu.SP = 0xFFFF;
                    StartInterruptTimer();
                    _emulator.Start();
                    StatusText = "Booting...";
                }
            }
            else if (wasRunning)
            {
                StartInterruptTimer();
                _emulator.Start();
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Mount failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void UnmountDisk(int drive)
    {
        _diskProvider.Unmount(drive);
        UpdateDriveStatus(drive, null);
        StatusText = $"Unmounted {(char)('A' + drive)}:";
    }

    private void UpdateDriveStatus(int drive, string? path)
    {
        switch (drive)
        {
            case 0: DriveAPath = path; break;
            case 1: DriveBPath = path; break;
            case 2: DriveCPath = path; break;
            case 3: DriveDPath = path; break;
        }
    }

    public void Dispose()
    {
        StopInterruptTimer();
        _emulator.StopAsync().Wait(TimeSpan.FromSeconds(1));
        _emulator.Dispose();
        _diskProvider.Dispose();

        // Clean up temp file
        if (_extractedDiskPath != null && File.Exists(_extractedDiskPath))
        {
            try { File.Delete(_extractedDiskPath); }
            catch { /* Ignore cleanup errors */ }
        }
    }
}

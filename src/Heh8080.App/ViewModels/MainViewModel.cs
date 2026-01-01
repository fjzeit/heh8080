using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Heh8080.Core;
using Heh8080.Devices;
using Heh8080.Terminal;

namespace Heh8080.UI.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private Emulator _emulator;
    private readonly IDiskImageProvider _diskProvider;
    private readonly Adm3aTerminal _terminal;
    private CpuType _cpuType = CpuType.ZilogZ80;

    // Devices
    private readonly ConsolePortHandler _console;
    private FloppyDiskController _fdc;
    private MemoryManagementUnit _mmu;
    private readonly TimerDevice _timer;
    private readonly DelayDevice _delay;
    private readonly HardwareControlDevice _hwControl;
    private readonly PrinterPortHandler _printer;
    private readonly AuxiliaryPortHandler _aux;

    // Timer for 10ms interrupts
    private System.Threading.Timer? _interruptTimer;

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
    public IDiskImageProvider DiskProvider => _diskProvider;
    public bool IsRunning => _emulator.IsRunning;
    public CpuType CpuType => _cpuType;
    public string CpuTypeName => _cpuType == CpuType.ZilogZ80 ? "Z80" : "8080";

    public MainViewModel(IDiskImageProvider diskProvider)
    {
        _diskProvider = diskProvider;

        // Initialize emulator with Z80 by default
        _emulator = new Emulator(_cpuType);
        _terminal = new Adm3aTerminal();

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
    }

    /// <summary>
    /// Boot from the disk mounted on drive A:
    /// </summary>
    public void Boot()
    {
        try
        {
            if (!_diskProvider.IsMounted(0))
            {
                StatusText = "Ready - mount a disk to begin";
                WriteToTerminal("heh8080 - Intel 8080 Emulator\r\n");
                WriteToTerminal("FJM-3A Terminal Ready\r\n");
                WriteToTerminal("\r\nNo boot disk found. Click the FJM-3A logo\r\n");
                WriteToTerminal("to mount a disk image.\r\n");
                return;
            }

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
                StatusText = "Booting...";
                return;
            }

            StatusText = "Boot failed - could not read boot sector";
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
        // Delay n x 10ms - just sleep on the CPU thread
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
    public async Task Reset()
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

    public async Task SwitchCpuType(CpuType newType)
    {
        if (newType == _cpuType) return;

        await _emulator.StopAsync();
        StopInterruptTimer();
        _emulator.Dispose();

        _cpuType = newType;
        _emulator = new Emulator(_cpuType);

        // Re-register all devices
        _console.Register(_emulator.IoBus);
        _fdc = new FloppyDiskController(_diskProvider, _emulator.Memory);
        _fdc.Register(_emulator.IoBus);
        _mmu = new MemoryManagementUnit(_emulator.Memory);
        _mmu.Register(_emulator.IoBus);
        _timer.Register(_emulator.IoBus);
        _delay.Register(_emulator.IoBus);
        _hwControl.Register(_emulator.IoBus);
        _printer.Register(_emulator.IoBus);
        _aux.Register(_emulator.IoBus);

        // Wire up emulator events
        _emulator.Started += () => Dispatcher.UIThread.Post(() => StatusText = "Running");
        _emulator.Stopped += () => Dispatcher.UIThread.Post(() => StatusText = "Stopped");
        _emulator.Error += ex => Dispatcher.UIThread.Post(() => StatusText = $"Error: {ex.Message}");

        _terminal.Buffer.Clear();

        // Reboot if disk is mounted
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
                StatusText = $"Switched to {CpuTypeName} - rebooting...";
                return;
            }
        }

        StatusText = $"Switched to {CpuTypeName}";
    }

    public void UpdateDriveStatus(int drive, string? path)
    {
        switch (drive)
        {
            case 0: DriveAPath = path; break;
            case 1: DriveBPath = path; break;
            case 2: DriveCPath = path; break;
            case 3: DriveDPath = path; break;
        }
    }

    public async Task MountAndBoot(int drive)
    {
        bool wasRunning = _emulator.IsRunning;
        if (wasRunning)
        {
            await _emulator.StopAsync();
            StopInterruptTimer();
        }

        // If mounting to A: and not running, boot from it
        if (drive == 0 && !wasRunning && _diskProvider.IsMounted(0))
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
                return;
            }
        }

        if (wasRunning)
        {
            StartInterruptTimer();
            _emulator.Start();
        }
    }

    [RelayCommand]
    public void UnmountDisk(int drive)
    {
        _diskProvider.Unmount(drive);
        UpdateDriveStatus(drive, null);
        StatusText = $"Unmounted {(char)('A' + drive)}:";
    }

    public void Dispose()
    {
        StopInterruptTimer();
        _emulator.StopAsync().Wait(TimeSpan.FromSeconds(1));
        _emulator.Dispose();

        if (_diskProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

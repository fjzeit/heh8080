# Desktop Application Architecture

The Avalonia desktop application orchestrates the 8080 emulator with the FJM-3A terminal display.

## Component Diagram

```mermaid
graph TB
    subgraph Desktop["Heh8080.Desktop"]
        App[App.axaml.cs]
        MW[MainWindow]
        MV[MainView]
        VM[MainViewModel]
        RTC[RetroTerminalControl]
        CD[ConfigDialog]
    end

    subgraph Core["Heh8080.Core"]
        EMU[Emulator]
        CPU[Cpu8080]
        MEM[Memory]
        BUS[IoBus]
    end

    subgraph Devices["Heh8080.Devices"]
        CON[ConsolePortHandler]
        FDC[FloppyDiskController]
        MMU[MemoryManagementUnit]
        TIM[TimerDevice]
        DEL[DelayDevice]
        HWC[HardwareControlDevice]
    end

    subgraph Terminal["Heh8080.Terminal"]
        ADM[Adm3aTerminal]
        BUF[TerminalBuffer]
    end

    App --> MW
    MW --> MV
    MV --> RTC
    MV --> VM
    RTC -->|LogoClicked| CD

    VM --> EMU
    VM --> ADM
    VM -->|10ms tick| TIM

    EMU --> CPU
    EMU --> MEM
    EMU --> BUS

    CON --> ADM
    FDC --> MEM
    MMU --> MEM

    BUS --> CON
    BUS --> FDC
    BUS --> MMU
    BUS --> TIM
    BUS --> DEL
    BUS --> HWC

    ADM --> BUF
    RTC -->|renders| BUF
```

## Key Files

| File | Purpose |
|------|---------|
| `Emulator.cs` | CPU execution loop on background thread |
| `MainViewModel.cs` | Device wiring, auto-boot, commands |
| `MainView.axaml.cs` | Terminal wiring, config dialog trigger |
| `RetroTerminalControl.cs` | CRT shader, FJM-3A logo button |
| `ConfigDialog.axaml` | Disk mount/unmount, reset |

## Threading Model

```
┌─────────────────┐     ┌─────────────────┐
│   UI Thread     │     │  CPU Thread     │
├─────────────────┤     ├─────────────────┤
│ Avalonia render │     │ Cpu.Step() loop │
│ Input handling  │◄────│ ContentChanged  │
│ Dialog display  │────►│ Start/Stop      │
└─────────────────┘     └─────────────────┘
         ▲
         │ Dispatcher.UIThread.Post()
         │
┌─────────────────┐
│  Timer Thread   │
├─────────────────┤
│ 10ms tick       │
│ → TimerDevice   │
│ → Cpu.Interrupt │
└─────────────────┘
```

## Auto-Boot Sequence

1. Extract bundled `lolos.dsk` from embedded resources to temp file
2. Mount to drive A: via `FileDiskImageProvider`
3. Read boot sector (track 0, sector 1, 128 bytes)
4. Load boot sector to memory at 0x0000
5. Set PC=0x0000, SP=0xFFFF
6. Start 10ms interrupt timer
7. Start Emulator (CPU thread)

## Device Registration

All devices implement `Register(IoBus bus)` pattern:

```csharp
_console = new ConsolePortHandler(_terminal);
_console.Register(_emulator.IoBus);           // Ports 0-1

_fdc = new FloppyDiskController(_diskProvider, _emulator.Memory);
_fdc.Register(_emulator.IoBus);               // Ports 10-17

_mmu = new MemoryManagementUnit(_emulator.Memory);
_mmu.Register(_emulator.IoBus);               // Ports 20-23

_timer = new TimerDevice();
_timer.Register(_emulator.IoBus);             // Port 27
_timer.SetInterruptCallback(OnTimerInterrupt);

_delay = new DelayDevice();
_delay.Register(_emulator.IoBus);             // Port 28

_hwControl = new HardwareControlDevice();
_hwControl.Register(_emulator.IoBus);         // Port 160
_hwControl.SetResetCallback(OnResetRequested);
_hwControl.SetHaltCallback(OnHaltRequested);
```

## UI Design

Minimal chrome - just the FJM-3A terminal with logo button:

- **Logo button**: Top-left bezel corner, "FJM-3A" text
- **Click logo**: Opens ConfigDialog modal
- **ConfigDialog**: Disk mount/unmount for A:-D:, Reset button
- No Start/Stop - emulator runs continuously from launch

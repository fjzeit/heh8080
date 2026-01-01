# heh8080 - Cross-Platform 8080 Emulator for LOLOS

## Goal
Create an 8080 CPU emulator that runs LOLOS (CP/M 2.2 compatible OS) targeting:
- Desktop apps on Windows, Mac, Linux (with NativeAOT)
- Web browser via Avalonia WASM

## Technology Stack
- **.NET 10** (latest)
- **Avalonia UI 11.x** (latest stable, MIT licensed)
- **NativeAOT** for desktop builds
- **WASM** for browser builds

## User Requirements
- **UI Framework**: Avalonia UI (single codebase for desktop + web)
- **Speed**: Maximum speed (no cycle throttling)
- **Features**: Full CP/M I/O support (MMU, printer, aux, network)
- **UI Style**: Custom retro terminal on ALL platforms (green phosphor, CRT effects)
- **Disk handling**: Bundle LOLOS disk + platform-appropriate persistence
- **Validation**: Use standard 8080 CPU test suites

## 8080 CPU Test Suites

Standard test files from [altairclone.com](https://altairclone.com/downloads/cpu_tests/) and [superzazu/8080](https://github.com/superzazu/8080):

| Test | Purpose | Duration |
|------|---------|----------|
| **TST8080.COM** | Basic diagnostic (Microcosm Associates) | ~651 instructions, instant |
| **8080PRE.COM** | Preliminary tests, quick sanity check | Seconds |
| **CPUTEST.COM** | General CPU testing (Supersoft) | Minutes |
| **8080EXM.COM** | Comprehensive exerciser (CRC-based) | 3+ hours at 2MHz |

### Test Strategy
1. **During development**: Run TST8080.COM and 8080PRE.COM after each opcode group
2. **Before release**: Pass CPUTEST.COM completely
3. **Full validation**: Run 8080EXM.COM (comprehensive, tests all flags/edge cases)
4. **Integration test**: Boot LOLOS and run its test suite

### Test Harness
Implement minimal CP/M BDOS emulation for tests:
- BDOS function 2: Output character (C register to console)
- BDOS function 9: Output string (DE points to $-terminated string)
- Load COM file at 0x0100, set PC=0x0100
- Trap CALL 0x0005 for BDOS, RET from 0x0000 for exit

## Architecture

```
heh8080/
├── src/
│   ├── Heh8080.Core/           # CPU, memory, I/O bus (.NET 10 library)
│   ├── Heh8080.Devices/        # FDC, console, printer, MMU, network
│   ├── Heh8080.Terminal/       # FJM-3A terminal emulator
│   ├── Heh8080.Desktop/        # Desktop app with Avalonia UI (NativeAOT)
│   │   ├── Controls/
│   │   │   └── RetroTerminalControl.cs  # CRT shader effects
│   │   └── Views/
│   └── Heh8080.Browser/        # WASM entry point (Phase 7)
├── tests/
│   ├── Heh8080.Tests/          # Unit tests (62 total)
│   └── cpu_tests/              # Standard 8080 test COM files
├── assets/
│   └── disks/                  # Bundled LOLOS disk image
└── lode/                       # Project knowledge base
```

```
┌─────────────────────────────────────────────────────────────┐
│                   Platform Entry Points                      │
├─────────────────────────────┬───────────────────────────────┤
│   Heh8080.Desktop           │   Heh8080.Browser             │
│   - NativeAOT publish       │   - WASM publish              │
│   - File-based disk I/O     │   - IndexedDB disk storage    │
└─────────────────────────────┴───────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    Heh8080.App (Avalonia 11.x)               │
│   - RetroTerminal control (ALL platforms)                   │
│     • Green phosphor text (#33FF33 on #0A1A0A)             │
│     • CRT curvature effect (barrel distortion)             │
│     • Scanline overlay                                      │
│     • Phosphor glow/bloom                                   │
│   - MainViewModel (emulator orchestration)                  │
│   - Disk management UI                                      │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    Heh8080.Devices                           │
│   - IConsoleDevice          - IFloppyDiskController         │
│   - IPrinterDevice          - IAuxiliaryDevice              │
│   - IMemoryManagementUnit   - INetworkDevice                │
│   - IDiskImageProvider (file vs IndexedDB)                  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                     Heh8080.Core                             │
│   - Cpu8080 (registers, flags, 256 opcodes)                 │
│   - Memory (64KB + banked memory support)                   │
│   - IoBus (port 0-255 dispatch)                             │
│   - Emulator (run loop, device orchestration)               │
└─────────────────────────────────────────────────────────────┘
```

## I/O Port Map

| Port | Device | Function |
|------|--------|----------|
| 0-1 | Console | Status/data |
| 2-3 | Printer | Status/data |
| 4-5 | Auxiliary | Status/data (pipes) |
| 10-17 | FDC | Drive/track/sector/cmd/status/DMA |
| 20-23 | MMU | Memory management unit |
| 25-26 | RTC | Real-time clock |
| 27 | Timer | 10ms interrupt timer |
| 28 | Delay | Busy-wait detection |
| 30-31 | CPU Speed | Speed control |
| 40-51 | Network | TCP/IP sockets |
| 160 | Hardware | System control |
| 161 | BDOS Hook | Host file I/O |

## Key Design Decisions

1. **Framework**: .NET 10 + Avalonia 11.x
   - Latest .NET with best NativeAOT and WASM support
   - Avalonia 11.x is stable, MIT licensed, works with .NET 10

2. **UI Framework**: Avalonia UI
   - Single XAML-based UI for desktop and web
   - MVVM pattern with CommunityToolkit.Mvvm
   - NativeAOT compatible for desktop
   - WASM build for browser

3. **Retro Terminal** (ALL PLATFORMS):
   - FJM-3A terminal emulator (ADM-3A compatible escape sequences)
   - SKSL shader for CRT effects: barrel distortion, bloom, scanlines, vignette
   - 4:3 aspect ratio with light gray housing, anti-aliased edges
   - Green phosphor color scheme (#33FF33 on #0A140A)

4. **CPU**: Switch-based opcode dispatch
   - 256 opcodes, all 8080 flags (S, Z, AC, P, CY)
   - No T-state throttling (max speed)

5. **Memory**: 64KB base + banked segments
   - Bank 0 always present, up to 16 additional banks
   - Configurable segment size (default 48KB)

6. **Disk Images**: Standard CP/M format
   - Flat binary, 256KB each (77×26×128)
   - Desktop: File-based I/O
   - Browser: IndexedDB via JS interop

## Implementation Plan

### Phase 1: Project Setup
1. Create .NET 10 solution with Avalonia 11.x template
2. Set up project structure (Core, Devices, App, Desktop, Browser)
3. Configure NativeAOT for Desktop project
4. Configure WASM build for Browser project
5. Download CPU test files manually (see `tests/cpu_tests/README.md`), set `HEH8080_CPU_TESTS` env var

### Phase 2: CPU Implementation
1. Implement Cpu8080 class (registers, flags)
2. Implement all 256 opcodes (group by instruction type)
3. Implement Memory class (64KB + banking)
4. Implement IoBus with port dispatch
5. Create test harness with minimal BDOS (functions 2, 9)
6. **Validate with TST8080.COM after each opcode group**
7. **Pass 8080PRE.COM before proceeding**

### Phase 3: CPU Validation
1. **Run CPUTEST.COM** - fix any failures
2. **Run 8080EXM.COM** - comprehensive validation
3. Document any known deviations (undocumented opcodes, etc.)

### Phase 4: Device Layer
1. Define device interfaces (IConsoleDevice, IFloppyDiskController, etc.)
2. Implement FDC with disk image I/O
3. Implement console device abstraction
4. Implement MMU for banked memory
5. Implement printer/auxiliary stubs
6. Implement IDiskImageProvider (file-based + IndexedDB)

### Phase 5: Retro Terminal Control ✓
1. ✓ Created Heh8080.Terminal project with FJM-3A emulator
   - TerminalBuffer (80×24 character grid)
   - Adm3aParser (escape sequence state machine)
   - Adm3aTerminal (implements IConsoleDevice)
2. ✓ Created RetroTerminalControl with SKSL shader:
   - Green phosphor (#33FF33 on #0A140A)
   - Barrel distortion (`1.0 + 0.3*r² + 0.2*r⁴`)
   - 8-tap bloom, scanlines, vignette
   - Edge shadow (housing overlap effect)
   - Anti-aliased screen boundary
3. ✓ 4:3 aspect ratio with light gray housing
4. ✓ Keyboard input handling (ADM-3A cursor codes)
5. ✓ 18 parser unit tests

### Phase 6: Avalonia Application ✓
1. ✓ Created Emulator class (background thread CPU execution, max speed)
2. ✓ MainViewModel wires all devices to I/O bus:
   - ConsolePortHandler → Adm3aTerminal
   - FloppyDiskController → FileDiskImageProvider
   - MemoryManagementUnit, TimerDevice, DelayDevice, HardwareControlDevice
   - PrinterPortHandler (NullPrinterDevice), AuxiliaryPortHandler (null)
3. ✓ FJM-3A logo button in terminal bezel opens ConfigDialog
4. ✓ ConfigDialog: disk mount/unmount (A:-D:), reset, file picker
5. ✓ Auto-boot: extracts bundled lolos.dsk, mounts to A:, boots
6. ✓ 10ms timer interrupt via System.Threading.Timer → Cpu.Interrupt(7)
7. ✓ Proper lifecycle cleanup on shutdown
8. ✓ Idle detection in ConsolePortHandler (Thread.Sleep after 100 polls)
9. ✓ **Verified**: MBASIC 5.29 (24KB, multi-extent) loads and runs correctly

### Phase 7: Platform Integration
1. Browser: IndexedDB disk storage via JS interop
2. Desktop NativeAOT publishing and testing
3. Test on all desktop platforms (Win/Mac/Linux)

### Phase 8: Integration Testing
1. Boot LOLOS successfully
2. Run LOLOS automated test suite (27 tests)
3. Test with MBASIC, BBC BASIC, Colossal Cave
4. Performance profiling and optimization
5. Cross-platform testing (Win/Mac/Linux/Web)

## Critical Files

### Core
- `src/Heh8080.Core/Cpu8080.cs` - CPU emulation
- `src/Heh8080.Core/Memory.cs` - Memory subsystem
- `src/Heh8080.Core/IoBus.cs` - I/O port dispatch
- `src/Heh8080.Core/Emulator.cs` - Main orchestrator

### Devices
- `src/Heh8080.Devices/FloppyDiskController.cs` - FDC
- `src/Heh8080.Devices/IConsoleDevice.cs` - Console interface
- `src/Heh8080.Devices/IDiskImageProvider.cs` - Disk storage abstraction

### Terminal
- `src/Heh8080.Terminal/TerminalBuffer.cs` - 80×24 character grid
- `src/Heh8080.Terminal/Adm3aParser.cs` - Escape sequence parser
- `src/Heh8080.Terminal/Adm3aTerminal.cs` - IConsoleDevice implementation

### Avalonia App
- `src/Heh8080.Desktop/Controls/RetroTerminalControl.cs` - CRT terminal with SKSL shader
- `src/Heh8080.Desktop/Views/MainWindow.axaml` - Main window
- `src/Heh8080.Desktop/Views/MainView.axaml.cs` - Terminal wiring

### Entry Points
- `src/Heh8080.Desktop/Program.cs` - Desktop entry (NativeAOT)
- `src/Heh8080.Browser/Program.cs` - WASM entry

### Tests
- `tests/cpu_tests/TST8080.COM` - Basic diagnostic
- `tests/cpu_tests/8080PRE.COM` - Preliminary tests
- `tests/cpu_tests/CPUTEST.COM` - General CPU testing
- `tests/cpu_tests/8080EXM.COM` - Comprehensive exerciser

## Acknowledgments

### 8080 CPU Test Suites
- TST8080.COM - Microcosm Associates 8080/8085 CPU Diagnostic v1.0, Copyright (C) 1980
- 8080EXM.COM - Based on zexlax.z80 by Frank D. Cringle, Copyright (C) 1994
  - Modified for 8080 by Ian Bartholomew
  - Further modified by Mike Douglas

## References
- I/O port specification: `lode/devices/io-ports.md`
- LOLOS: https://github.com/fjzeit/lolos
- CPU tests: https://altairclone.com/downloads/cpu_tests/
- Avalonia docs: https://docs.avaloniaui.net/

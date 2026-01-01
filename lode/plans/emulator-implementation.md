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
- **Features**: Full cpmsim compatibility (MMU, printer, aux, network)
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
│   ├── Heh8080.App/            # Avalonia UI (desktop + WASM)
│   │   ├── Controls/
│   │   │   └── RetroTerminal.cs  # Custom control with CRT effects
│   │   ├── Views/
│   │   │   └── MainWindow.axaml
│   │   └── ViewModels/
│   ├── Heh8080.Desktop/        # Desktop entry point (NativeAOT)
│   └── Heh8080.Browser/        # WASM entry point
├── tests/
│   ├── Heh8080.Tests/          # Unit tests
│   └── cpu_tests/              # Standard 8080 test COM files
│       ├── TST8080.COM
│       ├── 8080PRE.COM
│       ├── CPUTEST.COM
│       └── 8080EXM.COM
├── assets/
│   └── disks/                  # Bundled LOLOS disk image
└── lode/
    └── plans/                  # This plan
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

## cpmsim I/O Port Map (Full Compatibility)

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
   - Custom Avalonia control using SkiaSharp rendering
   - Green phosphor color scheme (#33FF33 on #0A1A0A)
   - CRT barrel distortion effect
   - Horizontal scanlines overlay
   - Phosphor bloom/glow effect
   - Optional: flicker simulation, screen curvature

4. **CPU**: Switch-based opcode dispatch
   - 256 opcodes, all 8080 flags (S, Z, AC, P, CY)
   - No T-state throttling (max speed)

5. **Memory**: 64KB base + banked segments
   - Bank 0 always present, up to 16 additional banks
   - Configurable segment size (default 48KB)

6. **Disk Images**: z80pack compatible
   - Flat binary, 256KB each (77×26×128)
   - Desktop: File-based I/O
   - Browser: IndexedDB via JS interop

## Implementation Plan

### Phase 1: Project Setup
1. Create .NET 10 solution with Avalonia 11.x template
2. Set up project structure (Core, Devices, App, Desktop, Browser)
3. Configure NativeAOT for Desktop project
4. Configure WASM build for Browser project
5. Download CPU test files to `tests/cpu_tests/`

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

### Phase 5: Retro Terminal Control
1. Create RetroTerminal custom control using SkiaSharp
   - Character buffer with fixed-width font
   - Green phosphor color palette
   - Render to SKCanvas
2. Add CRT effects layer:
   - Barrel distortion shader
   - Scanline overlay
   - Bloom/glow post-processing
3. Keyboard input handling (focus, key events)
4. Performance optimization (dirty region tracking)

### Phase 6: Avalonia Application
1. Create MainWindow with RetroTerminal
2. Add toolbar/menu for disk operations
3. Wire up ViewModel to emulator
4. Implement disk image loading UI
5. Add settings for CRT effects intensity

### Phase 7: Platform Integration
1. Desktop: File dialogs, NativeAOT publishing
2. Browser: IndexedDB disk storage via JS interop
3. Bundle LOLOS disk image as embedded resource
4. Test on all platforms

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

### Avalonia App
- `src/Heh8080.App/Controls/RetroTerminal.cs` - CRT terminal control
- `src/Heh8080.App/Views/MainWindow.axaml` - Main window
- `src/Heh8080.App/ViewModels/MainViewModel.cs` - App logic

### Entry Points
- `src/Heh8080.Desktop/Program.cs` - Desktop entry (NativeAOT)
- `src/Heh8080.Browser/Program.cs` - WASM entry

### Tests
- `tests/cpu_tests/TST8080.COM` - Basic diagnostic
- `tests/cpu_tests/8080PRE.COM` - Preliminary tests
- `tests/cpu_tests/CPUTEST.COM` - General CPU testing
- `tests/cpu_tests/8080EXM.COM` - Comprehensive exerciser

## Acknowledgments

### z80pack
This project uses z80pack as a reference implementation for:
- I/O port mapping (cpmsim device architecture)
- Memory banking (MMU implementation)
- Disk image format compatibility

**z80pack** - Copyright (c) 1987-2025 Udo Munk and others
- License: MIT
- Repository: https://github.com/udo-munk/z80pack
- Documentation: https://www.icl1900.co.uk/unix4fun/z80pack

The I/O port assignments and device behavior in heh8080 are designed to be
compatible with z80pack's cpmsim, allowing LOLOS and other CP/M systems
developed for cpmsim to run unmodified.

### 8080 CPU Test Suites
- TST8080.COM - Microcosm Associates 8080/8085 CPU Diagnostic v1.0, Copyright (C) 1980
- 8080EXM.COM - Based on zexlax.z80 by Frank D. Cringle, Copyright (C) 1994
  - Modified for 8080 by Ian Bartholomew
  - Further modified by Mike Douglas

## References
- LOLOS source: `../lolos/src/` (BIOS at `bios.asm`)
- z80pack cpmsim: `../z80pack/cpmsim/srcsim/` (simio.c for port map)
- z80pack CPU: `../z80pack/z80core/sim8080.c` (reference implementation)
- CPU tests: https://altairclone.com/downloads/cpu_tests/
- superzazu/8080: https://github.com/superzazu/8080
- Avalonia docs: https://docs.avaloniaui.net/
- Avalonia releases: https://github.com/AvaloniaUI/Avalonia/releases

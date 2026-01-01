# heh8080

Cross-platform Intel 8080 CPU emulator designed to run LOLOS (a CP/M 2.2 compatible OS).

## Technology
- .NET 10, Avalonia UI 11.x
- Targets: Windows, Mac, Linux (NativeAOT), Web (WASM)
- MIT licensed

## Key Features
- CP/M compatible I/O ports (console, FDC, MMU, etc.)
- Retro CRT terminal aesthetic on all platforms
- Supports standard 8080 CPU test suites

## Related Projects
- **LOLOS**: https://github.com/fjzeit/lolos - CP/M 2.2 compatible OS in pure 8080 assembly

## Project Structure
```
src/
  Heh8080.Core/      # CPU, memory, I/O bus
  Heh8080.Devices/   # FDC, console, MMU, printer, auxiliary, timer
  Heh8080.Terminal/  # FJM-3A terminal emulator (IConsoleDevice)
  Heh8080.Desktop/   # Desktop app with Avalonia UI (NativeAOT)
  Heh8080.Browser/   # WASM entry point (stub, Phase 7)
tests/
  Heh8080.Tests/     # Unit tests (62 total)
  cpu_tests/         # External test suites (TST8080, 8080PRE, CPUTEST, 8080EXM)
```

## Current Status
Phases 1-6 complete. Emulator runs LOLOS on desktop.

### Completed
- **Phase 1**: Solution skeleton with all projects
- **Phase 2**: CPU implementation (all 256 opcodes, memory, I/O bus)
- **Phase 3**: CPU validation - all tests pass:
  - TST8080.COM: "CPU IS OPERATIONAL"
  - 8080PRE.COM: "8080 Preliminary tests complete"
  - CPUTEST.COM: "CPU TESTS OK"
  - 8080EXM.COM: Skipped (hours to run)
- **Phase 4**: Device layer complete:
  - ConsolePortHandler (ports 0-1)
  - PrinterPortHandler (ports 2-3)
  - AuxiliaryPortHandler (ports 4-5)
  - FloppyDiskController (ports 10-17)
  - MemoryManagementUnit (ports 20-23)
  - TimerDevice (port 27)
  - DelayDevice (port 28)
  - HardwareControlDevice (port 160)
  - FileDiskImageProvider for desktop
- **Phase 5**: FJM-3A terminal emulator with CRT effects:
  - Heh8080.Terminal project (TerminalBuffer, Adm3aParser, Adm3aTerminal)
  - RetroTerminalControl with SKSL shader: barrel distortion, bloom, scanlines, vignette
  - 4:3 aspect ratio, light gray housing with 3D shading, anti-aliased edges
  - ADM-3A compatible input: printable ASCII + control codes only
  - Supports ESC sequences for WordStar compatibility (ESC T, ESC Y)
  - 18 parser unit tests
- **Phase 6**: Avalonia application:
  - Emulator class orchestrates CPU on background thread (max speed)
  - MainViewModel wires devices, handles auto-boot from bundled LOLOS disk
  - FJM-3A logo button opens ConfigDialog for disk mount/unmount/reset
  - 10ms timer interrupt via System.Threading.Timer
  - Bundled lolos.dsk as embedded resource
  - Idle detection in ConsolePortHandler reduces CPU usage when waiting for input
  - **Verified working**: MBASIC (24KB multi-extent file) runs correctly

### Next
- **Phase 7**: Platform integration (Browser WASM with IndexedDB disk storage)
- **Phase 8**: LOLOS integration testing

## Build Notes
- All projects target `net10.0`
- Desktop uses NativeAOT - all code must be AoT-compatible
- Browser stub for Phase 7 (will need multi-targeted Core/Devices)
- Central package management via `Directory.Packages.props`

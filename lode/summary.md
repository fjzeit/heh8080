# heh8080

Cross-platform Intel 8080 / Zilog Z80 CPU emulator designed to run LOLOS (a CP/M 2.2 compatible OS) and legacy Z80 software.

## Technology
- .NET 10, Avalonia UI 11.x
- Targets: Windows, Mac, Linux (NativeAOT), Web (WASM)
- MIT licensed

## Key Features
- Dual CPU support: Intel 8080 and Zilog Z80
- CP/M compatible I/O ports (console, FDC, MMU, etc.)
- Retro CRT terminal aesthetic on all platforms
- Responsive scaling (100%, 75%, 50%, 25%) with platform-aware defaults
- Supports standard 8080 CPU test suites
- MCP (Model Context Protocol) server for AI-assisted development

## Related Projects
- **LOLOS**: https://github.com/fjzeit/lolos - CP/M 2.2 compatible OS in pure 8080 assembly

## Project Structure
```
src/
  Heh8080.Core/      # Cpu8080, CpuZ80, memory, I/O bus (net10.0;net9.0-browser)
  Heh8080.Devices/   # FDC, console, MMU, printer, auxiliary, timer (net10.0;net9.0-browser)
  Heh8080.Terminal/  # FJM-3A terminal emulator (net10.0;net9.0-browser)
  Heh8080.App/       # Shared UI library: ViewModels, Views, Controls (net10.0;net9.0-browser)
  Heh8080.Mcp/       # MCP server for AI-assisted development (net10.0)
  Heh8080.Desktop/   # Desktop entry point with Avalonia UI (net10.0, NativeAOT)
  Heh8080.Browser/   # Browser entry point with Avalonia WASM (net9.0-browser)
tests/
  Heh8080.Tests/     # Unit and integration tests (72 total)
  cpu_tests/         # External test suites (8080: TST8080, 8080PRE, CPUTEST, 8080EXM; Z80: ZEXDOC, ZEXALL)
```

## Current Status
All phases complete (1-8) plus Z80 support. Emulator runs LOLOS on desktop (net10.0 NativeAOT) and browser (net9.0 WASM).

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
- **Phase 5**: FJM-3A terminal emulator with authentic 1970s CRT effects:
  - Heh8080.Terminal project (TerminalBuffer, Adm3aParser, Adm3aTerminal)
  - RetroTerminalControl with SKSL shader: barrel distortion, bloom, scanlines, vignette
  - 4:3 aspect ratio with 3-layer housing design:
    - Outer housing: light gray (#B8B8B0) with 3D shading, 16px rounded corners
    - Inner bezel: medium gray (#505048) with inset shading, 44px rounded corners
    - CRT glass: superellipse boundary (n=7) for curved edges like real CRT tube
  - Shader returns transparent pixels outside curved boundary (inner bezel shows through)
  - 120px horizontal padding keeps text away from curved edges
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
- **Z80 Support**: Full Zilog Z80 CPU implementation:
  - CpuZ80 class with complete instruction set (~1,300 opcodes)
  - All prefix tables: CB (bit ops), ED (extended), DD/FD (IX/IY), DDCB/FDCB
  - Z80 registers: IX, IY, I, R, alternate set (AF', BC', DE', HL')
  - Interrupt modes 0/1/2
  - CPU type selection in ConfigDialog (Z80 default, 8080 available)
  - ZEXDOC/ZEXALL test suites integrated (first test passes)
- **Phase 7**: Platform integration complete:
  - Multi-targeted libraries: Core, Devices, Terminal, App (net10.0;net9.0-browser)
  - All libraries marked `<IsTrimmable>true</IsTrimmable>` and `<IsAotCompatible>true</IsAotCompatible>`
  - Shared UI library (Heh8080.App): MainViewModel, MainView, RetroTerminalControl
  - Browser entry point (Heh8080.Browser): Avalonia WASM with WebGL rendering
  - IndexedDB disk storage via JS interop (save/load disk images)
  - MemoryDiskImageProvider for in-browser disk operations
  - Base64 encoding for byte[] marshalling across JS/C# boundary
  - Viewport-filling CSS: `#out { position: fixed; inset: 0; }` with canvas 100% sizing
  - Desktop NativeAOT verified: 19MB native binary (linux-x64)
- **Phase 8**: Integration testing complete:
  - 72 automated tests (69 pass, 3 skipped long-running)
  - LOLOS boot verification on Z80 and 8080
  - LOLOS command execution tests (DIR, wildcards, error handling)
  - Desktop NativeAOT: 19MB native binary
  - Browser WASM: 39MB AppBundle
  - MBASIC verified working (24KB multi-extent file)

### Production Ready
The emulator is feature-complete and tested. Future enhancements could include:
- Additional test coverage from LOLOS test suite (requires building test programs)
- Performance optimization based on profiling
- Additional classic software testing (BBC BASIC, Colossal Cave, etc.)

## Build Notes
- Libraries target `net10.0;net9.0-browser` for cross-platform support
- Desktop (net10.0) uses NativeAOT - all code must be AoT-compatible
- Browser (net9.0-browser) uses WASM with `browser-wasm` RuntimeIdentifier
- Central package management via `Directory.Packages.props`
- Directory.Build.props removed TargetFramework to allow per-project targeting

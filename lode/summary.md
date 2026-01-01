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
  Heh8080.Devices/   # FDC, console, MMU, network
  Heh8080.App/       # Avalonia UI (shared)
  Heh8080.Desktop/   # NativeAOT entry point
  Heh8080.Browser/   # WASM entry point
tests/
  Heh8080.Tests/     # Unit tests
  cpu_tests/         # External test suite integration
```

## Current Status
Planning complete. Ready for Phase 1: Project Setup.

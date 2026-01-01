# Practices

## CPU Emulation
- Switch-based opcode dispatch for clarity and debuggability
- All 256 opcodes implemented, no undocumented instruction support
- Flag computation: S, Z, AC (auxiliary carry), P (parity), CY (carry)
- Maximum speed execution (no cycle throttling)

## Testing
- Validate CPU with standard test suites (TST8080, 8080PRE, CPUTEST, 8080EXM)
- Run TST8080/8080PRE after each opcode group during development
- Full 8080EXM pass required before release
- Integration test with LOLOS boot and test suite

## z80pack Compatibility
- Match cpmsim I/O port assignments exactly
- Use same disk image format (256KB flat binary, 77×26×128)
- Maintain BIOS/BDOS entry points for LOLOS compatibility

## Cross-Platform
- Shared core library (Heh8080.Core) for all platforms
- Platform-specific storage via IDiskImageProvider abstraction
- Single Avalonia UI codebase for desktop and web

## UI
- Retro CRT terminal on all platforms (not just web)
- Green phosphor color scheme (#33FF33 on #0A1A0A)
- CRT effects: scanlines, barrel distortion, phosphor bloom

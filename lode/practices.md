# Practices

## CPU Emulation
- **8080**: Switch-based opcode dispatch for clarity and debuggability
- **Z80**: Table-driven dispatch (Func<int>[] arrays) for prefix handling
- Both CPUs implement `ICpu` interface for polymorphic use
- Z80 is the default CPU; 8080 available via ConfigDialog
- All opcodes implemented including Z80 undocumented (SLL, X/Y flags)
- Flag computation: S, Z, H (half-carry), PV (parity/overflow), N (add/subtract), C (carry)
- Maximum speed execution (no cycle throttling)

## Testing
- Validate 8080 CPU with standard test suites (TST8080, 8080PRE, CPUTEST, 8080EXM)
- Validate Z80 CPU with ZEXDOC.COM and ZEXALL.COM exercisers
- Run TST8080/8080PRE after each opcode group during development
- Full 8080EXM pass required before release (8080 mode)
- Full ZEXALL pass required before release (Z80 mode)
- Integration test with LOLOS boot and test suite

## Licensing
- heh8080 is MIT licensed
- All dependencies must be MIT-compatible

## Third-Party Integration
- CPU test suites configured via HEH8080_CPU_TESTS env var
- Tests skip gracefully when external tools not available

## I/O Compatibility
- CP/M compatible I/O ports (see devices/io-ports.md)
- Disk format: 256KB flat binary (77 tracks × 26 sectors × 128 bytes)
- Maintain BIOS/BDOS entry points for LOLOS compatibility

## Cross-Platform
- Shared core library (Heh8080.Core) for all platforms
- Platform-specific storage via IDiskImageProvider abstraction
- Desktop has full UI; Browser is a separate implementation (Phase 7)

## NativeAOT
All code must be AoT-compatible:
- No reflection-based patterns (use source generators)
- Avoid APIs marked with `RequiresUnreferencedCode`
- Use compiled bindings in Avalonia (`AvaloniaUseCompiledBindingsByDefault`)
- Test with `dotnet publish -c Release` to catch trimming issues early

## UI
- Retro CRT terminal on all platforms (not just web)
- Green phosphor color scheme (#33FF33 on #0A1A0A)
- CRT effects: scanlines, barrel distortion, phosphor bloom

## LOLOS Disk Updates
To update the bundled LOLOS disk image from the latest build:
```bash
# Download latest artifact from fjzeit/lolos GitHub Actions
gh run download $(gh run list --repo fjzeit/lolos --limit 1 --json databaseId -q '.[0].databaseId') \
  --repo fjzeit/lolos --dir /tmp/lolos-build

# Copy to embedded asset location
cp /tmp/lolos-build/lolos-disk/drivea.dsk src/Heh8080.Desktop/Assets/disks/lolos.dsk
```

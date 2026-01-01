# 8080 CPU Implementation

## Overview

`Cpu8080` in `src/Heh8080.Core/Cpu8080.cs` implements the full Intel 8080 instruction set.

## Registers

```
8-bit: A (accumulator), B, C, D, E, H, L
16-bit pairs: BC, DE, HL (accessible as pairs or individual bytes)
16-bit: SP (stack pointer), PC (program counter)
```

## Flags

Stored as individual bools for performance:

| Flag | Bit | Description |
|------|-----|-------------|
| S | 7 | Sign (bit 7 of result) |
| Z | 6 | Zero (result is 0) |
| AC | 4 | Auxiliary carry (carry from bit 3) |
| P | 2 | Parity (even parity) |
| CY | 0 | Carry (overflow/borrow) |

The F register reconstructs flags on demand via `GetFlags()` / `SetFlags()`.

## Opcode Coverage

All 256 opcodes implemented:
- 0x00-0x3F: Data transfer, arithmetic, rotate, special
- 0x40-0x7F: MOV instructions (0x76 = HLT)
- 0x80-0xBF: Arithmetic and logic operations
- 0xC0-0xFF: Control flow, stack, I/O

Undocumented opcodes act as documented duplicates or NOPs.

## Key Methods

```csharp
cpu.Step()           // Execute one instruction
cpu.Reset()          // Reset all state
cpu.Interrupt(n)     // Trigger RST n (if enabled)
```

## Memory Interface

```csharp
public interface IMemory
{
    byte Read(ushort address);
    void Write(ushort address, byte value);
}
```

## I/O Interface

```csharp
public interface IIoBus
{
    byte In(byte port);
    void Out(byte port, byte value);
}
```

## Test Harness

`CpmTestHarness` provides minimal CP/M BDOS emulation for running CPU test suites:
- Function 2: Output character
- Function 9: Output $-terminated string
- Trap CALL 0x0005 for BDOS
- Exit on RET/JMP to 0x0000

## Example Usage

```csharp
var memory = new Memory();
var ioBus = new IoBus();
var cpu = new Cpu8080(memory, ioBus);

memory.Load(0x0100, programBytes);
cpu.PC = 0x0100;

while (!cpu.Halted)
{
    cpu.Step();
}
```

## Related Files

- `src/Heh8080.Core/Memory.cs` - 64KB memory with banking
- `src/Heh8080.Core/IoBus.cs` - I/O port dispatch
- `src/Heh8080.Core/CpmTestHarness.cs` - Test runner
- `tests/Heh8080.Tests/Cpu8080Tests.cs` - 41 unit tests

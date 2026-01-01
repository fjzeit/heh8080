# Phase 5: Retro Terminal Control

## Summary

Implement an ADM-3A terminal emulator with CRT visual effects for heh8080. Uses a simple, open-source terminal implementation (~850 lines) instead of external dependencies.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Heh8080.Desktop                                            │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  RetroTerminalControl                                 │  │
│  │  - Renders buffer with CRT effects                    │  │
│  │  - Green phosphor, scanlines, bloom                   │  │
│  │  - Keyboard input → Adm3aTerminal.QueueInput()        │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
        ↓ references
┌─────────────────────────────────────────────────────────────┐
│  Heh8080.Terminal (new project)                             │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Adm3aTerminal : IConsoleDevice                       │  │
│  │  - WriteChar(byte) → Adm3aParser                      │  │
│  │  - ReadChar() ← input queue                           │  │
│  │  - Buffer property → TerminalBuffer (80×24)           │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
        ↓ implements IConsoleDevice from
┌─────────────────────────────────────────────────────────────┐
│  Heh8080.Devices                                            │
│  - IConsoleDevice interface                                 │
│  - ConsolePortHandler wires to IoBus                        │
└─────────────────────────────────────────────────────────────┘
        ↓
┌─────────────────────────────────────────────────────────────┐
│  Heh8080.Core                                               │
│  - CPU IN/OUT on ports 0-1                                  │
└─────────────────────────────────────────────────────────────┘
```

## ADM-3A Escape Sequences

### Cursor Positioning
| Sequence | Description |
|----------|-------------|
| `ESC = row col` | Absolute cursor position (row/col are ASCII + 0x20) |
| `Ctrl+H` (0x08) | Cursor left / backspace |
| `Ctrl+J` (0x0A) | Line feed (cursor down, scroll if at bottom) |
| `Ctrl+K` (0x0B) | Cursor up |
| `Ctrl+L` (0x0C) | Cursor right |
| `Ctrl+M` (0x0D) | Carriage return |
| `Ctrl+^` (0x1E) | Home cursor |

### Screen Control
| Sequence | Description |
|----------|-------------|
| `Ctrl+Z` (0x1A) | Clear screen and home cursor |
| `Ctrl+G` (0x07) | Bell (ignored or visual flash) |

### Kaypro/Osborne Extensions (required for WordStar)
| Sequence | Description |
|----------|-------------|
| `ESC T` | Clear to end of line |
| `ESC Y` | Clear to end of screen |
| `ESC *` | Clear screen (alternative) |
| `ESC :` | Clear screen (alternative) |

## Files to Create

### Core Terminal (`src/Heh8080.Terminal/`)
| File | Purpose | Lines |
|------|---------|-------|
| `Heh8080.Terminal.csproj` | New project (no Avalonia dependency) | ~15 |
| `TerminalCell.cs` | Character + attribute struct | ~20 |
| `TerminalBuffer.cs` | Fixed 80×24 grid, cursor state, dirty tracking | ~50 |
| `Adm3aParser.cs` | Escape sequence state machine | ~150 |
| `Adm3aTerminal.cs` | Combines buffer + parser, implements IConsoleDevice | ~50 |

### Avalonia UI (`src/Heh8080.Desktop/Controls/`)
| File | Purpose | Lines |
|------|---------|-------|
| `RetroTerminalControl.cs` | Custom control with CRT effects | ~500 |

### Tests (`tests/Heh8080.Tests/`)
| File | Purpose | Lines |
|------|---------|-------|
| `Adm3aParserTests.cs` | Parser unit tests | ~100 |

## CRT Visual Effects

### Color Palette (Green Phosphor P1)
```csharp
Background = Color.FromRgb(0x0A, 0x1A, 0x0A);  // Near black with green tint
Foreground = Color.FromRgb(0x33, 0xFF, 0x33);  // Bright green
DimGreen   = Color.FromRgb(0x20, 0xA0, 0x20);  // For dim text
```

### Effects (SkiaSharp)
1. **Scanlines** - Horizontal lines at 50% opacity every 2nd pixel row
2. **Phosphor glow** - Blur pass behind bright text
3. **Barrel distortion** - Optional, subtle CRT curvature
4. **Vignette** - Darker corners

### Rendering Pipeline
1. Render text to offscreen buffer (no effects)
2. Apply glow (blur bright pixels)
3. Composite scanlines
4. Apply barrel distortion (if enabled)
5. Apply vignette

## IConsoleDevice Implementation

```csharp
public class Adm3aTerminal : IConsoleDevice
{
    private readonly Queue<byte> _inputQueue = new();
    private readonly TerminalBuffer _buffer = new(80, 24);
    private readonly Adm3aParser _parser;

    public Adm3aTerminal() => _parser = new Adm3aParser(_buffer);

    // IConsoleDevice
    public bool IsInputReady => _inputQueue.Count > 0;
    public byte ReadChar() => _inputQueue.Dequeue();
    public void WriteChar(byte c) => _parser.ProcessByte(c);

    // For keyboard input from RetroTerminalControl
    public void QueueInput(byte c) => _inputQueue.Enqueue(c);

    // For RetroTerminalControl to read
    public TerminalBuffer Buffer => _buffer;
}
```

## Keyboard Mapping

| Key | Byte sent |
|-----|-----------|
| Printable ASCII | Direct (0x20-0x7E) |
| Backspace | 0x08 |
| Enter | 0x0D |
| Delete | 0x7F |
| Ctrl+A..Z | 0x01..0x1A |

Note: Arrow keys not mapped - CP/M software uses Ctrl+H/J/K/L (WordStar diamond).

## Implementation Steps

1. **TerminalBuffer** - Character grid with cursor tracking
2. **Adm3aParser** - State machine for escape sequences
3. **Adm3aTerminal** - IConsoleDevice wrapper
4. **RetroTerminalControl** - Basic rendering (no effects)
5. **CRT Effects** - Scanlines, glow, distortion
6. **Integration** - Wire to ConsolePortHandler, update MainView

## Testing Strategy

1. **Unit tests**: Parser handles all escape sequences
2. **Visual test**: Cursor movement, scrolling
3. **Integration**: TST8080.COM output display
4. **Application**: WordStar full-screen editing

## Design Decisions

- **Fixed 80×24**: Standard CP/M terminal size, no resize needed
- **No scrollback**: Period-accurate (CP/M terminals didn't have this)
- **ADM-3A + extensions**: Simpler than VT100, sufficient for CP/M software
- **Separate project**: `Heh8080.Terminal` keeps terminal code isolated from Devices
- **Hardcoded retro effects**: No user settings for now, pure retro aesthetic

## References

- ADM-3A manual: cursor positioning, control codes
- Kaypro technical manual: ADM-3A extensions
- [lode/plans/emulator-implementation.md](emulator-implementation.md): Overall project plan
- [lode/devices/io-ports.md](../devices/io-ports.md): Console ports 0-1

# MCP Server Integration

heh8080 includes an MCP (Model Context Protocol) server that enables AI assistants to interact directly with the CP/M emulator.

## Overview

The MCP server runs as an alternative mode of the Desktop application, exposing CP/M machine controls as MCP tools. This enables programmatic interaction with CP/M for automated development and testing.

## Architecture

```
┌─────────────────┐     ┌──────────────────────────────────┐
│   MCP Client    │────▶│  Heh8080.Desktop --mcp           │
│  (Claude, etc.) │     │  ┌────────────────────────────┐  │
└─────────────────┘     │  │  CpmTools (MCP Tools)      │  │
        │               │  └────────────┬───────────────┘  │
        │               │               │                  │
   stdio transport      │  ┌────────────▼───────────────┐  │
                        │  │  CpmMachine (Headless)     │  │
                        │  │  - Emulator                │  │
                        │  │  - Adm3aTerminal           │  │
                        │  │  - FloppyDiskController    │  │
                        │  └────────────────────────────┘  │
                        └──────────────────────────────────┘
```

## Usage

### Starting the MCP Server

```bash
# Basic - starts with no disk mounted
dotnet run --project src/Heh8080.Desktop -- --mcp

# With boot disk
dotnet run --project src/Heh8080.Desktop -- --mcp --disk /path/to/disk.dsk
```

### Claude Code Configuration

Add to `~/.claude/settings.json`:

```json
{
  "mcpServers": {
    "cpm": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/heh8080/src/Heh8080.Desktop", "--", "--mcp", "--disk", "/path/to/lolos/drivea.dsk"]
    }
  }
}
```

## Available Tools

### send_input
Send text input to the CP/M console.

```
send_input(text: "DIR\r")
```

Use `\r` for Enter key.

### read_screen
Read the current terminal screen contents (80x24 characters).

```
read_screen() -> string
```

### wait_for_text
Wait for specific text to appear on screen.

```
wait_for_text(pattern: "A>", timeoutMs: 5000) -> string
```

### peek_memory
Read bytes from emulator memory. Returns hex-encoded bytes.

```
peek_memory(address: 256, length: 16) -> "C30003C30603..."
```

### poke_memory
Write bytes to emulator memory. Data should be hex-encoded.

```
poke_memory(address: 256, hexData: "C30003")
```

### status
Get current machine status including CPU registers.

```
status() -> "Running: True, PC: E406, SP: EFF6, Halted: False"
```

### reset
Reset the CP/M machine and reboot.

```
reset() -> "Machine reset and rebooted"
```

### mount_disk
Mount a disk image file to a drive.

```
mount_disk(drive: 0, path: "/path/to/disk.dsk")
```

### disk_info
Get information about mounted disk drives.

```
disk_info() -> "A: Mounted\nB: Empty\n..."
```

## Implementation Details

### Key Files

| File | Purpose |
|------|---------|
| `src/Heh8080.Mcp/CpmMachine.cs` | Headless emulator wrapper |
| `src/Heh8080.Mcp/CpmTools.cs` | MCP tool definitions |
| `src/Heh8080.Mcp/McpServerHost.cs` | MCP server host setup |
| `src/Heh8080.Desktop/Program.cs` | Entry point with --mcp handling |

### CpmMachine

`CpmMachine` wraps the emulator components without UI dependencies:
- Creates `Emulator` with Intel 8080 CPU
- Creates `Adm3aTerminal` for console I/O
- Creates `FileDiskImageProvider` for disk access
- Configures all device handlers (FDC, MMU, timer, etc.)

### Dependencies

- `ModelContextProtocol` - Official MCP C# SDK
- `Microsoft.Extensions.Hosting` - Generic host for MCP server

## Use Cases

### spazm8080 Development

The primary use case is developing spazm8080 (a self-hosted 8080 assembler):

1. Mount lolos disk with assembler sources
2. Use `send_input` to invoke ED editor
3. Use `read_screen` to monitor output
4. Use `wait_for_text` to detect completion
5. Use `peek_memory` to inspect assembled code

### Automated Testing

Run CP/M programs and verify output:

```
1. reset()
2. wait_for_text("A>")
3. send_input("MYPROG\r")
4. wait_for_text("PASS") or wait_for_text("FAIL")
```

## Testing

Test using named pipes (regular pipes don't work well with interactive protocol):

```bash
mkfifo /tmp/mcp_test
(
  echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}'
  sleep 0.5
  echo '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"Status","arguments":{}}}'
  sleep 1
) > /tmp/mcp_test &
./Heh8080.Desktop --mcp --disk ../lolos/drivea.dsk < /tmp/mcp_test
rm /tmp/mcp_test
```

## Limitations

- Single-user mode only (one MCP client at a time)
- No disk file read/write through CP/M filesystem (use disk mounting or cpmtools)
- CPU registers beyond PC/SP not exposed via ICpu interface

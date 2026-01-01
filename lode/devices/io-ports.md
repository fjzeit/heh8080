# I/O Port Specification

heh8080 I/O port assignments for CP/M compatibility.

## Port Map

| Port | Device | Read | Write |
|------|--------|------|-------|
| 0 | Console | Status (FFh=ready, 00h=not ready) | - |
| 1 | Console | Data (character) | Data (character) |
| 2 | Printer | Status (FFh=ready) | - |
| 3 | Printer | Data (1Ah=EOF) | Data (character) |
| 4 | Auxiliary | Status | EOF flag |
| 5 | Auxiliary | Data | Data |
| 10 | FDC | Current drive | Set drive (0-15) |
| 11 | FDC | Current track | Set track (0-255) |
| 12 | FDC | Sector low byte | Set sector low |
| 13 | FDC | 00h | Command (0=read, 1=write) |
| 14 | FDC | Status | - |
| 15 | DMA | Address low byte | Set address low |
| 16 | DMA | Address high byte | Set address high |
| 17 | FDC | Sector high byte | Set sector high |
| 20 | MMU | Bank count | Initialize banks |
| 21 | MMU | Current bank | Select bank |
| 22 | MMU | Segment size (pages) | Set segment size |
| 23 | MMU | Write protect status | Set write protect |
| 25 | Clock | - | Command |
| 26 | Clock | Data | Data |
| 27 | Timer | Status (1=enabled) | Enable/disable |
| 28 | Delay | 00h | Delay (n × 10ms) |
| 30 | Speed | CPU speed low | Set speed low |
| 31 | Speed | CPU speed high | Set speed high |
| 40-47 | Network | Server socket 1-4 status/data | |
| 50-51 | Network | Client socket status/data | |
| 160 | Hardware | Lock status | Control (see below) |

## Console (Ports 0-1)

**Port 0 - Status (IN)**
- Returns FFh if character available
- Returns 00h if no input ready

**Port 1 - Data (IN/OUT)**
- IN: Read character from keyboard
- OUT: Write character to display

## Printer (Ports 2-3)

**Port 2 - Status (IN)**
- Always returns FFh (ready)

**Port 3 - Data (IN/OUT)**
- IN: Returns 1Ah (CP/M EOF)
- OUT: Write character to printer

## FDC - Floppy Disk Controller (Ports 10-17)

### Registers
- Port 10: Drive select (0-15, A-P)
- Port 11: Track number (0-255)
- Port 12: Sector number low byte
- Port 17: Sector number high byte
- Port 13: Command register
- Port 14: Status register
- Port 15-16: DMA address (low/high)

### Commands (Port 13 OUT)
- 0: Read sector (128 bytes from disk to DMA address)
- 1: Write sector (128 bytes from DMA address to disk)

### Status Codes (Port 14 IN)
| Code | Meaning |
|------|---------|
| 0 | OK |
| 1 | Invalid drive |
| 2 | Invalid track |
| 3 | Invalid sector |
| 4 | Seek error |
| 5 | Read error |
| 6 | Write error |
| 7 | Invalid command |

### Disk Geometry

Standard 8" floppy (drives A-D):
- 77 tracks (0-76)
- 26 sectors per track (1-26)
- 128 bytes per sector
- Total: 256,256 bytes per disk

File offset calculation:
```
offset = (track × sectors_per_track + (sector - 1)) × 128
```

## MMU - Memory Management Unit (Ports 20-23)

**Port 20 - Initialize (OUT)**
- Allocate N memory banks (including bank 0)
- Read returns current bank count

**Port 21 - Bank Select (OUT)**
- Select active bank (0 to N-1)
- Read returns current bank

**Port 22 - Segment Size (OUT)**
- Set segment size in 256-byte pages
- Default: 192 pages (48KB banked, 16KB common)
- Must set before allocating banks

**Port 23 - Write Protect (OUT)**
- Protect/unprotect common memory segment

## Timer (Port 27)

**OUT**
- 1: Enable 10ms interrupt timer
- 0: Disable timer

**IN**
- Returns current timer state

When enabled, generates maskable interrupt every 10ms.

## Hardware Control (Port 160)

Port is locked until magic byte AAh is written.

**Control bits (after unlock):**
| Bit | Function |
|-----|----------|
| 4 | Switch to 8080 mode |
| 5 | Switch to Z80 mode |
| 6 | Reset CPU and MMU, reboot |
| 7 | Halt emulation |

## Unassigned Ports

All unassigned ports return FFh on read and ignore writes.

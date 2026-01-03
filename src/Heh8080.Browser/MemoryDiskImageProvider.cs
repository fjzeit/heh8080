using System;
using System.Collections.Generic;
using Heh8080.Devices;

namespace Heh8080.Browser;

/// <summary>
/// In-memory disk image provider for browser.
/// Disk images are stored entirely in memory. For persistence,
/// use JS interop to save/load from IndexedDB or localStorage.
/// </summary>
public sealed class MemoryDiskImageProvider : IDiskImageProvider, IDisposable
{
    private const int MaxDrives = 16;
    private const int BytesPerSector = 128;
    private const int SectorsPerTrack = 26;
    private const int TracksPerDisk = 77;
    private const int DiskSize = BytesPerSector * SectorsPerTrack * TracksPerDisk; // 256,256 bytes

    // CP/M disk parameters for standard IBM 3740 format
    private const int SystemTracks = 2;          // Tracks 0-1 reserved for system
    private const int DirectorySectors = 16;     // 64 entries × 32 bytes = 2048 bytes = 16 sectors
    private const int BlockSize = 1024;          // 1KB blocks (8 sectors)
    private const int MaxDirectoryEntries = 64;
    private const int RecordsPerExtent = 128;    // 128 × 128 = 16KB per extent

    private readonly DriveInfo?[] _drives = new DriveInfo?[MaxDrives];

    private sealed class DriveInfo
    {
        public byte[] Data { get; }
        public bool ReadOnly { get; }
        public string Name { get; }

        public DriveInfo(byte[] data, bool readOnly, string name)
        {
            Data = data;
            ReadOnly = readOnly;
            Name = name;
        }
    }

    public bool IsMounted(int drive)
    {
        return drive >= 0 && drive < MaxDrives && _drives[drive] != null;
    }

    /// <summary>
    /// Mount a disk from a byte array.
    /// </summary>
    public void MountFromBytes(int drive, byte[] data, string name, bool readOnly = false)
    {
        if (drive < 0 || drive >= MaxDrives)
            throw new ArgumentOutOfRangeException(nameof(drive), "Drive must be 0-15");

        Unmount(drive);

        // Ensure disk is at least minimum size
        byte[] diskData;
        if (data.Length >= DiskSize)
        {
            diskData = data;
        }
        else
        {
            diskData = new byte[DiskSize];
            Array.Copy(data, diskData, data.Length);
            // Fill rest with E5 (CP/M empty)
            for (int i = data.Length; i < DiskSize; i++)
                diskData[i] = 0xE5;
        }

        _drives[drive] = new DriveInfo(diskData, readOnly, name);
    }

    public void Mount(int drive, string imagePath, bool readOnly = false)
    {
        // In browser, we don't have file paths - use MountFromBytes instead
        throw new NotSupportedException("Use MountFromBytes in browser environment");
    }

    public void Unmount(int drive)
    {
        if (drive >= 0 && drive < MaxDrives)
        {
            _drives[drive] = null;
        }
    }

    public bool ReadSector(int drive, int track, int sector, Span<byte> buffer)
    {
        if (buffer.Length < BytesPerSector)
            return false;

        var driveInfo = GetDrive(drive);
        if (driveInfo == null)
            return false;

        int offset = CalculateOffset(track, sector);
        if (offset < 0 || offset + BytesPerSector > driveInfo.Data.Length)
            return false;

        driveInfo.Data.AsSpan(offset, BytesPerSector).CopyTo(buffer);
        return true;
    }

    public bool WriteSector(int drive, int track, int sector, ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < BytesPerSector)
            return false;

        var driveInfo = GetDrive(drive);
        if (driveInfo == null || driveInfo.ReadOnly)
            return false;

        int offset = CalculateOffset(track, sector);
        if (offset < 0 || offset + BytesPerSector > driveInfo.Data.Length)
            return false;

        buffer[..BytesPerSector].CopyTo(driveInfo.Data.AsSpan(offset, BytesPerSector));
        return true;
    }

    public bool IsReadOnly(int drive)
    {
        var driveInfo = GetDrive(drive);
        return driveInfo?.ReadOnly ?? true;
    }

    public bool Refresh(int drive)
    {
        // No-op for browser - data is in memory, no external file to refresh
        return IsMounted(drive);
    }

    /// <summary>
    /// Get the disk data for saving to IndexedDB.
    /// </summary>
    public byte[]? GetDiskData(int drive)
    {
        return GetDrive(drive)?.Data;
    }

    /// <summary>
    /// Create and mount an empty formatted CP/M disk.
    /// </summary>
    public void CreateEmptyDisk(int drive, string name)
    {
        var data = new byte[DiskSize];

        // Fill entire disk with 0xE5 (CP/M empty marker)
        Array.Fill(data, (byte)0xE5);

        // Clear system tracks (tracks 0-1) to 0x00
        var systemBytes = SystemTracks * SectorsPerTrack * BytesPerSector;
        Array.Fill(data, (byte)0x00, 0, systemBytes);

        MountFromBytes(drive, data, name, readOnly: false);
    }

    /// <summary>
    /// Write a file to a CP/M formatted disk.
    /// Returns true on success, false if disk is full or file too large.
    /// </summary>
    public bool WriteFile(int drive, string filename, byte[] fileData)
    {
        var driveInfo = GetDrive(drive);
        if (driveInfo == null || driveInfo.ReadOnly)
            return false;

        // Parse filename (NAME.EXT format)
        var (name, ext) = ParseFilename(filename);
        if (name.Length == 0)
            return false;

        // Calculate how many extents we need
        var recordCount = (fileData.Length + BytesPerSector - 1) / BytesPerSector;
        var extentCount = (recordCount + RecordsPerExtent - 1) / RecordsPerExtent;
        if (extentCount > 16) // Limit to 256KB files for simplicity
            return false;

        // Find free directory entries
        var freeEntries = FindFreeDirectoryEntries(driveInfo.Data, extentCount);
        if (freeEntries.Count < extentCount)
            return false;

        // Find free blocks
        var blocksNeeded = (fileData.Length + BlockSize - 1) / BlockSize;
        var freeBlocks = FindFreeBlocks(driveInfo.Data, blocksNeeded);
        if (freeBlocks.Count < blocksNeeded)
            return false;

        // Write file data to blocks
        int dataOffset = 0;
        foreach (var block in freeBlocks)
        {
            var blockOffset = GetBlockOffset(block);
            var bytesToWrite = Math.Min(BlockSize, fileData.Length - dataOffset);
            Array.Copy(fileData, dataOffset, driveInfo.Data, blockOffset, bytesToWrite);
            dataOffset += bytesToWrite;
        }

        // Create directory entries
        int recordsWritten = 0;
        int blocksUsed = 0;
        for (int extent = 0; extent < extentCount; extent++)
        {
            var entryOffset = GetDirectoryEntryOffset(freeEntries[extent]);
            var entry = new byte[32];

            // User number (0)
            entry[0] = 0;

            // Filename (8 chars, space padded)
            for (int i = 0; i < 8; i++)
                entry[1 + i] = i < name.Length ? (byte)name[i] : (byte)' ';

            // Extension (3 chars, space padded)
            for (int i = 0; i < 3; i++)
                entry[9 + i] = i < ext.Length ? (byte)ext[i] : (byte)' ';

            // Extent number
            entry[12] = (byte)extent;

            // Reserved
            entry[13] = 0;
            entry[14] = 0;

            // Record count for this extent
            var recordsInExtent = Math.Min(RecordsPerExtent, recordCount - recordsWritten);
            entry[15] = (byte)recordsInExtent;
            recordsWritten += recordsInExtent;

            // Block allocation (16 bytes for blocks used by this extent)
            var blocksInExtent = (recordsInExtent * BytesPerSector + BlockSize - 1) / BlockSize;
            for (int i = 0; i < 16 && blocksUsed < freeBlocks.Count; i++)
            {
                if (i < blocksInExtent)
                {
                    entry[16 + i] = (byte)freeBlocks[blocksUsed++];
                }
            }

            // Write directory entry
            Array.Copy(entry, 0, driveInfo.Data, entryOffset, 32);
        }

        return true;
    }

    /// <summary>
    /// List files on the disk.
    /// </summary>
    public List<(string Name, int Size)> ListFiles(int drive)
    {
        var files = new Dictionary<string, int>();
        var driveInfo = GetDrive(drive);
        if (driveInfo == null)
            return new List<(string, int)>();

        for (int i = 0; i < MaxDirectoryEntries; i++)
        {
            var offset = GetDirectoryEntryOffset(i);
            var userNum = driveInfo.Data[offset];

            // Skip empty entries (0xE5) and deleted entries
            if (userNum == 0xE5 || userNum > 15)
                continue;

            // Read filename
            var name = "";
            for (int j = 0; j < 8; j++)
            {
                var c = (char)(driveInfo.Data[offset + 1 + j] & 0x7F);
                if (c != ' ') name += c;
            }
            name += ".";
            for (int j = 0; j < 3; j++)
            {
                var c = (char)(driveInfo.Data[offset + 9 + j] & 0x7F);
                if (c != ' ') name += c;
            }
            if (name.EndsWith(".")) name = name[..^1];

            // Calculate size from record count
            var recordCount = driveInfo.Data[offset + 15];
            var extentNum = driveInfo.Data[offset + 12];
            var size = (extentNum * RecordsPerExtent + recordCount) * BytesPerSector;

            // Keep largest size for multi-extent files
            if (!files.ContainsKey(name) || files[name] < size)
                files[name] = size;
        }

        var result = new List<(string, int)>();
        foreach (var kvp in files)
            result.Add((kvp.Key, kvp.Value));
        return result;
    }

    private static (string name, string ext) ParseFilename(string filename)
    {
        filename = filename.ToUpperInvariant();

        // Remove path if present
        var lastSlash = Math.Max(filename.LastIndexOf('/'), filename.LastIndexOf('\\'));
        if (lastSlash >= 0)
            filename = filename[(lastSlash + 1)..];

        var dot = filename.LastIndexOf('.');
        string name, ext;
        if (dot >= 0)
        {
            name = filename[..dot];
            ext = filename[(dot + 1)..];
        }
        else
        {
            name = filename;
            ext = "";
        }

        // Truncate to CP/M limits
        if (name.Length > 8) name = name[..8];
        if (ext.Length > 3) ext = ext[..3];

        return (name, ext);
    }

    private List<int> FindFreeDirectoryEntries(byte[] data, int count)
    {
        var free = new List<int>();
        for (int i = 0; i < MaxDirectoryEntries && free.Count < count; i++)
        {
            var offset = GetDirectoryEntryOffset(i);
            if (data[offset] == 0xE5)
                free.Add(i);
        }
        return free;
    }

    private List<int> FindFreeBlocks(byte[] data, int count)
    {
        // Build bitmap of used blocks from directory
        var usedBlocks = new HashSet<int>();

        for (int i = 0; i < MaxDirectoryEntries; i++)
        {
            var offset = GetDirectoryEntryOffset(i);
            if (data[offset] == 0xE5 || data[offset] > 15)
                continue;

            // Read block allocation
            for (int j = 0; j < 16; j++)
            {
                var block = data[offset + 16 + j];
                if (block != 0)
                    usedBlocks.Add(block);
            }
        }

        // Find free blocks (skip block 0-1 which are directory)
        var free = new List<int>();
        var totalBlocks = (DiskSize - SystemTracks * SectorsPerTrack * BytesPerSector) / BlockSize;
        for (int i = 2; i < totalBlocks && free.Count < count; i++)
        {
            if (!usedBlocks.Contains(i))
                free.Add(i);
        }
        return free;
    }

    private static int GetDirectoryEntryOffset(int entryIndex)
    {
        // Directory starts at track 2
        var dirStart = SystemTracks * SectorsPerTrack * BytesPerSector;
        return dirStart + entryIndex * 32;
    }

    private static int GetBlockOffset(int blockNum)
    {
        // Blocks start after system tracks
        var dataStart = SystemTracks * SectorsPerTrack * BytesPerSector;
        return dataStart + blockNum * BlockSize;
    }

    private DriveInfo? GetDrive(int drive)
    {
        return drive >= 0 && drive < MaxDrives ? _drives[drive] : null;
    }

    private static int CalculateOffset(int track, int sector)
    {
        // Sector numbers are 1-based
        if (sector < 1)
            return -1;

        return (track * SectorsPerTrack + (sector - 1)) * BytesPerSector;
    }

    public void Dispose()
    {
        for (int i = 0; i < MaxDrives; i++)
        {
            _drives[i] = null;
        }
    }
}

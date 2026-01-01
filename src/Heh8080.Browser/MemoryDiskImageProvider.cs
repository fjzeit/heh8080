using System;
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

    /// <summary>
    /// Get the disk data for saving to IndexedDB.
    /// </summary>
    public byte[]? GetDiskData(int drive)
    {
        return GetDrive(drive)?.Data;
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

namespace Heh8080.Devices;

/// <summary>
/// File-based disk image provider for desktop platforms.
/// Disk images are flat binary files (256KB for standard 8" floppy).
/// </summary>
public sealed class FileDiskImageProvider : IDiskImageProvider, IDisposable
{
    private const int MaxDrives = 16;
    private const int BytesPerSector = 128;
    private const int SectorsPerTrack = 26;

    private readonly DriveInfo?[] _drives = new DriveInfo?[MaxDrives];

    private sealed class DriveInfo : IDisposable
    {
        public FileStream Stream { get; }
        public bool ReadOnly { get; }
        public string Path { get; }

        public DriveInfo(FileStream stream, bool readOnly, string path)
        {
            Stream = stream;
            ReadOnly = readOnly;
            Path = path;
        }

        public void Dispose()
        {
            Stream.Dispose();
        }
    }

    public bool IsMounted(int drive)
    {
        return drive >= 0 && drive < MaxDrives && _drives[drive] != null;
    }

    public void Mount(int drive, string imagePath, bool readOnly = false)
    {
        if (drive < 0 || drive >= MaxDrives)
            throw new ArgumentOutOfRangeException(nameof(drive), "Drive must be 0-15");

        // Unmount if already mounted
        Unmount(drive);

        var access = readOnly ? FileAccess.Read : FileAccess.ReadWrite;
        var share = readOnly ? FileShare.Read : FileShare.None;
        var stream = new FileStream(imagePath, FileMode.OpenOrCreate, access, share);

        _drives[drive] = new DriveInfo(stream, readOnly, imagePath);
    }

    public void Unmount(int drive)
    {
        if (drive >= 0 && drive < MaxDrives && _drives[drive] != null)
        {
            _drives[drive]!.Dispose();
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

        long offset = CalculateOffset(track, sector);
        if (offset < 0)
            return false;

        try
        {
            driveInfo.Stream.Seek(offset, SeekOrigin.Begin);
            int bytesRead = driveInfo.Stream.Read(buffer[..BytesPerSector]);

            // If we read less than a full sector (e.g., past end of file),
            // fill the rest with 0xE5 (CP/M empty sector filler)
            if (bytesRead < BytesPerSector)
            {
                buffer[bytesRead..BytesPerSector].Fill(0xE5);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool WriteSector(int drive, int track, int sector, ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < BytesPerSector)
            return false;

        var driveInfo = GetDrive(drive);
        if (driveInfo == null || driveInfo.ReadOnly)
            return false;

        long offset = CalculateOffset(track, sector);
        if (offset < 0)
            return false;

        try
        {
            driveInfo.Stream.Seek(offset, SeekOrigin.Begin);
            driveInfo.Stream.Write(buffer[..BytesPerSector]);
            driveInfo.Stream.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool IsReadOnly(int drive)
    {
        var driveInfo = GetDrive(drive);
        return driveInfo?.ReadOnly ?? true;
    }

    public bool Refresh(int drive)
    {
        var driveInfo = GetDrive(drive);
        if (driveInfo == null)
            return false;

        // Remount with same path and read-only setting
        Mount(drive, driveInfo.Path, driveInfo.ReadOnly);
        return true;
    }

    private DriveInfo? GetDrive(int drive)
    {
        return drive >= 0 && drive < MaxDrives ? _drives[drive] : null;
    }

    private static long CalculateOffset(int track, int sector)
    {
        // Sector numbers are 1-based
        if (sector < 1)
            return -1;

        return ((long)track * SectorsPerTrack + (sector - 1)) * BytesPerSector;
    }

    public void Dispose()
    {
        for (int i = 0; i < MaxDrives; i++)
        {
            _drives[i]?.Dispose();
            _drives[i] = null;
        }
    }
}

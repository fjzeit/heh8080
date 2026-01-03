namespace Heh8080.Devices;

/// <summary>
/// Disk image storage abstraction for platform-specific implementations.
/// Desktop uses file I/O, browser uses IndexedDB.
/// </summary>
public interface IDiskImageProvider
{
    /// <summary>
    /// Check if a disk is mounted for the specified drive.
    /// </summary>
    bool IsMounted(int drive);

    /// <summary>
    /// Mount a disk image for the specified drive.
    /// </summary>
    /// <param name="drive">Drive number (0-15, A-P)</param>
    /// <param name="imagePath">Path or identifier for the disk image</param>
    /// <param name="readOnly">Mount as read-only</param>
    void Mount(int drive, string imagePath, bool readOnly = false);

    /// <summary>
    /// Unmount the disk from the specified drive.
    /// </summary>
    void Unmount(int drive);

    /// <summary>
    /// Read a sector from disk.
    /// </summary>
    /// <param name="drive">Drive number (0-15)</param>
    /// <param name="track">Track number (0-76 for standard 8" floppy)</param>
    /// <param name="sector">Sector number (1-26 for standard 8" floppy)</param>
    /// <param name="buffer">Buffer to read into (128 bytes)</param>
    /// <returns>True if read succeeded</returns>
    bool ReadSector(int drive, int track, int sector, Span<byte> buffer);

    /// <summary>
    /// Write a sector to disk.
    /// </summary>
    /// <param name="drive">Drive number (0-15)</param>
    /// <param name="track">Track number</param>
    /// <param name="sector">Sector number</param>
    /// <param name="buffer">Buffer to write from (128 bytes)</param>
    /// <returns>True if write succeeded</returns>
    bool WriteSector(int drive, int track, int sector, ReadOnlySpan<byte> buffer);

    /// <summary>
    /// Check if the mounted disk is read-only.
    /// </summary>
    bool IsReadOnly(int drive);

    /// <summary>
    /// Refresh disk to see external changes (closes and reopens file handle).
    /// </summary>
    /// <param name="drive">Drive number (0-15)</param>
    /// <returns>True if refresh succeeded</returns>
    bool Refresh(int drive);
}

using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace Heh8080.Browser;

/// <summary>
/// JS interop for IndexedDB disk storage.
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class DiskStorageInterop
{
    [JSImport("saveDisk", "interop")]
    public static partial Task<bool> SaveDiskAsync(int drive, string name, byte[] data);

    [JSImport("loadDiskName", "interop")]
    public static partial Task<string?> LoadDiskNameAsync(int drive);

    [JSImport("loadDiskDataBase64", "interop")]
    public static partial Task<string?> LoadDiskDataBase64Async(int drive);

    [JSImport("deleteDisk", "interop")]
    public static partial Task<bool> DeleteDiskAsync(int drive);

    [JSImport("getOrigin", "interop")]
    public static partial string GetOrigin();

    /// <summary>
    /// Load disk data from IndexedDB.
    /// </summary>
    public static async Task<(string? name, byte[]? data)> LoadDiskDataAsync(int drive)
    {
        var name = await LoadDiskNameAsync(drive);
        if (name == null)
            return (null, null);

        var base64 = await LoadDiskDataBase64Async(drive);
        if (string.IsNullOrEmpty(base64))
            return (name, null);

        var data = System.Convert.FromBase64String(base64);
        return (name, data);
    }
}

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

    [JSImport("getViewportWidth", "interop")]
    public static partial int GetViewportWidth();

    [JSImport("getViewportHeight", "interop")]
    public static partial int GetViewportHeight();

    [JSImport("registerResizeListener", "interop")]
    public static partial void RegisterResizeListener([JSMarshalAs<JSType.Function<JSType.Number, JSType.Number>>] Action<int, int> callback);

    [JSImport("clearAllDisks", "interop")]
    public static partial Task<bool> ClearAllDisksAsync();

    [JSImport("fetchFileAsBase64", "interop")]
    public static partial Task<string> FetchFileAsBase64Async(string url);

    [JSImport("jsLog", "interop")]
    public static partial void JsLog(string message);

    [JSImport("pickFile", "interop")]
    public static partial Task<string?> PickFileAsync(string acceptTypes);

    /// <summary>
    /// Fetch a file using JS fetch API (workaround for HttpClient issues in WASM).
    /// </summary>
    public static async Task<byte[]> FetchFileAsync(string url)
    {
        var base64 = await FetchFileAsBase64Async(url);
        return System.Convert.FromBase64String(base64);
    }

    /// <summary>
    /// Open file picker and return selected file.
    /// Returns null if user cancels.
    /// </summary>
    public static async Task<(string Name, byte[] Data)?> PickFileDataAsync(string acceptTypes = "*")
    {
        var json = await PickFileAsync(acceptTypes);
        if (string.IsNullOrEmpty(json))
            return null;

        // Parse JSON: { "name": "...", "dataBase64": "..." }
        // Simple parsing without JSON library
        var nameStart = json.IndexOf("\"name\":\"") + 8;
        var nameEnd = json.IndexOf("\"", nameStart);
        var name = json[nameStart..nameEnd];

        var dataStart = json.IndexOf("\"dataBase64\":\"") + 14;
        var dataEnd = json.IndexOf("\"", dataStart);
        var dataBase64 = json[dataStart..dataEnd];

        var data = System.Convert.FromBase64String(dataBase64);
        return (name, data);
    }

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

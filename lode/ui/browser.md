# Browser Platform

Avalonia WASM build for running heh8080 in web browsers.

## Project Structure

```
src/Heh8080.Browser/
├── Program.cs              # WASM entry point, imports interop.js
├── App.axaml(.cs)          # Application lifecycle, boots emulator
├── MemoryDiskImageProvider # In-memory disk storage
├── DiskStorageInterop.cs   # C# JSImport bindings for IndexedDB/fetch
└── wwwroot/
    ├── index.html          # Minimal HTML shell with cache buster
    ├── app.css             # Viewport-filling CSS
    ├── main.js             # dotnet.js loader
    └── interop.js          # IndexedDB, fetch, viewport interop
```

## Target Framework

- `net9.0-browser` with `RuntimeIdentifier=browser-wasm`
- Single-threaded execution (no real threads)
- References shared `Heh8080.App` library (multi-targeted)

## Critical WASM Constraints

### Async/Threading Differences

Single-threaded WASM has critical differences from desktop .NET:

| Feature | Desktop | WASM | Solution |
|---------|---------|------|----------|
| `Task.Yield()` | Yields to scheduler | Does NOT yield event loop | Use `Task.Delay(1)` |
| `Task.Run()` | Runs on thread pool | Blocks main thread | Use async directly |
| `Thread.Sleep()` | Sleeps thread | Blocks everything | Avoid or use `Task.Delay` |
| `System.Threading.Timer` | Works normally | May not fire reliably | Works for now, monitor |
| `HttpClient` | Works normally | May hang on binary data | Use JS fetch via interop |

### Emulator Loop

The emulator uses `Task.Delay(1)` between instruction batches to allow the browser event loop to run:

```csharp
// Emulator.cs - RunLoopAsync
while (!ct.IsCancellationRequested && !Cpu.Halted)
{
    for (int i = 0; i < BatchSize; i++)
    {
        Cpu.Step();
    }
    // CRITICAL: Task.Delay(1) allows browser events to process
    // Task.Yield() does NOT work in single-threaded WASM
    await Task.Delay(1);
}
```

### JS Interop for Fetch

`HttpClient.GetByteArrayAsync` can hang in WASM. Use JS fetch instead:

```javascript
// interop.js
export async function fetchFileAsBase64(url) {
    const response = await fetch(url);
    const arrayBuffer = await response.arrayBuffer();
    const bytes = new Uint8Array(arrayBuffer);
    // Convert to base64 for C# marshalling
    let binary = '';
    for (let i = 0; i < bytes.length; i++) {
        binary += String.fromCharCode(bytes[i]);
    }
    return btoa(binary);
}
```

```csharp
// DiskStorageInterop.cs
[JSImport("fetchFileAsBase64", "interop")]
public static partial Task<string> FetchFileAsBase64Async(string url);

public static async Task<byte[]> FetchFileAsync(string url)
{
    var base64 = await FetchFileAsBase64Async(url);
    return Convert.FromBase64String(base64);
}
```

## Viewport CSS

The terminal is centered in the browser window:

```css
#out {
    position: fixed;
    inset: 0;
    display: flex;
    justify-content: center;
    align-items: center;
    background: #1a1a1a;
}
```

## Auto-Scaling

The browser version automatically selects the largest scale that fits the viewport:

- **Available scales**: 100%, 80%, 60%, 40%
- **On startup**: Calculates best fit based on viewport dimensions
- **On resize**: Recalculates and updates scale automatically
- **User control**: Scale buttons still available for manual override

```csharp
// App.axaml.cs
private static readonly double[] AvailableScales = { 1.0, 0.75, 0.5, 0.25 };

private void UpdateScale(int viewportWidth, int viewportHeight)
{
    foreach (var scale in AvailableScales)
    {
        if (BaseWidth * scale <= viewportWidth && BaseHeight * scale <= viewportHeight)
        {
            _mainView.TerminalScale = scale;
            break;
        }
    }
}
```

### JS Interop for Viewport

```javascript
// interop.js
export function getViewportWidth() { return globalThis.innerWidth; }
export function getViewportHeight() { return globalThis.innerHeight; }
export function registerResizeListener(callback) {
    globalThis.addEventListener('resize', () => callback(innerWidth, innerHeight));
}
```

## IndexedDB Disk Storage

Disk images persist across sessions via IndexedDB:

```javascript
// interop.js
export async function saveDisk(drive, name, data) { ... }
export async function loadDiskName(drive) { ... }
export async function loadDiskDataBase64(drive) { ... }
```

C# uses `[JSImport]` with base64 encoding for byte[] marshalling:

```csharp
[JSImport("saveDisk", "interop")]
public static partial Task<bool> SaveDiskAsync(int drive, string name, byte[] data);

[JSImport("loadDiskDataBase64", "interop")]
public static partial Task<string?> LoadDiskDataBase64Async(int drive);
```

## Boot Sequence

1. Import interop.js via `JSHost.ImportAsync`
2. Start Avalonia with WebGL2/WebGL1 rendering
3. Try loading saved disk from IndexedDB
4. Fall back to bundled `wwwroot/lolos.dsk`
5. Boot emulator

## Related

- [desktop-app.md](desktop-app.md) - Desktop platform
- [../plans/emulator-implementation.md](../plans/emulator-implementation.md) - Phase 7 details

# Browser Platform

Avalonia WASM build for running heh8080 in web browsers.

## Project Structure

```
src/Heh8080.Browser/
├── Program.cs              # WASM entry point, imports interop.js
├── App.axaml(.cs)          # Application lifecycle, boots emulator
├── MemoryDiskImageProvider # In-memory disk storage
├── DiskStorageInterop.cs   # C# JSImport bindings for IndexedDB
└── wwwroot/
    ├── index.html          # Minimal HTML shell
    ├── app.css             # Viewport-filling CSS
    ├── main.js             # dotnet.js loader
    └── interop.js          # IndexedDB save/load functions
```

## Target Framework

- `net9.0-browser` with `RuntimeIdentifier=browser-wasm`
- References shared `Heh8080.App` library (multi-targeted)

## Viewport CSS

The app fills the entire browser window:

```css
#out {
    position: fixed;
    inset: 0;
    overflow: hidden;
}

#out canvas {
    width: 100% !important;
    height: 100% !important;
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

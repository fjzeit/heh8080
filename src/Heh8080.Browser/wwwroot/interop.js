// IndexedDB disk storage for heh8080 browser version
const DB_NAME = 'heh8080';
const DB_VERSION = 1;
const STORE_NAME = 'disks';

let db = null;

async function openDb() {
    if (db) return db;

    return new Promise((resolve, reject) => {
        const request = indexedDB.open(DB_NAME, DB_VERSION);

        request.onerror = () => reject(request.error);

        request.onsuccess = () => {
            db = request.result;
            resolve(db);
        };

        request.onupgradeneeded = (event) => {
            const database = event.target.result;
            if (!database.objectStoreNames.contains(STORE_NAME)) {
                database.createObjectStore(STORE_NAME, { keyPath: 'drive' });
            }
        };
    });
}

// Save disk image to IndexedDB
// data comes as Uint8Array from C#
export async function saveDisk(drive, name, data) {
    const database = await openDb();
    return new Promise((resolve, reject) => {
        const tx = database.transaction(STORE_NAME, 'readwrite');
        const store = tx.objectStore(STORE_NAME);

        // Store as Uint8Array for efficiency
        const record = {
            drive: drive,
            name: name,
            data: new Uint8Array(data),
            savedAt: Date.now()
        };

        const request = store.put(record);
        request.onsuccess = () => resolve(true);
        request.onerror = () => reject(request.error);
    });
}

// Load disk name from IndexedDB
export async function loadDiskName(drive) {
    const database = await openDb();
    return new Promise((resolve, reject) => {
        const tx = database.transaction(STORE_NAME, 'readonly');
        const store = tx.objectStore(STORE_NAME);
        const request = store.get(drive);

        request.onsuccess = () => {
            const result = request.result;
            resolve(result ? result.name : null);
        };
        request.onerror = () => reject(request.error);
    });
}

// Load disk data from IndexedDB as base64 string
export async function loadDiskDataBase64(drive) {
    const database = await openDb();
    return new Promise((resolve, reject) => {
        const tx = database.transaction(STORE_NAME, 'readonly');
        const store = tx.objectStore(STORE_NAME);
        const request = store.get(drive);

        request.onsuccess = () => {
            const result = request.result;
            if (!result || !result.data) {
                resolve(null);
                return;
            }
            // Convert Uint8Array to base64
            const bytes = new Uint8Array(result.data);
            let binary = '';
            for (let i = 0; i < bytes.length; i++) {
                binary += String.fromCharCode(bytes[i]);
            }
            resolve(btoa(binary));
        };
        request.onerror = () => reject(request.error);
    });
}

// Delete disk from IndexedDB
export async function deleteDisk(drive) {
    const database = await openDb();
    return new Promise((resolve, reject) => {
        const tx = database.transaction(STORE_NAME, 'readwrite');
        const store = tx.objectStore(STORE_NAME);
        const request = store.delete(drive);

        request.onsuccess = () => resolve(true);
        request.onerror = () => reject(request.error);
    });
}

// List all saved disks
export async function listDisks() {
    const database = await openDb();
    return new Promise((resolve, reject) => {
        const tx = database.transaction(STORE_NAME, 'readonly');
        const store = tx.objectStore(STORE_NAME);
        const request = store.getAll();

        request.onsuccess = () => {
            const results = request.result.map(r => ({
                drive: r.drive,
                name: r.name,
                savedAt: r.savedAt
            }));
            resolve(results);
        };
        request.onerror = () => reject(request.error);
    });
}

// Get origin URL
export function getOrigin() {
    return globalThis.location.origin;
}

// Get viewport dimensions
export function getViewportWidth() {
    return globalThis.innerWidth;
}

export function getViewportHeight() {
    return globalThis.innerHeight;
}

// Resize callback reference
let resizeCallback = null;

// Register resize listener - callback receives [width, height]
export function registerResizeListener(callback) {
    resizeCallback = callback;
    globalThis.addEventListener('resize', () => {
        if (resizeCallback) {
            resizeCallback(globalThis.innerWidth, globalThis.innerHeight);
        }
    });
}

// Clear all IndexedDB data (for debugging)
export async function clearAllDisks() {
    const database = await openDb();
    return new Promise((resolve, reject) => {
        const tx = database.transaction(STORE_NAME, 'readwrite');
        const store = tx.objectStore(STORE_NAME);
        const request = store.clear();

        request.onsuccess = () => {
            console.log('IndexedDB cleared');
            resolve(true);
        };
        request.onerror = () => reject(request.error);
    });
}

// Direct console log from C#
export function jsLog(message) {
    console.log('[C#]', message);
}

// Fetch a file as base64 (workaround for HttpClient issues in WASM)
export async function fetchFileAsBase64(url) {
    console.log('JS fetchFileAsBase64:', url);
    const response = await fetch(url);
    if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }
    const arrayBuffer = await response.arrayBuffer();
    const bytes = new Uint8Array(arrayBuffer);
    console.log('Fetched', bytes.length, 'bytes');

    // Convert to base64
    let binary = '';
    for (let i = 0; i < bytes.length; i++) {
        binary += String.fromCharCode(bytes[i]);
    }
    return btoa(binary);
}

// Open file picker and return selected file as {name, dataBase64}
// Returns null if cancelled
export function pickFile(acceptTypes) {
    return new Promise((resolve) => {
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = acceptTypes || '*';
        input.style.display = 'none';
        document.body.appendChild(input);

        input.onchange = async () => {
            document.body.removeChild(input);
            if (input.files && input.files.length > 0) {
                const file = input.files[0];
                const arrayBuffer = await file.arrayBuffer();
                const bytes = new Uint8Array(arrayBuffer);

                // Convert to base64
                let binary = '';
                for (let i = 0; i < bytes.length; i++) {
                    binary += String.fromCharCode(bytes[i]);
                }
                const dataBase64 = btoa(binary);

                resolve(JSON.stringify({ name: file.name, dataBase64 }));
            } else {
                resolve(null);
            }
        };

        input.oncancel = () => {
            document.body.removeChild(input);
            resolve(null);
        };

        // Fallback for browsers that don't support oncancel
        const handleBlur = () => {
            setTimeout(() => {
                if (document.body.contains(input)) {
                    document.body.removeChild(input);
                    resolve(null);
                }
            }, 300);
        };
        globalThis.addEventListener('focus', handleBlur, { once: true });

        input.click();
    });
}

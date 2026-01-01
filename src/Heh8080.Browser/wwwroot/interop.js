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

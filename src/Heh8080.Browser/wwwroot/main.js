// Force cache clear on load - unregister service workers and clear caches
async function clearAllCaches() {
    console.log('=== Clearing all caches ===');

    // Unregister all service workers
    if ('serviceWorker' in navigator) {
        const registrations = await navigator.serviceWorker.getRegistrations();
        for (const registration of registrations) {
            await registration.unregister();
            console.log('Unregistered service worker:', registration.scope);
        }
    }

    // Clear all caches
    if ('caches' in window) {
        const cacheNames = await caches.keys();
        for (const cacheName of cacheNames) {
            await caches.delete(cacheName);
            console.log('Deleted cache:', cacheName);
        }
    }

    console.log('=== Cache clearing complete ===');
}

await clearAllCaches();

import { dotnet } from './_framework/dotnet.js'

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const config = dotnetRuntime.getConfig();

await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);

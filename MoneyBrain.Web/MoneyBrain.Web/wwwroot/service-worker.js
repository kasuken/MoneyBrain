// MoneyBrain Service Worker — Install Support Only
// This service worker enables PWA installation (Add to Home Screen)
// without intercepting network requests or caching any resources.
// All network requests are handled normally by the browser.

const CACHE_VERSION = 'moneybrain-v2-icon-fix';

// Install: activate immediately, no precaching
self.addEventListener('install', (event) => {
    console.log('[SW] Installing (no-cache mode)');
    self.skipWaiting();
});

// Activate: clean up any caches from previous versions, then take control
self.addEventListener('activate', (event) => {
    console.log('[SW] Activating (no-cache mode)');
    event.waitUntil(
        caches.keys().then((cacheNames) => {
            return Promise.all(
                cacheNames.map((cacheName) => {
                    console.log('[SW] Deleting cache:', cacheName);
                    return caches.delete(cacheName);
                })
            );
        }).then(() => self.clients.claim())
    );
});

// No fetch event listener — browser handles all requests normally

// Message handler for backward compatibility with pwa.js
self.addEventListener('message', (event) => {
    if (event.data && event.data.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }
});

// MoneyBrain Service Worker - Advanced PWA Implementation
// Version: 1.2.0 - Performance optimized for mobile + iOS popup timing fix

const CACHE_VERSION = 'moneybrain-v1.3';
const RUNTIME_CACHE = 'moneybrain-runtime-v1.2';
const DATA_CACHE = 'moneybrain-data-v1.2';

// Assets to cache on install
const STATIC_ASSETS = [
  '/',
  '/manifest.json',
  '/_framework/blazor.web.js',
  '/offline.html'
];

// Cache strategies
const CACHE_STRATEGIES = {
  NETWORK_FIRST: 'network-first',
  CACHE_FIRST: 'cache-first',
  STALE_WHILE_REVALIDATE: 'stale-while-revalidate',
  NETWORK_ONLY: 'network-only',
  CACHE_ONLY: 'cache-only'
};

// Route patterns and their strategies
const ROUTE_CONFIG = [
  { pattern: /\/_framework\/.*\.wasm$/, strategy: CACHE_STRATEGIES.CACHE_FIRST },
  { pattern: /\/_framework\/.*\.dll$/, strategy: CACHE_STRATEGIES.CACHE_FIRST },
  { pattern: /\/_framework\/.*\.pdb$/, strategy: CACHE_STRATEGIES.CACHE_FIRST },
  { pattern: /\/_framework\/blazor\.boot\.json$/, strategy: CACHE_STRATEGIES.NETWORK_FIRST },
  { pattern: /\/api\/.*/, strategy: CACHE_STRATEGIES.NETWORK_FIRST, cache: DATA_CACHE },
  { pattern: /\.(png|jpg|jpeg|svg|gif|webp)$/, strategy: CACHE_STRATEGIES.CACHE_FIRST },
  { pattern: /\.(woff|woff2|ttf|eot)$/, strategy: CACHE_STRATEGIES.CACHE_FIRST },
  { pattern: /\.css$/, strategy: CACHE_STRATEGIES.STALE_WHILE_REVALIDATE },
  { pattern: /\.js$/, strategy: CACHE_STRATEGIES.STALE_WHILE_REVALIDATE }
];

// Install event - cache static assets
self.addEventListener('install', (event) => {
  console.log('[Service Worker] Installing MoneyBrain PWA...');
  
  event.waitUntil(
    caches.open(CACHE_VERSION)
      .then((cache) => {
        console.log('[Service Worker] Caching static assets');
        
        // Separate critical assets (must cache) from non-critical (can fail gracefully)
        const criticalAssets = [
          '/',
          '/manifest.json',
          '/offline.html'
        ];
        
        const nonCriticalAssets = STATIC_ASSETS.filter(url => !criticalAssets.includes(url));
        
        // Cache critical assets first (blocks installation if fails)
        return cache.addAll(criticalAssets.map(url => new Request(url, { cache: 'no-cache' })))
          .then(() => {
            // Cache non-critical assets in parallel (don't block installation)
            return Promise.all(
              nonCriticalAssets.map(url => 
                cache.add(new Request(url, { cache: 'no-cache' }))
                  .catch(error => {
                    console.warn(`[Service Worker] Failed to cache ${url}:`, error);
                    // Don't fail installation if a non-critical asset fails
                  })
              )
            );
          });
      })
      .then(() => {
        console.log('[Service Worker] Installation complete');
        return self.skipWaiting();
      })
      .catch((error) => {
        console.error('[Service Worker] Installation failed:', error);
      })
  );
});

// Activate event - clean up old caches
self.addEventListener('activate', (event) => {
  console.log('[Service Worker] Activating MoneyBrain PWA...');
  
  event.waitUntil(
    caches.keys()
      .then((cacheNames) => {
        return Promise.all(
          cacheNames
            .filter((name) => {
              return name !== CACHE_VERSION && 
                     name !== RUNTIME_CACHE && 
                     name !== DATA_CACHE;
            })
            .map((name) => {
              console.log('[Service Worker] Deleting old cache:', name);
              return caches.delete(name);
            })
        );
      })
      .then(() => {
        console.log('[Service Worker] Activation complete');
        return self.clients.claim();
      })
  );
});

// Fetch event - smart caching strategies
self.addEventListener('fetch', (event) => {
  const { request } = event;
  const url = new URL(request.url);
  
  // Cache URL parts for efficient pattern matching
  const urlPath = url.pathname;
  const urlPathSearch = urlPath + url.search;

  // Skip non-http(s) requests
  if (!url.protocol.startsWith('http')) {
    return;
  }

  // Skip SignalR and other real-time connections
  if (urlPath.includes('/_blazor') || 
      urlPath.includes('/signalr') ||
      urlPath.includes('/Identity/Account')) {
    return;
  }

  // Find matching strategy (using cached urlPathSearch)
  const routeConfig = ROUTE_CONFIG.find(config => config.pattern.test(urlPathSearch));
  const strategy = routeConfig?.strategy || CACHE_STRATEGIES.NETWORK_FIRST;
  const cacheName = routeConfig?.cache || RUNTIME_CACHE;

  event.respondWith(handleRequest(request, strategy, cacheName));
});

// Request handlers for different strategies
async function handleRequest(request, strategy, cacheName) {
  switch (strategy) {
    case CACHE_STRATEGIES.CACHE_FIRST:
      return cacheFirst(request, cacheName);
    
    case CACHE_STRATEGIES.NETWORK_FIRST:
      return networkFirst(request, cacheName);
    
    case CACHE_STRATEGIES.STALE_WHILE_REVALIDATE:
      return staleWhileRevalidate(request, cacheName);
    
    case CACHE_STRATEGIES.NETWORK_ONLY:
      return fetch(request);
    
    case CACHE_STRATEGIES.CACHE_ONLY:
      return caches.match(request);
    
    default:
      return networkFirst(request, cacheName);
  }
}

// Cache-first strategy
async function cacheFirst(request, cacheName) {
  const cached = await caches.match(request);
  if (cached) {
    return cached;
  }

  try {
    const response = await fetch(request);
    if (response.ok) {
      const cache = await caches.open(cacheName);
      cache.put(request, response.clone());
    }
    return response;
  } catch (error) {
    console.error('[Service Worker] Cache-first fetch failed:', error);
    return getOfflineResponse(request);
  }
}

// Network-first strategy
async function networkFirst(request, cacheName) {
  try {
    const response = await fetch(request);
    if (response.ok) {
      const cache = await caches.open(cacheName);
      cache.put(request, response.clone());
    }
    return response;
  } catch (error) {
    console.warn('[Service Worker] Network fetch failed, trying cache:', error);
    const cached = await caches.match(request);
    if (cached) {
      return cached;
    }
    return getOfflineResponse(request);
  }
}

// Stale-while-revalidate strategy
async function staleWhileRevalidate(request, cacheName) {
  const cached = await caches.match(request);
  
  const fetchPromise = fetch(request)
    .then((response) => {
      if (response.ok) {
        const cache = caches.open(cacheName);
        cache.then(c => c.put(request, response.clone()));
      }
      return response;
    })
    .catch(() => cached);

  return cached || fetchPromise;
}

// Offline response fallback
async function getOfflineResponse(request) {
  const url = new URL(request.url);
  
  // For navigation requests, return offline page
  if (request.mode === 'navigate' || request.headers.get('accept').includes('text/html')) {
    const offlinePage = await caches.match('/offline.html');
    if (offlinePage) {
      return offlinePage;
    }
  }

  // Return a basic offline response
  return new Response('Offline - MoneyBrain is not available', {
    status: 503,
    statusText: 'Service Unavailable',
    headers: new Headers({
      'Content-Type': 'text/plain'
    })
  });
}

// Background sync for offline transactions
self.addEventListener('sync', (event) => {
  if (event.tag === 'sync-transactions') {
    event.waitUntil(syncTransactions());
  }
});

async function syncTransactions() {
  console.log('[Service Worker] Syncing offline transactions...');
  // Implementation would integrate with your transaction service
  // This is a placeholder for future enhancement
}

// Push notifications (for future use)
self.addEventListener('push', (event) => {
  if (!event.data) {
    return;
  }

  const data = event.data.json();
  const title = data.title || 'MoneyBrain';
  const options = {
    body: data.body || 'You have a new notification',
    icon: '/icons/192.png',
    badge: '/icons/72.png',
    vibrate: [200, 100, 200],
    tag: data.tag || 'moneybrain-notification',
    requireInteraction: false,
    data: data.data || {}
  };

  event.waitUntil(
    self.registration.showNotification(title, options)
  );
});

// Notification click handler
self.addEventListener('notificationclick', (event) => {
  event.notification.close();

  event.waitUntil(
    clients.openWindow(event.notification.data.url || '/')
  );
});

// Periodic background sync (for budget reminders, etc.)
self.addEventListener('periodicsync', (event) => {
  if (event.tag === 'budget-check') {
    event.waitUntil(checkBudgets());
  }
});

async function checkBudgets() {
  console.log('[Service Worker] Checking budgets...');
  // Future implementation for budget alerts
}

// Message handling from the app
self.addEventListener('message', (event) => {
  if (event.data && event.data.type === 'SKIP_WAITING') {
    self.skipWaiting();
  }
  
  if (event.data && event.data.type === 'CLEAR_CACHE') {
    event.waitUntil(
      caches.keys().then((cacheNames) => {
        return Promise.all(
          cacheNames.map((cacheName) => caches.delete(cacheName))
        );
      })
    );
  }
});

console.log('[Service Worker] MoneyBrain PWA service worker loaded');

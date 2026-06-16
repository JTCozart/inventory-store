/*
 * Service worker for Inventory Store.
 *
 * Purpose is twofold:
 *   1. Make the main app and the Terminal installable ("Add to Home Screen") by
 *      providing a fetch handler, which browsers require for installability.
 *   2. Cache only static, non-sensitive assets so the app shell loads fast and
 *      survives a flaky connection.
 *
 * It deliberately NEVER caches navigations, API responses, or any authenticated
 * content. Those always go straight to the network so a logged-in user never
 * sees stale or another session's data, and a logged-out user is never served a
 * cached page they should not see.
 */
const CACHE = 'inventory-store-static-v1';

// Same-origin path prefixes that only ever hold static, public assets.
const STATIC_PREFIXES = ['/lib/', '/css/', '/js/', '/img/'];
const STATIC_FILES = ['/favicon.svg', '/favicon.ico'];

function isStaticAsset(url) {
  if (STATIC_FILES.includes(url.pathname)) return true;
  return STATIC_PREFIXES.some(p => url.pathname.startsWith(p));
}

self.addEventListener('install', () => {
  // Take over as soon as installed; nothing to pre-cache up front.
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys()
      .then(keys => Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', (event) => {
  const req = event.request;
  if (req.method !== 'GET') return;

  const url = new URL(req.url);
  if (url.origin !== self.location.origin) return;

  // Only static assets are cached. Everything else (pages, APIs) hits the
  // network untouched so authenticated content is always fresh.
  if (!isStaticAsset(url)) return;

  // Stale-while-revalidate: serve cache instantly, refresh in the background.
  event.respondWith(
    caches.open(CACHE).then(cache =>
      cache.match(req).then(cached => {
        const network = fetch(req).then(res => {
          if (res && res.ok && res.type === 'basic') cache.put(req, res.clone());
          return res;
        }).catch(() => cached);
        return cached || network;
      })
    )
  );
});

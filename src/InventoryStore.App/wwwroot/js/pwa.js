// Registers the service worker so the main app and Terminal are installable
// ("Add to Home Screen") on mobile browsers. No UI: the browser surfaces its
// own install prompt. Served from /sw.js so its scope is the whole origin.
if ('serviceWorker' in navigator) {
  window.addEventListener('load', function () {
    navigator.serviceWorker.register('/sw.js').catch(function () {
      // Registration failures are non-fatal; the app works without it.
    });
  });
}

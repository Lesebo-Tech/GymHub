// ============================================================
// GymHub Service Worker — minimal, just enough to satisfy PWA
// installability criteria. Not doing offline caching yet — that's
// a real future enhancement, not something to fake for a demo.
// ============================================================

self.addEventListener('install', function (event) {
    self.skipWaiting();
});

self.addEventListener('activate', function (event) {
    event.waitUntil(self.clients.claim());
});

// Pass-through fetch handler — required for "installable" criteria
// on most browsers, even though it doesn't do anything special yet.
self.addEventListener('fetch', function (event) {
    // Intentionally not intercepting requests — site behaves exactly
    // as it does in a normal browser tab, just installable as an app.
});
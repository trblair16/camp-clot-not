const CACHE_NAME = 'ccn-shell-v1';
const SHELL_ASSETS = [
    '/offline.html',
    '/app.css',
    '/_content/MudBlazor/MudBlazor.min.css',
    '/_content/MudBlazor/MudBlazor.min.js',
    '/icons/icon-192.png',
    '/icons/icon-512.png'
];
const SKIP_PREFIXES = ['/livehub', '/account/', '/api/'];

self.addEventListener('install', function(event) {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(function(cache) {
                return cache.addAll(SHELL_ASSETS);
            })
            .then(function() { return self.skipWaiting(); })
    );
});

self.addEventListener('activate', function(event) {
    event.waitUntil(
        caches.keys().then(function(keys) {
            return Promise.all(
                keys.filter(function(k) { return k !== CACHE_NAME; })
                    .map(function(k) { return caches.delete(k); })
            );
        }).then(function() { return self.clients.claim(); })
    );
});

self.addEventListener('fetch', function(event) {
    var url = new URL(event.request.url);
    var skip = SKIP_PREFIXES.some(function(p) { return url.pathname.startsWith(p); });
    if (skip || event.request.method !== 'GET') return;

    event.respondWith(
        caches.match(event.request).then(function(cached) {
            return cached || fetch(event.request).catch(function() {
                return caches.match('/offline.html');
            });
        })
    );
});

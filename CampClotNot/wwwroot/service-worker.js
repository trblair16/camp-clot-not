// Static assets are served cache-first, so the app.css version here MUST match the one in
// Pages/_Layout.cshtml, and CACHE_NAME must be bumped whenever it changes. Otherwise installed
// apps keep serving whatever app.css was current when this worker was installed (#326: v9 was
// precached before the app shell existed, so bumping the page to v9 served the stale copy).
const CACHE_NAME = 'ccn-shell-v11';
const SHELL_ASSETS = [
    '/offline.html',
    '/app.css?v=10',
    '/_content/MudBlazor/MudBlazor.min.css',
    '/_content/MudBlazor/MudBlazor.min.js',
    '/icons/icon-192.png?v=3',
    '/icons/icon-512.png?v=3'
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

    if (event.request.mode === 'navigate') {
        event.respondWith(
            fetch(event.request).catch(function() {
                return caches.match('/offline.html');
            })
        );
        return;
    }

    event.respondWith(
        caches.match(event.request).then(function(cached) {
            return cached || fetch(event.request).catch(function() {
                return caches.match('/offline.html');
            });
        })
    );
});

self.addEventListener('push', function(event) {
    var data = { title: 'Camp Clot Not', body: 'New announcement', url: '/hub/announcements' };
    try { data = event.data.json(); } catch(e) {}
    event.waitUntil(
        self.registration.showNotification(data.title, {
            body: data.body,
            icon: '/icons/icon-192.png?v=3',
            badge: '/icons/icon-192.png?v=3',
            data: { url: data.url || '/hub/announcements' },
            vibrate: [200, 100, 200]
        })
    );
});

self.addEventListener('notificationclick', function(event) {
    event.notification.close();
    var url = event.notification.data && event.notification.data.url ? event.notification.data.url : '/hub/announcements';
    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then(function(clientList) {
            for (var i = 0; i < clientList.length; i++) {
                if (clientList[i].url.indexOf(url) !== -1 && 'focus' in clientList[i])
                    return clientList[i].focus();
            }
            if (clients.openWindow) return clients.openWindow(url);
        })
    );
});

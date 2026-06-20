const CACHE_NAME = 'ccn-shell-v4';
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
            icon: '/icons/icon-192.png',
            badge: '/icons/icon-192.png',
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

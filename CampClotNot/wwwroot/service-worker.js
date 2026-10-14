// Our CSS/JS URLs carry a content hash (asp-append-version in _Layout.cshtml), so a deploy
// always means new URLs. This worker goes network-first for same-origin files and only falls
// back to its cache offline, so it can never serve a stale stylesheet (#326). The fetch still
// goes through the browser's HTTP cache, where those hashed files are immutable, so this costs
// no extra downloads. Bump CACHE_NAME only to drop old offline copies.
const CACHE_NAME = 'ccn-shell-v13';
const SHELL_ASSETS = [
    '/offline.html',
    '/icons/icon-192.png?v=3',
    '/icons/icon-512.png?v=3'
];
const SKIP_PREFIXES = ['/livehub', '/account/', '/api/'];
// Same-origin responses worth keeping for offline use.
const OFFLINE_COPY = /\.(css|js|png|webp|svg|woff2?)$/i;

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

    if (url.origin !== self.location.origin) return;   // fonts etc.: let the browser handle them

    event.respondWith(
        fetch(event.request).then(function(response) {
            if (response.ok && OFFLINE_COPY.test(url.pathname)) {
                var copy = response.clone();
                // Keep one offline copy per file: drop older deploys' versions (other ?v= hashes).
                caches.open(CACHE_NAME).then(function(cache) {
                    return cache.keys().then(function(keys) {
                        return Promise.all(keys.filter(function(k) {
                            var u = new URL(k.url);
                            return u.pathname === url.pathname && u.search !== url.search;
                        }).map(function(k) { return cache.delete(k); }));
                    }).then(function() { return cache.put(event.request, copy); });
                });
            }
            return response;
        }).catch(function() {
            // Offline: this exact URL, else the same file from an older deploy, else the offline page.
            return caches.match(event.request)
                .then(function(hit) { return hit || caches.match(event.request, { ignoreSearch: true }); })
                .then(function(hit) { return hit || caches.match('/offline.html'); });
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

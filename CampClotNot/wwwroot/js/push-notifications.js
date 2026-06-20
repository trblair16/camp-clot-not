window.ccnPush = {
    subscribe: async function () {
        if (!('serviceWorker' in navigator) || !('PushManager' in window)) return false;

        var permission = await Notification.requestPermission();
        if (permission !== 'granted') return false;

        var reg = await navigator.serviceWorker.ready;
        var keyResponse = await fetch('/api/vapid-public-key');
        var keyData = await keyResponse.json();

        var sub = await reg.pushManager.subscribe({
            userVisibleOnly: true,
            applicationServerKey: ccnPush._urlBase64ToUint8Array(keyData.key)
        });

        var json = sub.toJSON();
        await fetch('/api/push/subscribe', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                endpoint: json.endpoint,
                p256dh: json.keys.p256dh,
                auth: json.keys.auth
            })
        });
        return true;
    },

    isSubscribed: async function () {
        if (!('serviceWorker' in navigator) || !('PushManager' in window)) return false;
        var reg = await navigator.serviceWorker.ready;
        var sub = await reg.pushManager.getSubscription();
        return sub !== null;
    },

    isSupported: function () {
        return 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
    },

    _urlBase64ToUint8Array: function (base64String) {
        var padding = '='.repeat((4 - base64String.length % 4) % 4);
        var base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
        var rawData = window.atob(base64);
        var outputArray = new Uint8Array(rawData.length);
        for (var i = 0; i < rawData.length; ++i) {
            outputArray[i] = rawData.charCodeAt(i);
        }
        return outputArray;
    }
};

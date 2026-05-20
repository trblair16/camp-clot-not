window.installPrompt = {
    _deferred: null,
    init: function(dotnetRef) {
        var dismissed = localStorage.getItem('ccn_install_dismissed') === '1';
        if (dismissed) return;

        window.addEventListener('beforeinstallprompt', function(e) {
            e.preventDefault();
            window.installPrompt._deferred = e;
            dotnetRef.invokeMethodAsync('ShowAndroidBanner');
        });

        var isIos = /iphone|ipad|ipod/i.test(navigator.userAgent);
        var isStandalone = window.navigator.standalone === true;
        if (isIos && !isStandalone) {
            dotnetRef.invokeMethodAsync('ShowIosBanner');
        }
    },
    trigger: function() {
        if (window.installPrompt._deferred) {
            window.installPrompt._deferred.prompt();
            window.installPrompt._deferred = null;
        }
    },
    dismiss: function() {
        localStorage.setItem('ccn_install_dismissed', '1');
    },
    isDismissed: function() {
        return localStorage.getItem('ccn_install_dismissed') === '1';
    }
};

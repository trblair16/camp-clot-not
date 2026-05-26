window.connectionIndicator = {
    init: function(dotnetRef) {
        function update() {
            dotnetRef.invokeMethodAsync('SetOnline', navigator.onLine);
        }
        window.addEventListener('online', update);
        window.addEventListener('offline', update);
        document.addEventListener('blazor-reconnecting', function() {
            dotnetRef.invokeMethodAsync('SetOnline', false);
        });
        document.addEventListener('blazor-connected', function() {
            dotnetRef.invokeMethodAsync('SetOnline', true);
        });
    }
};

(function (global) {
    function initNotificationHub(options) {
        if (typeof signalR === "undefined") {
            return null;
        }

        const settings = options || {};
        const baseUrl = settings.baseUrl || (typeof API_BASE !== "undefined" ? API_BASE : "https://localhost:7127");
        const rootUrl = baseUrl.replace(/\/api\/?$/, "");
        const hubUrl = `${rootUrl}${settings.hubPath || "/notificationHub"}`;
        const eventName = settings.eventName || "ReceiveUpdate";

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
            .withAutomaticReconnect()
            .configureLogging(settings.logLevel || signalR.LogLevel.Warning)
            .build();

        if (typeof settings.onReceiveUpdate === "function") {
            connection.on(eventName, settings.onReceiveUpdate);
        }

        connection.start()
            .then(function () {
                if (typeof settings.onConnected === "function") {
                    settings.onConnected(hubUrl, connection);
                }
            })
            .catch(function (err) {
                if (typeof settings.onError === "function") {
                    settings.onError(err, hubUrl);
                } else {
                    QcsErrorPresenter.log("SignalR Connection Error", err);
                }
            });

        return connection;
    }

    global.QcsRealtime = {
        initNotificationHub: initNotificationHub
    };
})(window);

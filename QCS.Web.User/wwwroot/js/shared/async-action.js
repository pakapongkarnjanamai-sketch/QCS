(function (global) {
    function notify(message, type, duration) {
        if (!message) {
            return;
        }

        if (typeof DevExpress !== "undefined" && DevExpress.ui && typeof DevExpress.ui.notify === "function") {
            const notifyOptions = {
                message: message,
                type: type || "info",
                displayTime: duration || 2000,
                width: "auto",
                minWidth: 0,
                maxWidth: 480,
                position: {
                    my: "right bottom",
                    at: "right bottom",
                    of: window,
                    offset: "-20 -20"
                },
                stack: "vertical",
                direction: "up-stack",
                autoHideDelay: duration || 2000
            };
            
            DevExpress.ui.notify(notifyOptions);
            return;
        }

        if (type === "error") {
            alert(message);
        }
    }

    function resolveLoader(options) {
        const settings = options || {};
        if (settings.loader) {
            return {
                instance: settings.loader,
                shouldShow: settings.autoShow !== false,
                created: false
            };
        }

        if (settings.inlineLoaderMessage) {
            return {
                instance: QcsLoading.createInlineLoader(
                    settings.inlineLoaderMessage,
                    settings.inlineLoaderTarget,
                    settings.inlineLoaderOptions
                ),
                shouldShow: false,
                created: true
            };
        }

        return {
            instance: null,
            shouldShow: false,
            created: false
        };
    }

    async function run(task, options) {
        const settings = options || {};
        const loaderState = resolveLoader(settings);
        const loader = loaderState.instance;

        try {
            if (loader && loaderState.shouldShow && typeof loader.show === "function") {
                loader.show();
            }

            const result = await task();

            if (settings.successMessage) {
                notify(settings.successMessage, settings.successType || "success", settings.successDuration || 2000);
            }

            if (typeof settings.onSuccess === "function") {
                settings.onSuccess(result);
            }

            return result;
        } catch (error) {
            if (typeof settings.onError === "function") {
                settings.onError(error);
            } else if (settings.errorMessage) {
                const message = typeof settings.errorMessage === "function"
                    ? settings.errorMessage(error)
                    : settings.errorMessage;
                notify(message, settings.errorType || "error", settings.errorDuration || 4000);
            }

            if (settings.rethrow === true) {
                throw error;
            }

            return undefined;
        } finally {
            if (loader && typeof loader.hide === "function") {
                loader.hide();
            }
        }
    }

    global.QcsAsync = {
        notify: notify,
        run: run
    };
})(window);
(function (global) {
    function getMessage(error, fallbackMessage) {
        if (error && error.message) {
            return error.message;
        }

        return fallbackMessage || "An unexpected error occurred.";
    }

    function log(context, error) {
        const label = context || "Error";
        if (typeof console !== "undefined" && typeof console.error === "function") {
            console.error(label + ":", error);
        }
    }

    function notifyError(error, options) {
        const settings = options || {};
        const message = settings.message || getMessage(error, settings.fallbackMessage);
        QcsAsync.notify(message, settings.type || "error", settings.duration || 4000);
        return message;
    }

    function logAndNotify(context, error, options) {
        log(context, error);
        return notifyError(error, options);
    }

    function handlePopupBlocked(error, options) {
        const settings = options || {};
        if (error && error.code === "POPUP_BLOCKED") {
            notifyError(error, {
                message: settings.message || getMessage(error, "Pop-up blocked!"),
                duration: settings.duration || 4000
            });
            return true;
        }

        return false;
    }

    function renderViewerError(containerSelector, error, options) {
        const settings = options || {};
        const message = settings.message || getMessage(error, settings.fallbackMessage);
        const iconClass = settings.iconClass || "fas fa-exclamation-triangle";
        const extraClass = settings.className || "error-message text-danger p-4";

        $(containerSelector).html(`<div class="${extraClass}"><i class="${iconClass}"></i> ${message}</div>`);
        return message;
    }

    global.QcsErrorPresenter = {
        getMessage: getMessage,
        log: log,
        notifyError: notifyError,
        logAndNotify: logAndNotify,
        handlePopupBlocked: handlePopupBlocked,
        renderViewerError: renderViewerError
    };
})(window);
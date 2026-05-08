(function (global) {
    function resolveEndpoint(action, options) {
        const settings = options || {};
        return action === "Approve" ? settings.approveUrl : settings.rejectUrl;
    }

    async function send(action, options) {
        const settings = options || {};
        if (settings.requestId == null || settings.requestId === "") {
            throw new Error(settings.missingRequestIdMessage || "Document ID not found.");
        }

        const endpoint = resolveEndpoint(action, settings);
        const body = settings.bodyFactory
            ? settings.bodyFactory(action, settings)
            : { requestId: settings.requestId, comment: settings.comment };

        const response = await fetch(endpoint, {
            method: settings.method || "POST",
            headers: settings.headers || { "Content-Type": "application/json" },
            body: JSON.stringify(body),
            credentials: settings.credentials || "include"
        });

        if (!response.ok) {
            throw new Error(await response.text());
        }

        return response;
    }

    global.QcsApprovalAction = {
        resolveEndpoint: resolveEndpoint,
        send: send
    };
})(window);
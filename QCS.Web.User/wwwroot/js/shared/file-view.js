(function (global) {
    function createPopupBlockedError(message) {
        const error = new Error(message || "Pop-up blocked!");
        error.code = "POPUP_BLOCKED";
        return error;
    }

    async function fetchBlob(url, options) {
        const settings = options || {};
        const response = await fetch(url, {
            method: settings.method || "GET",
            credentials: settings.credentials || "include",
            headers: settings.headers,
            body: settings.body
        });

        if (!response.ok) {
            throw new Error(settings.errorMessage || "Unable to load file.");
        }

        const blob = await response.blob();
        if (settings.expectedMimeType && blob.type !== settings.expectedMimeType) {
            throw new Error(settings.invalidTypeMessage || "Invalid file type.");
        }

        return blob;
    }

    function createLoadingWindowUrl(options) {
        const settings = options || {};
        const loadingHtml = `
            <div style="text-align:center; padding-top:20px; font-family:Segoe UI, sans-serif;">
                <h3>${settings.heading || "Processing PDF..."}</h3>
                <p>${settings.message || "Please wait..."}</p>
            </div>
        `;

        return URL.createObjectURL(new Blob([loadingHtml], { type: "text/html" }));
    }

    async function openBlobInNewWindow(options) {
        const settings = options || {};
        const loadingUrl = createLoadingWindowUrl(settings.loadingOptions);
        const previewWindow = window.open(loadingUrl, settings.target || "_blank");

        if (!previewWindow) {
            URL.revokeObjectURL(loadingUrl);
            throw createPopupBlockedError(settings.popupBlockedMessage);
        }

        try {
            const blob = settings.blob || await fetchBlob(settings.url, settings.fetchOptions);
            const objectUrl = URL.createObjectURL(blob);
            previewWindow.location.href = objectUrl;
            return {
                blob: blob,
                objectUrl: objectUrl,
                previewWindow: previewWindow
            };
        } catch (error) {
            if (!previewWindow.closed) {
                previewWindow.close();
            }
            throw error;
        } finally {
            URL.revokeObjectURL(loadingUrl);
        }
    }

    function mountPdfEmbed(containerSelector, blob, options) {
        const settings = options || {};
        const $container = $(containerSelector).empty();
        const objectUrl = settings.objectUrl || URL.createObjectURL(blob);

        $("<embed>").attr({
            src: objectUrl,
            type: settings.mimeType || "application/pdf",
            width: settings.width || "100%",
            height: settings.height || "100%"
        }).appendTo($container);

        return {
            objectUrl: objectUrl,
            dispose: function () {
                URL.revokeObjectURL(objectUrl);
            }
        };
    }

    function downloadObjectUrl(objectUrl, fileName) {
        const link = document.createElement("a");
        link.href = objectUrl;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    }

    function sanitizeFileNamePart(value) {
        return (value || "")
            .toString()
            .replace(/[\/\\?%*:|"<>]/g, "-")
            .trim();
    }

    global.QcsFileView = {
        fetchBlob: fetchBlob,
        openBlobInNewWindow: openBlobInNewWindow,
        mountPdfEmbed: mountPdfEmbed,
        downloadObjectUrl: downloadObjectUrl,
        sanitizeFileNamePart: sanitizeFileNamePart,
        createPopupBlockedError: createPopupBlockedError
    };
})(window);
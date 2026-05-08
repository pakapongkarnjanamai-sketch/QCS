(function (global) {
    function getSafeFallbackLoader() {
        return {
            show: function () { },
            hide: function () { }
        };
    }

    function createPageLoadPanel(selector, options) {
        if (typeof DevExpress === "undefined" || !$(selector).length) {
            return getSafeFallbackLoader();
        }

        const defaultOptions = {
            shadingColor: "rgba(0,0,0,0.4)",
            position: { of: "window" },
            visible: false,
            showIndicator: true,
            showPane: true,
            shading: true,
            message: "Loading..."
        };

        return $(selector)
            .dxLoadPanel($.extend(true, {}, defaultOptions, options || {}))
            .dxLoadPanel("instance");
    }

    function createInlineLoader(message, target, options) {
        const resolvedTarget = target || "body";
        if (typeof DevExpress === "undefined") {
            return getSafeFallbackLoader();
        }

        const id = "qcs-inline-loader-" + Math.floor(Math.random() * 1000000);
        const $el = $("<div>").attr("id", id).appendTo(resolvedTarget);
        const defaultOptions = {
            shadingColor: "rgba(255,255,255,0.7)",
            position: { of: resolvedTarget },
            visible: true,
            showIndicator: true,
            showPane: true,
            message: message || "Loading...",
            container: resolvedTarget
        };
        const loadPanel = $el
            .dxLoadPanel($.extend(true, {}, defaultOptions, options || {}))
            .dxLoadPanel("instance");

        return {
            show: function () {
                loadPanel.show();
            },
            hide: function () {
                loadPanel.hide();
                $el.remove();
            }
        };
    }

    global.QcsLoading = {
        createPageLoadPanel: createPageLoadPanel,
        createInlineLoader: createInlineLoader
    };
})(window);

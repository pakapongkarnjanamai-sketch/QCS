(function (global) {
    function normalizeId(value) {
        const parsed = parseInt(value, 10);
        return Number.isNaN(parsed) ? value : parsed;
    }

    function findEnumItem(source, id) {
        if (!Array.isArray(source)) {
            return null;
        }

        return source.find(function (item) {
            return normalizeId(item && (item.Id ?? item.id)) === id;
        }) || null;
    }

    function getDisplayName(source, id, fallbackText) {
        const item = findEnumItem(source, id);
        return item ? (item.DisplayName ?? item.displayName ?? fallbackText) : fallbackText;
    }

    function buildBadgeInfo(id, source, defaultClassMap, options) {
        const settings = options || {};
        const normalizedId = normalizeId(id);
        const classMap = Object.assign({}, defaultClassMap, settings.classMap || {});
        const fallbackText = settings.fallbackText || "Unknown";

        return {
            cls: Object.prototype.hasOwnProperty.call(classMap, normalizedId)
                ? classMap[normalizedId]
                : (settings.defaultClass || "bg-light text-muted border"),
            txt: getDisplayName(source, normalizedId, fallbackText)
        };
    }

    function getWorkflowStepBadgeInfo(stepId, source, options) {
        const normalizedId = normalizeId(stepId);
        return buildBadgeInfo(normalizedId, source, {
            0: "bg-secondary-soft text-dark",
            1: "bg-info-soft text-dark",
            2: "bg-warning-soft text-dark",
            3: "bg-warning-soft text-dark",
            99: "bg-success-soft text-dark",
            "-1": "bg-danger-soft text-dark"
        }, Object.assign({ fallbackText: "Step " + normalizedId }, options));
    }

    function getApprovalStatusBadgeInfo(statusId, source, options) {
        const normalizedId = normalizeId(statusId);
        return buildBadgeInfo(normalizedId, source, {
            0: "bg-secondary-soft text-dark",
            1: "bg-warning-soft text-dark",
            2: "bg-success-soft text-dark",
            3: "bg-info-soft text-dark",
            9: "bg-danger-soft text-dark",
            99: "bg-dark text-white"
        }, Object.assign({ fallbackText: "Step " + normalizedId }, options));
    }

    global.QcsStatusBadge = {
        getWorkflowStepBadgeInfo: getWorkflowStepBadgeInfo,
        getApprovalStatusBadgeInfo: getApprovalStatusBadgeInfo
    };
})(window);

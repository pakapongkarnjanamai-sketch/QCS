(function (global) {
    function getItemId(item) {
        return item && item.options && item.options.elementAttr
            ? item.options.elementAttr.id
            : undefined;
    }

    function findItemIndexById(items, itemId) {
        return (items || []).findIndex(function (item) {
            return getItemId(item) === itemId;
        });
    }

    function setItemVisibility(items, itemId, visible) {
        const index = findItemIndexById(items, itemId);
        if (index >= 0) {
            items[index].visible = visible;
        }

        return items;
    }

    function updateToolbarItems(toolbarInstance, updater) {
        if (!toolbarInstance) {
            return;
        }

        const items = toolbarInstance.option("items") || [];
        if (typeof updater === "function") {
            updater(items);
        }
        toolbarInstance.option("items", items);
    }

    function setToolbarVisibility(toolbarInstance, visibilityMap) {
        updateToolbarItems(toolbarInstance, function (items) {
            Object.keys(visibilityMap || {}).forEach(function (itemId) {
                setItemVisibility(items, itemId, visibilityMap[itemId]);
            });
        });
    }

    global.QcsToolbar = {
        getItemId: getItemId,
        findItemIndexById: findItemIndexById,
        setItemVisibility: setItemVisibility,
        updateToolbarItems: updateToolbarItems,
        setToolbarVisibility: setToolbarVisibility
    };
})(window);
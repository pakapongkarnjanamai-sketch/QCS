(function (global) {
    let actionDialogCounter = 0;

    function getActionOptions(action, overrides) {
        const resolvedOverrides = overrides || {};
        const actionMap = {
            Approve: {
                title: "ยืนยันอนุมัติ (Confirm Approve)",
                buttonType: "success",
                placeholder: "ความคิดเห็นเพิ่มเติม (Optional)...",
                requireComment: false,
                validationMessage: "กรุณาระบุเหตุผล"
            },
            Reject: {
                title: "ระบุเหตุผลไม่อนุมัติ (Reject Reason)",
                buttonType: "danger",
                placeholder: "ต้องระบุเหตุผล (Required)...",
                requireComment: true,
                validationMessage: "กรุณาระบุเหตุผล"
            },
            Submit: {
                title: "ยืนยันส่งอนุมัติ (Confirm Submit)",
                buttonType: "default",
                placeholder: "ความคิดเห็นเพิ่มเติม (Optional)...",
                requireComment: false,
                validationMessage: "กรุณาระบุเหตุผล"
            },
            Default: {
                title: "ยืนยัน (Confirm)",
                buttonType: "default",
                placeholder: "ความคิดเห็นเพิ่มเติม (Optional)...",
                requireComment: false,
                validationMessage: "กรุณาระบุเหตุผล"
            }
        };

        return Object.assign({}, actionMap.Default, actionMap[action] || {}, resolvedOverrides);
    }

    function getActionButtonOptions(action, overrides) {
        // Use centralized button defaults
        if (typeof QcsButtonDefaults !== 'undefined' && QcsButtonDefaults.getCommonButtonOptions) {
            const variantMap = {
                Approve: "approve",
                Reject: "reject",
                Submit: "submit",
                Default: "primary"
            };
            const variant = variantMap[action] || variantMap.Default;
            return QcsButtonDefaults.getCommonButtonOptions(variant, overrides || {});
        }
        
        // Fallback (legacy support)
        const resolvedOverrides = overrides || {};
        const actionMap = {
            Approve: {
                text: "อนุมัติ (Approve)",
                type: "success",
                stylingMode: "contained",
                minWidth: 100,
                height: 42
            },
            Reject: {
                text: "ไม่อนุมัติ (Reject)",
                type: "danger",
                stylingMode: "contained",
                minWidth: 100,
                height: 42
            },
            Submit: {
                text: "ส่งอนุมัติ (Submit)",
                type: "default",
                stylingMode: "contained",
                minWidth: 100,
                height: 42
            },
            Default: {
                stylingMode: "contained",
                minWidth: 100,
                height: 42
            }
        };

        return Object.assign({}, actionMap.Default, actionMap[action] || {}, resolvedOverrides);
    }

    function createToolbarButtonItem(action, options) {
        const settings = options || {};
        const buttonOptions = Object.assign({}, settings.buttonOptions || {});

        return {
            location: settings.location || "after",
            widget: "dxButton",
            visible: settings.visible !== false,
            options: getActionButtonOptions(action, buttonOptions)
        };
    }

    function renderActionButtons(containerSelector, actions) {
        const $container = $(containerSelector).empty();

        (actions || []).forEach(function (actionConfig) {
            const config = actionConfig || {};
            const host = $("<div>").appendTo($container);

            host.dxButton(getActionButtonOptions(config.action, config.buttonOptions));

            if (config.hostClass) {
                host.addClass(config.hostClass);
            }
        });

        return $container;
    }

    function create(options) {
        const settings = options || {};
        const rootSelector = settings.rootSelector;
        actionDialogCounter += 1;

        const textAreaId = `qcs-action-reason-${actionDialogCounter}`;
        const confirmButtonId = `qcs-action-confirm-${actionDialogCounter}`;

        const popupInstance = $(rootSelector).dxPopup({
            width: settings.width || 400,
            height: settings.height || "auto",
            visible: false,
            dragEnabled: settings.dragEnabled !== false,
            hideOnOutsideClick: settings.hideOnOutsideClick === true,
            showCloseButton: settings.showCloseButton !== false,
            title: settings.defaultTitle || "Action",
            contentTemplate: function () {
                const container = $("<div>").addClass("p-2");
                $("<div>")
                    .attr("id", textAreaId)
                    .appendTo(container)
                    .dxTextArea({
                        height: settings.textAreaHeight || 100,
                        value: "",
                        stylingMode: "outlined"
                    });
                const btnGroup = $("<div>")
                    .addClass("mt-3 d-flex justify-content-end gap-2")
                    .appendTo(container);
                $("<div>")
                    .appendTo(btnGroup)
                    .dxButton({
                        text: settings.cancelText || "ยกเลิก (Cancel)",
                        stylingMode: "text",
                        onClick: function () {
                            popupInstance.hide();
                        }
                    });
                $("<div>")
                    .attr("id", confirmButtonId)
                    .appendTo(btnGroup)
                    .dxButton(Object.assign(
                        { text: settings.confirmText || "ยืนยัน (Confirm)" },
                        QcsButtonDefaults && QcsButtonDefaults.getCommonButtonOptions
                            ? QcsButtonDefaults.getCommonButtonOptions("primary", {})
                            : { stylingMode: "contained", type: "default", minWidth: 100, height: 42 }
                    ));
                return container;
            }
        }).dxPopup("instance");

        function getTextArea() {
            return $("#" + textAreaId).dxTextArea("instance");
        }

        function getConfirmButton() {
            return $("#" + confirmButtonId).dxButton("instance");
        }

        function open(dialogOptions) {
            const resolvedOptions = dialogOptions || {};
            popupInstance.option("title", resolvedOptions.title || settings.defaultTitle || "Action");
            popupInstance.show().done(function () {
                const textArea = getTextArea();
                const confirmButton = getConfirmButton();

                if (textArea) {
                    textArea.reset();
                    textArea.option("placeholder", resolvedOptions.placeholder || "");
                    setTimeout(function () {
                        textArea.focus();
                    }, 100);
                }

                if (confirmButton) {
                    confirmButton.option({
                        type: resolvedOptions.buttonType || "default",
                        onClick: function () {
                            const comment = textArea ? textArea.option("value") : "";
                            if (resolvedOptions.requireComment && !comment) {
                                QcsAsync.notify(resolvedOptions.validationMessage || "กรุณาระบุเหตุผล", "error", 2000);
                                if (textArea) {
                                    textArea.focus();
                                }
                                return;
                            }

                            popupInstance.hide();
                            if (typeof resolvedOptions.onConfirm === "function") {
                                resolvedOptions.onConfirm(comment);
                            }
                        }
                    });
                }
            });
        }

        function openAction(action, dialogOptions) {
            const resolvedOptions = getActionOptions(action, dialogOptions);
            open(resolvedOptions);
        }

        return {
            open: open,
            openAction: openAction,
            hide: function () {
                popupInstance.hide();
            },
            instance: popupInstance
        };
    }

    global.QcsActionDialog = {
        create: create,
        getActionOptions: getActionOptions,
        getActionButtonOptions: getActionButtonOptions,
        createToolbarButtonItem: createToolbarButtonItem,
        renderActionButtons: renderActionButtons
    };
})(window);

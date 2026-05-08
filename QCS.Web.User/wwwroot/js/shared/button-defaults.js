(function (global) {
    const BUTTON_VARIANTS = {
        primary: {
            stylingMode: "contained",
            type: "default",
            minWidth: 100,
            height: 42,
            fontSize: 13,
            fontWeight: 600
        },
        secondary: {
            stylingMode: "outlined",
            type: "default",
            minWidth: 80,
            height: 42,
            fontSize: 13
        },
        ghost: {
            stylingMode: "text",
            type: "default",
            minWidth: 60,
            height: 42,
            fontSize: 13
        },
        approve: {
            stylingMode: "contained",
            type: "success",
            minWidth: 100,
            height: 42,
            fontSize: 13,
            fontWeight: 600,
            text: "อนุมัติ (Approve)"
        },
        reject: {
            stylingMode: "contained",
            type: "danger",
            minWidth: 100,
            height: 42,
            fontSize: 13,
            fontWeight: 600,
            text: "ไม่อนุมัติ (Reject)"
        },
        submit: {
            stylingMode: "contained",
            type: "default",
            minWidth: 100,
            height: 42,
            fontSize: 13,
            fontWeight: 600,
            text: "ส่งอนุมัติ (Submit)"
        }
    };

    function getCommonButtonOptions(variant, overrides) {
        const base = BUTTON_VARIANTS[variant] || BUTTON_VARIANTS.primary;
        return Object.assign({}, base, overrides || {});
    }

    global.QcsButtonDefaults = {
        getCommonButtonOptions: getCommonButtonOptions,
        VARIANTS: BUTTON_VARIANTS
    };
})(window);

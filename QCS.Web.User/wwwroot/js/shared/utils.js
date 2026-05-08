(function (global) {
    function formatDateTime(dateStr) {
        if (!dateStr) {
            return "-";
        }

        const date = new Date(dateStr);
        if (Number.isNaN(date.getTime())) {
            return "-";
        }

        const pad = function (value) {
            return value.toString().padStart(2, '0');
        };

        return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
    }

    function formatSize(bytes) {
        if (!bytes) {
            return "";
        }

        const units = ["B", "KB", "MB", "GB"];
        const index = Math.floor(Math.log(bytes) / Math.log(1024));
        return (bytes / Math.pow(1024, index)).toFixed(1) + " " + units[index];
    }

    global.QcsUiUtils = {
        formatDateTime: formatDateTime,
        formatSize: formatSize
    };
})(window);

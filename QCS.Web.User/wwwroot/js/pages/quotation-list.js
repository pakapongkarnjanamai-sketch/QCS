(function (global) {
    $(function () {
        if (!$("#gridContainer").length) {
            return;
        }

        const config = global.QcsPageConfig || {};
        const detailUrl = config.detailUrl;
        const workflowStepSource = QcsRequestGrid.normalizeEnumSource(config.workflowStepSource || []);
        const columns = QcsRequestGrid.createRequestColumns({
            workflowStepsSource: workflowStepSource,
            targetUrl: detailUrl,
            openInNewWindow: true,
            paramField: "code",
            includeRequester: true,
            includeRemark: false,
            titleCaption: "หัวข้อ/รายการ (Title/Item)",
            statusCaption: "สถานะ (Status)",
            statusWidth: 150,
            buttonHint: "ดูรายละเอียด (View Details)",
            codeSortOrder: "desc"
        });
        const grid = $("#gridContainer").dxDataGrid(QcsRequestGrid.createGridOptions({
            store: QcsRequestGrid.createRequestStore("Approved"),
            columns: columns,
            placeholder: "ค้นหาใบเสนอราคา... (Search quotation...)",
            searchWidth: 350,
            targetUrl: detailUrl,
            openInNewWindow: true,
            paramField: "code"
        })).dxDataGrid("instance");

        QcsRealtime.initNotificationHub({
            logLevel: signalR.LogLevel.Warning,
            onReceiveUpdate: function (message) {
                if (grid) {
                    grid.refresh();
                }
            }
        });
    });
})(window);

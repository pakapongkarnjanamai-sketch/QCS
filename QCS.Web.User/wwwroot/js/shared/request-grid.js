(function (global) {
    function normalizeEnumSource(source) {
        return (source || []).map(function (item) {
            return {
                id: item.id ?? item.Id,
                displayName: item.displayName ?? item.DisplayName
            };
        });
    }

    function navigateToDocument(paramValue, baseUrl, openInNewWindow) {
        const url = `${baseUrl}/${encodeURIComponent(paramValue)}`;
        if (openInNewWindow) {
            window.open(url, "_blank");
        } else {
            window.location.href = url;
        }
    }

    function createRequestStore(actionName, options) {
        const settings = options || {};
        return DevExpress.data.AspNet.createStore({
            key: settings.key || "id",
            loadUrl: `${settings.baseUrl || API_BASE}/${settings.controller || "Request"}/${actionName}`,
            onBeforeSend: function (method, ajaxOptions) {
                ajaxOptions.xhrFields = { withCredentials: true };
                if (typeof settings.onBeforeSend === "function") {
                    settings.onBeforeSend(method, ajaxOptions);
                }
            }
        });
    }

    function createGridOptions(options) {
        const settings = options || {};
        const paramField = settings.paramField || "id";

        return {
            dataSource: {
                store: settings.store,
                sort: settings.sort || [{ selector: "code", desc: true }]
            },
            remoteOperations: settings.remoteOperations !== false,
            height: settings.height || "100%",
            showBorders: settings.showBorders !== false,
            rowAlternationEnabled: settings.rowAlternationEnabled !== false,
            hoverStateEnabled: settings.hoverStateEnabled !== false,
            columnAutoWidth: settings.columnAutoWidth === true,
            allowColumnResizing: settings.allowColumnResizing !== false,
            dateSerializationFormat: settings.dateSerializationFormat || "yyyy-MM-ddTHH:mm:ss",
            filterRow: settings.filterRow || { visible: true },
            headerFilter: settings.headerFilter || { visible: true },
            scrolling: settings.scrolling || { mode: "virtual" },
            paging: settings.paging || { pageSize: 20 },
            searchPanel: settings.searchPanel || {
                visible: true,
                width: settings.searchWidth || 300,
                placeholder: settings.placeholder || "ค้นหาเอกสาร... (Search document...)"
            },
            noDataText: settings.noDataText || "ไม่มีข้อมูล (No data)",
            columns: settings.columns || [],
            onContentReady: settings.onContentReady,
            onDataErrorOccurred: settings.onDataErrorOccurred,
            onRowDblClick: settings.targetUrl
                ? function (e) {
                    navigateToDocument(e.data[paramField], settings.targetUrl, settings.openInNewWindow === true);
                }
                : undefined
        };
    }

    function createRequestColumns(options) {
        const settings = options || {};
        const paramField = settings.paramField || "id";
        const workflowStepsSource = normalizeEnumSource(settings.workflowStepsSource);
        const statusBadgeOptions = settings.statusBadgeOptions || {};

        const columns = [
            {
                dataField: "code",
                caption: settings.codeCaption || "เลขที่เอกสาร (Document No.)",
                width: settings.codeWidth || 150,
                sortOrder: settings.codeSortOrder,
                cellTemplate: function (container, cellOptions) {
                    const displayText = cellOptions.value || settings.emptyCodeText || "(Draft)";
                    const value = cellOptions.data[paramField];
                    const url = settings.targetUrl ? `${settings.targetUrl}/${encodeURIComponent(value)}` : "#";
                    
                    $("<a>")
                        .addClass("doc-link")
                        .text(displayText)
                        .attr("href", url)
                        .attr("target", settings.openInNewWindow === true ? "_blank" : "_self")
                        .on("click", function (e) {
                            e.stopPropagation(); // Prevent row click from firing twice
                        })
                        .appendTo(container);
                }
            },
            {
                dataField: "title",
                caption: settings.titleCaption || "หัวข้อ (Title)",
                minWidth: settings.titleMinWidth || 150
            }
        ];

        if (settings.includeRequester) {
            columns.push({
                dataField: "requesterName",
                caption: settings.requesterCaption || "ผู้ขอ (Requester)",
                width: settings.requesterWidth || 180
            });
        }

        if (settings.includeRemark !== false) {
            columns.push({
                dataField: "remark",
                caption: settings.remarkCaption || "หมายเหตุ (Remark)",
                minWidth: settings.remarkMinWidth || 150
            });
        }

        columns.push(
            {
                dataField: "vendorName",
                caption: settings.vendorCaption || "ผู้ขาย (Vendor)",
                width: settings.vendorWidth || 320,
                calculateDisplayValue: function (rowData) {
                    return rowData.vendorCode
                        ? `${rowData.vendorCode} : ${rowData.vendorName}`
                        : rowData.vendorName;
                }
            },
            {
                dataField: "requestDate",
                caption: settings.requestDateCaption || "วันที่ขอ (Request Date)",
                dataType: "date",
                format: settings.requestDateFormat || "yyyy-MM-dd",
                width: settings.requestDateWidth || 120,
                alignment: "center"
            },
            {
                dataField: "currentStepId",
                caption: settings.statusCaption || "สถานะ (Status)",
                width: settings.statusWidth || 180,
                alignment: "center",
               
                lookup: {
                    dataSource: workflowStepsSource,
                    valueExpr: "id",
                    displayExpr: "displayName"
                },
                cellTemplate: function (container, cellOptions) {
                    const info = QcsStatusBadge.getWorkflowStepBadgeInfo(cellOptions.value, workflowStepsSource, statusBadgeOptions);
                    $("<span>")
                        .addClass("status-badge " + info.cls)
                        .text(info.txt)
                        .appendTo(container);
                }
            },
            {
                type: "buttons",
                width: settings.buttonWidth || 70,
                fixed: true,
                fixedPosition: "right",
                buttons: [{
                    hint: settings.buttonHint || "เปิดดู (View)",
                    icon: settings.buttonIcon || "find",
                    onClick: function (e) {
                        navigateToDocument(e.row.data[paramField], settings.targetUrl, settings.openInNewWindow === true);
                    }
                }]
            }
        );

        return columns;
    }

    global.QcsRequestGrid = {
        normalizeEnumSource: normalizeEnumSource,
        navigateToDocument: navigateToDocument,
        createRequestStore: createRequestStore,
        createGridOptions: createGridOptions,
        createRequestColumns: createRequestColumns
    };
})(window);

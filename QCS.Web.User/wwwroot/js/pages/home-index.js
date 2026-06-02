(function (global) {
    $(function () {
        if (!$("#workspaceGrid").length) {
            return;
        }

        const config = global.QcsPageConfig || {};
        const formUrl = config.formUrl;
        const codeUrl = config.codeUrl;
        const workflowStepsSource = QcsRequestGrid.normalizeEnumSource(config.workflowStepsSource || []);
        const myFiltersContainer = $("#workspaceMyFilters");
        const allFiltersContainer = $("#workspaceAllFilters");
        const gridElement = $("#workspaceGrid");
        const gridWrapper = $("#workspaceGridWrapper");
        const emptyState = $("#emptyState");
        const emptyStateTitle = $("#emptyStateTitle");
        const emptyStateButtonText = $("#emptyStateButtonText");
        const emptyStateButtonIcon = $("#emptyStateButtonIcon");
        const titleElement = $("#workspaceTitle");
        const descriptionElement = $("#workspaceDescription");

        const loadingIndicator = QcsLoading.createPageLoadPanel("#loadingIndicator", {
            message: "กำลังโหลด... (Loading...)"
        });
        let gridInstance = null;
        let activePresetKey = null;
        let taskCount = 0;
        let myRequestsCount = 0;
        let myApprovedCount = 0;
        let myRejectedCount = 0;

        const baseStatusBadgeOptions = {
            classMap: {
                0: "bg-secondary-soft text-secondary"
            }
        };

        const presetTextDefaults = {
            titleCaption: "หัวข้อ/รายการ (Title/Item)",
            statusCaption: "สถานะ (Status)",
            buttonHint: "ดูรายละเอียด (View Details)",
            emptyStateTitle: "ยังไม่มีข้อมูล (No data)",
            emptyActionLabel: "สร้างใบเสนอราคาใหม่ (Create new quotation)",
            emptyActionIconClass: "fas fa-plus",
            codeSortOrder: "desc"
        };

        function resolvePresetText(preset) {
            return Object.assign({}, presetTextDefaults, preset || {});
        }

        const presetMap = {
            "my-tasks": {
                key: "my-tasks",
                group: "my",
                label: "งานรออนุมัติ (Pending Tasks)",
                iconClass: "fas fa-user-check",
                description: "รายการที่รอการอนุมัติจากคุณ (Items awaiting your approval)",
                emptyStateTitle: "ยังไม่มีงานรออนุมัติ (No pending tasks)",
                emptyActionLabel: "ดูเอกสารของฉัน (View my documents)",
                emptyActionIconClass: "fas fa-folder-open",
                emptyActionView: "my-requests",
                actionName: "MyTasks",
                targetUrl: codeUrl,
                openInNewWindow: true,
                paramField: "code",
                includeRequester: true,
                includeRemark: false,
                titleCaption: "หัวข้อ/รายการ",
                statusCaption: "สถานะ",
                buttonHint: "ดูรายละเอียด",
                placeholder: "ค้นหางานรออนุมัติ... (Search pending tasks...)",
                buttonLabel: "งานรออนุมัติ (Pending)",
                getBadgeCount: function () {
                    return taskCount;
                }
            },
            "my-requests": {
                key: "my-requests",
                group: "my",
                label: "เอกสารของฉัน (My Documents)",
                iconClass: "fas fa-file-circle-plus",
                description: "เอกสารที่คุณสร้างและอยู่ระหว่างดำเนินการ (Your active documents)",
                emptyStateTitle: "ยังไม่มีเอกสารของฉัน (No documents)",
                emptyActionLabel: "สร้างใบเสนอราคาใหม่ (Create new quotation)",
                emptyActionIconClass: "fas fa-plus",
                emptyActionMode: "create",
                actionName: "MyRequests",
                targetUrl: formUrl,
                openInNewWindow: false,
                paramField: "id",
                includeRequester: false,
                placeholder: "ค้นหาเอกสารของฉัน... (Search my documents...)",
                buttonLabel: "เอกสารของฉัน (My Documents)",
                getBadgeCount: function () {
                    return myRequestsCount;
                }
            },
            "my-approved": {
                key: "my-approved",
                group: "my",
                label: "อนุมัติแล้ว (Approved)",
                iconClass: "fas fa-circle-check",
                description: "เอกสารของคุณที่อนุมัติแล้ว (Your approved documents)",
                emptyStateTitle: "ยังไม่มีเอกสารที่อนุมัติแล้ว (No approved documents)",
                emptyActionLabel: "ไปที่เอกสารของฉัน (Go to my documents)",
                emptyActionIconClass: "fas fa-folder-open",
                emptyActionView: "my-requests",
                actionName: "MyApproved",
                targetUrl: formUrl,
                openInNewWindow: false,
                paramField: "id",
                includeRequester: false,
                placeholder: "ค้นหาเอกสารที่อนุมัติแล้ว... (Search approved documents...)",
                buttonLabel: "อนุมัติแล้ว (Approved)",
                getBadgeCount: function () {
                    return myApprovedCount;
                }
            },
            "my-rejected": {
                key: "my-rejected",
                group: "my",
                label: "ไม่อนุมัติ (Rejected)",
                iconClass: "fas fa-ban",
                description: "เอกสารของคุณที่ไม่ผ่านการอนุมัติ (Your rejected documents)",
                emptyStateTitle: "ยังไม่มีเอกสารที่ไม่อนุมัติ (No rejected documents)",
                emptyActionLabel: "ไปที่เอกสารของฉัน (Go to my documents)",
                emptyActionIconClass: "fas fa-folder-open",
                emptyActionView: "my-requests",
                actionName: "Rejected",
                targetUrl: formUrl,
                openInNewWindow: false,
                paramField: "id",
                includeRequester: false,
                placeholder: "ค้นหาเอกสารที่ไม่อนุมัติ... (Search rejected documents...)",
                buttonLabel: "ไม่อนุมัติ (Rejected)",
                getBadgeCount: function () {
                    return myRejectedCount;
                }
            },
            "all-approved": {
                key: "all-approved",
                group: "all",
                label: "ใบเสนอราคาทั้งระบบ (All Quotations)",
                iconClass: "fas fa-file-invoice-dollar",
                description: "รายการใบเสนอราคาที่อนุมัติแล้วทั้งหมด (All approved quotations in system)",
                emptyStateTitle: "ยังไม่มีใบเสนอราคาในระบบ (No quotations in system)",
                emptyActionLabel: "สร้างใบเสนอราคาใหม่ (Create new quotation)",
                emptyActionIconClass: "fas fa-plus",
                emptyActionMode: "create",
                actionName: "Approved",
                targetUrl: codeUrl,
                openInNewWindow: true,
                paramField: "code",
                includeRequester: true,
                includeRemark: false,
                titleCaption: "หัวข้อ/รายการ",
                statusCaption: "สถานะ",
                statusWidth: 150,
                buttonHint: "ดูรายละเอียด",
                codeSortOrder: "desc",
                placeholder: "ค้นหาใบเสนอราคา... (Search quotations...)",
                buttonLabel: "ใบเสนอราคา (Quotations)"
            }
        };

        function buildPresetColumns(preset) {
            const resolvedPreset = resolvePresetText(preset);
            // ตรวจสอบว่าพนักงานไม่มีเอกสารของตัวเองในระบบเลย (เช่น กลุ่มผู้บริหาร/ผู้อนุมัติ)
            const hasNoOwnDocuments = (myRequestsCount === 0 && myApprovedCount === 0 && myRejectedCount === 0);
            
            const defaultFilter = (resolvedPreset.key === "all-approved" && window.CurrentUser && !hasNoOwnDocuments)
                ? window.CurrentUser.fullName
                : null;

            const columns = QcsRequestGrid.createRequestColumns({
                workflowStepsSource: workflowStepsSource,
                targetUrl: resolvedPreset.targetUrl,
                openInNewWindow: resolvedPreset.openInNewWindow,
                paramField: resolvedPreset.paramField,
                includeRequester: resolvedPreset.includeRequester,
                includeRemark: resolvedPreset.includeRemark,
                titleCaption: resolvedPreset.titleCaption,
                statusCaption: resolvedPreset.statusCaption,
                statusWidth: resolvedPreset.statusWidth,
                buttonHint: resolvedPreset.buttonHint,
                codeSortOrder: resolvedPreset.codeSortOrder,
                statusBadgeOptions: baseStatusBadgeOptions,
                defaultRequesterFilter: defaultFilter
            });

            if (resolvedPreset.key === "all-approved") {
                return columns.filter(function (column) {
                    return column && column.dataField !== "currentStepId";
                });
            }

            return columns;
        }

        function createPresetStore(preset) {
            return QcsRequestGrid.createRequestStore(preset.actionName);
        }

        function createRowDblClickHandler(preset) {
            return function (e) {
                QcsRequestGrid.navigateToDocument(
                    e.data[preset.paramField],
                    preset.targetUrl,
                    preset.openInNewWindow === true
                );
            };
        }

        function getGridRowCount() {
            if (!gridInstance) {
                return 0;
            }

            if (typeof gridInstance.totalCount === "function") {
                const totalCount = gridInstance.totalCount();
                if (typeof totalCount === "number") {
                    return totalCount;
                }
            }

            const dataSource = typeof gridInstance.getDataSource === "function"
                ? gridInstance.getDataSource()
                : null;

            if (dataSource) {
                if (typeof dataSource.totalCount === "function") {
                    const totalCount = dataSource.totalCount();
                    if (typeof totalCount === "number") {
                        return totalCount;
                    }
                }

                if (typeof dataSource.items === "function") {
                    return dataSource.items().length;
                }
            }

            return gridInstance.getVisibleRows().length;
        }

        function showGridSurface() {
            gridWrapper.removeClass("d-none");
            emptyState.addClass("d-none");

            if (gridInstance && typeof gridInstance.updateDimensions === "function") {
                gridInstance.updateDimensions();
            }
        }

        function toggleEmptyState() {
            if (!gridInstance) {
                return;
            }

            const hasRows = getGridRowCount() > 0;
            gridWrapper.toggleClass("d-none", !hasRows);
            emptyState.toggleClass("d-none", hasRows);

            if (hasRows && typeof gridInstance.updateDimensions === "function") {
                gridInstance.updateDimensions();
            }
        }

        function updateEmptyState(preset) {
            const resolvedPreset = resolvePresetText(preset);
            const title = resolvedPreset.emptyStateTitle;
            const actionLabel = resolvedPreset.emptyActionLabel;
            const iconClass = resolvedPreset.emptyActionIconClass;

            emptyStateTitle.text(title);
            emptyStateButtonText.text(actionLabel);
            emptyStateButtonIcon.attr("class", iconClass + " me-2");
            emptyState.data("action-mode", resolvedPreset.emptyActionMode || "create");
            emptyState.data("action-view", resolvedPreset.emptyActionView || "");
        }

        function syncViewQuery(viewKey) {
            const url = new URL(window.location.href);
            url.searchParams.set("view", viewKey);
            window.history.replaceState({}, "", url.toString());
        }

        function resolveRequestedPreset() {
            const view = new URLSearchParams(window.location.search).get("view");
            if (view && presetMap[view]) {
                return view;
            }

            return "all-approved";
        }

        function renderFilterButtons() {
            myFiltersContainer.empty();
            allFiltersContainer.empty();

            Object.keys(presetMap).forEach(function (key) {
                const preset = presetMap[key];
                const container = preset.group === "all" ? allFiltersContainer : myFiltersContainer;
                const button = $("<button>")
                    .attr({
                        type: "button",
                        "data-view": preset.key,
                        "aria-pressed": "false"
                    })
                    .addClass("btn btn-sm d-inline-flex align-items-center gap-2 px-3 py-2 workspace-filter-btn")
                    .append($("<i>").addClass(preset.iconClass))
                    .append($("<span>").text(preset.buttonLabel));

                container.append(button);
            });

            containerBindEvents();
            updateFilterState();
        }

        function containerBindEvents() {
            $(".workspace-filter-btn").off("click").on("click", function () {
                const presetKey = $(this).data("view");
                if (presetKey && presetKey !== activePresetKey) {
                    applyPreset(presetKey);
                }
            });
        }

        function getSummaryCount(data, camelCaseKey, pascalCaseKey) {
            if (data && typeof data[camelCaseKey] === "number") {
                return data[camelCaseKey];
            }

            if (data && typeof data[pascalCaseKey] === "number") {
                return data[pascalCaseKey];
            }

            return 0;
        }

        function applyFilterBadge(viewKey, count) {
            const button = $(".workspace-filter-btn[data-view='" + viewKey + "']");
            let badge = button.find(".workspace-filter-badge");

            if (count > 0) {
                if (badge.length === 0) {
                    badge = $("<span>")
                        .addClass("badge rounded-pill workspace-filter-badge bg-danger text-white")
                        .appendTo(button);
                    
                    // Trigger reflow for CSS scale-in animation
                    void badge[0].offsetHeight;
                    badge.addClass("badge-show");
                }
                
                // If count changed, animate text ping
                if (badge.text() !== count.toString()) {
                    badge.text(count);
                    badge.removeClass("badge-pulse");
                    void badge[0].offsetWidth; // trigger reflow
                    badge.addClass("badge-pulse");
                }
            } else if (badge.length > 0) {
                badge.removeClass("badge-show badge-pulse");
                setTimeout(function() {
                    badge.remove();
                }, 200); // Wait for CSS scale-out
            }
        }

        function updateMyFilterBadges() {
            ["my-tasks", "my-requests", "my-approved", "my-rejected"].forEach(function (viewKey) {
                const preset = presetMap[viewKey];
                const count = preset && typeof preset.getBadgeCount === "function"
                    ? preset.getBadgeCount()
                    : 0;

                applyFilterBadge(viewKey, count);
            });
        }

        function updateFilterState() {
            $(".workspace-filter-btn").each(function () {
                const button = $(this);
                const isActive = button.data("view") === activePresetKey;
                button.toggleClass("active", isActive);
                button.attr("aria-pressed", isActive ? "true" : "false");
            });

            updateMyFilterBadges();
        }

        function updateWorkspaceHeader(preset) {
            titleElement.text(preset.label);
            descriptionElement.text(preset.description);
        }

        function checkGridStateModified() {
            if (!gridInstance) {
                $("#btnResetGridState").removeClass("state-modified");
                return;
            }

            var state = gridInstance.state();
            var modified = false;
            var filteredFields = {};

            if (state && state.columns) {
                for (var i = 0; i < state.columns.length; i++) {
                    var col = state.columns[i];
                    var hasFilter = false;

                    if (col.filterValue !== undefined && col.filterValue !== null && col.filterValue !== "") {
                        hasFilter = true;
                    }
                    if (col.filterValues && col.filterValues.length > 0) {
                        hasFilter = true;
                    }

                    if (hasFilter) {
                        modified = true;
                        filteredFields[col.dataField] = true;
                    }

                    if (col.sortOrder && col.dataField !== "code") {
                        modified = true;
                    }
                    if (col.dataField === "code" && col.sortOrder === "asc") {
                        modified = true;
                    }
                }
            }

            if (state && state.searchText) {
                modified = true;
            }
            if (state && state.filterValue) {
                modified = true;
            }

            $("#btnResetGridState").toggleClass("state-modified", modified);

            // Highlight column headers that have active filters
            var $headers = gridElement.find(".dx-datagrid-headers .dx-header-row td");
            $headers.removeClass("column-filtered");
            var columns = gridInstance.getVisibleColumns();
            for (var j = 0; j < columns.length; j++) {
                if (columns[j].dataField && filteredFields[columns[j].dataField]) {
                    $headers.eq(j).addClass("column-filtered");
                }
            }
        }

        function buildGridOptions(preset) {
            const resolvedPreset = resolvePresetText(preset);
            return QcsRequestGrid.createGridOptions({
                store: createPresetStore(resolvedPreset),
                columns: buildPresetColumns(resolvedPreset),
                placeholder: resolvedPreset.placeholder,
                searchWidth: 350,
                searchPanel: { visible: false },
                targetUrl: resolvedPreset.targetUrl,
                openInNewWindow: resolvedPreset.openInNewWindow,
                paramField: resolvedPreset.paramField,
                noDataText: resolvedPreset.emptyStateTitle,
                stateStoring: {
                    enabled: true,
                    type: "localStorage",
                    storageKey: "qcs_workspace_grid_" + resolvedPreset.key
                },
                onContentReady: function (e) {
                    gridInstance = e.component;
                    toggleEmptyState();
                    checkGridStateModified();
                },
                onOptionChanged: function (e) {
                    if (e.name === "columns" || e.name === "filterValue" || e.name === "searchText") {
                        checkGridStateModified();
                    }
                },
                onDataErrorOccurred: function (e) {
                    QcsErrorPresenter.logAndNotify("Grid error", e.error, {
                        fallbackMessage: "โหลดข้อมูลล้มเหลว (Failed to load data)"
                    });
                }
            });
        }

        function renderGrid(preset) {
            const options = buildGridOptions(preset);
            gridElement.dxDataGrid(options);
            gridInstance = gridElement.dxDataGrid("instance");
        }

        function applyPreset(presetKey) {
            const preset = resolvePresetText(presetMap[presetKey] || presetMap[resolveRequestedPreset()]);

            // Save current grid state before switching so it persists per-view
            if (gridInstance && activePresetKey) {
                var oldKey = "qcs_workspace_grid_" + activePresetKey;
                var currentState = gridInstance.state();
                if (currentState) {
                    localStorage.setItem(oldKey, JSON.stringify(currentState));
                }
            }

            activePresetKey = preset.key;

            updateWorkspaceHeader(preset);
            updateEmptyState(preset);
            updateFilterState();
            syncViewQuery(preset.key);
            showGridSurface();

            if (!gridInstance) {
                renderGrid(preset);
                return;
            }

            var newStorageKey = "qcs_workspace_grid_" + preset.key;

            gridInstance.beginUpdate();
            gridInstance.option("stateStoring.storageKey", newStorageKey);
            gridInstance.option("columns", buildPresetColumns(preset));
            gridInstance.option("dataSource", {
                store: createPresetStore(preset),
                sort: [{ selector: "code", desc: true }]
            });
            gridInstance.option("searchPanel.placeholder", preset.placeholder);
            gridInstance.option("noDataText", preset.emptyStateTitle);
            gridInstance.option("onRowDblClick", createRowDblClickHandler(preset));
            gridInstance.endUpdate();

            // Restore saved state for this view (column filters, sort, search)
            var savedRaw = localStorage.getItem(newStorageKey);
            if (savedRaw) {
                try {
                    gridInstance.state(JSON.parse(savedRaw));
                } catch (_) { /* ignore corrupt state */ }
            }

            gridInstance.refresh().done(toggleEmptyState);
        }

        function fetchSummary() {
            return $.ajax({
                url: `${API_BASE}/Dashboard/Summary`,
                method: "GET",
                xhrFields: { withCredentials: true }
            });
        }

        function refreshActiveData() {
            QcsAsync.run(fetchSummary, {
                loader: loadingIndicator,
                autoShow: false,
                onSuccess: function (data) {
                    taskCount = getSummaryCount(data, "myTaskCount", "MyTaskCount");
                    myRequestsCount = getSummaryCount(data, "myRequestCount", "MyRequestCount");
                    myApprovedCount = getSummaryCount(data, "myApprovedCount", "MyApprovedCount");
                    myRejectedCount = getSummaryCount(data, "myRejectedCount", "MyRejectedCount");
                    updateFilterState();
                },
                onError: function (error) {
                    QcsErrorPresenter.log("Workspace summary refresh failed", error);
                }
            });

            if (gridInstance) {
                gridInstance.refresh().done(toggleEmptyState);
            }
        }

        function initializeWorkspace() {
            renderFilterButtons();

            QcsAsync.run(fetchSummary, {
                loader: loadingIndicator,
                onSuccess: function (data) {
                    taskCount = getSummaryCount(data, "myTaskCount", "MyTaskCount");
                    myRequestsCount = getSummaryCount(data, "myRequestCount", "MyRequestCount");
                    myApprovedCount = getSummaryCount(data, "myApprovedCount", "MyApprovedCount");
                    myRejectedCount = getSummaryCount(data, "myRejectedCount", "MyRejectedCount");
                    const initialPreset = resolveRequestedPreset();
                    updateFilterState();
                    applyPreset(initialPreset);
                },
                onError: function (error) {
                    taskCount = 0;
                    myApprovedCount = 0;
                    myRejectedCount = 0;
                    updateFilterState();
                    QcsErrorPresenter.logAndNotify("Summary load error", error, {
                        fallbackMessage: "โหลดข้อมูลล้มเหลว (Failed to load data)"
                    });
                    applyPreset(resolveRequestedPreset());
                }
            });
        }

        QcsRealtime.initNotificationHub({
            logLevel: signalR.LogLevel.Information,
            onReceiveUpdate: function (message) {
                refreshActiveData();
            },
            onConnected: function () {
            }
        });

        initializeWorkspace();
        $("#btnCreateHeader, #btnCreateEmpty").on("click", function () {
            const actionMode = emptyState.data("action-mode");
            const actionView = emptyState.data("action-view");

            if (this.id === "btnCreateHeader" || actionMode === "create") {
                window.location.href = formUrl;
                return;
            }

            if (actionView && presetMap[actionView]) {
                applyPreset(actionView);
                return;
            }

            window.location.href = formUrl;
        });

        $("#btnResetGridState").on("click", function () {
            // Remove state from localStorage for all presets
            Object.keys(presetMap).forEach(function (key) {
                localStorage.removeItem("qcs_workspace_grid_" + key);
            });

            if (gridInstance) {
                // Clear saved state then re-apply preset columns (which include default sort)
                gridInstance.state(null);
                var preset = resolvePresetText(presetMap[activePresetKey]);
                gridInstance.option("columns", buildPresetColumns(preset));
            }

            if (typeof QcsAsync !== "undefined" && QcsAsync.notify) {
                QcsAsync.notify("รีเซ็ตสถานะตารางเรียบร้อยแล้ว (Grid state reset)", "success", 3000);
            }

            $("#btnResetGridState").removeClass("state-modified");
        });
    });
})(window);

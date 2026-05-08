(function (global) {
    function getApproverText(step) {
        const name = step && step.approverName;
        return name
            ? (step.approverNId ? `${name} (${step.approverNId})` : name)
            : "-";
    }

    function mapRouteSteps(routeData) {
        return routeData && routeData.steps
            ? routeData.steps.map(function (step) {
                return {
                    sequenceNo: step.sequenceNo,
                    stepName: step.stepName,
                    rawAssignments: step.assignments,
                    status: step.status || 0,
                    approverName: step.approverName,
                    approverNId: step.approverNId,
                    actionDate: step.actionDate,
                    comment: step.comment
                };
            })
            : [];
    }

    function renderStatusBadge(container, status, getStatusBadgeInfo) {
        const badge = typeof getStatusBadgeInfo === "function"
            ? getStatusBadgeInfo(status)
            : { cls: "bg-secondary-soft", txt: status || "-" };

        $("<span>")
            .addClass("status-badge " + badge.cls)
            .text(badge.txt)
            .appendTo(container);
    }

    function createRouteGrid(selector, options) {
        const settings = options || {};
        const gridData = mapRouteSteps(settings.routeData);

        return $(selector).dxDataGrid({
            dataSource: gridData,
            showBorders: true,
            showRowLines: true,
            columnAutoWidth: false,
            wordWrapEnabled: true,
            paging: { enabled: false },
            sorting: { mode: "none" },
            onRowPrepared: function (e) {
                if (e.rowType === "data" && Number(e.data.sequenceNo) === Number(settings.currentStepId)) {
                    e.rowElement.addClass(settings.currentRowClass || "row-current-step");
                }
            },
            columns: [
                { dataField: "sequenceNo", caption: settings.sequenceCaption || "#", width: 40, alignment: "center", sortOrder: "asc", allowSorting: false, fixed: true, fixedPosition: "left" },
                { dataField: "stepName", caption: settings.stepCaption || "ขั้นตอน (Step)", width: 130, fixed: true, fixedPosition: "left"},
                {
                    dataField: "assignees",
                    caption: settings.assigneeCaption || "ผู้มีสิทธิ์อนุมัติ (Assignees)",
                    minWidth: 240, 
                    fixed: true, fixedPosition: "left",
                    cellTemplate: function (container, cellOptions) {
                        const list = cellOptions.data.rawAssignments || [];
                        if (list.length === 0) {
                            $("<span>").text("-").appendTo(container);
                            return;
                        }

                        list.forEach(function (assignment) {
                            const $icon = $("<i>").attr("aria-hidden", "true");
                            const $wrapper = $("<span>");
                            if (assignment.isCurrentUser) {
                                $wrapper.addClass("current-user-row");
                                $icon.addClass("fas fa-user-circle me-1");
                                $wrapper.append($icon).append(document.createTextNode(` ${assignment.employeeName} (You)`));
                            } else {
                                $wrapper.addClass("text-muted");
                                $icon.addClass("fas fa-user me-1");
                                $wrapper.append($icon).append(document.createTextNode(` ${assignment.employeeName}`));
                            }
                            $("<div>").append($wrapper).appendTo(container);
                        });
                    }
                },
                {
                    dataField: "status",
                    caption: settings.statusCaption || "สถานะ (Status)",
                    width: settings.statusWidth || 130,
                    alignment: "center", 
                    fixed: true, fixedPosition: "left",
                    cellTemplate: function (container, cellOptions) {
                        renderStatusBadge(container, cellOptions.value, settings.getStatusBadgeInfo);
                    }
                },
                {
                    dataField: "approverName",
                    caption: settings.approverCaption || "ผู้ดำเนินการ (Approver)",
                    minWidth: 150,
                    cellTemplate: function (container, cellOptions) {
                        $("<span>")
                            .text(getApproverText(cellOptions.data))
                            .appendTo(container);
                    }
                },
                {
                    dataField: "actionDate",
                    caption: settings.actionDateCaption || "วันที่ (Date)",
                    width: settings.actionDateWidth || 150,
                    alignment: "center",
                    customizeText: function (cellInfo) {
                        return typeof settings.formatDateTime === "function"
                            ? settings.formatDateTime(cellInfo.value)
                            : (cellInfo.value || "-");
                    }
                },
                {
                    dataField: "comment",
                    caption: settings.commentCaption || "ความเห็น (Remark)",
                    minWidth: 150,
                    customizeText: function (cellInfo) {
                        return cellInfo.value || "-";
                    }
                }
            ]
        }).dxDataGrid("instance");
    }

    function openHistoryPopup(selector, options) {
        const settings = options || {};
        const historyData = settings.steps || [];
        const isMobile = $(window).width() < 768;

        return $(selector).dxPopup({
            title: settings.title || "ประวัติการอนุมัติ (Approval History)",
            width: settings.width || (isMobile ? "95%" : 700),
            height: settings.height || (isMobile ? "auto" : 500),
            maxHeight: "90%",
            maxWidth: "100%",
            visible: true,
            dragEnabled: settings.dragEnabled !== false,
            showCloseButton: settings.showCloseButton !== false,
            contentTemplate: function (container) {
                const $timeline = $("<ul>").addClass("qcs-timeline").attr("aria-label", "Approval History Timeline").appendTo(container);

                if (!historyData || historyData.length === 0) {
                    $("<li>").css({ padding: "16px", color: "#6c757d" })
                        .text("ไม่มีข้อมูลประวัติการอนุมัติ (No approval history)")
                        .appendTo($timeline);
                    return;
                }

                const timelineItems = historyData.map(function (step) {
                    const $item = $("<li>").addClass("qcs-timeline-item");
                    
                    let statusClass = "qcs-timeline-status-pending";
                    const statusVal = step.status;
                    if (statusVal === 2) statusClass = "qcs-timeline-status-approved";
                    else if (statusVal === 9) statusClass = "qcs-timeline-status-rejected";
                    else if (statusVal === 1) statusClass = "qcs-timeline-status-current";

                    $item.addClass(statusClass);

                    // Timeline Node
                    $("<div>").addClass("qcs-timeline-node").appendTo($item);

                    const $content = $("<div>").addClass("qcs-timeline-content").appendTo($item);

                    // Step Name and Status Note
                    const badgeInfo = typeof settings.getStatusBadgeInfo === "function"
                        ? settings.getStatusBadgeInfo(step.status)
                        : { txt: step.status || "-" };
                    
                    const titleText = (step.stepName || "ขั้นตอน (Step)") + " (" + badgeInfo.txt + ")";
                    $("<h6>").addClass("qcs-timeline-title").text(titleText).appendTo($content);
                    
                    // Byline: Approver & Date
                    const approverText = getApproverText(step);
                    const actionDateText = typeof settings.formatDateTime === "function" ? settings.formatDateTime(step.actionDate) : step.actionDate;
                    
                    let bylineText = approverText;
                    if (actionDateText) {
                        bylineText += ` • ${actionDateText}`;
                    }

                    $("<div>").addClass("qcs-timeline-byline").text(bylineText).appendTo($content);

                    // Comment box
                    if (step.comment) {
                        $("<div>").addClass("qcs-timeline-comment")
                            .text(step.comment)
                            .appendTo($content);
                    }

                    return $item;
                });

                $timeline.append(timelineItems);
            }
        }).dxPopup("instance").show();
    }

    global.QcsRequestWorkflow = {
        getApproverText: getApproverText,
        mapRouteSteps: mapRouteSteps,
        createRouteGrid: createRouteGrid,
        openHistoryPopup: openHistoryPopup
    };
})(window);
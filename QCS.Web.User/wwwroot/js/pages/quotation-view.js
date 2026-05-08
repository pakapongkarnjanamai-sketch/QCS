(function (global) {
    $(function () {
        if (!$("#pdfViewer").length) {
            return;
        }

        const config = global.QcsPageConfig || {};
        const docCode = config.docCode;
        const documentTypes = config.documentTypes || [];
        const workflowStepSource = config.workflowStepSource || [];
        const approvalStatuses = config.approvalStatuses || [];

        const api = {
            GET_DATA: `${API_BASE}/Quotation/ByCode`,
            VIEW_FILE_QUOTATION: `${API_BASE}/Quotation/ViewFile`,
            VIEW_FILE_REQUEST: `${API_BASE}/Request/ViewFile`,
            APPROVE: `${API_BASE}/Approval/Approve`,
            REJECT: `${API_BASE}/Approval/Reject`
        };

        let currentDocId = null;
        let currentDocData = null;
        let currentViewerObjectUrl = null;
        let isFinalState = false;
        let viewFileApiUrl = "";
        const viewerBlobCache = new Map();
        const actionDialog = QcsActionDialog.create({ rootSelector: "#actionPopup" });

        function clearViewerBlobCache() {
            viewerBlobCache.clear();
        }

        function getApprovalStatusInfo(statusId) {
            return QcsStatusBadge.getApprovalStatusBadgeInfo(statusId, approvalStatuses);
        }

        function getStatusBadgeInfo(statusId) {
            return QcsStatusBadge.getWorkflowStepBadgeInfo(statusId, workflowStepSource, {
                classMap: {
                    0: "bg-secondary-soft",
                    1: "bg-info-soft",
                    2: "bg-warning-soft",
                    3: "bg-warning-soft",
                    99: "bg-success-soft",
                    "-1": "bg-danger-soft"
                }
            });
        }

        function getDocumentTypeText(id) {
            const found = documentTypes.find(function (item) { return item.Id === id; });
            return found ? found.DisplayName : "เอกสารแนบ (Attachment)";
        }

        const formatSize = QcsUiUtils.formatSize;
        const formatDateTime = QcsUiUtils.formatDateTime;

        function showError(msg) {
            QcsErrorPresenter.notifyError({ message: msg }, { message: msg });
        }

        function toggleFullscreen() {
            const el = document.getElementById("pdfViewer");
            if (!el) {
                return;
            }

            if (!document.fullscreenElement) {
                if (el.requestFullscreen) {
                    el.requestFullscreen();
                } else if (el.webkitRequestFullscreen) {
                    el.webkitRequestFullscreen();
                }
            } else if (document.exitFullscreen) {
                document.exitFullscreen();
            }
        }

        function renderHeader(data) {
            const info = getStatusBadgeInfo(data.currentStepId);
            $("#headerStatus").html(`<span class="status-badge ${info.cls}">${info.txt}</span>`);
        }

        function renderForm(data) {
            $("#requestInfoForm").dxForm({
                formData: data,
                readOnly: true,
                labelLocation: "top",
                colCount: 2,
                items: [
                    { dataField: "code", label: { text: "รหัสเอกสาร (Document Code)" }, colSpan: 2 },
                    { dataField: "title", label: { text: "หัวข้อ (Title)" }, colSpan: 2 },
                    { dataField: "vendorCode", label: { text: "รหัสผู้ขาย (Vendor Code)" }, colSpan: 2 },
                    { dataField: "vendorName", label: { text: "ชื่อผู้ขาย (Vendor Name)" }, colSpan: 2 },
                    { dataField: "validFrom", label: { text: "มีผลตั้งแต่ (Valid From)" }, editorType: "dxDateBox", editorOptions: { displayFormat: "yyyy-MM-dd" } },
                    { dataField: "validUntil", label: { text: "หมดอายุ (Valid Until)" }, editorType: "dxDateBox", editorOptions: { displayFormat: "yyyy-MM-dd" } },
                    { dataField: "remark", label: { text: "หมายเหตุ (Remark)" }, colSpan: 2, editorType: "dxTextArea", editorOptions: { autoResizeEnabled: true, minHeight: 60 } }
                ]
            });
        }

        function renderApprovals(steps) {
            const container = $("#approvalList").empty();
            if (!steps || steps.length === 0) {
                container.html('<div class="text-center text-muted small p-3">ยังไม่มีประวัติการอนุมัติ (No approval history)</div>');
                return;
            }

            steps.sort(function (a, b) {
                return (a.sequenceNo || a.sequence) - (b.sequenceNo || b.sequence);
            });

            steps.forEach(function (step) {
                const statusInfo = getApprovalStatusInfo(step.status);

                let dotClass = "";
                switch (parseInt(step.status, 10)) {
                    case 2:
                        dotClass = "completed";
                        break;
                    case 9:
                        dotClass = "rejected";
                        break;
                    case 1:
                        dotClass = "current";
                        break;
                    default:
                        dotClass = "";
                        break;
                }

                let approver = step.approverName;
                if (!approver && step.assignments) {
                    approver = step.assignments.map(function (assignment) { return assignment.employeeName; }).join(", ");
                }
                if (!approver) {
                    approver = "ระบบ/รอดำเนินการ (System/Pending)";
                }

                const html = `
                    <div class="timeline-item">
                        <div class="timeline-dot ${dotClass}"></div>
                        <div class="timeline-content">
                            <div class="timeline-step">ลำดับ (Step) ${step.sequenceNo || step.sequence}: ${step.stepName}</div>
                            <div class="timeline-user">
                                <i class="fas fa-user-circle"></i> ${approver}
                                <span class="badge rounded-pill fw-normal ms-1 ${statusInfo.cls}">${statusInfo.txt}</span>
                            </div>
                            ${step.actionDate ? `<div class="timeline-date"><i class="far fa-clock"></i> ${formatDateTime(step.actionDate)}</div>` : ""}
                            ${step.comment ? `<div class="timeline-comment">"${step.comment}"</div>` : ""}
                        </div>
                    </div>
                `;
                container.append(html);
            });
        }

        function renderDocuments(docs) {
            const container = $("#documentList").empty();
            $("#attachmentCount").text(docs ? docs.length : 0);

            if (!docs || docs.length === 0) {
                container.html('<div class="text-center text-muted small p-3">ไม่มีเอกสารแนบ (No attachments)</div>');
                return;
            }

            docs.forEach(function (doc) {
                const typeName = getDocumentTypeText(doc.documentTypeId);
                const sizeStr = doc.fileSize ? formatSize(doc.fileSize) : "";
                const displayFileName = doc.originalFileName || doc.fileName;

                const item = $(`
                    <div class="file-item" data-id="${doc.id}">
                        <i class="fas fa-file-pdf file-icon"></i>
                        <div class="file-details">
                            <div class="file-name" title="${displayFileName}">${displayFileName}</div>
                            <div class="file-meta">
                                <span class="badge bg-light text-dark border me-1">${typeName}</span>
                                <span>${sizeStr}</span>
                            </div>
                        </div>
                    </div>
                `);

                if (!isFinalState) {
                    item.on("click", function () {
                        loadViewer(doc.id, displayFileName);
                    });
                }

                container.append(item);
            });
        }

        function renderActionButtons(data) {
            const permissions = data.permissions || {};
            const $card = $("#actionCard");

            $card.removeClass("d-none");
            const actions = [];

            if (permissions.canReject) {
                actions.push({
                    action: "Reject",
                    hostClass: "flex-grow-1",
                    buttonOptions: Object.assign({
                        icon: "fas fa-times",
                        width: "100%",
                        elementAttr: { class: "fw-bold shadow-sm" },
                        onClick: function () {
                            openActionPopup("Reject");
                        }
                    }, QcsButtonDefaults.getCommonButtonOptions("reject"))
                });
            }

            if (permissions.canApprove) {
                actions.push({
                    action: "Approve",
                    hostClass: "flex-grow-1",
                    buttonOptions: Object.assign({
                        icon: "fas fa-check",
                        width: "100%",
                        elementAttr: { class: "fw-bold shadow-sm" },
                        onClick: function () {
                            openActionPopup("Approve");
                        }
                    }, QcsButtonDefaults.getCommonButtonOptions("approve"))
                });
            }

            QcsActionDialog.renderActionButtons("#actionButtonContainer", actions);
        }

        function openActionPopup(action) {
            actionDialog.openAction(action, {
                onConfirm: function (comment) {
                    sendApprovalAction(action, comment);
                }
            });
        }

        function disposeViewerObjectUrl() {
            if (currentViewerObjectUrl) {
                URL.revokeObjectURL(currentViewerObjectUrl);
                currentViewerObjectUrl = null;
            }
        }

        async function sendApprovalAction(action, comment) {
            if (!currentDocId) {
                showError("ไม่พบรหัสเอกสาร (Document code not found)");
                return;
            }
            await QcsAsync.run(async function () {
                await QcsApprovalAction.send(action, {
                    requestId: currentDocId,
                    comment: comment,
                    approveUrl: api.APPROVE,
                    rejectUrl: api.REJECT,
                    missingRequestIdMessage: "ไม่พบรหัสเอกสาร (Document code not found)"
                });
                await loadData();
            }, {
                inlineLoaderMessage: "กำลังดำเนินการ... (Processing...)",
                successMessage: "ดำเนินการสำเร็จ (Action completed successfully)",
                onError: function (err) {
                    QcsErrorPresenter.logAndNotify("Approval error", err, {
                        message: "การดำเนินการล้มเหลว (Action failed)"
                    });
                }
            });
        }

        async function loadViewer(idToLoad, label) {
            if (label) {
                $("#docTitleLabel").text(label);
            }
            if (!isFinalState) {
                $(".file-item").removeClass("active");
                $(`.file-item[data-id="${idToLoad}"]`).addClass("active");
            }

            $("#pdfViewer").empty();
            const $btnDownload = $("#btnDownload").prop("disabled", true).off("click");

            await QcsAsync.run(async function () {
                const url = `${viewFileApiUrl}/${idToLoad}`;
                const cacheKey = `${viewFileApiUrl}|${idToLoad}`;
                let blob = viewerBlobCache.get(cacheKey);

                if (!blob) {
                    blob = await QcsFileView.fetchBlob(url, {
                        errorMessage: "เปิดไฟล์ไม่สำเร็จ (Failed to open file)",
                        expectedMimeType: "application/pdf",
                        invalidTypeMessage: "ไฟล์ไม่ใช่ PDF (File is not a PDF)"
                    });

                    if (viewerBlobCache.size >= 20) {
                        clearViewerBlobCache();
                    }
                    viewerBlobCache.set(cacheKey, blob);
                }

                disposeViewerObjectUrl();
                const preview = QcsFileView.mountPdfEmbed("#pdfViewer", blob);
                currentViewerObjectUrl = preview.objectUrl;

                if (isFinalState) {
                    $btnDownload.prop("disabled", false).on("click", function () {
                        let dateStr = "";
                        if (currentDocData.validUntil) {
                            const date = new Date(currentDocData.validUntil);
                            if (!Number.isNaN(date.getTime())) {
                                const pad = function (n) { return n.toString().padStart(2, "0"); };
                                dateStr = `Exp-${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
                            }
                        }

                        const fileNameParts = [
                            QcsFileView.sanitizeFileNamePart(label),
                            QcsFileView.sanitizeFileNamePart(currentDocData.title),
                            QcsFileView.sanitizeFileNamePart(currentDocData.vendorCode),
                            dateStr
                        ];

                        const finalFileName = fileNameParts.filter(function (part) { return part; }).join("_") + ".pdf";
                        QcsFileView.downloadObjectUrl(currentViewerObjectUrl, finalFileName);
                    });
                }
            }, {
                inlineLoaderMessage: "กำลังเปิดไฟล์... (Opening file...)",
                inlineLoaderTarget: "#pdfViewer",
                onError: function (e) {
                    QcsErrorPresenter.log("Viewer Error", e);
                    QcsErrorPresenter.renderViewerError("#pdfViewer", e);
                }
            });
        }

        async function loadData() {
            await QcsAsync.run(async function () {
                const url = new URL(api.GET_DATA, window.location.origin);
                url.searchParams.append("code", docCode);

                const response = await fetch(url, { credentials: "include" });
                if (!response.ok) {
                    let detail = "ไม่พบเอกสาร (Document not found)";
                    try {
                        const errorPayload = await response.json();
                        if (errorPayload && (errorPayload.detail || errorPayload.title)) {
                            detail = errorPayload.detail || errorPayload.title;
                        }
                    } catch (_) {
                        // Ignore parse errors and keep fallback message.
                    }
                    throw new Error(detail);
                }
                const data = await response.json();
                if (!data) {
                    throw new Error("ไม่พบข้อมูล (Data not found)");
                }

                currentDocData = data;
                currentDocId = data.requestId || data.id;
                isFinalState = data.currentStepId === 99;
                viewFileApiUrl = isFinalState ? api.VIEW_FILE_QUOTATION : api.VIEW_FILE_REQUEST;
                clearViewerBlobCache();

                renderHeader(data);
                renderForm(data);
                renderDocuments(data.quotations || []);

                if (isFinalState) {
                    $("#btnDownload").show();
                    $("#btnFullscreen").removeClass("ms-auto");
                    $("#actionCard").addClass("d-none");
                    loadViewer(currentDocId, data.code);
                } else {
                    $("#btnDownload").hide();
                    $("#btnFullscreen").addClass("ms-auto");

                    if (data.permissions && (data.permissions.canApprove || data.permissions.canReject)) {
                        renderActionButtons(data);
                    } else {
                        $("#actionCard").addClass("d-none");
                    }

                    const docs = data.quotations || [];
                    if (docs.length > 0) {
                        loadViewer(docs[0].id, docs[0].originalFileName || data.code);
                    } else {
                        $("#pdfViewer").html('<div class="pdf-placeholder">ไม่มีไฟล์แนบ (No attachment files)</div>');
                    }
                }

                const steps = data.workflowRoute && data.workflowRoute.steps
                    ? data.workflowRoute.steps
                    : (data.approvalSteps || []);
                renderApprovals(steps);
                $("#btnFullscreen").off("click").on("click", toggleFullscreen);
            }, {
                inlineLoaderMessage: "กำลังโหลดข้อมูล... (Loading data...)",
                onError: function (err) {
                    QcsErrorPresenter.logAndNotify("Load data error", err, {
                        message: QcsErrorPresenter.getMessage(err, "ไม่พบเอกสาร (Document not found)")
                    });
                }
            });
        }

        if (docCode) {
            loadData();
        } else {
            QcsErrorPresenter.notifyError(new Error("ไม่พบรหัสเอกสาร (Document code not found)"), {
                message: "ไม่พบรหัสเอกสาร (Document code not found)"
            });
        }
    });
})(window);

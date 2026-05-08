(function (global) {
    $(function () {
        if (!$("#quotationForm").length) {
            return;
        }

        const config = global.QcsPageConfig || {};
        const prId = config.prId;
        const mode = prId ? "EDIT" : "CREATE";
        const homeUrl = config.homeUrl;
        const docTypesSource = config.docTypesSource || [];
        const approvalStatuses = config.approvalStatuses || [];

        const pageConfig = {
            API_BASE_URL: API_BASE,
            API_GET_DETAIL: `/Request/Detail/${prId}`,
            API_WORKFLOW_ROUTE_INIT: "/Workflow/route/1",
            API_PREVIEW_MERGE_STAMP: "/Request/PreviewMergeStamp",
            API_CREATE_SAVE: "/Request/Save",
            API_CREATE_SUBMIT: "/Request/Submit",
            API_UPDATE_SAVE: "/Request/Update",
            API_UPDATE_SUBMIT: "/Request/SubmitUpdate",
            API_APPROVE: "/Approval/Approve",
            API_REJECT: "/Approval/Reject",
            VENDOR_API_URL: "/Vendor"
        };

        let prData = {};
        let formInstance;
        let toolbarInstance;
        let attachmentManager;
        const actionDialog = QcsActionDialog.create({ rootSelector: "#actionPopup" });

        const loadingIndicator = QcsLoading.createPageLoadPanel("#loadingIndicator", {
            message: "กำลังดำเนินการ... (Processing...)"
        });

        const vendorStore = DevExpress.data.AspNet.createStore({
            key: "code",
            loadUrl: pageConfig.API_BASE_URL + pageConfig.VENDOR_API_URL,
            onBeforeSend: function (method, ajaxOptions) {
                ajaxOptions.xhrFields = { withCredentials: true };
            }
        });

        const formatDateTime = QcsUiUtils.formatDateTime;

        function getStatusBadgeInfo(statusId) {
            return QcsStatusBadge.getApprovalStatusBadgeInfo(statusId, approvalStatuses, {
                classMap: {
                    0: "bg-secondary-soft",
                    1: "bg-warning-soft",
                    2: "bg-success-soft",
                    9: "bg-danger-soft"
                }
            });
        }

        function initUI() {
            initToolbar();
            initForm();
        }

        function initToolbar() {
            const items = [
                { location: "after", widget: "dxButton", visible: mode === "EDIT", options: Object.assign({ text: "ประวัติ (History)", icon: "clock", onClick: showHistoryPopup }, QcsButtonDefaults.getCommonButtonOptions("ghost")) },
                { location: "after", widget: "dxButton", visible: false, options: Object.assign({ elementAttr: { id: "btnPreview" }, text: "Preview PDF", icon: "exportpdf", onClick: handlePreviewPdf }, QcsButtonDefaults.getCommonButtonOptions("secondary")) },
                { location: "after", widget: "dxButton", visible: false, options: Object.assign({ elementAttr: { id: "btnSave" }, text: "บันทึกฉบับร่าง (Save Draft)", icon: "save", onClick: function () { handleSaveOrSubmit(false); } }, QcsButtonDefaults.getCommonButtonOptions("secondary")) },
                QcsActionDialog.createToolbarButtonItem("Submit", {
                    visible: false,
                    buttonOptions: {
                        elementAttr: { id: "btnSubmit" },
                        icon: "check",
                        onClick: function () { handleSaveOrSubmit(true); }
                    }
                }),
                QcsActionDialog.createToolbarButtonItem("Reject", {
                    visible: false,
                    buttonOptions: {
                        elementAttr: { id: "btnReject" },
                        onClick: function () { openActionPopup("Reject"); }
                    }
                }),
                QcsActionDialog.createToolbarButtonItem("Approve", {
                    visible: false,
                    buttonOptions: {
                        elementAttr: { id: "btnApprove" },
                        onClick: function () { openActionPopup("Approve"); }
                    }
                })
            ];
            toolbarInstance = $("#actionButtons").dxToolbar({ items: items }).dxToolbar("instance");
        }

        function initForm() {
            formInstance = $("#quotationForm").dxForm({
                formData: { validFrom: new Date(), validUntil: new Date(new Date().setMonth(new Date().getMonth() + 1)) },
                colCount: 2,
                labelLocation: "top",
                items: [
                    {
                        itemType: "group",
                        colSpan: 2,
                        colCount: 2,
                        items: [
                            { dataField: "title", label: { text: "หัวข้อ (Title)" }, colSpan: 1, validationRules: [{ type: "required" }] },
                            {
                                dataField: "vendorCode",
                                label: { text: "ผู้ขาย (Vendor)" },
                                colSpan: 1,
                                editorType: "dxSelectBox",
                                editorOptions: {
                                    dataSource: vendorStore,
                                    valueExpr: "code",
                                    displayExpr: function (item) { return item ? `${item.code} : ${item.name}` : ""; },
                                    searchExpr: ["code", "name"],
                                    searchEnabled: true,
                                    placeholder: "พิมพ์รหัสหรือชื่อผู้ขาย... (Type vendor code or name...)",
                                    showClearButton: true
                                },
                                validationRules: [{ type: "required", message: "กรุณาเลือกผู้ขาย (Please select a vendor)" }]
                            },
                            { dataField: "validFrom", label: { text: "มีผลตั้งแต่ (Valid From)" }, editorType: "dxDateBox", editorOptions: { displayFormat: "yyyy-MM-dd" }, validationRules: [{ type: "required" }] },
                            { dataField: "validUntil", label: { text: "หมดอายุ (Valid Until)" }, editorType: "dxDateBox", editorOptions: { displayFormat: "yyyy-MM-dd" }, validationRules: [{ type: "required" }] },
                            { dataField: "remark", label: { text: "หมายเหตุ (Remark)" }, editorType: "dxTextArea", editorOptions: { height: 80 }, colSpan: 2 }
                        ]
                    }
                ]
            }).dxForm("instance");

            const $attachmentsHeader = $("<div>").addClass("section-header mt-4")
                .append($("<i>").addClass("fas fa-paperclip me-2").css("color", "var(--dx-primary-color)").attr("aria-hidden", "true"))
                .append($("<span>").text("เอกสารแนบ (Attachments)"));
            const $attachmentsBody = $("<div>").addClass("px-2 pb-2");
            $("#quotationForm").after($attachmentsBody).after($attachmentsHeader);

            renderAttachmentsSection([], $attachmentsBody, true);
        }

        async function loadCreateModeData() {
            await QcsAsync.run(async function () {
                const response = await fetch(pageConfig.API_BASE_URL + pageConfig.API_WORKFLOW_ROUTE_INIT, {
                    method: "GET",
                    credentials: "include"
                });
                if (!response.ok) {
                    throw new Error("ไม่สามารถโหลดเส้นทางอนุมัติได้ (Cannot load approval route)");
                }

                const data = await response.json();
                if (data) {
                    renderWorkflowRouteGrid(data, 1);
                    const canInitiate = data.canInitiate;
                    QcsToolbar.setToolbarVisibility(toolbarInstance, {
                        btnPreview: canInitiate,
                        btnSave: canInitiate,
                        btnSubmit: canInitiate
                    });
                    if (!canInitiate) {
                        $("#formContainer").addClass("unauthorized-overlay");
                    }
                }
            }, {
                loader: loadingIndicator,
                onError: function (err) {
                    QcsErrorPresenter.logAndNotify("Workflow load error", err, {
                        message: "โหลดเส้นทางล้มเหลว (Failed to load route)"
                    });
                }
            });
        }

        async function loadEditModeData() {
            await QcsAsync.run(async function () {
                const response = await fetch(pageConfig.API_BASE_URL + pageConfig.API_GET_DETAIL, { credentials: "include" });
                if (!response.ok) {
                    throw new Error("ไม่พบข้อมูลเอกสาร (Document data not found)");
                }
                const data = await response.json();

                prData = data;
                $("#lblDocNo").text(data.docNo || data.code || "-");
                const canEdit = data.permissions && data.permissions.canEdit;
                formInstance.option("formData", data);
                formInstance.option("readOnly", !canEdit);
                if (attachmentManager) {
                    attachmentManager.setFiles(data.quotations || []);
                    attachmentManager.setCanEdit(canEdit);
                }

                renderWorkflowRouteGrid(data.workflowRoute, data.currentStepId);
                QcsToolbar.setToolbarVisibility(toolbarInstance, {
                    btnPreview: canEdit,
                    btnSave: canEdit,
                    btnSubmit: canEdit,
                    btnApprove: data.permissions.canApprove,
                    btnReject: data.permissions.canReject
                });
            }, {
                loader: loadingIndicator,
                onError: function (err) {
                    QcsErrorPresenter.logAndNotify("Document load error", err, {
                        message: "ไม่พบเอกสาร (Document not found)"
                    });
                }
            });
        }

        function renderAttachmentsSection(files, itemElement, canEdit) {
            attachmentManager = QcsAttachmentGrid.create({
                rootElement: itemElement,
                files: files,
                canEdit: canEdit,
                documentTypesSource: docTypesSource,
                onViewFile: viewPdf
            });
        }

        async function handleSaveOrSubmit(isSubmit) {
            const validationResult = QcsRequestFormValidation.validateSubmission({
                formInstance: formInstance,
                attachmentManager: attachmentManager,
                requiredDocumentTypeId: 10
            });

            if (!validationResult.isValid) {
                return;
            }

            if (isSubmit) {
                openActionPopup("Submit");
            } else {
                executeSaveOrSubmit(false, null);
            }
        }

        function openActionPopup(action) {
            actionDialog.openAction(action, {
                onConfirm: function (comment) {
                    if (action === "Submit") {
                        executeSaveOrSubmit(true, comment);
                    } else {
                        sendApprovalAction(action, comment);
                    }
                }
            });
        }

        async function executeSaveOrSubmit(isSubmit, comment) {
            await QcsAsync.run(async function () {
                const data = formInstance.option("formData");

                const vendorEditor = formInstance.getEditor("vendorCode");
                const selectedVendor = vendorEditor.option("selectedItem");
                const gridFiles = attachmentManager ? attachmentManager.getFiles() : [];
                const pendingFiles = attachmentManager ? attachmentManager.getPendingFiles() : [];
                await QcsRequestSubmission.submitForm({
                    baseUrl: pageConfig.API_BASE_URL,
                    mode: mode,
                    isSubmit: isSubmit,
                    requestId: prId,
                    formValues: data,
                    vendorName: selectedVendor ? selectedVendor.name : (data.vendorName || ""),
                    comment: isSubmit ? comment : null,
                    attachmentFiles: gridFiles,
                    pendingFiles: pendingFiles,
                    deletedFileIds: attachmentManager ? attachmentManager.getDeletedFileIds() : [],
                    endpoints: {
                        createSave: pageConfig.API_CREATE_SAVE,
                        createSubmit: pageConfig.API_CREATE_SUBMIT,
                        updateSave: pageConfig.API_UPDATE_SAVE,
                        updateSubmit: pageConfig.API_UPDATE_SUBMIT
                    }
                });
                setTimeout(function () {
                    window.location.href = homeUrl;
                }, 1000);
            }, {
                loader: loadingIndicator,
                successMessage: "บันทึกสำเร็จ (Saved successfully)",
                successDuration: 1500,
                onError: function (err) {
                    QcsErrorPresenter.logAndNotify("Save error", err, {
                        message: "บันทึกล้มเหลว (Save failed)",
                        duration: 5000
                    });
                }
            });
        }

        async function sendApprovalAction(action, comment) {
            await QcsAsync.run(async function () {
                await QcsApprovalAction.send(action, {
                    requestId: prId,
                    comment: comment,
                    approveUrl: pageConfig.API_BASE_URL + pageConfig.API_APPROVE,
                    rejectUrl: pageConfig.API_BASE_URL + pageConfig.API_REJECT
                });
                setTimeout(function () {
                    location.reload();
                }, 500);
            }, {
                loader: loadingIndicator,
                onError: function (err) {
                    QcsErrorPresenter.logAndNotify("Approval error", err, {
                        message: "การดำเนินการล้มเหลว (Action failed)"
                    });
                }
            });
        }

        function buildPreviewFormData() {
            const formData = new FormData();
            const formValues = formInstance.option("formData") || {};
            const gridFiles = attachmentManager ? attachmentManager.getFiles() : [];
            const pendingFiles = attachmentManager ? attachmentManager.getPendingFiles() : [];

            if (prId) {
                formData.append("RequestId", prId);
            }
            formData.append("DocumentName", formValues.title || "Preview");
            formData.append("ReferenceCode", $("#lblDocNo").text() || "PREVIEW");
            formData.append("QuotationsJson", JSON.stringify(gridFiles.map(function (file) {
                return {
                    id: file.id || 0,
                    fileName: file.fileName || "",
                    originalFileName: file.originalFileName || file.fileName || "",
                    documentTypeId: file.documentTypeId || 10
                };
            })));

            pendingFiles.forEach(function (file) {
                formData.append("NewAttachments", file);
            });

            return formData;
        }

        async function handlePreviewPdf() {
            const gridFiles = attachmentManager ? attachmentManager.getFiles() : [];
            if (!gridFiles || gridFiles.length === 0) {
                QcsErrorPresenter.notifyError(new Error("กรุณาแนบไฟล์อย่างน้อย 1 ไฟล์ (Please attach at least 1 file)"), {
                    message: "กรุณาแนบไฟล์อย่างน้อย 1 ไฟล์ (Please attach at least 1 file)"
                });
                return;
            }

            try {
                await QcsFileView.openBlobInNewWindow({
                    url: pageConfig.API_BASE_URL + pageConfig.API_PREVIEW_MERGE_STAMP,
                    popupBlockedMessage: "เบราว์เซอร์บล็อกป๊อปอัป (Browser blocked popup)",
                    fetchOptions: {
                        method: "POST",
                        body: buildPreviewFormData(),
                        errorMessage: "สร้าง Preview ไม่สำเร็จ (Failed to create preview)",
                        expectedMimeType: "application/pdf",
                        invalidTypeMessage: "ไฟล์ไม่ใช่ PDF (File is not a PDF)"
                    },
                    loadingOptions: {
                        heading: "Preview PDF...",
                        message: ""
                    }
                });
            } catch (error) {
                if (QcsErrorPresenter.handlePopupBlocked(error, {
                    message: "เบราว์เซอร์บล็อกป๊อปอัป (Browser blocked popup)"
                })) {
                    return;
                }

                QcsErrorPresenter.logAndNotify("Preview PDF Error", error, {
                    message: "สร้าง Preview ไม่สำเร็จ (Failed to create preview)"
                });
            }
        }

        function renderWorkflowRouteGrid(routeData, currentStepId) {
            QcsRequestWorkflow.createRouteGrid("#workflowRouteGrid", {
                routeData: routeData,
                currentStepId: currentStepId,
                getStatusBadgeInfo: getStatusBadgeInfo,
                formatDateTime: formatDateTime
            });
        }

        function showHistoryPopup() {
            const historyData = prData ? (prData.workflowRoute ? prData.workflowRoute.steps : []) : [];
            QcsRequestWorkflow.openHistoryPopup("#historyPopup", {
                steps: historyData,
                getStatusBadgeInfo: getStatusBadgeInfo,
                formatDateTime: formatDateTime
            });
        }

        async function viewPdf(id) {
            try {
                await QcsFileView.openBlobInNewWindow({
                    url: `${pageConfig.API_BASE_URL}/Request/ViewFile/${id}`,
                    popupBlockedMessage: "เบราว์เซอร์บล็อกป๊อปอัป (Browser blocked popup)",
                    fetchOptions: {
                        errorMessage: "ดาวน์โหลดไฟล์ไม่สำเร็จ (Failed to download file)",
                        expectedMimeType: "application/pdf",
                        invalidTypeMessage: "ไฟล์ไม่ใช่ PDF (File is not a PDF)"
                    },
                    loadingOptions: {
                        heading: "กำลังเปิดไฟล์ PDF... (Opening PDF file...)",
                        message: "โปรดรอสักครู่ (Please wait...)"
                    }
                });
            } catch (error) {
                if (QcsErrorPresenter.handlePopupBlocked(error, {
                    message: "เบราว์เซอร์บล็อกป๊อปอัป (Browser blocked popup)"
                })) {
                    return;
                }
                QcsErrorPresenter.logAndNotify("View PDF Error", error, {
                    message: "ดาวน์โหลดไฟล์ไม่สำเร็จ (Failed to download file)"
                });
            }
        }

        initUI();
        if (mode === "CREATE") {
            loadCreateModeData();
        } else {
            loadEditModeData();
        }
    });
})(window);

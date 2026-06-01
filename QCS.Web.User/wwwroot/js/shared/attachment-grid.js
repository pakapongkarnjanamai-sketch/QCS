(function (global) {
    let attachmentGridCounter = 0;

    function getDisplayFileName(file) {
        return file.originalFileName || file.fileName;
    }

    function create(options) {
        const settings = options || {};
        attachmentGridCounter += 1;

        const state = {
            canEdit: settings.canEdit !== false,
            pendingFiles: [],
            deletedFileIds: [],
            gridInstance: null,
            uploaderContainer: null
        };

        const uploaderHostId = settings.uploaderHostId || `qcs-attachment-uploader-${attachmentGridCounter}`;

        function notify(message, type, duration) {
            QcsAsync.notify(message, type || "info", duration || 2000);
        }

        function removePendingFile(fileName) {
            const index = state.pendingFiles.findIndex(function (file) {
                return file.name === fileName;
            });

            if (index > -1) {
                state.pendingFiles.splice(index, 1);
            }
        }

        function removeRow(row) {
            var confirmText = settings.deleteConfirmText || "ต้องการลบไฟล์นี้หรือไม่? (Delete this file?)";
            var confirmTitle = settings.deleteConfirmTitle || "ยืนยันการลบไฟล์ (Confirm File Deletion)";

            DevExpress.ui.dialog.confirm(confirmText, confirmTitle).done(function (result) {
                if (!result) return;

                if (row.id && row.id > 0) {
                    if (state.deletedFileIds.indexOf(row.id) === -1) {
                        state.deletedFileIds.push(row.id);
                    }
                } else {
                    removePendingFile(getDisplayFileName(row));
                }

                var dataSource = getFiles();
                var rowIndex = dataSource.findIndex(function (record) {
                    return record.id === row.id;
                });

                if (rowIndex > -1) {
                    dataSource.splice(rowIndex, 1);
                    state.gridInstance.refresh();
                }
            });
        }

        function createButtons() {
            return [
                {
                    hint: settings.viewHint || "เปิดดู (View)",
                    icon: settings.viewIcon || "eyeopen",
                    onClick: function (e) {
                        if (e.row.data.id && e.row.data.id > 0) {
                            if (typeof settings.onViewFile === "function") {
                                settings.onViewFile(e.row.data.id);
                            }
                        } else {
                            notify(settings.unsavedFileMessage || "ไฟล์ใหม่ต้องบันทึกก่อน (New files must be saved first)", "warning", 2000);
                        }
                    }
                },
                {
                    name: "delete",
                    visible: state.canEdit,
                    icon: settings.deleteIcon || "trash",
                    onClick: function (e) {
                        removeRow(e.row.data);
                    }
                }
            ];
        }

        function setCanEdit(canEdit) {
            state.canEdit = canEdit !== false;

            if (state.gridInstance) {
                state.gridInstance.option("editing.allowUpdating", state.canEdit);
                state.gridInstance.option("rowDragging.allowReordering", state.canEdit);
                state.gridInstance.columnOption("actions", "buttons", createButtons());
            }

            if (state.uploaderContainer) {
                state.uploaderContainer.toggle(state.canEdit);
            }
        }

        function getFiles() {
            return state.gridInstance
                ? (state.gridInstance.option("dataSource") || [])
                : [];
        }

        function mount(rootElement) {
            const files = (settings.files || []).slice();
            const $container = $("<div>").addClass("attachments-container");
            const $uploaderContainer = $("<div>").attr("id", uploaderHostId).appendTo($container);

            state.uploaderContainer = $uploaderContainer;

            $("<div>").dxFileUploader({
                multiple: true,
                accept: ".pdf",
                uploadMode: "useForm",
                labelText: settings.labelText || "ลากไฟล์ PDF มาวางที่นี่ (Drag PDF file here)",
                selectButtonText: settings.selectButtonText || "เพิ่มไฟล์ (Add File)",
                onValueChanged: function (e) {
                    if (!e.value || e.value.length === 0) {
                        return;
                    }

                    const currentData = getFiles();
                    const filesToAdd = [];

                    e.value.forEach(function (file) {
                        if (file.type !== "application/pdf") {
                            notify(`ไฟล์ ${file.name} ไม่ใช่ PDF (Not a PDF)`, "error", 3000);
                            return;
                        }

                        if (currentData.some(function (row) { return getDisplayFileName(row) === file.name; })) {
                            notify(`ไฟล์ ${file.name} มีอยู่แล้ว (File already exists)`, "warning", 2000);
                            return;
                        }

                        state.pendingFiles.push(file);
                        filesToAdd.push({
                            id: -1 * (Date.now() + Math.floor(Math.random() * 1000)),
                            originalFileName: file.name,
                            fileName: file.name,
                            isNew: true,
                            fileSize: file.size,
                            documentTypeId: settings.defaultDocumentTypeId || 10
                        });
                    });

                    if (filesToAdd.length > 0) {
                        state.gridInstance.option("dataSource", currentData.concat(filesToAdd));
                    }

                    setTimeout(function () {
                        e.component.reset();
                    }, 100);
                }
            }).appendTo($uploaderContainer);

            state.gridInstance = $("<div>").dxDataGrid({
                dataSource: files,
                showBorders: true,
                columnAutoWidth: false,
                noDataText: settings.noDataText || "ไม่มีไฟล์แนบ (No attachments)",
                keyExpr: settings.keyExpr || "id",
                sorting: { mode: "none" },
                editing: { mode: "cell", allowUpdating: state.canEdit, allowDeleting: false },
                rowDragging: {
                    allowReordering: state.canEdit,
                    onReorder: function (e) {
                        const dataSource = state.gridInstance.option("dataSource");
                        if (Array.isArray(dataSource)) {
                            const item = dataSource[e.fromIndex];
                            dataSource.splice(e.fromIndex, 1);
                            dataSource.splice(e.toIndex, 0, item);
                            state.gridInstance.refresh();
                        }
                        e.promise = Promise.resolve();
                    }
                },
                columns: [
                    {
                        dataField: "originalFileName",
                        caption: settings.fileNameCaption || "ชื่อไฟล์ (File Name)",
                        minWidth: settings.fileNameMinWidth || 300,
                        allowEditing: false,
                        calculateDisplayValue: function (row) {
                            return getDisplayFileName(row);
                        },
                        cellTemplate: function (container, cellOptions) {
                            const $infoContainer = $("<div>").addClass("file-info d-flex align-items-center gap-2")
                                .append($("<i>").addClass("fas fa-file-pdf").css("color", "var(--dx-primary-color)").attr("aria-hidden", "true"))
                                .append($("<span>").text(cellOptions.displayValue));

                            if (cellOptions.data.isNew) {
                                $infoContainer.append($("<span>").addClass("badge bg-success").text("New"));
                            }

                            $infoContainer.appendTo(container);
                        }
                    },
                    {
                        dataField: "documentTypeId",
                        caption: settings.typeCaption || "ประเภท (Type)",
                        width: settings.typeWidth || 250,
                        fixed: true,
                        fixedPosition: "right",
                        lookup: {
                            dataSource: settings.documentTypesSource || [],
                            valueExpr: settings.typeValueExpr || "Id",
                            displayExpr: settings.typeDisplayExpr || "DisplayName"
                        },
                        validationRules: [{ type: "required" }],
                        setCellValue: function (newData, value, currentRowData) {
                            newData.documentTypeId = value;
                            if (state.gridInstance) {
                                setTimeout(function () {
                                    state.gridInstance.saveEditData();
                                }, 50);
                            }
                        }
                    },
                    {
                        type: "buttons",
                        name: "actions",
                        width: settings.actionsWidth || 100,
                        fixed: true,
                        fixedPosition: "right",
                        buttons: createButtons()
                    }
                ]
            }).dxDataGrid("instance");

            state.gridInstance.element().appendTo($container);

            if (!state.canEdit) {
                $uploaderContainer.hide();
            }

            $container.appendTo(rootElement);
            return api;
        }

        function setFiles(files) {
            if (state.gridInstance) {
                state.gridInstance.option("dataSource", (files || []).slice());
            }
        }

        const api = {
            mount: mount,
            setFiles: setFiles,
            setCanEdit: setCanEdit,
            getFiles: getFiles,
            getPendingFiles: function () {
                return state.pendingFiles.slice();
            },
            getDeletedFileIds: function () {
                return state.deletedFileIds.slice();
            },
            getGridInstance: function () {
                return state.gridInstance;
            }
        };

        if (settings.rootElement) {
            mount(settings.rootElement);
        }

        return api;
    }

    global.QcsAttachmentGrid = {
        create: create,
        getDisplayFileName: getDisplayFileName
    };
})(window);
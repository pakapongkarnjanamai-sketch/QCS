(function (global) {
    function getDisplayFileName(file) {
        return file.fileName || file.originalFileName || "";
    }

    function mapQuotationMetadata(files) {
        const quotationFiles = files || [];
        const newFilesMeta = [];
        const existingUpdates = [];

        quotationFiles.forEach(function (file) {
            if (file.isNew) {
                newFilesMeta.push({
                    FileName: getDisplayFileName(file),
                    DocumentTypeId: file.documentTypeId
                });
                return;
            }

            if (file.id) {
                existingUpdates.push({
                    Id: file.id,
                    DocumentTypeId: file.documentTypeId
                });
            }
        });

        return {
            newFilesMeta: newFilesMeta,
            existingUpdates: existingUpdates
        };
    }

    function appendIsoDate(formData, key, value) {
        if (!value) {
            return;
        }

        const dateValue = new Date(value);
        if (!Number.isNaN(dateValue.getTime())) {
            formData.append(key, dateValue.toISOString());
        }
    }

    function createFormData(options) {
        const settings = options || {};
        const formData = new FormData();
        const formValues = settings.formValues || {};
        const attachmentFiles = settings.attachmentFiles || [];
        const quotationMetadata = mapQuotationMetadata(attachmentFiles);
        const pendingFiles = settings.pendingFiles || [];
        const deletedFileIds = settings.deletedFileIds || [];
        const mode = settings.mode || "CREATE";

        if (mode === "EDIT" && settings.requestId != null) {
            formData.append("Id", settings.requestId);
        }

        formData.append("Title", formValues.title || "");
        formData.append("VendorCode", formValues.vendorCode || "");
        formData.append("VendorName", settings.vendorName || formValues.vendorName || "");
        formData.append("SourceSystem", formValues.sourceSystem || "");
        formData.append("SourceCode", formValues.sourceCode || "");
        appendIsoDate(formData, "ValidFrom", formValues.validFrom);
        appendIsoDate(formData, "ValidUntil", formValues.validUntil);
        formData.append("Remark", formValues.remark || "");

        if (settings.comment) {
            formData.append("Comment", settings.comment);
        }

        formData.append("QuotationsJson", JSON.stringify(quotationMetadata.newFilesMeta));

        if (mode === "EDIT") {
            formData.append("UpdatedQuotationsJson", JSON.stringify(quotationMetadata.existingUpdates));
            if (deletedFileIds.length > 0) {
                formData.append("DeletedFileIds", deletedFileIds.join(","));
            }
        }

        pendingFiles.forEach(function (file) {
            formData.append(mode === "CREATE" ? "Attachments" : "NewAttachments", file);
        });

        return formData;
    }

    function resolveEndpoint(options) {
        const settings = options || {};
        const mode = settings.mode || "CREATE";
        const endpoints = settings.endpoints || {};

        if (mode === "CREATE") {
            return settings.isSubmit ? endpoints.createSubmit : endpoints.createSave;
        }

        return settings.isSubmit ? endpoints.updateSubmit : endpoints.updateSave;
    }

    async function submitForm(options) {
        const settings = options || {};
        const endpoint = resolveEndpoint(settings);
        const formData = createFormData(settings);
        const response = await fetch((settings.baseUrl || "") + endpoint, {
            method: "POST",
            body: formData,
            credentials: settings.credentials || "include"
        });

        if (!response.ok) {
            throw new Error(await response.text());
        }

        return response;
    }

    global.QcsRequestSubmission = {
        mapQuotationMetadata: mapQuotationMetadata,
        createFormData: createFormData,
        resolveEndpoint: resolveEndpoint,
        submitForm: submitForm
    };
})(window);
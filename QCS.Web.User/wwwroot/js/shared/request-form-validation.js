(function (global) {
    function getAttachmentFiles(attachmentManager) {
        return attachmentManager ? (attachmentManager.getFiles() || []) : [];
    }

    function runGuards(guards) {
        const resolvedGuards = guards || [];

        for (let index = 0; index < resolvedGuards.length; index += 1) {
            const guard = resolvedGuards[index];
            const isValid = typeof guard.isValid === "function"
                ? guard.isValid()
                : guard.isValid !== false;

            if (!isValid) {
                QcsAsync.notify(guard.message, guard.type || "error", guard.duration || 2000);
                return false;
            }
        }

        return true;
    }

    function validateSubmission(options) {
        const settings = options || {};
        const attachmentFiles = getAttachmentFiles(settings.attachmentManager);
        const requiredDocumentTypeId = settings.requiredDocumentTypeId || 10;
        const result = runGuards([
            {
                isValid: function () {
                    return settings.formInstance && settings.formInstance.validate().isValid;
                },
                message: settings.invalidFormMessage || "กรุณากรอกข้อมูลที่จำเป็นให้ครบถ้วน",
                duration: settings.invalidFormDuration || 2000
            },
            {
                isValid: function () {
                    return attachmentFiles.length > 0;
                },
                message: settings.missingAttachmentMessage || "กรุณาแนบไฟล์เอกสารประกอบ",
                duration: settings.missingAttachmentDuration || 3000
            },
            {
                isValid: function () {
                    return attachmentFiles.some(function (file) {
                        return file.documentTypeId === requiredDocumentTypeId;
                    });
                },
                message: settings.missingRequiredTypeMessage || "กรุณาแนบไฟล์ใบเสนอราคาต้นฉบับอย่างน้อย 1 ไฟล์",
                duration: settings.missingRequiredTypeDuration || 4000
            }
        ]);

        return {
            isValid: result,
            attachmentFiles: attachmentFiles
        };
    }

    global.QcsRequestFormValidation = {
        getAttachmentFiles: getAttachmentFiles,
        runGuards: runGuards,
        validateSubmission: validateSubmission
    };
})(window);
const createDataStore = (Url, controller, options = {}) => {
    const { action = '', key = null, ParamKeys = null, ParamKey = null } = options;
    const baseUrl = `${Url}api/${controller}${action ? '/' + action : ''}`;

    // สร้าง query string จาก ParamKeys
    const queryString = ParamKeys ? Object.keys(ParamKeys)
        .map(k => encodeURIComponent(k) + '=' + encodeURIComponent(ParamKeys[k]))
        .join('&') : '';

    const fullUrl = queryString ? `${baseUrl}?${queryString}` : (ParamKey ? `${baseUrl}?key=${ParamKey}` : baseUrl);

    return DevExpress.data.AspNet.createStore({
        loadUrl: fullUrl,
        key: key || 'id',
        insertUrl: baseUrl,
        updateUrl: baseUrl,
        deleteUrl: baseUrl,
        onBeforeSend: function (method, ajaxOptions) {
            // ใช้ Windows Authentication แทน Bearer token
            ajaxOptions.xhrFields = {
                withCredentials: true
            };
            // ลบ Authorization header ออก เพราะใช้ Windows Authentication
            ajaxOptions.headers = ajaxOptions.headers || {};
        },
        errorHandler: function (error) {
            if (error && error.xhr && error.xhr.status === 401) {
                console.log('401 Unauthorized. Authentication required.');
                showAuthenticationError();
            } else {
                console.error('Error:', error);
                showGenericError(error);
            }
        }
    });
};


async function handleExporting(e, dataName) {
    let fileName = dataName || 'data';
    const workbook = new ExcelJS.Workbook();
    const worksheet = workbook.addWorksheet(fileName);
    const time = formatDate(new Date());

    await DevExpress.excelExporter.exportDataGrid({
        component: e.component,
        worksheet: worksheet,
        autoFilterEnabled: true
    });

    const buffer = await workbook.xlsx.writeBuffer();
    saveAs(new Blob([buffer], { type: 'application/octet-stream' }), `${fileName}_${time}.xlsx`);
}

function formatDate(date) {
    const year = date.getFullYear();
    const month = ('0' + (date.getMonth() + 1)).slice(-2);
    const day = ('0' + date.getDate()).slice(-2);
    const hours = ('0' + date.getHours()).slice(-2);
    const minutes = ('0' + date.getMinutes()).slice(-2);
    const seconds = ('0' + date.getSeconds()).slice(-2);

    return `${year}${month}${day}_${hours}${minutes}${seconds}`;
}
function getStatusInfo(status, stepId) {
    // 2.1 Determine Color Class (UI Logic)
    let cls = "bg-secondary-soft"; // Default / Draft

    if (status === 1) cls = "bg-warning-soft";
    else if (status === 2) cls = "bg-success-soft";
    else if (status === 3) cls = "bg-info-soft";
    else if (status === 9 || status === 99) cls = "bg-danger-soft";

    // Special case for "Waiting Verify" (ถ้าต้องการแยกสี)
    if (status === 1 && stepId === 2) {
        cls = "bg-info-soft";
    }

    // 2.2 Determine Text (Data Logic - Single Source of Truth)
    // ใช้ SystemEnums ที่โหลดมาจาก site.js
    let text = "Status " + status;
    if (typeof SystemEnums !== 'undefined') {
        text = SystemEnums.getDisplayName('requestStatus', status);
    }

    // คืนค่ารูปแบบเดียวกันกับที่หน้าอื่นใช้
    return { text: text, cls: "status-badge " + cls };
}

// ประกาศตัวแปร Global สำหรับเก็บ Enum
var SystemEnums = {
    requestStatus: [],
    workflowStep: [],
    documentType: [],

    // ฟังก์ชันสำหรับดึงชื่อภาษาไทยโดยใช้ ID (เอาไว้ใช้ตอนแสดงผลนอก Grid)
    getDisplayName: function (enumName, id) {
        if (!this[enumName]) return id;
        var found = this[enumName].find(function (item) { return item.id == id; });
        return found ? found.displayName : id;
    }
};



// ฟังก์ชันดึงข้อมูลจาก API (ควรเรียกใน _Layout หรือส่วนต้นของหน้าเว็บ)
function initSystemEnums() {
   
    $.ajax({
        url: API_BASE + "/Enum/all", // ตรวจสอบ URL API ของคุณให้ถูกต้อง
        method: "GET",
        xhrFields: { withCredentials: true },
        async: false, // จำเป็นต้องรอให้โหลดเสร็จก่อน render หน้าเว็บ (เพื่อป้องกัน dropdown ว่าง)
        success: function (data) {
            SystemEnums.requestStatus = data.requestStatus;
            SystemEnums.workflowStep = data.workflowStep;
            SystemEnums.documentType = data.documentType;
            SystemEnums.approvalStatus = data.approvalStatus;
            console.log("System Enums Loaded:", SystemEnums);
        },
        error: function (err) {
            console.error("Failed to load Enums", err);
        }
    });
}


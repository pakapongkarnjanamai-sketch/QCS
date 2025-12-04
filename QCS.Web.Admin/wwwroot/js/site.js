const createDataStore = (Url, controller, options = {}) => {
    const { action = '', key = null, ParamKeys = null, ParamKey = null } = options;
    const baseUrl = `${Url}${controller}${action ? '/' + action : ''}`;

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

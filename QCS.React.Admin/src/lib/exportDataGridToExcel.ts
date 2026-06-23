import { exportDataGrid } from 'devextreme/excel_exporter'
import { Workbook } from 'devextreme-exceljs-fork'
import { saveAs } from 'file-saver'
import type dxDataGrid from 'devextreme/ui/data_grid'

type ExportDataGridToExcelOptions = {
  component: dxDataGrid
  fileName: string
  worksheetName: string
}

export async function exportDataGridToExcel({
  component,
  fileName,
  worksheetName,
}: ExportDataGridToExcelOptions) {
  const workbook = new Workbook()
  const worksheet = workbook.addWorksheet(worksheetName)

  await exportDataGrid({
    component,
    worksheet,
    autoFilterEnabled: true,
  })

  const buffer = await workbook.xlsx.writeBuffer()
  const blob = new Blob([buffer], {
    type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  })

  saveAs(blob, `${fileName}.xlsx`)
}
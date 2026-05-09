import { useMemo } from 'react'
import DataGrid, { Column, FilterRow, HeaderFilter, RemoteOperations, Scrolling } from 'devextreme-react/data-grid'
import { useNavigate } from 'react-router-dom'
import { createDataSource } from '../../lib/createDataSource.ts'

type VendorRow = {
  vendorCode: string
  vendorName: string
  taxId: string
  contactName: string
  phone: string
  email: string
  address: string
  quotationCount: number
}

export function VendorsPage() {
  const navigate = useNavigate()
  const dataSource = useMemo(
    () => createDataSource<VendorRow>('/api/Vendor/Grid', 'vendorCode'),
    [],
  )

  return (
    <div className="flex flex-col flex-1 min-h-0">
      <section className="flex-1 min-h-0 overflow-hidden rounded-sm border border-(--border-subtle) bg-(--surface-panel)">
        <DataGrid
          dataSource={dataSource}
          keyExpr="vendorCode"
          showBorders={false}
          showColumnLines={false}
          showRowLines={true}
          rowAlternationEnabled={false}
          columnAutoWidth={true}
          wordWrapEnabled={false}
          noDataText="No vendor data"
          height="100%"
        >
          <RemoteOperations filtering paging sorting grouping={false} summary={false} />
          <FilterRow visible={true} />
          <HeaderFilter visible={true} />
          <Scrolling mode="virtual" rowRenderingMode="virtual" />

          <Column dataField="vendorCode" caption="Vendor Code" width={150} />
          <Column dataField="vendorName" caption="Vendor Name" minWidth={220} />
          <Column
            dataField="quotationCount"
            caption="Quotations"
            width={120}
            alignment="right"
             sortOrder="desc"
              sortIndex={0}
          />
          <Column
            caption="Action"
            width={160}
            alignment="center"
            cellRender={(cell) => {
              const data = cell.data as VendorRow
              const disabled = !data.vendorCode || data.vendorCode === '-'

              return (
                <button
                  type="button"
                  disabled={disabled}
                  onClick={() => {
                    if (disabled) return

                    const query = new URLSearchParams({
                      vendorCode: data.vendorCode,
                      vendorName: data.vendorName,
                    })

                    void navigate(`/quotations?${query.toString()}`)
                  }}
                  className="focus-ring inline-flex h-8 items-center justify-center rounded-sm border border-(--border-subtle) px-3 text-[12px] text-(--ink-strong) disabled:cursor-not-allowed disabled:opacity-50"
                >
                  View quotations
                </button>
              )
            }}
          />
        </DataGrid>
      </section>
    </div>
  )
}

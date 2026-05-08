import { useMemo } from 'react'
import DataGrid, { Column, FilterRow, HeaderFilter, RemoteOperations, Scrolling } from 'devextreme-react/data-grid'
import { useNavigate } from 'react-router-dom'
import { createDataSource } from '../../lib/createDataSource.ts'

type RequesterRow = {
  requesterNId: string
  requesterName: string
  departmentName: string
  quotationCount: number
}

export function RequesterPage() {
  const navigate = useNavigate()
  const dataSource = useMemo(
    () => createDataSource<RequesterRow>('/api/Request/Admin/Requesters', 'requesterNId'),
    [],
  )

  return (
    <div className="space-y-3">
      <section className="overflow-hidden rounded-sm border border-(--border-subtle) bg-(--surface-panel)">
        <DataGrid
          dataSource={dataSource}
          keyExpr="requesterNId"
          showBorders={false}
          showColumnLines={false}
          showRowLines={true}
          rowAlternationEnabled={false}
          columnAutoWidth={true}
          wordWrapEnabled={false}
          noDataText="No requester data"
          height="calc(100vh - 250px)"
        >
          <RemoteOperations filtering paging sorting grouping={false} summary={false} />
          <FilterRow visible={true} />
          <HeaderFilter visible={true} />
          <Scrolling mode="virtual" rowRenderingMode="virtual" />

          <Column dataField="requesterNId" caption="NID" width={140} />
          <Column dataField="requesterName" caption="Requester" minWidth={220} />
          <Column dataField="departmentName" caption="Department" minWidth={180} />
          <Column
            dataField="quotationCount"
            caption="Quotations"
            width={120}
            alignment="right"
          />
          <Column
            caption="Action"
            width={160}
            alignment="center"
            cellRender={(cell) => {
              const data = cell.data as RequesterRow
              const disabled = !data.requesterNId || data.requesterNId === '-'

              return (
                <button
                  type="button"
                  disabled={disabled}
                  onClick={() => {
                    if (disabled) return

                    const query = new URLSearchParams({
                      requesterNId: data.requesterNId,
                      requesterName: data.requesterName,
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

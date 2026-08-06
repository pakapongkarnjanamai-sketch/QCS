import { useCallback, useMemo, useRef, useState } from 'react'
import DataGrid, {
  Column,
  Export,
  FilterRow,
  HeaderFilter,
  Item,
  RemoteOperations,
  Scrolling,
  Toolbar,
} from 'devextreme-react/data-grid'
import type { ExportingEvent } from 'devextreme/ui/data_grid'
import { confirm } from 'devextreme/ui/dialog'
import { createDataSource } from '../../lib/createDataSource.ts'
import { appConfig } from '../../config/appConfig.ts'
import { fetchWithAccessControl } from '../../lib/apiClient.ts'
import { exportDataGridToExcel } from '../../lib/exportDataGridToExcel.ts'
import { toast } from '../../lib/toast.ts'

type RequestRow = {
  id: number
  code: string
  title: string
  vendorCode: string
  vendorName: string
  requestDate: string
  currentStepId: number
  requesterName: string
  remark: string
  validFrom: string | null
  validUntil: string | null
}

type Category = {
  key: string
  label: string
  path: string
  description: string
}

const CATEGORIES: Category[] = [
// Labels are the central Approval Service's status names; the route URLs are historical and stay
// as they are. `Admin/Pending` filters InProcess and `Admin/Approved` filters Completed — renaming
// the endpoints would have broken every caller to rename a word.
//
// These four queues do NOT cover every status. Returned, Waiting effective and Cancelled have no
// queue of their own, so a request in one of those appears under All and nowhere else. That is why
// All leads and why the note below says so: an operator who adds up the other four and compares to
// All must not read the difference as missing data.
  {
    key: 'all',
    label: 'All',
    path: '/api/Request/Admin/All',
    description: 'Every request, including Returned, Waiting effective and Cancelled',
  },
  {
    key: 'draft',
    label: 'Draft',
    path: '/api/Request/Admin/Draft',
    description: 'Saved but not yet submitted',
  },
  {
    key: 'pending',
    label: 'In process',
    path: '/api/Request/Admin/Pending',
    description: 'Somewhere in the approval route',
  },
  {
    key: 'approved',
    label: 'Completed',
    path: '/api/Request/Admin/Approved',
    description: 'Approved and in force — quotation ready',
  },
  {
    key: 'rejected',
    label: 'Rejected',
    path: '/api/Request/Admin/Rejected',
    description: 'Rejected by an approver',
  },
]

export function RequestsPage() {
  const [activeKey, setActiveKey] = useState<string>('all')
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const gridRef = useRef<any>(null)

  const activeCategory = CATEGORIES.find((c) => c.key === activeKey)!
  const isDraft = activeKey === 'draft'

  const dataSource = useMemo(
    () => createDataSource<RequestRow>(activeCategory.path, 'id'),
    [activeCategory.path],
  )

  const handleDelete = useCallback(async (row: RequestRow) => {
    const confirmed = await confirm(
      `Delete draft "${row.code}"? This cannot be undone.`,
      'Delete Draft',
    )
    if (!confirmed) return

    try {
      const res = await fetchWithAccessControl(
        `${appConfig.apiBaseUrl}/api/Request/${row.id}`,
        { method: 'DELETE', credentials: 'include' },
      )
      if (!res.ok) {
        const body = await res.json().catch(() => null)
        throw new Error(body?.detail ?? body?.message ?? `Delete failed (${res.status})`)
      }
      toast.success(`Deleted ${row.code}`)
      gridRef.current?.instance().refresh()
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Delete failed')
    }
  }, [])

  const handleExporting = useCallback((e: ExportingEvent) => {
    const stamp = new Date().toISOString().slice(0, 10).replace(/-/g, '')
    void exportDataGridToExcel({
      component: e.component,
      fileName: `Requests_${activeKey}_${stamp}`,
      worksheetName: 'Requests',
    }).catch((err) => {
      toast.error(err instanceof Error ? err.message : 'Export failed')
    })
    e.cancel = true
  }, [activeKey])

  return (
    <div className="flex flex-1 min-h-0 min-w-0 flex-col">
      <div className="mb-3 shrink-0">
        <div className="overflow-x-auto">
          <div
            className="inline-flex min-w-max gap-1 rounded-sm border border-(--border-subtle) bg-(--surface-panel) p-1"
            role="tablist"
            aria-label="Request status"
          >
            {CATEGORIES.map((cat) => {
              const isActive = cat.key === activeKey
              return (
                <button
                  key={cat.key}
                  type="button"
                  role="tab"
                  aria-selected={isActive}
                  onClick={() => setActiveKey(cat.key)}
                  className={`rounded-sm px-3 py-1.5 text-left text-[12px] transition-colors ${
                    isActive
                      ? 'bg-(--surface-muted) font-medium text-(--ink-strong)'
                      : 'text-(--ink-muted) hover:bg-(--surface-muted) hover:text-(--ink-strong)'
                  }`}
                >
                  {cat.label}
                </button>
              )
            })}
          </div>
        </div>
        <p className="mt-2 px-1 text-[12px] text-(--ink-soft)">{activeCategory.description}</p>
      </div>

      <section className="flex-1 min-h-0 overflow-hidden rounded-sm border border-(--border-subtle) bg-(--surface-panel)">
        <DataGrid
          ref={gridRef}
          key={activeKey}
          dataSource={dataSource}
          onExporting={handleExporting}
          showBorders={false}
          showColumnLines={false}
          showRowLines={true}
          rowAlternationEnabled={false}
          columnAutoWidth={true}
          wordWrapEnabled={false}
          height="100%"
        >
          <RemoteOperations
            filtering={true}
            paging={true}
            sorting={true}
            grouping={false}
            summary={false}
          />

          <FilterRow visible={true} />
          <HeaderFilter visible={true} />

          <Scrolling mode="virtual" rowRenderingMode="virtual" />
          <Export enabled={true} allowExportSelectedData={false} />
          <Toolbar>
            <Item name="exportButton" location="after" />
          </Toolbar>

          <Column dataField="code" caption="Doc No." width={140} />
          <Column dataField="title" caption="Title" minWidth={200} />
          <Column dataField="requesterName" caption="Requester" width={160} />
          <Column dataField="vendorName" caption="Vendor" width={180} />
          <Column
            dataField="requestDate"
            caption="Date"
            dataType="date"
            format="dd/MM/yyyy"
            width={110}
            alignment="center"
            sortOrder="desc"
            sortIndex={0}
          />
          <Column dataField="remark" caption="Remark" minWidth={120} />
          <Column
            dataField="validFrom"
            caption="Valid From"
            dataType="date"
            format="dd/MM/yyyy"
            width={110}
            alignment="center"
          />
          <Column
            dataField="validUntil"
            caption="Valid Until"
            dataType="date"
            format="dd/MM/yyyy"
            width={110}
            alignment="center"
          />
          {isDraft && (
            <Column
              caption=""
              width={70}
              allowFiltering={false}
              allowSorting={false}
              allowHeaderFiltering={false}
              cellRender={({ data }: { data: RequestRow }) => (
                <button
                  type="button"
                  onClick={() => handleDelete(data)}
                  className="text-[12px] text-red-600 hover:text-red-800 transition-colors"
                >
                  Delete
                </button>
              )}
            />
          )}
        </DataGrid>
      </section>
    </div>
  )
}


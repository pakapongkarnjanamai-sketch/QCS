import { useMemo, useState } from 'react'
import DataGrid, {
  Column,
  FilterRow,
  HeaderFilter,
  RemoteOperations,
  Scrolling,
} from 'devextreme-react/data-grid'
import { createDataSource } from '../../lib/createDataSource.ts'

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
  {
    key: 'all',
    label: 'All',
    path: '/api/Request/Admin/All',
    description: 'All requests in the system',
  },
  {
    key: 'draft',
    label: 'Draft',
    path: '/api/Request/Admin/Draft',
    description: 'Saved but not yet submitted',
  },
  {
    key: 'pending',
    label: 'Pending',
    path: '/api/Request/Admin/Pending',
    description: 'Awaiting approval',
  },
  {
    key: 'approved',
    label: 'Approved',
    path: '/api/Request/Admin/Approved',
    description: 'Fully approved',
  },
  {
    key: 'rejected',
    label: 'Rejected',
    path: '/api/Request/Admin/Rejected',
    description: 'Rejected by approver',
  },
]

export function RequestsPage() {
  const [activeKey, setActiveKey] = useState<string>('all')

  const activeCategory = CATEGORIES.find((c) => c.key === activeKey)!

  const dataSource = useMemo(
    () => createDataSource<RequestRow>(activeCategory.path, 'id'),
    [activeCategory.path],
  )

  return (
    <div className="flex flex-1 min-h-0 gap-4">
      {/* Sub-sidebar */}
      <nav className="w-44 shrink-0">
        <p className="mb-2 px-2 text-[11px] font-medium uppercase tracking-[0.12em] text-(--ink-soft)">
          Status
        </p>
        <ul className="space-y-0.5">
          {CATEGORIES.map((cat) => {
            const isActive = cat.key === activeKey
            return (
              <li key={cat.key}>
                <button
                  type="button"
                  onClick={() => setActiveKey(cat.key)}
                  className={`w-full rounded-sm px-3 py-2 text-left text-[13px] transition-colors ${
                    isActive
                      ? 'bg-(--surface-muted) font-medium text-(--ink-strong)'
                      : 'text-(--ink-muted) hover:bg-(--surface-muted) hover:text-(--ink-strong)'
                  }`}
                >
                  {cat.label}
                </button>
              </li>
            )
          })}
        </ul>
      </nav>

      {/* Main content */}
      <div className="flex flex-col min-h-0 min-w-0 flex-1">
        <div className="mb-3 shrink-0">
          <p className="text-[12px] text-(--ink-soft)">{activeCategory.description}</p>
        </div>
        <section className="flex-1 min-h-0 overflow-hidden rounded-sm border border-(--border-subtle) bg-(--surface-panel)">
          <DataGrid
            key={activeKey}
            dataSource={dataSource}
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
          </DataGrid>
        </section>
      </div>
    </div>
  )
}


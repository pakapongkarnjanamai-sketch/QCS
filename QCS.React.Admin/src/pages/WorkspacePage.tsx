import { useState } from 'react'
import type { WorkspaceDefinition, WorkspaceRow } from './pageData.ts'
import { Toolbar } from '../components/ui/Toolbar.tsx'
import { TableSurface } from '../components/ui/TableSurface.tsx'
import type { TableColumn } from '../components/ui/TableSurface.tsx'
import { SidePanel } from '../components/ui/SidePanel.tsx'

type WorkspacePageProps = {
  page: WorkspaceDefinition
}

const ROW_COLUMNS: TableColumn<WorkspaceRow>[] = [
  { key: 'name', label: 'Record' },
  { key: 'context', label: 'Context' },
  { key: 'owner', label: 'Owner' },
  { key: 'updated', label: 'Updated' },
]

const actionButtonClassName =
  'focus-ring inline-flex h-9 items-center justify-center rounded-sm border border-[var(--border-subtle)] px-3 text-[13px] font-medium text-[var(--ink-strong)]'

export function WorkspacePage({ page }: WorkspacePageProps) {
  const [activeFilter, setActiveFilter] = useState(0)
  const [search, setSearch] = useState('')

  const filteredRows = page.rows.filter(
    (row) =>
      search === '' ||
      row.name.toLowerCase().includes(search.toLowerCase()) ||
      row.context.toLowerCase().includes(search.toLowerCase()),
  )

  return (
    <div className="space-y-6">
      <section className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div className="max-w-[72ch] space-y-2">
          <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-[var(--ink-soft)]">
            {page.eyebrow}
          </p>
          <div className="space-y-1">
            <h2
              className="text-[28px] font-semibold leading-none text-[var(--ink-strong)]"
              style={{ fontFamily: 'var(--font-display)' }}
            >
              {page.title}
            </h2>
            <p className="text-[13px] leading-6 text-[var(--ink-muted)]">{page.description}</p>
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <button type="button" className={actionButtonClassName}>
            {page.secondaryAction}
          </button>
          <button
            type="button"
            className="focus-ring inline-flex h-9 items-center justify-center rounded-sm border border-[var(--ink-strong)] bg-[var(--ink-strong)] px-3 text-[13px] font-medium text-[var(--surface-panel)]"
          >
            {page.primaryAction}
          </button>
        </div>
      </section>

      <section className="grid gap-px overflow-hidden rounded-sm border border-[var(--border-subtle)] bg-[var(--border-subtle)] sm:grid-cols-3">
        {page.focusItems.map((item) => (
          <div key={item.label} className="bg-[var(--surface-panel)] px-4 py-3">
            <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-[var(--ink-soft)]">
              {item.label}
            </p>
            <p className="mt-1 text-[13px] leading-6 text-[var(--ink-strong)]">{item.value}</p>
          </div>
        ))}
      </section>

      <section className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_320px]">
        <div className="overflow-hidden rounded-sm border border-[var(--border-subtle)] bg-[var(--surface-panel)]">
          <Toolbar
            title={page.tableTitle}
            description={page.tableDescription}
            searchPlaceholder={`Search ${page.title.toLowerCase()}`}
            filters={page.toolbarFilters}
            activeFilterIndex={activeFilter}
            onFilterChange={setActiveFilter}
            onSearch={setSearch}
          />
          <TableSurface<WorkspaceRow>
            columns={ROW_COLUMNS}
            rows={filteredRows}
            rowKey="name"
            actionLabel="Inspect"
            onAction={(row) => {
              console.info('Inspect:', row.name)
            }}
          />
        </div>

        <SidePanel title={page.sideTitle} items={page.sideItems} />
      </section>
    </div>
  )
}
import { ChevronDown, ChevronUp, LoaderCircle } from 'lucide-react'
import { Link } from 'react-router'
import { AppButton } from '@/components/ui/AppButton'
import { StatusBadge } from '@/components/ui/StatusBadge'
import type { ApiError } from '@/lib/apiClient'
import type { PortalPage, PortalRequestListItem } from './types'

interface RequestTableProps {
  data: PortalPage<PortalRequestListItem>
  refreshing: boolean
  loadingMore: boolean
  loadMoreError?: ApiError
  returnSearch: string
  returnPath: string
  sortBy?: string
  sortDescending: boolean
  onSort: (key: string) => void
  onRetryLoadMore: () => void
  tableScrollRef: React.RefObject<HTMLDivElement | null>
  sentinelRef: React.RefObject<HTMLTableRowElement | null>
}

function formatDate(value?: string): string {
  return value ? new Intl.DateTimeFormat('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }).format(new Date(value)) : '-'
}

function isFinal(row: PortalRequestListItem): boolean {
  return row.currentStepId === 99 || ['approved', 'completed'].includes(row.statusName.toLowerCase())
}

function SortButton({ label, sortKey, active, descending, onSort }: { label: string; sortKey: string; active: boolean; descending: boolean; onSort: (key: string) => void }) {
  return <button type="button" onClick={() => onSort(sortKey)} className="inline-flex items-center gap-1 rounded-sm font-medium hover:text-ink-strong focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">{label}{active && (descending ? <ChevronDown size={14} aria-label="descending" /> : <ChevronUp size={14} aria-label="ascending" />)}</button>
}

export function RequestTable({ data, refreshing, loadingMore, loadMoreError, returnSearch, returnPath, sortBy, sortDescending, onSort, onRetryLoadMore, tableScrollRef, sentinelRef }: RequestTableProps) {
  return (
    <section className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-sm border border-border-subtle bg-surface-panel">
      <div ref={tableScrollRef} aria-busy={refreshing} className={`min-h-0 flex-1 overflow-auto ${refreshing ? 'pointer-events-none opacity-60' : ''}`}>
        <table className="min-w-[960px] w-full border-collapse text-left text-body">
          <thead className="sticky top-0 z-10 border-b border-border-subtle bg-surface-muted text-caption uppercase tracking-[0.08em] text-ink-muted">
            <tr>
              <th className="whitespace-nowrap px-4 py-2.5"><SortButton label="CODE" sortKey="code" active={sortBy === 'code'} descending={sortDescending} onSort={onSort} /></th>
              <th className="px-4 py-2.5"><SortButton label="TITLE" sortKey="title" active={sortBy === 'title'} descending={sortDescending} onSort={onSort} /></th>
              <th className="px-4 py-2.5"><SortButton label="VENDOR" sortKey="vendorname" active={sortBy === 'vendorname'} descending={sortDescending} onSort={onSort} /></th>
              <th className="px-4 py-2.5">REQUESTER</th>
              <th className="whitespace-nowrap px-4 py-2.5"><SortButton label="REQUEST DATE" sortKey="requestdate" active={sortBy === 'requestdate'} descending={sortDescending} onSort={onSort} /></th>
              <th className="whitespace-nowrap px-4 py-2.5">VALIDITY</th>
              <th className="whitespace-nowrap px-4 py-2.5"><SortButton label="STATUS" sortKey="status" active={sortBy === 'status'} descending={sortDescending} onSort={onSort} /></th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border-subtle">
            {data.items.map((row) => {
              const final = isFinal(row)
              const destination = final ? `/quotations/${encodeURIComponent(row.code)}` : `/requests/${row.id}`

              return (
                <tr key={row.id} className="hover:bg-surface-muted">
                  <td className="whitespace-nowrap px-4 py-2.5 font-medium"><Link state={{ workspaceSearch: returnSearch, returnPath }} to={destination} className="rounded-sm text-accent underline decoration-1 underline-offset-2 hover:text-accent-hover focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">{row.code}</Link></td>
                  <td className="px-4 py-2.5"><span className="line-clamp-2">{row.title}</span></td>
                  <td className="px-4 py-2.5 text-ink-muted">{row.vendorName || row.vendorCode || '-'}</td>
                  <td className="px-4 py-2.5 text-ink-muted">{row.requesterName || row.requesterNId || '-'}</td>
                  <td className="whitespace-nowrap px-4 py-2.5 text-ink-muted">{formatDate(row.requestDate)}</td>
                  <td className="whitespace-nowrap px-4 py-2.5 text-ink-muted">{formatDate(row.validUntil)}</td>
                  <td className="px-4 py-2.5"><StatusBadge status={row.statusName} /></td>
                </tr>
              )
            })}
            {data.hasNextPage && <tr ref={sentinelRef}><td colSpan={7} className="px-4 py-2.5 text-center text-caption text-ink-muted">{loadMoreError ? <span className="inline-flex items-center gap-2">Could not load more requests.<AppButton variant="secondary" onClick={onRetryLoadMore}>Try again</AppButton></span> : loadingMore && <span className="inline-flex items-center gap-2"><LoaderCircle className="size-4 animate-spin" aria-hidden />Loading more...</span>}</td></tr>}
          </tbody>
        </table>
      </div>
    </section>
  )
}
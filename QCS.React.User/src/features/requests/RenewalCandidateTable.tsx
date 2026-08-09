import { ChevronLeft, ChevronRight } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { IconButton } from '@/components/ui/IconButton'
import { LookupTableShell } from '@/components/ui/LookupTableShell'
import { toApiError } from '@/lib/apiClient'
import { getRenewalCandidates } from './requestApi'
import type { PortalPage, RenewalCandidate } from './types'

interface RenewalCandidateTableProps {
  selectedId?: number
  onSelect: (candidate: RenewalCandidate) => void
}

export function RenewalCandidateTable({ selectedId, onSelect }: RenewalCandidateTableProps) {
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [pageSize] = useState(10)
  const [data, setData] = useState<PortalPage<RenewalCandidate>>()
  const [loading, setLoading] = useState(true)
  const [refreshing, setRefreshing] = useState(false)
  const [error, setError] = useState<string>()
  const [retryToken, setRetryToken] = useState(0)
  const hasDataRef = useRef(false)

  useEffect(() => {
    const controller = new AbortController()
    setLoading(!hasDataRef.current)
    setRefreshing(hasDataRef.current)
    setError(undefined)

    const timer = setTimeout(() => {
      getRenewalCandidates({ search: search.trim(), page, pageSize }, controller.signal)
        .then((result) => {
          hasDataRef.current = true
          setData(result)
        })
        .catch((reason: unknown) => {
          if (!controller.signal.aborted) {
            const apiError = toApiError(reason)
            setError(apiError.detail ?? apiError.title)
          }
        })
        .finally(() => {
          if (!controller.signal.aborted) {
            setLoading(false)
            setRefreshing(false)
          }
        })
    }, 300)

    return () => {
      clearTimeout(timer)
      controller.abort()
    }
  }, [search, page, pageSize, retryToken])

  const handleSearchChange = (value: string) => {
    setSearch(value)
    setPage(1)
  }

  return (
    <LookupTableShell
      title="Select QCS quotation to renew"
      search={search}
      searchPlaceholder="Search code, title, vendor..."
      onSearchChange={handleSearchChange}
      loading={loading}
      refreshing={refreshing}
      error={error}
      hasData={Boolean(data)}
      isEmpty={data?.items.length === 0}
      emptyMessage="No eligible quotations found for renewal."
      onRetry={() => setRetryToken((token) => token + 1)}
      footer={data && <div className="mt-3 flex items-center justify-between border-t border-border-subtle pt-3 text-caption text-ink-muted">
        <span>Total {data.totalCount} item{data.totalCount !== 1 ? 's' : ''} (Page {data.page})</span>
        <div className="flex items-center gap-1">
          <IconButton size="sm" disabled={data.page <= 1} onClick={() => setPage((current) => Math.max(1, current - 1))} label="Previous page"><ChevronLeft className="size-4" /></IconButton>
          <IconButton size="sm" disabled={!data.hasNextPage} onClick={() => setPage((current) => current + 1)} label="Next page"><ChevronRight className="size-4" /></IconButton>
        </div>
      </div>}
    >
      {data && (
        <div className="overflow-x-auto">
              <table className="w-full min-w-[820px] border-collapse text-left text-body">
                <thead>
                  <tr className="border-b border-border-subtle bg-surface-muted text-caption font-medium text-ink-muted">
                    <th className="px-4 py-2.5">Select</th>
                    <th className="px-4 py-2.5">Code</th>
                    <th className="px-4 py-2.5">Title</th>
                    <th className="px-4 py-2.5">Vendor</th>
                    <th className="px-4 py-2.5">Valid Until</th>
                    <th className="px-4 py-2.5">Status</th>
                    <th className="px-4 py-2.5">QRS Source</th>
                    <th className="px-4 py-2.5 text-right">PDFs</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border-subtle">
                  {data.items.map((item) => {
                    const isSelected = item.id === selectedId
                    return (
                      <tr
                        key={item.id}
                        onClick={() => onSelect(item)}
                        className={`cursor-pointer transition-colors hover:bg-surface-muted ${
                          isSelected ? 'bg-accent-soft font-medium' : ''
                        }`}
                      >
                        <td className="px-4 py-2.5">
                          <input
                            type="radio"
                            name="renewalCandidate"
                            checked={isSelected}
                            onChange={() => onSelect(item)}
                            className="h-4 w-4 text-accent"
                          />
                        </td>
                        <td className="px-4 py-2.5 font-mono text-ink-strong">{item.code}</td>
                        <td className="px-4 py-2.5">{item.title}</td>
                        <td className="px-4 py-2.5">{item.vendorName || item.vendorCode}</td>
                        <td className="px-4 py-2.5 text-caption text-ink-muted">
                          {item.validUntil ? item.validUntil.slice(0, 10) : '-'}
                        </td>
                        <td className="px-4 py-2.5 text-caption text-ink-muted">{item.renewalWindowStatus === 'Expired' ? 'Expired' : 'Expiring soon'}</td>
                        <td className="px-4 py-2.5 font-mono text-caption">{item.sourceCode || '-'}</td>
                        <td className="px-4 py-2.5 text-right text-caption font-medium">
                          {item.originalQuotationCount}
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
        </div>
      )}
    </LookupTableShell>
  )
}

import { Search, ChevronLeft, ChevronRight } from 'lucide-react'
import { useEffect, useState } from 'react'
import { appInputClassName } from '@/components/ui/inputStyles'
import { ErrorSurface, LoadingSurface } from '@/components/ui/Surfaces'
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
  const [error, setError] = useState<string>()
  const [retryToken, setRetryToken] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    setLoading(true)
    setError(undefined)

    const timer = setTimeout(() => {
      getRenewalCandidates({ search: search.trim(), page, pageSize }, controller.signal)
        .then((result) => {
          setData(result)
        })
        .catch((reason: unknown) => {
          if (!controller.signal.aborted) {
            setError(toApiError(reason).title)
          }
        })
        .finally(() => {
          if (!controller.signal.aborted) {
            setLoading(false)
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
    <div className="flex flex-col gap-3 rounded-md border border-border-subtle bg-surface-panel p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h4 className="text-subheading font-medium text-ink-strong">
          Select QCS Quotation to Renew
        </h4>
        <div className="relative min-w-[240px]">
          <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-ink-muted" />
          <input
            type="text"
            value={search}
            onChange={(e) => handleSearchChange(e.target.value)}
            placeholder="Search code, title, vendor..."
            className={appInputClassName('sm', 'w-full pl-8')}
          />
        </div>
      </div>

      {loading && <LoadingSurface />}
      {error && <ErrorSurface><div className="flex flex-wrap items-center justify-between gap-3"><span>{error}</span><button type="button" onClick={() => setRetryToken((token) => token + 1)} className="rounded-sm text-accent underline underline-offset-2">Try again</button></div></ErrorSurface>}

      {!loading && !error && data && (
        <>
          {data.items.length === 0 ? (
            <div className="p-4 text-center text-body text-ink-muted">
              No eligible quotations found for renewal.
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-body">
                <thead>
                  <tr className="border-b border-border-subtle bg-surface-muted text-caption font-medium text-ink-muted">
                    <th className="px-3 py-2">Select</th>
                    <th className="px-3 py-2">Code</th>
                    <th className="px-3 py-2">Title</th>
                    <th className="px-3 py-2">Vendor</th>
                    <th className="px-3 py-2">Valid Until</th>
                    <th className="px-3 py-2">Status</th>
                    <th className="px-3 py-2">QRS Source</th>
                    <th className="px-3 py-2 text-right">PDFs</th>
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
                          isSelected ? 'bg-accent-subtle/20 font-medium' : ''
                        }`}
                      >
                        <td className="px-3 py-2">
                          <input
                            type="radio"
                            name="renewalCandidate"
                            checked={isSelected}
                            onChange={() => onSelect(item)}
                            className="h-4 w-4 text-accent"
                          />
                        </td>
                        <td className="px-3 py-2 font-mono text-ink-strong">{item.code}</td>
                        <td className="px-3 py-2">{item.title}</td>
                        <td className="px-3 py-2">{item.vendorName || item.vendorCode}</td>
                        <td className="px-3 py-2 text-caption text-ink-muted">
                          {item.validUntil ? item.validUntil.slice(0, 10) : '-'}
                        </td>
                        <td className="px-3 py-2 text-caption text-ink-muted">{item.renewalWindowStatus === 'Expired' ? 'Expired' : 'Expiring soon'}</td>
                        <td className="px-3 py-2 font-mono text-caption">{item.sourceCode || '-'}</td>
                        <td className="px-3 py-2 text-right text-caption font-medium">
                          {item.originalQuotationCount}
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}

          <div className="flex items-center justify-between text-caption text-ink-muted pt-2 border-t border-border-subtle">
            <span>
              Total {data.totalCount} item{data.totalCount !== 1 ? 's' : ''} (Page {data.page})
            </span>
            <div className="flex items-center gap-1">
              <button
                type="button"
                disabled={data.page <= 1}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                className="rounded p-1 hover:bg-surface-muted disabled:opacity-40"
                aria-label="Previous page"
              >
                <ChevronLeft className="h-4 w-4" />
              </button>
              <button
                type="button"
                disabled={!data.hasNextPage}
                onClick={() => setPage((p) => p + 1)}
                className="rounded p-1 hover:bg-surface-muted disabled:opacity-40"
                aria-label="Next page"
              >
                <ChevronRight className="h-4 w-4" />
              </button>
            </div>
          </div>
        </>
      )}
    </div>
  )
}

import { Search, ChevronLeft, ChevronRight, AlertCircle } from 'lucide-react'
import { useEffect, useState } from 'react'
import { appInputClassName } from '@/components/ui/inputStyles'
import { LoadingSurface } from '@/components/ui/Surfaces'
import { toApiError } from '@/lib/apiClient'
import { getQrsSourcingRequests } from './requestApi'
import type { QrsSourcingPage, QrsSourcingRequest } from './types'

interface QrsSourceTableProps {
  selectedCode?: string
  intent: 'New' | 'Renewal'
  allowManualCode?: boolean
  onSelect: (row: { code: string; title?: string }) => void
}

export function QrsSourceTable({ selectedCode, intent, allowManualCode = false, onSelect }: QrsSourceTableProps) {
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [pageSize] = useState(10)
  const [data, setData] = useState<QrsSourcingPage<QrsSourcingRequest>>()
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string>()
  const [retryToken, setRetryToken] = useState(0)
  const [manualCode, setManualCode] = useState('')

  useEffect(() => {
    const controller = new AbortController()
    setLoading(true)
    setError(undefined)

    const timer = setTimeout(() => {
      getQrsSourcingRequests({ search: search.trim(), page, pageSize, intent }, controller.signal)
        .then(setData)
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
  }, [intent, search, page, pageSize, retryToken])

  const handleSearchChange = (value: string) => {
    setSearch(value)
    setPage(1)
  }

  const handleManualApply = () => {
    if (manualCode.trim()) {
      onSelect({ code: manualCode.trim().toUpperCase(), title: '' })
    }
  }

  return (
    <div className="flex flex-col gap-3 rounded-md border border-border-subtle bg-surface-panel p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h4 className="text-subheading font-medium text-ink-strong">
          Select QRS Sourcing Request
        </h4>
        <div className="relative min-w-[240px]">
          <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-ink-muted" />
          <input
            type="text"
            value={search}
            onChange={(e) => handleSearchChange(e.target.value)}
            placeholder="Search QRS code or title..."
            className={appInputClassName('sm', 'w-full pl-8')}
          />
        </div>
      </div>

      {loading && <LoadingSurface />}
      {error && (
        <div className="flex flex-col gap-2 rounded border border-warning/30 bg-warning-subtle/10 p-3 text-caption">
          <div className="flex items-center gap-2 text-ink-strong font-medium">
            <AlertCircle className="h-4 w-4 text-warning" />
            <span>QRS lookup unavailable</span>
          </div>
          <p className="text-ink-muted">Could not retrieve QRS list ({error}).</p>
          <button type="button" onClick={() => setRetryToken((token) => token + 1)} className="w-fit rounded-sm text-accent underline underline-offset-2">Try again</button>
        </div>
      )}

      {!loading && !error && data && (
        <>
          {data.items.length === 0 ? (
            <div className="p-4 text-center text-body text-ink-muted">
              No QRS sourcing requests found.
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-body">
                <thead>
                  <tr className="border-b border-border-subtle bg-surface-muted text-caption font-medium text-ink-muted">
                    <th className="px-3 py-2">Select</th>
                    <th className="px-3 py-2">Code</th>
                    <th className="px-3 py-2">Title</th>
                    <th className="px-3 py-2">Type</th>
                    <th className="px-3 py-2">Requester</th>
                    <th className="px-3 py-2">Department</th>
                    <th className="px-3 py-2">Required By</th>
                    <th className="px-3 py-2">Urgency</th>
                    <th className="px-3 py-2 text-right">Est. Total</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border-subtle">
                  {data.items.map((item) => {
                    const isSelected = item.code.toUpperCase() === selectedCode?.toUpperCase()
                    return (
                      <tr
                        key={item.code}
                        onClick={() => onSelect({ code: item.code, title: item.title })}
                        className={`cursor-pointer transition-colors hover:bg-surface-muted ${
                          isSelected ? 'bg-accent-subtle/20 font-medium' : ''
                        }`}
                      >
                        <td className="px-3 py-2">
                          <input
                            type="radio"
                            name="qrsSource"
                            checked={isSelected}
                            onChange={() => onSelect({ code: item.code, title: item.title })}
                            className="h-4 w-4 text-accent"
                          />
                        </td>
                        <td className="px-3 py-2 font-mono text-ink-strong">{item.code}</td>
                        <td className="px-3 py-2">{item.title}</td>
                        <td className="px-3 py-2 text-caption text-ink-muted">{item.requestTypeName}</td>
                        <td className="px-3 py-2 text-caption">{item.requesterName || '-'}</td>
                        <td className="px-3 py-2 text-caption">{item.requesterDepartment || '-'}</td>
                        <td className="px-3 py-2 text-caption text-ink-muted">
                          {item.requiredBy ? item.requiredBy.slice(0, 10) : '-'}
                        </td>
                        <td className="px-3 py-2 text-caption">{item.isUrgent ? 'Urgent' : 'Normal'}</td>
                        <td className="px-3 py-2 text-right text-caption font-medium">
                          {item.estimatedTotal.toLocaleString()} {item.currency}
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
              Total {data.totalCount} item{data.totalCount !== 1 ? 's' : ''} (Page {data.pageNumber})
            </span>
            <div className="flex items-center gap-1">
              <button
                type="button"
                disabled={!data.hasPreviousPage}
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

      {allowManualCode && <div className="flex flex-wrap items-center gap-2 border-t border-border-subtle pt-2">
        <span className="text-caption text-ink-muted">Manual QRS Code entry:</span>
        <input
          type="text"
          value={manualCode}
          onChange={(e) => setManualCode(e.target.value)}
          placeholder="e.g. QRS-20260806-001"
          className={appInputClassName('sm', 'min-w-0 flex-1 basis-48')}
        />
        <button
          type="button"
          onClick={handleManualApply}
          disabled={!manualCode.trim()}
          className="rounded bg-surface-muted px-3 py-1 text-caption font-medium hover:bg-surface-subtle disabled:opacity-40"
        >
          Use Code
        </button>
      </div>}
    </div>
  )
}

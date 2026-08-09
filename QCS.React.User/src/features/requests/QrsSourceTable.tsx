import { ChevronLeft, ChevronRight } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { AppButton } from '@/components/ui/AppButton'
import { appInputClassName } from '@/components/ui/inputStyles'
import { IconButton } from '@/components/ui/IconButton'
import { LookupTableShell } from '@/components/ui/LookupTableShell'
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
  const [refreshing, setRefreshing] = useState(false)
  const [error, setError] = useState<string>()
  const [retryToken, setRetryToken] = useState(0)
  const [manualCode, setManualCode] = useState('')
  const hasDataRef = useRef(false)
  const intentRef = useRef(intent)

  useEffect(() => {
    const controller = new AbortController()
    const contextChanged = intentRef.current !== intent
    if (contextChanged) {
      intentRef.current = intent
      hasDataRef.current = false
      setData(undefined)
    }
    setLoading(!hasDataRef.current)
    setRefreshing(hasDataRef.current)
    setError(undefined)

    const timer = setTimeout(() => {
      getQrsSourcingRequests({ search: search.trim(), page, pageSize, intent }, controller.signal)
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
    <LookupTableShell
      title="Select QRS sourcing request"
      search={search}
      searchPlaceholder="Search QRS code or title..."
      onSearchChange={handleSearchChange}
      loading={loading}
      refreshing={refreshing}
      error={error}
      hasData={Boolean(data)}
      isEmpty={data?.items.length === 0}
      emptyMessage="No QRS sourcing requests found."
      onRetry={() => setRetryToken((token) => token + 1)}
      footer={data && <div className="mt-3 flex items-center justify-between border-t border-border-subtle pt-3 text-caption text-ink-muted">
        <span>Total {data.totalCount} item{data.totalCount !== 1 ? 's' : ''} (Page {data.pageNumber})</span>
        <div className="flex items-center gap-1">
          <IconButton size="sm" disabled={!data.hasPreviousPage} onClick={() => setPage((current) => Math.max(1, current - 1))} label="Previous page"><ChevronLeft className="size-4" /></IconButton>
          <IconButton size="sm" disabled={!data.hasNextPage} onClick={() => setPage((current) => current + 1)} label="Next page"><ChevronRight className="size-4" /></IconButton>
        </div>
      </div>}
      after={allowManualCode && <div className="flex flex-wrap items-center gap-2 border-t border-border-subtle pt-3">
        <span className="text-caption text-ink-muted">Manual QRS Code entry:</span>
        <input type="text" value={manualCode} onChange={(event) => setManualCode(event.target.value)} placeholder="e.g. QRS-20260806-001" className={appInputClassName('sm', 'min-w-0 flex-1 basis-48')} />
        <AppButton variant="secondary" size="sm" onClick={handleManualApply} disabled={!manualCode.trim()}>Use Code</AppButton>
      </div>}
    >
      {data && (
        <div className="overflow-x-auto">
              <table className="w-full min-w-[920px] border-collapse text-left text-body">
                <thead>
                  <tr className="border-b border-border-subtle bg-surface-muted text-caption font-medium text-ink-muted">
                    <th className="px-4 py-2.5">Select</th>
                    <th className="px-4 py-2.5">Code</th>
                    <th className="px-4 py-2.5">Title</th>
                    <th className="px-4 py-2.5">Type</th>
                    <th className="px-4 py-2.5">Requester</th>
                    <th className="px-4 py-2.5">Department</th>
                    <th className="px-4 py-2.5">Required By</th>
                    <th className="px-4 py-2.5">Urgency</th>
                    <th className="px-4 py-2.5 text-right">Est. Total</th>
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
                          isSelected ? 'bg-accent-soft font-medium' : ''
                        }`}
                      >
                        <td className="px-4 py-2.5">
                          <input
                            type="radio"
                            name="qrsSource"
                            checked={isSelected}
                            onChange={() => onSelect({ code: item.code, title: item.title })}
                            className="h-4 w-4 text-accent"
                          />
                        </td>
                        <td className="px-4 py-2.5 font-mono text-ink-strong">{item.code}</td>
                        <td className="px-4 py-2.5">{item.title}</td>
                        <td className="px-4 py-2.5 text-caption text-ink-muted">{item.requestTypeName}</td>
                        <td className="px-4 py-2.5 text-caption">{item.requesterName || '-'}</td>
                        <td className="px-4 py-2.5 text-caption">{item.requesterDepartment || '-'}</td>
                        <td className="px-4 py-2.5 text-caption text-ink-muted">
                          {item.requiredBy ? item.requiredBy.slice(0, 10) : '-'}
                        </td>
                        <td className="px-4 py-2.5 text-caption">{item.isUrgent ? 'Urgent' : 'Normal'}</td>
                        <td className="px-4 py-2.5 text-right text-caption font-medium">
                          {item.estimatedTotal.toLocaleString()} {item.currency}
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

import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { appConfig } from '../../config/appConfig.ts'

type DashboardSummary = {
  myTaskCount: number
  myApprovedCount: number
  myRejectedCount: number
  myRequestCount: number
}

type DataSourceResult<T> = {
  data?: T[]
  totalCount?: number
}

type QueueCount = {
  key: 'all' | 'draft' | 'pending' | 'approved' | 'rejected'
  label: string
  description: string
  count: number
}

type TopVendorRow = {
  vendorCode: string
  vendorName: string
  quotationCount: number
}

type TopRequesterRow = {
  requesterNId: string
  requesterName: string
  quotationCount: number
}

type OverviewData = {
  summary: DashboardSummary
  queueCounts: QueueCount[]
  topVendors: TopVendorRow[]
  topRequesters: TopRequesterRow[]
}

type LoadIssue = {
  section: string
  message: string
}

type OverviewLoadResult = {
  data: OverviewData
  issues: LoadIssue[]
}

const queueDefinitions: Array<Pick<QueueCount, 'key' | 'label' | 'description'>> = [
  {
    key: 'all',
    label: 'All',
    description: 'Every document currently tracked in QCS.',
  },
  {
    key: 'draft',
    label: 'Draft',
    description: 'Prepared by requester but not submitted yet.',
  },
  {
    key: 'pending',
    label: 'Pending',
    description: 'Waiting for approval decision.',
  },
  {
    key: 'approved',
    label: 'Approved',
    description: 'Completed approval route and ready downstream.',
  },
  {
    key: 'rejected',
    label: 'Rejected',
    description: 'Returned for correction or canceled.',
  },
]

const cardClassName =
  'rounded-sm border border-(--border-subtle) bg-(--surface-panel) p-4 sm:p-6'

const listLinkClassName =
  'focus-ring flex min-h-11 items-center justify-between gap-3 border border-(--border-subtle) px-3 py-2.5 hover:bg-(--surface-muted)'

const numberFormat = new Intl.NumberFormat('en-US')

function formatCount(value: number): string {
  return numberFormat.format(value)
}

function toErrorMessage(reason: unknown): string {
  if (reason instanceof DOMException && reason.name === 'AbortError') {
    return 'Request was aborted.'
  }

  return reason instanceof Error ? reason.message : 'Unknown request error.'
}

async function fetchJson<T>(path: string, signal: AbortSignal): Promise<T> {
  const response = await fetch(`${appConfig.apiBaseUrl}${path}`, {
    credentials: 'include',
    signal,
  })

  if (!response.ok) {
    throw new Error(`Cannot load ${path} (${response.status})`)
  }

  return response.json() as Promise<T>
}

async function fetchQueueCount(path: string, signal: AbortSignal): Promise<number> {
  const query = new URLSearchParams({
    skip: '0',
    take: '1',
    requireTotalCount: 'true',
  })

  const result = await fetchJson<DataSourceResult<unknown>>(`${path}?${query.toString()}`, signal)
  return result.totalCount ?? 0
}

async function fetchOverviewData(signal: AbortSignal): Promise<OverviewLoadResult> {
  const results = await Promise.allSettled([
    fetchJson<DashboardSummary>('/api/Dashboard/Summary', signal),
    fetchQueueCount('/api/Request/Admin/All', signal),
    fetchQueueCount('/api/Request/Admin/Draft', signal),
    fetchQueueCount('/api/Request/Admin/Pending', signal),
    fetchQueueCount('/api/Request/Admin/Approved', signal),
    fetchQueueCount('/api/Request/Admin/Rejected', signal),
    fetchJson<DataSourceResult<TopVendorRow>>(
      '/api/Vendor/Grid?skip=0&take=5&sort=%5B%7B%22selector%22%3A%22quotationCount%22%2C%22desc%22%3Atrue%7D%5D',
      signal,
    ),
    fetchJson<DataSourceResult<TopRequesterRow>>(
      '/api/Request/Admin/Requesters?skip=0&take=5&sort=%5B%7B%22selector%22%3A%22quotationCount%22%2C%22desc%22%3Atrue%7D%5D',
      signal,
    ),
  ])

  const [summaryResult, allResult, draftResult, pendingResult, approvedResult, rejectedResult, vendorsResult, requestersResult] = results

  const issues: LoadIssue[] = []

  const summary =
    summaryResult.status === 'fulfilled'
      ? summaryResult.value
      : {
        myTaskCount: 0,
        myApprovedCount: 0,
        myRejectedCount: 0,
        myRequestCount: 0,
      }

  if (summaryResult.status === 'rejected') {
    issues.push({
      section: 'Summary',
      message: toErrorMessage(summaryResult.reason),
    })
  }

  const countByKey: Record<QueueCount['key'], number> = {
    all: allResult.status === 'fulfilled' ? allResult.value : 0,
    draft: draftResult.status === 'fulfilled' ? draftResult.value : 0,
    pending: pendingResult.status === 'fulfilled' ? pendingResult.value : 0,
    approved: approvedResult.status === 'fulfilled' ? approvedResult.value : 0,
    rejected: rejectedResult.status === 'fulfilled' ? rejectedResult.value : 0,
  }

  ;([
    { key: 'All queue', value: allResult },
    { key: 'Draft queue', value: draftResult },
    { key: 'Pending queue', value: pendingResult },
    { key: 'Approved queue', value: approvedResult },
    { key: 'Rejected queue', value: rejectedResult },
  ] as const).forEach((item) => {
    if (item.value.status === 'rejected') {
      issues.push({
        section: item.key,
        message: toErrorMessage(item.value.reason),
      })
    }
  })

  const topVendors = vendorsResult.status === 'fulfilled' ? vendorsResult.value.data ?? [] : []
  const topRequesters = requestersResult.status === 'fulfilled' ? requestersResult.value.data ?? [] : []

  if (vendorsResult.status === 'rejected') {
    issues.push({
      section: 'Top vendors',
      message: toErrorMessage(vendorsResult.reason),
    })
  }

  if (requestersResult.status === 'rejected') {
    issues.push({
      section: 'Top requesters',
      message: toErrorMessage(requestersResult.reason),
    })
  }

  const queueCounts: QueueCount[] = queueDefinitions.map((item) => {
    return {
      ...item,
      count: countByKey[item.key],
    }
  })

  return {
    data: {
      summary,
      queueCounts,
      topVendors,
      topRequesters,
    },
    issues,
  }
}

export function OverviewPage() {
  const [data, setData] = useState<OverviewData | null>(null)
  const [issues, setIssues] = useState<LoadIssue[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    const load = async () => {
      setLoading(true)
      setError(null)
      setIssues([])

      try {
        const result = await fetchOverviewData(controller.signal)
        setData(result.data)
        setIssues(result.issues)
      } catch (reason) {
        if (reason instanceof DOMException && reason.name === 'AbortError') {
          return
        }

        const message = reason instanceof Error ? reason.message : 'Cannot load overview data.'
        setError(message)
        setData(null)
      } finally {
        setLoading(false)
      }
    }

    void load()

    return () => {
      controller.abort()
    }
  }, [])

  const topSummary = useMemo(() => {
    if (!data) {
      return []
    }

    return [
      {
        label: 'My pending tasks',
        value: data.summary.myTaskCount,
        note: 'Approvals assigned to your account now.',
      },
      {
        label: 'My requests',
        value: data.summary.myRequestCount,
        note: 'Requests created by your account.',
      },
      {
        label: 'My approved',
        value: data.summary.myApprovedCount,
        note: 'Your requests that passed all approval steps.',
      },
      {
        label: 'My rejected',
        value: data.summary.myRejectedCount,
        note: 'Requests that need rework before resubmission.',
      },
    ]
  }, [data])

  return (
    <div className="space-y-4 sm:space-y-6">
      

      {loading && (
        <section
          role="status"
          aria-live="polite"
          className={`${cardClassName} text-[13px] text-(--ink-muted)`}
        >
          Loading overview data...
        </section>
      )}

      {error && (
        <section
          role="alert"
          aria-live="assertive"
          className={`${cardClassName} text-[13px] text-(--status-danger-text)`}
        >
          <p className="font-medium">Overview is temporarily unavailable.</p>
          <p className="mt-1 text-(--ink-muted)">{error}</p>
        </section>
      )}

      {!loading && !error && data && (
        <>
          {issues.length > 0 && (
            <section
              role="status"
              aria-live="polite"
              className="rounded-sm border border-(--border-subtle) bg-(--surface-muted) px-4 py-3"
            >
              <p className="text-[12px] font-medium text-(--ink-strong)">
                Some panels are showing fallback values.
              </p>
              <p className="mt-1 text-[12px] text-(--ink-muted)">
                Live updates are temporarily unavailable for {issues.map((item) => item.section).join(', ')}.
              </p>
            </section>
          )}

          <section className="grid gap-4 lg:grid-cols-[minmax(0,1.2fr)_minmax(0,0.8fr)]">
            <article className={cardClassName}>
              <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-(--ink-soft)">
                Workload now
              </p>
              <div className="mt-3 grid gap-4 sm:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
                <div className="rounded-sm border border-(--border-subtle) bg-(--surface-muted) px-4 py-4">
                  <p className="text-[12px] font-medium text-(--ink-muted)">Pending approvals</p>
                  <p
                    className="mt-2 text-[40px] font-semibold leading-none text-(--ink-strong)"
                    style={{ fontFamily: 'var(--font-display)' }}
                  >
                    {formatCount(data.summary.myTaskCount)}
                  </p>
                  <p className="mt-2 text-[12px] text-(--ink-muted)">
                    Priority queue assigned to your account.
                  </p>
                </div>

                <dl className="grid content-start gap-2">
                  {topSummary
                    .filter((item) => item.label !== 'My pending tasks')
                    .map((item) => (
                      <div
                        key={item.label}
                        className="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-3 border-b border-(--border-subtle) py-2"
                      >
                        <dt className="text-[12px] text-(--ink-muted)">{item.label}</dt>
                        <dd
                          className="text-[22px] font-semibold leading-none text-(--ink-strong)"
                          style={{ fontFamily: 'var(--font-display)' }}
                        >
                          {formatCount(item.value)}
                        </dd>
                      </div>
                    ))}
                </dl>
              </div>
            </article>

            <article className={cardClassName}>
              <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-(--ink-soft)">
                Operational notes
              </p>
              <ul className="mt-3 space-y-2 text-[12px] text-(--ink-muted)">
                <li className="border border-(--border-subtle) px-3 py-2.5">
                  Requests remain the single source for queue actions and approvals.
                </li>
                <li className="border border-(--border-subtle) px-3 py-2.5">
                  Quotation links here preserve requester and vendor filters for direct follow-up.
                </li>
                <li className="border border-(--border-subtle) px-3 py-2.5">
                  If fallback values appear, the page keeps partial data instead of blocking all operations.
                </li>
              </ul>
            </article>
          </section>

          <section className="grid gap-4 xl:grid-cols-[minmax(0,1.15fr)_minmax(0,0.85fr)]">
            <article className={cardClassName}>
              <div className="mb-3 flex items-end justify-between gap-3 border-b border-(--border-subtle) pb-3">
                <div>
                  <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-(--ink-soft)">
                    Queue health
                  </p>
                  <h3
                    className="mt-1 text-[21px] font-semibold leading-none text-(--ink-strong)"
                    style={{ fontFamily: 'var(--font-display)' }}
                  >
                    Request status distribution
                  </h3>
                </div>
                <Link
                  to="/requests"
                  className="focus-ring inline-flex min-h-11 items-center text-[12px] font-medium text-(--ink-muted) underline decoration-(--border-strong) underline-offset-4 hover:text-(--ink-strong)"
                >
                  View full queue
                </Link>
              </div>

              <div className="space-y-2">
                {data.queueCounts.map((item) => (
                  <div
                    key={item.key}
                    className="grid grid-cols-[minmax(0,1fr)_auto] items-start gap-x-4 gap-y-1 border border-(--border-subtle) bg-(--surface-muted) px-3 py-2"
                  >
                    <div>
                      <p className="text-[13px] font-medium text-(--ink-strong)">{item.label}</p>
                      <p className="text-[12px] text-(--ink-muted)">{item.description}</p>
                    </div>
                    <p
                      className="pt-0.5 text-[24px] font-semibold leading-none text-(--ink-strong)"
                      style={{ fontFamily: 'var(--font-display)' }}
                    >
                      {formatCount(item.count)}
                    </p>
                  </div>
                ))}
              </div>
            </article>

            <article className={cardClassName}>
              <div className="mb-3 border-b border-(--border-subtle) pb-3">
                <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-(--ink-soft)">
                  Traffic sources
                </p>
                <h3
                  className="mt-1 text-[21px] font-semibold leading-none text-(--ink-strong)"
                  style={{ fontFamily: 'var(--font-display)' }}
                >
                  Most active profiles
                </h3>
              </div>

              <div className="space-y-4">
                <section className="space-y-2">
                  <div className="flex items-center justify-between">
                    <p className="text-[12px] font-semibold uppercase tracking-[0.13em] text-(--ink-soft)">
                      Requesters
                    </p>
                    <Link
                      to="/requester"
                      className="focus-ring inline-flex min-h-11 items-center text-[12px] font-medium text-(--ink-muted) underline decoration-(--border-strong) underline-offset-4 hover:text-(--ink-strong)"
                    >
                      Open requester list
                    </Link>
                  </div>

                  {data.topRequesters.length === 0 ? (
                    <p className="text-[12px] text-(--ink-muted)">No requester activity found.</p>
                  ) : (
                    <ul className="space-y-1">
                      {data.topRequesters.map((item) => {
                        const query = new URLSearchParams({
                          requesterNId: item.requesterNId,
                          requesterName: item.requesterName,
                        })

                        return (
                          <li key={`${item.requesterNId}-${item.requesterName}`}>
                            <Link
                              to={`/quotations?${query.toString()}`}
                              className={listLinkClassName}
                            >
                              <span className="min-w-0">
                                <span className="block truncate text-[13px] font-medium text-(--ink-strong)">
                                  {item.requesterName}
                                </span>
                                <span className="block text-[12px] text-(--ink-muted)">{item.requesterNId}</span>
                              </span>
                              <span className="shrink-0 text-[12px] font-medium text-(--ink-muted)">
                                {formatCount(item.quotationCount)}
                              </span>
                            </Link>
                          </li>
                        )
                      })}
                    </ul>
                  )}
                </section>

                <section className="space-y-2">
                  <div className="flex items-center justify-between">
                    <p className="text-[12px] font-semibold uppercase tracking-[0.13em] text-(--ink-soft)">
                      Vendors
                    </p>
                    <Link
                      to="/vendors"
                      className="focus-ring inline-flex min-h-11 items-center text-[12px] font-medium text-(--ink-muted) underline decoration-(--border-strong) underline-offset-4 hover:text-(--ink-strong)"
                    >
                      Open vendor list
                    </Link>
                  </div>

                  {data.topVendors.length === 0 ? (
                    <p className="text-[12px] text-(--ink-muted)">No vendor activity found.</p>
                  ) : (
                    <ul className="space-y-1">
                      {data.topVendors.map((item) => {
                        const query = new URLSearchParams({
                          vendorCode: item.vendorCode,
                          vendorName: item.vendorName,
                        })

                        return (
                          <li key={item.vendorCode}>
                            <Link
                              to={`/quotations?${query.toString()}`}
                              className={listLinkClassName}
                            >
                              <span className="min-w-0">
                                <span className="block truncate text-[13px] font-medium text-(--ink-strong)">
                                  {item.vendorName}
                                </span>
                                <span className="block text-[12px] text-(--ink-muted)">{item.vendorCode}</span>
                              </span>
                              <span className="shrink-0 text-[12px] font-medium text-(--ink-muted)">
                                {formatCount(item.quotationCount)}
                              </span>
                            </Link>
                          </li>
                        )
                      })}
                    </ul>
                  )}
                </section>
              </div>
            </article>
          </section>
        </>
      )}
    </div>
  )
}

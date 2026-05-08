import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { appConfig } from '../../config/appConfig.ts'
import Chart, {
  Series as ChartSeries,
  CommonSeriesSettings,
  SeriesTemplate,
  Tooltip as ChartTooltip,
  ArgumentAxis,
  ValueAxis,
  Legend as ChartLegend,
} from 'devextreme-react/chart'
import TreeMap, { Tooltip as TreeMapTooltip, Colorizer } from 'devextreme-react/tree-map'
import PieChart, {
  Series as PieSeries,
  Tooltip as PieTooltip,
  Legend as PieLegend,
} from 'devextreme-react/pie-chart'

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

type TrendPoint = {
  year: number
  month: number
  label: string
  count: number
}

type SeriesTrendPoint = {
  label: string
  name: string
  count: number
}

type Granularity = 'week' | 'month' | 'year'

type ValidityStatus = {
  active: number
  expiringSoon: number
  expired: number
}

type ActiveVendorPoint = {
  name: string
  value: number
}

type StaticData = {
  requesterTrend: SeriesTrendPoint[]
  activeVendors: ActiveVendorPoint[]
  validityStatus: ValidityStatus
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

const GRANULARITY_OPTIONS: Array<{ key: Granularity; label: string; rangeLabel: string }> = [
  { key: 'week', label: 'Week', rangeLabel: 'Last 7 days' },
  { key: 'month', label: 'Month', rangeLabel: 'Last 4 weeks' },
  { key: 'year', label: 'Year', rangeLabel: 'Last 12 months' },
]

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

  const [
    summaryResult,
    allResult,
    draftResult,
    pendingResult,
    approvedResult,
    rejectedResult,
    vendorsResult,
    requestersResult,
  ] = results

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

async function fetchTrendData(granularity: Granularity, signal: AbortSignal): Promise<TrendPoint[]> {
  return fetchJson<TrendPoint[]>(`/api/Dashboard/RequestTrend?granularity=${granularity}`, signal)
}

async function fetchStaticData(signal: AbortSignal): Promise<StaticData> {
  const [requesterTrendResult, activeVendorsResult, validityResult] = await Promise.allSettled([
    fetchJson<SeriesTrendPoint[]>('/api/Dashboard/RequesterTrend?days=15&top=5', signal),
    fetchJson<ActiveVendorPoint[]>('/api/Dashboard/ActiveVendors?top=10', signal),
    fetchJson<ValidityStatus>('/api/Dashboard/ValidityStatus', signal),
  ])
  return {
    requesterTrend: requesterTrendResult.status === 'fulfilled' ? requesterTrendResult.value : [],
    activeVendors: activeVendorsResult.status === 'fulfilled' ? activeVendorsResult.value : [],
    validityStatus:
      validityResult.status === 'fulfilled'
        ? validityResult.value
        : { active: 0, expiringSoon: 0, expired: 0 },
  }
}

export function OverviewPage() {
  const [data, setData] = useState<OverviewData | null>(null)
  const [trend, setTrend] = useState<TrendPoint[]>([])
  const [trendLoading, setTrendLoading] = useState(false)
  const [staticData, setStaticData] = useState<StaticData>({
    requesterTrend: [],
    activeVendors: [],
    validityStatus: { active: 0, expiringSoon: 0, expired: 0 },
  })
  const [issues, setIssues] = useState<LoadIssue[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [granularity, setGranularity] = useState<Granularity>('week')

  // Load static data once
  useEffect(() => {
    const controller = new AbortController()
    void fetchStaticData(controller.signal).then((result) => {
      setStaticData(result)
    })
    return () => { controller.abort() }
  }, [])

  // Load initial page data once
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
        if (reason instanceof DOMException && reason.name === 'AbortError') return
        setError(reason instanceof Error ? reason.message : 'Cannot load overview data.')
        setData(null)
      } finally {
        setLoading(false)
      }
    }
    void load()
    return () => { controller.abort() }
  }, [])

  // Reload only the trend chart on granularity change
  useEffect(() => {
    const controller = new AbortController()
    const load = async () => {
      setTrendLoading(true)
      try {
        const result = await fetchTrendData(granularity, controller.signal)
        setTrend(result)
      } catch (reason) {
        if (reason instanceof DOMException && reason.name === 'AbortError') return
        setTrend([])
      } finally {
        setTrendLoading(false)
      }
    }
    void load()
    return () => { controller.abort() }
  }, [granularity])

  const activeRangeLabel = useMemo(
    () => GRANULARITY_OPTIONS.find((g) => g.key === granularity)?.rangeLabel ?? '',
    [granularity],
  )

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

          <section className={cardClassName}>
            <div className="flex flex-wrap items-end justify-between gap-3 border-b border-(--border-subtle) pb-3">
              <div>
                <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-(--ink-soft)">
                  Trend window
                </p>
                <h3
                  className="mt-1 text-[18px] font-semibold leading-none text-(--ink-strong)"
                  style={{ fontFamily: 'var(--font-display)' }}
                >
                  Aggregation: {activeRangeLabel}
                </h3>
              </div>
              <div
                role="group"
                aria-label="Trend granularity"
                className="inline-flex border border-(--border-subtle)"
              >
                {GRANULARITY_OPTIONS.map((opt) => {
                  const active = opt.key === granularity
                  return (
                    <button
                      key={opt.key}
                      type="button"
                      onClick={() => setGranularity(opt.key)}
                      aria-pressed={active}
                      className={`focus-ring min-h-11 px-4 text-[12px] font-medium ${
                        active
                          ? 'bg-(--ink-strong) text-(--surface-panel)'
                          : 'bg-(--surface-panel) text-(--ink-muted) hover:bg-(--surface-muted)'
                      }`}
                    >
                      {opt.label}
                    </button>
                  )
                })}
              </div>
            </div>

            <div className="mt-4">
              <p className="mb-1 text-[11px] font-semibold uppercase tracking-[0.14em] text-(--ink-soft)">
                Request volume
              </p>
              {trendLoading ? (
                <div
                  role="status"
                  aria-live="polite"
                  className="flex h-60 items-center justify-center text-[12px] text-(--ink-muted)"
                >
                  Loading…
                </div>
              ) : (
                <Chart dataSource={trend} height={240}>
                  <ChartSeries
                    valueField="count"
                    argumentField="label"
                    type="bar"
                    color="#3b82f6"
                    name="Requests"
                  />
                  <ArgumentAxis />
                  <ValueAxis allowDecimals={false} />
                  <ChartLegend visible={false} />
                  <ChartTooltip
                    enabled
                    customizeTooltip={(arg: { argument: string; value: number }) => ({
                      text: `${arg.argument}: ${formatCount(arg.value)}`,
                    })}
                  />
                </Chart>
              )}
            </div>
          </section>

          <section className="grid gap-4 xl:grid-cols-2">
            <article className={cardClassName}>
              <div className="mb-3 border-b border-(--border-subtle) pb-3">
                <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-(--ink-soft)">
                  Top requesters
                </p>
                <h3
                  className="mt-1 text-[16px] font-semibold leading-none text-(--ink-strong)"
                  style={{ fontFamily: 'var(--font-display)' }}
                >
                  Requests created by top 5 requesters
                </h3>
              </div>
              {staticData.requesterTrend.length === 0 ? (
                <p className="text-[12px] text-(--ink-muted)">No requester activity found.</p>
              ) : (
                <Chart dataSource={staticData.requesterTrend} height={260}>
                  <CommonSeriesSettings argumentField="label" valueField="count" type="line" />
                  <SeriesTemplate nameField="name" />
                  <ArgumentAxis />
                  <ValueAxis allowDecimals={false} />
                  <ChartLegend visible verticalAlignment="bottom" horizontalAlignment="center" />
                  <ChartTooltip
                    enabled
                    customizeTooltip={(arg: { seriesName: string; argument: string; value: number }) => ({
                      text: `${arg.seriesName}\n${arg.argument}: ${formatCount(arg.value)}`,
                    })}
                  />
                </Chart>
              )}
            </article>

            <article className={cardClassName}>
              <div className="mb-3 border-b border-(--border-subtle) pb-3">
                <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-(--ink-soft)">
                  Top vendors
                </p>
                <h3
                  className="mt-1 text-[16px] font-semibold leading-none text-(--ink-strong)"
                  style={{ fontFamily: 'var(--font-display)' }}
                >
                  Active quotations by vendor
                </h3>
              </div>
              {staticData.activeVendors.length === 0 ? (
                <p className="text-[12px] text-(--ink-muted)">No active quotations found.</p>
              ) : (
                <TreeMap
                  dataSource={staticData.activeVendors}
                  valueField="value"
                  labelField="name"
                  height={260}
                >
                  <Colorizer type="discrete" palette="Soft Pastel" />
                  <TreeMapTooltip
                    enabled
                    customizeTooltip={(arg: { node: { label: () => string }; value: number }) => ({
                      text: `${arg.node.label()}\n${formatCount(arg.value)} active quotations`,
                    })}
                  />
                </TreeMap>
              )}
            </article>
          </section>

          <section className={cardClassName}>
            <div className="mb-4 border-b border-(--border-subtle) pb-3">
              <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-(--ink-soft)">
                Validity health
              </p>
              <h3
                className="mt-1 text-[18px] font-semibold leading-none text-(--ink-strong)"
                style={{ fontFamily: 'var(--font-display)' }}
              >
                Quotation expiry status
              </h3>
            </div>
            <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_280px]">
              <div className="space-y-2">
                <div className="grid grid-cols-[1fr_auto] items-start gap-4 border border-(--border-subtle) bg-(--surface-muted) px-3 py-2">
                  <div>
                    <p className="text-[13px] font-medium text-(--ink-strong)">Active</p>
                    <p className="text-[12px] text-(--ink-muted)">No expiry set, or more than 30 days remaining</p>
                  </div>
                  <p
                    className="pt-0.5 text-[24px] font-semibold leading-none"
                    style={{ fontFamily: 'var(--font-display)', color: '#16a34a' }}
                  >
                    {formatCount(staticData.validityStatus.active)}
                  </p>
                </div>
                <div className="grid grid-cols-[1fr_auto] items-start gap-4 border border-(--border-subtle) bg-(--surface-muted) px-3 py-2">
                  <div>
                    <p className="text-[13px] font-medium text-(--ink-strong)">Expiring this month</p>
                    <p className="text-[12px] text-(--ink-muted)">Valid now but expires within 30 days</p>
                  </div>
                  <p
                    className="pt-0.5 text-[24px] font-semibold leading-none"
                    style={{ fontFamily: 'var(--font-display)', color: '#d97706' }}
                  >
                    {formatCount(staticData.validityStatus.expiringSoon)}
                  </p>
                </div>
                <div className="grid grid-cols-[1fr_auto] items-start gap-4 border border-(--border-subtle) bg-(--surface-muted) px-3 py-2">
                  <div>
                    <p className="text-[13px] font-medium text-(--ink-strong)">Expired</p>
                    <p className="text-[12px] text-(--ink-muted)">Past the ValidUntil date</p>
                  </div>
                  <p
                    className="pt-0.5 text-[24px] font-semibold leading-none text-(--status-danger-text)"
                    style={{ fontFamily: 'var(--font-display)' }}
                  >
                    {formatCount(staticData.validityStatus.expired)}
                  </p>
                </div>
              </div>

              <div>
                {(staticData.validityStatus.active + staticData.validityStatus.expiringSoon + staticData.validityStatus.expired) === 0 ? (
                  <p className="text-[12px] text-(--ink-muted)">No validity data available.</p>
                ) : (
                  <PieChart
                    type="doughnut"
                    palette={['#16a34a', '#d97706', '#dc2626']}
                    dataSource={[
                      { label: 'Active', count: staticData.validityStatus.active },
                      { label: 'Expiring soon', count: staticData.validityStatus.expiringSoon },
                      { label: 'Expired', count: staticData.validityStatus.expired },
                    ].filter((d) => d.count > 0)}
                    height={220}
                  >
                    <PieSeries argumentField="label" valueField="count" />
                    <PieLegend visible verticalAlignment="bottom" horizontalAlignment="center" />
                    <PieTooltip
                      enabled
                      customizeTooltip={(arg: { argumentText: string; value: number; percentText: string }) => ({
                        text: `${arg.argumentText}: ${formatCount(arg.value)} (${arg.percentText})`,
                      })}
                    />
                  </PieChart>
                )}
              </div>
            </div>
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

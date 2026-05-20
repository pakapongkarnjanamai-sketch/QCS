import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { appConfig } from '../../config/appConfig.ts'
import { fetchWithAccessControl } from '../../lib/apiClient.ts'
import { toast } from '../../lib/toast.ts'
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

type ChartType = 'line' | 'bar'
type Timeframe = '7d' | '30d' | '6m' | '1y'
type Aggregation = 'day' | 'week' | 'month'

type ValidityStatus = {
  active: number
  expiringSoon: number
  expired: number
}

type ActiveVendorPoint = {
  name: string
  value: number
}

type PaperSaved = {
  totalPages: number
  quotationFileCount: number
  approvedRequestCount: number
  co2GramsSaved: number
  waterLitersSaved: number
  treesEquivalent: number
}

type StaticData = {
  requesterTrend: SeriesTrendPoint[]
  activeVendors: ActiveVendorPoint[]
  validityStatus: ValidityStatus
  paperSaved: PaperSaved
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

const CHART_TYPE_OPTIONS: Array<{ key: ChartType; label: string }> = [
  { key: 'line', label: 'Line' },
  { key: 'bar', label: 'Bar' },
]

type TimeframeOption = { key: Timeframe; label: string; description: string }
const TIMEFRAME_OPTIONS: TimeframeOption[] = [
  { key: '7d', label: '7D', description: 'Last 7 days' },
  { key: '30d', label: '30D', description: 'Last 30 days' },
  { key: '6m', label: '6M', description: 'Last 6 months' },
  { key: '1y', label: '1Y', description: 'Last 1 year' },
]

type AggregationOption = { key: Aggregation; label: string }
const AGGREGATION_OPTIONS: AggregationOption[] = [
  { key: 'day', label: 'Daily' },
  { key: 'week', label: 'Weekly' },
  { key: 'month', label: 'Monthly' },
]

const VALID_AGGREGATIONS: Record<Timeframe, Aggregation[]> = {
  '7d': ['day'],
  '30d': ['day', 'week'],
  '6m': ['week', 'month'],
  '1y': ['week', 'month'],
}

function toErrorMessage(reason: unknown): string {
  if (reason instanceof DOMException && reason.name === 'AbortError') {
    return 'Request was aborted.'
  }

  return reason instanceof Error ? reason.message : 'Unknown request error.'
}

function isAbortReason(reason: unknown): boolean {
  return reason instanceof DOMException && reason.name === 'AbortError'
}

async function fetchJson<T>(path: string, signal: AbortSignal): Promise<T> {
  const response = await fetchWithAccessControl(`${appConfig.apiBaseUrl}${path}`, {
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

  if (summaryResult.status === 'rejected' && !isAbortReason(summaryResult.reason)) {
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
    if (item.value.status === 'rejected' && !isAbortReason(item.value.reason)) {
      issues.push({
        section: item.key,
        message: toErrorMessage(item.value.reason),
      })
    }
  })

  const topVendors = vendorsResult.status === 'fulfilled' ? vendorsResult.value.data ?? [] : []
  const topRequesters = requestersResult.status === 'fulfilled' ? requestersResult.value.data ?? [] : []

  if (vendorsResult.status === 'rejected' && !isAbortReason(vendorsResult.reason)) {
    issues.push({
      section: 'Top vendors',
      message: toErrorMessage(vendorsResult.reason),
    })
  }

  if (requestersResult.status === 'rejected' && !isAbortReason(requestersResult.reason)) {
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

async function fetchTrendData(timeframe: Timeframe, aggregation: Aggregation, signal: AbortSignal): Promise<TrendPoint[]> {
  return fetchJson<TrendPoint[]>(`/api/Dashboard/RequestTrend?timeframe=${timeframe}&aggregation=${aggregation}`, signal)
}

const EMPTY_PAPER_SAVED: PaperSaved = {
  totalPages: 0,
  quotationFileCount: 0,
  approvedRequestCount: 0,
  co2GramsSaved: 0,
  waterLitersSaved: 0,
  treesEquivalent: 0,
}

async function fetchStaticData(signal: AbortSignal): Promise<StaticData> {
  const [requesterTrendResult, activeVendorsResult, validityResult, paperSavedResult] = await Promise.allSettled([
    fetchJson<SeriesTrendPoint[]>('/api/Dashboard/RequesterTrend?days=7&top=5', signal),
    fetchJson<ActiveVendorPoint[]>('/api/Dashboard/ActiveVendors?top=10', signal),
    fetchJson<ValidityStatus>('/api/Dashboard/ValidityStatus', signal),
    fetchJson<PaperSaved>('/api/Dashboard/PaperSaved', signal),
  ])
  return {
    requesterTrend: requesterTrendResult.status === 'fulfilled' ? requesterTrendResult.value : [],
    activeVendors: activeVendorsResult.status === 'fulfilled' ? activeVendorsResult.value : [],
    validityStatus:
      validityResult.status === 'fulfilled'
        ? validityResult.value
        : { active: 0, expiringSoon: 0, expired: 0 },
    paperSaved: paperSavedResult.status === 'fulfilled' ? paperSavedResult.value : EMPTY_PAPER_SAVED,
  }
}

export function OverviewPage() {
  const [data, setData] = useState<OverviewData | null>(null)
  const [trend, setTrend] = useState<TrendPoint[]>([])
  const [trendLoading, setTrendLoading] = useState(false)
  const [staticLoading, setStaticLoading] = useState(true)
  const [staticData, setStaticData] = useState<StaticData>({
    requesterTrend: [],
    activeVendors: [],
    validityStatus: { active: 0, expiringSoon: 0, expired: 0 },
    paperSaved: EMPTY_PAPER_SAVED,
  })
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [chartType, setChartType] = useState<ChartType>('bar')
  const [timeframe, setTimeframe] = useState<Timeframe>('7d')
  const [aggregation, setAggregation] = useState<Aggregation>('day')

  // Load static data once
  useEffect(() => {
    const controller = new AbortController()
    const load = async () => {
      setStaticLoading(true)
      try {
        const result = await fetchStaticData(controller.signal)
        setStaticData(result)
      } finally {
        setStaticLoading(false)
      }
    }

    void load()
    return () => { controller.abort() }
  }, [])

  // Load initial page data once
  useEffect(() => {
    const controller = new AbortController()
    const load = async () => {
      setLoading(true)
      setError(null)
      try {
        const result = await fetchOverviewData(controller.signal)
        setData(result.data)
        if (result.issues.length > 0) {
          toast.warning('Some overview panels are showing fallback values.')
        }
      } catch (reason) {
        if (reason instanceof DOMException && reason.name === 'AbortError') return
        const message = reason instanceof Error ? reason.message : 'Cannot load overview data.'
        setError(message)
        setData(null)
      } finally {
        setLoading(false)
      }
    }
    void load()
    return () => { controller.abort() }
  }, [])

  // Reload only the trend chart on timeframe/aggregation change
  useEffect(() => {
    const controller = new AbortController()
    const load = async () => {
      setTrendLoading(true)
      try {
        const result = await fetchTrendData(timeframe, aggregation, controller.signal)
        setTrend(result)
      } catch (reason) {
        if (reason instanceof DOMException && reason.name === 'AbortError') return
        setTrend([])
        toast.warning('Trend chart is temporarily unavailable.')
      } finally {
        setTrendLoading(false)
      }
    }
    void load()
    return () => { controller.abort() }
  }, [timeframe, aggregation])

  const activeTimeframeLabel = useMemo(
    () => TIMEFRAME_OPTIONS.find((t) => t.key === timeframe)?.description ?? '',
    [timeframe],
  )

  const activeAggregationLabel = useMemo(
    () => AGGREGATION_OPTIONS.find((a) => a.key === aggregation)?.label ?? '',
    [aggregation],
  )

  const validAggregations = useMemo(() => VALID_AGGREGATIONS[timeframe], [timeframe])

  function handleTimeframeChange(newTimeframe: Timeframe) {
    setTimeframe(newTimeframe)
    const valid = VALID_AGGREGATIONS[newTimeframe]
    if (!valid.includes(aggregation)) {
      setAggregation(valid[0])
    }
  }

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
          <section className={cardClassName}>
            <div className="flex flex-wrap items-start justify-between gap-3 border-b border-(--border-subtle) pb-3">
              <div>
                <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-(--ink-soft)">
                  Sustainability
                </p>
                <h3
                  className="mt-1 text-[18px] font-semibold leading-none text-(--ink-strong)"
                  style={{ fontFamily: 'var(--font-display)' }}
                >
                  Paper saved by going digital
                </h3>
                <p className="mt-1 text-[12px] text-(--ink-muted)">
                  Pages of approved quotation PDFs that did not need to be printed (1 PDF page = 1 sheet).
                </p>
              </div>
              <Link
                to="/sustainability"
                className="focus-ring inline-flex min-h-11 items-center text-[12px] font-medium text-(--ink-muted) underline decoration-(--border-strong) underline-offset-4 hover:text-(--ink-strong)"
              >
                View sustainability
              </Link>
            </div>

            <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
              <div className="border border-(--border-subtle) bg-(--surface-muted) px-3 py-3">
                <p className="text-[11px] font-semibold uppercase tracking-[0.13em] text-(--ink-soft)">Sheets saved</p>
                <p
                  className="mt-1 text-[28px] font-semibold leading-none text-(--ink-strong)"
                  style={{ fontFamily: 'var(--font-display)' }}
                >
                  {formatCount(staticData.paperSaved.totalPages)}
                </p>
                <p className="mt-1 text-[12px] text-(--ink-muted)">
                  from {formatCount(staticData.paperSaved.quotationFileCount)} approved quotation files
                </p>
              </div>
              <div className="border border-(--border-subtle) bg-(--surface-muted) px-3 py-3">
                <p className="text-[11px] font-semibold uppercase tracking-[0.13em] text-(--ink-soft)">CO₂ avoided</p>
                <p
                  className="mt-1 text-[28px] font-semibold leading-none text-(--ink-strong)"
                  style={{ fontFamily: 'var(--font-display)' }}
                >
                  {formatCount(Math.round(staticData.paperSaved.co2GramsSaved))}
                  <span className="ml-1 text-[14px] font-normal text-(--ink-muted)">g</span>
                </p>
                <p className="mt-1 text-[12px] text-(--ink-muted)">≈ 4.6 g CO₂ per A4 sheet</p>
              </div>
              <div className="border border-(--border-subtle) bg-(--surface-muted) px-3 py-3">
                <p className="text-[11px] font-semibold uppercase tracking-[0.13em] text-(--ink-soft)">Water saved</p>
                <p
                  className="mt-1 text-[28px] font-semibold leading-none text-(--ink-strong)"
                  style={{ fontFamily: 'var(--font-display)' }}
                >
                  {formatCount(Math.round(staticData.paperSaved.waterLitersSaved))}
                  <span className="ml-1 text-[14px] font-normal text-(--ink-muted)">L</span>
                </p>
                <p className="mt-1 text-[12px] text-(--ink-muted)">≈ 10 L per A4 sheet</p>
              </div>
              <div className="border border-(--border-subtle) bg-(--surface-muted) px-3 py-3">
                <p className="text-[11px] font-semibold uppercase tracking-[0.13em] text-(--ink-soft)">Trees equivalent</p>
                <p
                  className="mt-1 text-[28px] font-semibold leading-none text-(--ink-strong)"
                  style={{ fontFamily: 'var(--font-display)' }}
                >
                  {staticData.paperSaved.treesEquivalent.toFixed(2)}
                </p>
                <p className="mt-1 text-[12px] text-(--ink-muted)">≈ 8,333 sheets per tree</p>
              </div>
            </div>
          </section>

          <section className={cardClassName}>
            <div className="flex flex-wrap items-start justify-between gap-3 border-b border-(--border-subtle) pb-3">
              <div>
                <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-(--ink-soft)">
                  Trend window
                </p>
                <h3
                  className="mt-1 text-[18px] font-semibold leading-none text-(--ink-strong)"
                  style={{ fontFamily: 'var(--font-display)' }}
                >
                  {activeAggregationLabel} — {activeTimeframeLabel}
                </h3>
              </div>
              <div className="flex flex-wrap items-start gap-2">
                <div className="flex flex-col gap-1">
                  <p className="text-[10px] font-semibold uppercase tracking-[0.13em] text-(--ink-soft)">Chart</p>
                  <div role="group" aria-label="Chart type" className="inline-flex border border-(--border-subtle)">
                    {CHART_TYPE_OPTIONS.map((opt) => {
                      const active = opt.key === chartType
                      return (
                        <button
                          key={opt.key}
                          type="button"
                          onClick={() => setChartType(opt.key)}
                          aria-pressed={active}
                          className={`focus-ring min-h-8 px-3 text-[12px] font-medium ${
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
                <div className="flex flex-col gap-1">
                  <p className="text-[10px] font-semibold uppercase tracking-[0.13em] text-(--ink-soft)">Timeframe</p>
                  <div role="group" aria-label="Timeframe" className="inline-flex border border-(--border-subtle)">
                    {TIMEFRAME_OPTIONS.map((opt) => {
                      const active = opt.key === timeframe
                      return (
                        <button
                          key={opt.key}
                          type="button"
                          onClick={() => handleTimeframeChange(opt.key)}
                          aria-pressed={active}
                          className={`focus-ring min-h-8 px-3 text-[12px] font-medium ${
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
                <div className="flex flex-col gap-1">
                  <p className="text-[10px] font-semibold uppercase tracking-[0.13em] text-(--ink-soft)">Aggregation</p>
                  <div role="group" aria-label="Data aggregation" className="inline-flex border border-(--border-subtle)">
                    {AGGREGATION_OPTIONS.filter((opt) => validAggregations.includes(opt.key)).map((opt) => {
                      const active = opt.key === aggregation
                      return (
                        <button
                          key={opt.key}
                          type="button"
                          onClick={() => setAggregation(opt.key)}
                          aria-pressed={active}
                          className={`focus-ring min-h-8 px-3 text-[12px] font-medium ${
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
                    type={chartType}
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
                  Requests created by top 5 requesters (last 7 days)
                </h3>
              </div>
              {staticLoading ? (
                <div className="flex h-65 items-center justify-center text-[12px] text-(--ink-muted)">
                  Loading requester activity...
                </div>
              ) : staticData.requesterTrend.length === 0 ? (
                <p className="text-[12px] text-(--ink-muted)">No requester activity found.</p>
              ) : (
                <Chart dataSource={staticData.requesterTrend} height={260}>
                  <CommonSeriesSettings argumentField="label" valueField="count" type="spline" />
                  <SeriesTemplate nameField="name" />
                  <ArgumentAxis />
                  <ValueAxis allowDecimals={false} />
                  <ChartLegend visible verticalAlignment="bottom" horizontalAlignment="center" />
                  <ChartTooltip
                    enabled
                    customizeTooltip={(arg: { seriesName: string; argument: string; value: number }) => ({
                      text: `${arg.seriesName}\n${arg.argument}: ${formatCount(arg.value)} requests`,
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
              {staticLoading ? (
                <div className="flex h-65 items-center justify-center text-[12px] text-(--ink-muted)">
                  Loading vendor activity...
                </div>
              ) : staticData.activeVendors.length === 0 ? (
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
                {staticLoading ? (
                  <div className="flex h-55 items-center justify-center text-[12px] text-(--ink-muted)">
                    Loading validity data...
                  </div>
                ) : (staticData.validityStatus.active + staticData.validityStatus.expiringSoon + staticData.validityStatus.expired) === 0 ? (
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
                      to="/users"
                      className="focus-ring inline-flex min-h-11 items-center text-[12px] font-medium text-(--ink-muted) underline decoration-(--border-strong) underline-offset-4 hover:text-(--ink-strong)"
                    >
                      Open user access
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

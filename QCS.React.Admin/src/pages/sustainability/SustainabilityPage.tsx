import { useEffect, useMemo, useState } from 'react'
import { appConfig } from '../../config/appConfig.ts'
import { fetchWithAccessControl } from '../../lib/apiClient.ts'
import { toast } from '../../lib/toast.ts'
import Chart, {
  Series as ChartSeries,
  Tooltip as ChartTooltip,
  ArgumentAxis,
  ValueAxis,
  Legend as ChartLegend,
} from 'devextreme-react/chart'

type PaperSaved = {
  totalPages: number
  quotationFileCount: number
  approvedRequestCount: number
  co2GramsSaved: number
  waterLitersSaved: number
  treesEquivalent: number
}

type PaperSavedTrendPoint = {
  label: string
  year: number
  month: number
  pages: number
}

type Timeframe = '30d' | '6m' | '1y'
type Aggregation = 'day' | 'week' | 'month'

const TIMEFRAME_OPTIONS: Array<{ key: Timeframe; label: string; description: string }> = [
  { key: '30d', label: '30D', description: 'Last 30 days' },
  { key: '6m', label: '6M', description: 'Last 6 months' },
  { key: '1y', label: '1Y', description: 'Last 1 year' },
]

const AGGREGATION_OPTIONS: Array<{ key: Aggregation; label: string }> = [
  { key: 'day', label: 'Daily' },
  { key: 'week', label: 'Weekly' },
  { key: 'month', label: 'Monthly' },
]

const VALID_AGGREGATIONS: Record<Timeframe, Aggregation[]> = {
  '30d': ['day', 'week'],
  '6m': ['week', 'month'],
  '1y': ['week', 'month'],
}

const cardClassName =
  'rounded-sm border border-(--border-subtle) bg-(--surface-panel) p-4 sm:p-6'

const numberFormat = new Intl.NumberFormat('en-US')
const formatCount = (value: number) => numberFormat.format(value)

const EMPTY_PAPER_SAVED: PaperSaved = {
  totalPages: 0,
  quotationFileCount: 0,
  approvedRequestCount: 0,
  co2GramsSaved: 0,
  waterLitersSaved: 0,
  treesEquivalent: 0,
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

export function SustainabilityPage() {
  const [summary, setSummary] = useState<PaperSaved>(EMPTY_PAPER_SAVED)
  const [summaryLoading, setSummaryLoading] = useState(true)
  const [trend, setTrend] = useState<PaperSavedTrendPoint[]>([])
  const [trendLoading, setTrendLoading] = useState(true)
  const [timeframe, setTimeframe] = useState<Timeframe>('6m')
  const [aggregation, setAggregation] = useState<Aggregation>('month')
  const [backfillBusy, setBackfillBusy] = useState(false)

  useEffect(() => {
    const controller = new AbortController()
    const load = async () => {
      setSummaryLoading(true)
      try {
        const result = await fetchJson<PaperSaved>('/api/Dashboard/PaperSaved', controller.signal)
        setSummary(result)
      } catch (reason) {
        if (reason instanceof DOMException && reason.name === 'AbortError') return
        toast.warning('Cannot load paper saved summary.')
      } finally {
        setSummaryLoading(false)
      }
    }
    void load()
    return () => { controller.abort() }
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    const load = async () => {
      setTrendLoading(true)
      try {
        const result = await fetchJson<PaperSavedTrendPoint[]>(
          `/api/Dashboard/PaperSavedTrend?timeframe=${timeframe}&aggregation=${aggregation}`,
          controller.signal,
        )
        setTrend(result)
      } catch (reason) {
        if (reason instanceof DOMException && reason.name === 'AbortError') return
        setTrend([])
        toast.warning('Cannot load paper saved trend.')
      } finally {
        setTrendLoading(false)
      }
    }
    void load()
    return () => { controller.abort() }
  }, [timeframe, aggregation])

  const validAggregations = useMemo(() => VALID_AGGREGATIONS[timeframe], [timeframe])

  function handleTimeframeChange(newTimeframe: Timeframe) {
    setTimeframe(newTimeframe)
    const valid = VALID_AGGREGATIONS[newTimeframe]
    if (!valid.includes(aggregation)) {
      setAggregation(valid[0])
    }
  }

  async function handleBackfill() {
    setBackfillBusy(true)
    try {
      const response = await fetchWithAccessControl(
        `${appConfig.apiBaseUrl}/api/Dashboard/BackfillPageCount?batchSize=100`,
        { method: 'POST', credentials: 'include' },
      )
      if (!response.ok) {
        throw new Error(`Backfill failed (${response.status})`)
      }
      const result = (await response.json()) as {
        processed: number
        updated: number
        failed: number
        remaining: number
      }
      toast.success(
        `Backfilled ${result.updated} of ${result.processed} files. Remaining: ${result.remaining}.`,
      )
      const refreshed = await fetchJson<PaperSaved>('/api/Dashboard/PaperSaved', new AbortController().signal)
      setSummary(refreshed)
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : 'Backfill failed.'
      toast.warning(message)
    } finally {
      setBackfillBusy(false)
    }
  }

  return (
    <div className="space-y-4 sm:space-y-6">
      <section className={cardClassName}>
        <div className="flex flex-wrap items-start justify-between gap-3 border-b border-(--border-subtle) pb-3">
          <div>
            <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-(--ink-soft)">
              Sustainability
            </p>
            <h2
              className="mt-1 text-[24px] font-semibold leading-none text-(--ink-strong)"
              style={{ fontFamily: 'var(--font-display)' }}
            >
              Paper saved by going digital
            </h2>
            <p className="mt-2 text-[13px] text-(--ink-muted)">
              Each page of an approved quotation PDF replaces one A4 sheet that would have been printed.
              Estimates use widely cited industry averages.
            </p>
          </div>
          <button
            type="button"
            onClick={handleBackfill}
            disabled={backfillBusy}
            className="focus-ring inline-flex min-h-9 items-center border border-(--border-subtle) bg-(--surface-panel) px-3 text-[12px] font-medium text-(--ink-strong) hover:bg-(--surface-muted) disabled:cursor-not-allowed disabled:opacity-60"
          >
            {backfillBusy ? 'Counting…' : 'Count older PDFs'}
          </button>
        </div>

        <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          <div className="border border-(--border-subtle) bg-(--surface-muted) px-4 py-4">
            <p className="text-[11px] font-semibold uppercase tracking-[0.13em] text-(--ink-soft)">
              Sheets saved
            </p>
            <p
              className="mt-1 text-[40px] font-semibold leading-none text-(--ink-strong)"
              style={{ fontFamily: 'var(--font-display)' }}
            >
              {summaryLoading ? '…' : formatCount(summary.totalPages)}
            </p>
            <p className="mt-2 text-[12px] text-(--ink-muted)">
              from {formatCount(summary.quotationFileCount)} approved quotation files
              <br />
              across {formatCount(summary.approvedRequestCount)} approved requests
            </p>
          </div>
          <div className="border border-(--border-subtle) bg-(--surface-muted) px-4 py-4">
            <p className="text-[11px] font-semibold uppercase tracking-[0.13em] text-(--ink-soft)">
              CO₂ avoided
            </p>
            <p
              className="mt-1 text-[40px] font-semibold leading-none text-(--ink-strong)"
              style={{ fontFamily: 'var(--font-display)' }}
            >
              {summaryLoading ? '…' : formatCount(Math.round(summary.co2GramsSaved))}
              <span className="ml-1 text-[18px] font-normal text-(--ink-muted)">g</span>
            </p>
            <p className="mt-2 text-[12px] text-(--ink-muted)">≈ 4.6 g of CO₂ per A4 sheet produced</p>
          </div>
          <div className="border border-(--border-subtle) bg-(--surface-muted) px-4 py-4">
            <p className="text-[11px] font-semibold uppercase tracking-[0.13em] text-(--ink-soft)">
              Water saved
            </p>
            <p
              className="mt-1 text-[40px] font-semibold leading-none text-(--ink-strong)"
              style={{ fontFamily: 'var(--font-display)' }}
            >
              {summaryLoading ? '…' : formatCount(Math.round(summary.waterLitersSaved))}
              <span className="ml-1 text-[18px] font-normal text-(--ink-muted)">L</span>
            </p>
            <p className="mt-2 text-[12px] text-(--ink-muted)">≈ 10 L of water per A4 sheet</p>
          </div>
          <div className="border border-(--border-subtle) bg-(--surface-muted) px-4 py-4">
            <p className="text-[11px] font-semibold uppercase tracking-[0.13em] text-(--ink-soft)">
              Trees equivalent
            </p>
            <p
              className="mt-1 text-[40px] font-semibold leading-none text-(--ink-strong)"
              style={{ fontFamily: 'var(--font-display)' }}
            >
              {summaryLoading ? '…' : summary.treesEquivalent.toFixed(2)}
            </p>
            <p className="mt-2 text-[12px] text-(--ink-muted)">≈ 8,333 A4 sheets per tree</p>
          </div>
        </div>
      </section>

      <section className={cardClassName}>
        <div className="flex flex-wrap items-start justify-between gap-3 border-b border-(--border-subtle) pb-3">
          <div>
            <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-(--ink-soft)">
              Trend
            </p>
            <h3
              className="mt-1 text-[18px] font-semibold leading-none text-(--ink-strong)"
              style={{ fontFamily: 'var(--font-display)' }}
            >
              Sheets saved over time
            </h3>
          </div>
          <div className="flex flex-wrap items-start gap-2">
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
              <div role="group" aria-label="Aggregation" className="inline-flex border border-(--border-subtle)">
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
          {trendLoading ? (
            <div
              role="status"
              aria-live="polite"
              className="flex h-72 items-center justify-center text-[12px] text-(--ink-muted)"
            >
              Loading…
            </div>
          ) : trend.length === 0 ? (
            <p className="text-[12px] text-(--ink-muted)">No data in this period.</p>
          ) : (
            <Chart dataSource={trend} height={280}>
              <ChartSeries
                valueField="pages"
                argumentField="label"
                type="bar"
                color="#16a34a"
                name="Sheets saved"
              />
              <ArgumentAxis />
              <ValueAxis allowDecimals={false} />
              <ChartLegend visible={false} />
              <ChartTooltip
                enabled
                customizeTooltip={(arg: { argument: string; value: number }) => ({
                  text: `${arg.argument}: ${formatCount(arg.value)} sheets`,
                })}
              />
            </Chart>
          )}
        </div>
      </section>
    </div>
  )
}

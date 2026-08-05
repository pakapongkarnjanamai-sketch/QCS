import { ArrowLeft, ExternalLink } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useLocation, useParams } from 'react-router'
import { AppButton } from '@/components/ui/AppButton'
import { StatusBadge } from '@/components/ui/StatusBadge'
import { ErrorSurface, LoadingSurface } from '@/components/ui/Surfaces'
import { qrsRequestUrl } from '@/config/appConfig'
import { PdfViewer } from '@/features/quotations/PdfViewer'
import { toApiError, type ApiError } from '@/lib/apiClient'
import { DocumentList } from './DocumentList'
import { HistoryList } from './HistoryList'
import { getPortalRequestById } from './requestApi'
import type { PortalDocument, PortalRequestDetail } from './types'
import { WorkflowTimeline } from './WorkflowTimeline'

function formatDate(value?: string): string {
  return value
    ? new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium' }).format(
        new Date(value),
      )
    : '-'
}

export function RequestDetailPage() {
  const { id } = useParams()
  const location = useLocation()
  const [request, setRequest] = useState<PortalRequestDetail>()
  const [error, setError] = useState<ApiError>()
  const [loading, setLoading] = useState(true)
  const [retryToken, setRetryToken] = useState(0)
  const [preview, setPreview] = useState<PortalDocument>()
  const numericId = Number(id)
  useEffect(() => {
    if (!Number.isInteger(numericId) || numericId <= 0) {
      setError({ status: 400, title: 'Invalid request' })
      setLoading(false)
      return undefined
    }
    const controller = new AbortController()
    setLoading(true)
    setError(undefined)
    void getPortalRequestById(numericId, controller.signal)
      .then(setRequest)
      .catch((reason: unknown) => {
        if (!controller.signal.aborted) setError(toApiError(reason))
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [numericId, retryToken])
  const returnSearch = (location.state as { workspaceSearch?: string } | null)
    ?.workspaceSearch
  const backTo = returnSearch ? `/requests?${returnSearch}` : '/requests'
  if (loading && !request) return <LoadingSurface />
  if (!request)
    return (
      <ErrorSurface>
        <div className="flex items-center justify-between gap-3">
          <span>{error?.title || 'Request unavailable.'}</span>
          <AppButton
            tone="secondary"
            onClick={() => setRetryToken((value) => value + 1)}
          >
            Try again
          </AppButton>
        </div>
      </ErrorSurface>
    )
  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <Link
        to={backTo}
        className="inline-flex w-fit items-center gap-2 rounded-sm text-body text-accent hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
      >
        <ArrowLeft size={16} aria-hidden />
        Back to requests
      </Link>
      {error && (
        <ErrorSurface>{error.title} Showing the previous details.</ErrorSurface>
      )}
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="break-words text-title font-semibold">
              {request.code}
            </h1>
            <StatusBadge status={request.statusName} />
          </div>
          <p className="mt-1 break-words text-heading">{request.title}</p>
          <p className="mt-0.5 text-caption text-ink-muted">
            {request.requesterName || request.requesterNId} ·{' '}
            {request.vendorName || request.vendorCode} ·{' '}
            {formatDate(request.requestDate)}
          </p>
        </div>
        {request.sourceSystem === 'QRS' && request.sourceCode && (
          <a
            href={qrsRequestUrl(request.sourceCode)}
            target="_blank"
            rel="noreferrer"
            className="inline-flex items-center gap-2 rounded-sm text-body text-accent hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          >
            Open source request {request.sourceCode}
            <ExternalLink size={15} aria-hidden />
          </a>
        )}
      </header>
      <section className="rounded-sm border border-border-subtle bg-white">
        <h2 className="border-b border-border-subtle px-4 py-3 text-caption font-semibold uppercase tracking-[0.12em] text-ink-muted">
          Request details
        </h2>
        <dl className="grid gap-x-6 gap-y-3 p-4 sm:grid-cols-2 lg:grid-cols-3">
          <Detail
            label="Requester"
            value={request.requesterName || request.requesterNId}
          />
          <Detail
            label="Vendor"
            value={request.vendorName || request.vendorCode}
          />
          <Detail label="Current step" value={request.currentStepName || '-'} />
          <Detail label="Valid from" value={formatDate(request.validFrom)} />
          <Detail label="Valid until" value={formatDate(request.validUntil)} />
          {request.remark && <Detail label="Remark" value={request.remark} />}
        </dl>
      </section>
      <div className="grid gap-4 md:grid-cols-2">
        <section className="rounded-sm border border-border-subtle bg-white">
          <h2 className="border-b border-border-subtle px-4 py-3 text-caption font-semibold uppercase tracking-[0.12em] text-ink-muted">
            Documents
          </h2>
          <DocumentList documents={request.documents} onPreview={setPreview} />
        </section>
        <section className="rounded-sm border border-border-subtle bg-white">
          <h2 className="border-b border-border-subtle px-4 py-3 text-caption font-semibold uppercase tracking-[0.12em] text-ink-muted">
            Workflow
          </h2>
          <div className="p-4">
            <WorkflowTimeline steps={request.workflowSteps} />
          </div>
        </section>
      </div>
      <section className="rounded-sm border border-border-subtle bg-white">
        <h2 className="border-b border-border-subtle px-4 py-3 text-caption font-semibold uppercase tracking-[0.12em] text-ink-muted">
          History
        </h2>
        <div className="p-4">
          <HistoryList histories={request.histories} />
        </div>
      </section>
      <PdfViewer document={preview} onClose={() => setPreview(undefined)} />
    </div>
  )
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-caption text-ink-soft">{label}</dt>
      <dd className="mt-0.5 break-words text-body text-ink-strong">{value}</dd>
    </div>
  )
}

import { ExternalLink } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useParams } from 'react-router'
import { AppButton } from '@/components/ui/AppButton'
import { StatusBadge } from '@/components/ui/StatusBadge'
import { ErrorSurface, LoadingSurface } from '@/components/ui/Surfaces'
import { qrsRequestUrl } from '@/config/appConfig'
import { PdfViewer } from '@/features/quotations/PdfViewer'
import { toApiError, type ApiError } from '@/lib/apiClient'
import { toast } from '@/lib/toast'
import { ApprovalActionDialog } from './ApprovalActionDialog'
import { DocumentList } from './DocumentList'
import { ApprovalSteps } from './ApprovalSteps'
import { approvePortalRequest, getPortalRequestById, rejectPortalRequest } from './requestApi'
import type { PortalDocument, PortalRequestDetail } from './types'

function formatDate(value?: string): string {
  return value
    ? new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium' }).format(
        new Date(value),
      )
    : '-'
}

export function RequestDetailPage() {
  const { id } = useParams()
  const [request, setRequest] = useState<PortalRequestDetail>()
  const [error, setError] = useState<ApiError>()
  const [loading, setLoading] = useState(true)
  const [retryToken, setRetryToken] = useState(0)
  const [preview, setPreview] = useState<PortalDocument>()
  const [approvalAction, setApprovalAction] = useState<'approve' | 'reject'>()
  const [busyAction, setBusyAction] = useState<'approve' | 'reject'>()
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
  if (loading && !request) return <LoadingSurface />
  if (!request)
    return (
      <ErrorSurface>
        <div className="flex items-center justify-between gap-3">
          <span>{error?.title || 'Request unavailable.'}</span>
          <AppButton
            variant="secondary"
            onClick={() => setRetryToken((value) => value + 1)}
          >
            Try again
          </AppButton>
        </div>
      </ErrorSurface>
    )
  const runApprovalAction = async (comment: string) => {
    const action = approvalAction
    if (!action) return
    setBusyAction(action)
    setError(undefined)
    try {
      await (action === 'approve'
        ? approvePortalRequest(request.id, { comment })
        : rejectPortalRequest(request.id, { comment }))
      toast.success(action === 'approve' ? 'Request approved.' : 'Request rejected.')
      setApprovalAction(undefined)
      setRetryToken((value) => value + 1)
    } catch (reason) {
      setError(toApiError(reason))
    } finally {
      setBusyAction(undefined)
    }
  }
  return (
    <div className="mx-auto max-w-5xl space-y-6">
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
        <div className="flex flex-wrap items-center gap-2">
          {request.sourceSystem === 'QRS' && request.sourceCode && (
            <a
              href={qrsRequestUrl(request.sourceCode)}
              target="_blank"
              rel="noreferrer"
              className="mr-1 inline-flex items-center gap-2 rounded-sm text-body text-accent hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            >
              Open source request {request.sourceCode}
              <ExternalLink className="size-3.5" aria-hidden />
            </a>
          )}
          {request.permissions.canApprove && <AppButton disabled={Boolean(busyAction)} onClick={() => setApprovalAction('approve')}>Approve</AppButton>}
          {request.permissions.canReject && <AppButton variant="danger" disabled={Boolean(busyAction)} onClick={() => setApprovalAction('reject')}>Reject</AppButton>}
        </div>
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
      <section className="rounded-sm border border-border-subtle bg-white">
        <h2 className="border-b border-border-subtle px-4 py-3 text-caption font-semibold uppercase tracking-[0.12em] text-ink-muted">
          Documents
        </h2>
        <DocumentList documents={request.documents} onPreview={setPreview} />
      </section>
      <ApprovalSteps steps={request.workflowSteps} histories={request.histories} />
      <PdfViewer document={preview && { url: preview.viewUrl, fileName: preview.fileName }} onClose={() => setPreview(undefined)} />
      <ApprovalActionDialog action={approvalAction} busy={Boolean(busyAction)} onClose={() => { if (!busyAction) setApprovalAction(undefined) }} onConfirm={(comment) => void runApprovalAction(comment)} />
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

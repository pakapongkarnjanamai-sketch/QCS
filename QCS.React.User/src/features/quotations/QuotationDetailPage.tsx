import { ExternalLink } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useParams } from 'react-router'
import { AppButton } from '@/components/ui/AppButton'
import { StatusBadge } from '@/components/ui/StatusBadge'
import { ErrorSurface, LoadingSurface } from '@/components/ui/Surfaces'
import { qrsRequestUrl } from '@/config/appConfig'
import { DocumentList } from '@/features/requests/DocumentList'
import { getPortalRequestByCode } from '@/features/requests/requestApi'
import type {
  PortalDocument,
  PortalRequestDetail,
} from '@/features/requests/types'
import { toApiError, type ApiError } from '@/lib/apiClient'
import { PdfViewer } from './PdfViewer'

function formatDate(value?: string): string {
  return value
    ? new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium' }).format(
        new Date(value),
      )
    : '-'
}

export function QuotationDetailPage() {
  const { code } = useParams()
  const [request, setRequest] = useState<PortalRequestDetail>()
  const [error, setError] = useState<ApiError>()
  const [loading, setLoading] = useState(true)
  const [retryToken, setRetryToken] = useState(0)
  const [preview, setPreview] = useState<PortalDocument>()
  useEffect(() => {
    if (!code) {
      setError({ status: 400, title: 'Invalid quotation' })
      setLoading(false)
      return undefined
    }
    const controller = new AbortController()
    setLoading(true)
    setError(undefined)
    void getPortalRequestByCode(code, controller.signal)
      .then(setRequest)
      .catch((reason: unknown) => {
        if (!controller.signal.aborted) setError(toApiError(reason))
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [code, retryToken])
  if (loading && !request) return <LoadingSurface />
  if (!request)
    return (
      <ErrorSurface>
        <div className="flex items-center justify-between gap-3">
          <span>{error?.title || 'Quotation unavailable.'}</span>
          <AppButton
            tone="secondary"
            onClick={() => setRetryToken((value) => value + 1)}
          >
            Try again
          </AppButton>
        </div>
      </ErrorSurface>
    )
  const finalDocument = request.documents.find((document) =>
    /final|stamp/i.test(document.documentTypeName),
  )
  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-2">
          <h1 className="text-title font-semibold">{request.code}</h1>
          <StatusBadge status={request.statusName} />
        </div>
          <p className="mt-1 text-heading">{request.title}</p>
          <p className="mt-0.5 text-caption text-ink-muted">
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
          Stamped final PDF
        </h2>
        {finalDocument ? (
          <DocumentList
            documents={[finalDocument]}
            onPreview={setPreview}
          />
        ) : (
          <p className="px-4 py-6 text-body text-ink-muted">
            A stamped final PDF is not available.
          </p>
        )}
      </section>
      {error && (
        <ErrorSurface>{error.title} Showing the previous details.</ErrorSurface>
      )}
      <section className="rounded-sm border border-border-subtle bg-white">
        <h2 className="border-b border-border-subtle px-4 py-3 text-caption font-semibold uppercase tracking-[0.12em] text-ink-muted">
          Documents
        </h2>
        <DocumentList documents={request.documents} onPreview={setPreview} />
      </section>
      <PdfViewer document={preview} onClose={() => setPreview(undefined)} />
    </div>
  )
}

import { useEffect, useState } from 'react'
import { useParams } from 'react-router'
import { AppButton } from '@/components/ui/AppButton'
import { ExternalActionLink } from '@/components/ui/ExternalActionLink'
import { FormPage, FormPageHeader } from '@/components/ui/FormPage'
import { SectionCard } from '@/components/ui/SectionCard'
import { StatusBadge } from '@/components/ui/StatusBadge'
import { ErrorSurface, LoadingSurface } from '@/components/ui/Surfaces'
import { qrsRequestUrl } from '@/config/appConfig'
import { DocumentList } from '@/features/requests/DocumentList'
import { RenewQuotationLink } from '@/features/requests/RenewQuotationLink'
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
            variant="secondary"
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
    <FormPage>
      <FormPageHeader
        title={request.code}
        status={<StatusBadge status={request.statusName} />}
        description={<><span className="block text-heading text-ink-strong">{request.title}</span><span className="mt-0.5 block text-caption text-ink-muted">{request.vendorName || request.vendorCode} · {formatDate(request.requestDate)}</span></>}
        actions={(request.sourceSystem === 'QRS' && request.sourceCode) || request.canRenew ? <>
          {request.sourceSystem === 'QRS' && request.sourceCode && <ExternalActionLink href={qrsRequestUrl(request.sourceCode)}>Open source request {request.sourceCode}</ExternalActionLink>}
          {request.canRenew && <RenewQuotationLink code={request.code} />}
        </> : undefined}
      />
      {error && <ErrorSurface>{error.title} Showing the previous details.</ErrorSurface>}
      <SectionCard title="Stamped final PDF">
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
      </SectionCard>
      <SectionCard title="Documents">
        <DocumentList documents={request.documents} onPreview={setPreview} />
      </SectionCard>
      <PdfViewer document={preview && { url: preview.viewUrl, fileName: preview.fileName }} onClose={() => setPreview(undefined)} />
    </FormPage>
  )
}

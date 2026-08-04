import { ExternalLink } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useParams } from 'react-router'
import { AppButton } from '@/components/ui/AppButton'
import { ErrorSurface, LoadingSurface } from '@/components/ui/Surfaces'
import { appConfig } from '@/config/appConfig'
import { toApiError, type ApiError } from '@/lib/apiClient'
import { DocumentList } from '@/features/requests/DocumentList'
import { getPortalRequestByCode } from '@/features/requests/requestApi'
import type { PortalDocument, PortalRequestDetail } from '@/features/requests/types'
import { PdfViewer } from './PdfViewer'

function qrsUrl(sourceCode: string): string { return `${appConfig.qrsRequestBaseUrl}/${encodeURIComponent(sourceCode)}` }

export function QuotationDetailPage() {
  const { code } = useParams(); const [request, setRequest] = useState<PortalRequestDetail>(); const [error, setError] = useState<ApiError>(); const [loading, setLoading] = useState(true); const [retryToken, setRetryToken] = useState(0); const [preview, setPreview] = useState<PortalDocument>()
  useEffect(() => { if (!code) { setError({ status: 400, title: 'Invalid quotation' }); setLoading(false); return undefined }; const controller = new AbortController(); setLoading(true); setError(undefined); void getPortalRequestByCode(code, controller.signal).then(setRequest).catch((reason: unknown) => { if (!controller.signal.aborted) setError(toApiError(reason)) }).finally(() => { if (!controller.signal.aborted) setLoading(false) }); return () => controller.abort() }, [code, retryToken])
  if (loading && !request) return <LoadingSurface />
  if (!request) return <ErrorSurface><div className="flex items-center justify-between gap-3"><span>{error?.title || 'Quotation unavailable.'}</span><AppButton tone="secondary" onClick={() => setRetryToken((value) => value + 1)}>Try again</AppButton></div></ErrorSurface>
  const finalDocument = request.documents.find((document) => /final|stamp/i.test(document.documentTypeName))
  return <div className="mx-auto grid max-w-6xl gap-6"><section className="border border-border-subtle bg-white p-4 md:p-6"><h1 className="text-title font-semibold">{request.code}</h1><p className="mt-1 text-heading text-ink-muted">{request.title}</p>{request.sourceSystem === 'QRS' && request.sourceCode && <a href={qrsUrl(request.sourceCode)} target="_blank" rel="noreferrer" className="mt-4 inline-flex items-center gap-2 rounded-sm text-body text-accent hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">Open source request {request.sourceCode}<ExternalLink size={15} aria-hidden /></a>}{finalDocument ? <div className="mt-6"><h2 className="text-heading font-semibold">Stamped final PDF</h2><div className="mt-3"><DocumentList documents={[finalDocument]} onPreview={setPreview} /></div></div> : <div className="mt-6 border-l-2 border-border-subtle pl-3 text-body text-ink-muted">A stamped final PDF is not available.</div>}</section>{error && <ErrorSurface>{error.title} Showing the previous details.</ErrorSurface>}<section className="border border-border-subtle bg-white p-4 md:p-6"><h2 className="text-heading font-semibold">Documents</h2><div className="mt-4"><DocumentList documents={request.documents} onPreview={setPreview} /></div></section><PdfViewer document={preview} onClose={() => setPreview(undefined)} /></div>
}
import { ArrowLeft, ExternalLink } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useParams, useLocation } from 'react-router'
import { AppButton } from '@/components/ui/AppButton'
import { ErrorSurface, LoadingSurface } from '@/components/ui/Surfaces'
import { appConfig } from '@/config/appConfig'
import { toApiError, type ApiError } from '@/lib/apiClient'
import { DocumentList } from './DocumentList'
import { HistoryList } from './HistoryList'
import { getPortalRequestById } from './requestApi'
import { RequestStatusText } from './RequestStatusText'
import type { PortalDocument, PortalRequestDetail } from './types'
import { WorkflowTimeline } from './WorkflowTimeline'
import { PdfViewer } from '@/features/quotations/PdfViewer'

function formatDate(value?: string): string { return value ? new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium' }).format(new Date(value)) : '-' }
function qrsUrl(sourceCode: string): string { return `${appConfig.qrsRequestBaseUrl}/${encodeURIComponent(sourceCode)}` }

export function RequestDetailPage() {
  const { id } = useParams(); const location = useLocation()
  const [request, setRequest] = useState<PortalRequestDetail>(); const [error, setError] = useState<ApiError>(); const [loading, setLoading] = useState(true); const [retryToken, setRetryToken] = useState(0); const [preview, setPreview] = useState<PortalDocument>()
  const numericId = Number(id)
  useEffect(() => { if (!Number.isInteger(numericId) || numericId <= 0) { setError({ status: 400, title: 'Invalid request' }); setLoading(false); return undefined }; const controller = new AbortController(); setLoading(true); setError(undefined); void getPortalRequestById(numericId, controller.signal).then(setRequest).catch((reason: unknown) => { if (!controller.signal.aborted) setError(toApiError(reason)) }).finally(() => { if (!controller.signal.aborted) setLoading(false) }); return () => controller.abort() }, [numericId, retryToken])
  const returnSearch = (location.state as { workspaceSearch?: string } | null)?.workspaceSearch
  const backTo = returnSearch ? `/?${returnSearch}` : '/'
  if (loading && !request) return <LoadingSurface />
  if (!request) return <ErrorSurface><div className="flex items-center justify-between gap-3"><span>{error?.title || 'Request unavailable.'}</span><AppButton tone="secondary" onClick={() => setRetryToken((value) => value + 1)}>Try again</AppButton></div></ErrorSurface>
  return <div className="mx-auto grid max-w-6xl gap-6"><Link to={backTo} className="inline-flex w-fit items-center gap-2 rounded-sm text-body text-accent hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"><ArrowLeft size={16} aria-hidden />Back to requests</Link>{error && <ErrorSurface>{error.title} Showing the previous details.</ErrorSurface>}<section className="border border-border-subtle bg-white p-4 md:p-6"><div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-start"><div className="min-w-0"><div className="flex flex-wrap items-center gap-2"><h1 className="break-words text-title font-semibold">{request.code}</h1><RequestStatusText statusName={request.statusName} /></div><p className="mt-1 break-words text-heading text-ink-muted">{request.title}</p></div><p className="text-body text-ink-muted">{formatDate(request.requestDate)}</p></div><dl className="mt-6 grid gap-x-6 gap-y-4 border-t border-border-subtle pt-4 sm:grid-cols-2 lg:grid-cols-3"><Detail label="Requester" value={request.requesterName || request.requesterNId} /><Detail label="Vendor" value={request.vendorName || request.vendorCode} /><Detail label="Current step" value={request.currentStepName || '-'} /><Detail label="Valid from" value={formatDate(request.validFrom)} /><Detail label="Valid until" value={formatDate(request.validUntil)} />{request.remark && <Detail label="Remark" value={request.remark} />}</dl>{request.sourceSystem === 'QRS' && request.sourceCode && <a href={qrsUrl(request.sourceCode)} target="_blank" rel="noreferrer" className="mt-5 inline-flex items-center gap-2 rounded-sm text-body text-accent hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">Open source request {request.sourceCode}<ExternalLink size={15} aria-hidden /></a>}</section><div className="grid gap-6 lg:grid-cols-[minmax(0,1.4fr)_minmax(18rem,1fr)]"><section className="border border-border-subtle bg-white p-4 md:p-6"><h2 className="text-heading font-semibold">Documents</h2><div className="mt-4"><DocumentList documents={request.documents} onPreview={setPreview} /></div></section><section className="border border-border-subtle bg-white p-4 md:p-6"><h2 className="text-heading font-semibold">Workflow</h2><div className="mt-4"><WorkflowTimeline steps={request.workflowSteps} /></div></section></div><section className="border border-border-subtle bg-white p-4 md:p-6"><h2 className="text-heading font-semibold">History</h2><div className="mt-4"><HistoryList histories={request.histories} /></div></section><PdfViewer document={preview} onClose={() => setPreview(undefined)} /></div>
}

function Detail({ label, value }: { label: string; value: string }) { return <div><dt className="text-caption text-ink-soft">{label}</dt><dd className="mt-0.5 break-words text-body text-ink-strong">{value}</dd></div> }
import { Download } from 'lucide-react'
import { Modal } from '@/components/ui/Modal'
import type { PortalDocument } from '@/features/requests/types'

interface PdfViewerProps { document?: PortalDocument; onClose: () => void }

export function PdfViewer({ document, onClose }: PdfViewerProps) {
  return <Modal open={Boolean(document)} title={document?.fileName ?? 'Document preview'} onClose={onClose} className="h-full max-w-none"><div className="flex min-h-0 flex-1 flex-col gap-3"><a href={document?.viewUrl} target="_blank" rel="noreferrer" className="inline-flex w-fit items-center gap-2 rounded-sm bg-accent px-3 py-2 text-body font-medium text-white hover:bg-accent-hover focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"><Download size={16} aria-hidden />Open or download</a>{document && <object data={document.viewUrl} type="application/pdf" className="min-h-0 w-full flex-1 border border-border-subtle" aria-label={`Preview of ${document.fileName}`}><p className="p-4 text-body text-ink-muted">This browser cannot preview the document. Use Open or download.</p></object>}</div></Modal>
}
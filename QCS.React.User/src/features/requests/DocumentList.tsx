import { ExternalLink, FileText } from 'lucide-react'
import type { PortalDocument } from './types'

export function DocumentList({ documents, onPreview }: { documents: PortalDocument[]; onPreview: (document: PortalDocument) => void }) {
  if (documents.length === 0) return <p className="text-body text-ink-muted">No documents are attached to this request.</p>
  return <ul className="divide-y divide-border-subtle border-y border-border-subtle">{documents.map((document) => <li key={document.id} className="flex min-w-0 items-center gap-3 py-3"><FileText size={18} className="shrink-0 text-ink-soft" aria-hidden /><div className="min-w-0 flex-1"><p className="truncate text-body font-medium">{document.fileName}</p><p className="text-caption text-ink-muted">{document.documentTypeName}</p></div><button type="button" onClick={() => onPreview(document)} className="inline-flex shrink-0 items-center gap-1 rounded-sm px-2 py-1 text-body text-accent hover:bg-accent-soft focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">View<ExternalLink size={14} aria-hidden /></button></li>)}</ul>
}
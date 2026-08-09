import { ExternalLink, Paperclip } from 'lucide-react'
import { AppButton } from '@/components/ui/AppButton'
import { formatFileSize } from './format'
import type { PortalDocument } from './types'

export function DocumentList({
  documents,
  onPreview,
}: {
  documents: PortalDocument[]
  onPreview: (document: PortalDocument) => void
}) {
  if (documents.length === 0)
    return (
      <p className="px-4 py-6 text-center text-body text-ink-muted">
        No documents are attached to this request.
      </p>
    )
  return (
    <ul className="divide-y divide-border-subtle">
      {documents.map((document) => (
        <li
          key={document.id}
          className="flex min-w-0 items-center gap-3 px-4 py-2.5"
        >
          <Paperclip className="size-4 shrink-0 text-ink-soft" aria-hidden />
          <div className="min-w-0 flex-1">
            <p className="truncate text-body font-medium">
              {document.fileName}
            </p>
            <p className="text-caption text-ink-muted">
              {[document.documentTypeName, formatFileSize(document.fileSize)]
                .filter(Boolean)
                .join(' · ')}
            </p>
          </div>
          <AppButton variant="ghost" size="sm" className="shrink-0" onClick={() => onPreview(document)}>
            View
            <ExternalLink className="size-3.5" aria-hidden />
          </AppButton>
        </li>
      ))}
    </ul>
  )
}

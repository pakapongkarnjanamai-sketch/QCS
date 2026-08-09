import { ExternalActionLink } from '@/components/ui/ExternalActionLink'
import { Modal } from '@/components/ui/Modal'

/**
 * The ONE way this portal shows a PDF: previewed in a modal first, with opening it in a tab as a
 * deliberate second step.
 *
 * There used to be three ways — this modal for detail documents, `window.open` for the form's
 * attachments, and an iframe pinned to the bottom of the form for the merged preview. The same
 * file type presented three ways. Everything routes through here now, which is why it takes a
 * plain url and name rather than a PortalDocument: the merged preview is a blob with no document
 * row behind it.
 */
export interface PdfPreview {
  url: string
  fileName: string
}

interface PdfViewerProps {
  document?: PdfPreview
  onClose: () => void
}

export function PdfViewer({ document, onClose }: PdfViewerProps) {
  return (
    <Modal
      open={Boolean(document)}
      title={document?.fileName ?? 'Document preview'}
      onClose={onClose}
      className="h-full max-w-5xl"
      // The body has to be told to grow; see the note on Modal's contentClassName. The wrapper
      // div that used to carry these classes sat inside a body that never grew, so flex-1 had
      // nothing to resolve against and the object rendered at zero height.
      contentClassName="flex min-h-0 flex-1 flex-col gap-3 p-5"
    >
      {document && <ExternalActionLink href={document.url} className="w-fit">Open in a new tab</ExternalActionLink>}
      {document && (
        <object data={document.url} type="application/pdf" className="min-h-0 w-full flex-1 border border-border-subtle" aria-label={`Preview of ${document.fileName}`}>
          <p className="p-4 text-body text-ink-muted">This browser cannot preview the document. Use Open in a new tab.</p>
        </object>
      )}
    </Modal>
  )
}
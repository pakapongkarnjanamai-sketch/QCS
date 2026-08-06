import { ArrowDown, ArrowUp, ExternalLink, FileText, Link2, Trash2, Upload } from 'lucide-react'
import { useState } from 'react'
import { IconButton } from '@/components/ui/IconButton'
import { appInputClassName } from '@/components/ui/inputStyles'
import { formatFileSize } from './format'
import type { PortalDocument } from './types'

const documentTypes = [
  { id: 10, name: 'ORIGINAL QUOTATION' },
  { id: 20, name: 'COMPARISON DOCUMENT' },
  { id: 30, name: 'PRODUCT SPECIFICATIONS' },
  { id: 40, name: 'ATTACHMENT' },
  { id: 50, name: 'EXPIRED QUOTATION' },
]

interface TypedDocumentEditorProps {
  documents: PortalDocument[]
  disabled: boolean
  uploading: boolean
  error?: string
  onUpload: (files: File[]) => void
  onAddReference: (code: string) => Promise<string | undefined>
  onUpdate: (documents: PortalDocument[]) => void
  onView: (document: PortalDocument) => void
  onRemove: (document: PortalDocument) => void
}

function isPdf(file: File) {
  return file.name.toLowerCase().endsWith('.pdf') && file.type.toLowerCase() === 'application/pdf'
}

export function TypedDocumentEditor({
  documents,
  disabled,
  uploading,
  error,
  onUpload,
  onAddReference,
  onUpdate,
  onView,
  onRemove,
}: TypedDocumentEditorProps) {
  const [fileError, setFileError] = useState<string>()
  const [referenceCode, setReferenceCode] = useState('')
  const [referenceError, setReferenceError] = useState<string>()

  const moveDocument = (index: number, offset: -1 | 1) => {
    const targetIndex = index + offset
    if (targetIndex < 0 || targetIndex >= documents.length) return
    const next = [...documents]
    ;[next[index], next[targetIndex]] = [next[targetIndex], next[index]]
    onUpdate(next)
  }

  const displayedError = referenceError ?? fileError ?? error

  return (
    <section className="rounded-sm border border-border-subtle bg-white" data-invalid={displayedError ? 'true' : undefined}>
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border-subtle px-4 py-3">
        <div>
          <h2 className="text-caption font-semibold uppercase tracking-[0.12em] text-ink-muted">Documents</h2>
          <p className="mt-0.5 text-caption text-ink-muted">PDF files only. Original Quotation is required when submitting.</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <label className="inline-flex h-8 cursor-pointer items-center gap-2 rounded-sm border border-border-subtle bg-white px-3 text-body font-medium hover:bg-surface-muted focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-accent has-[:disabled]:cursor-not-allowed has-[:disabled]:opacity-50">
            <Upload className="size-3.5" aria-hidden />
            {uploading ? 'Uploading...' : 'Upload PDFs'}
            <input
              type="file"
              className="sr-only"
              accept=".pdf,application/pdf"
              multiple
              disabled={disabled}
              onChange={(event) => {
                const files = Array.from(event.target.files ?? [])
                const invalidFile = files.find((file) => !isPdf(file))
                if (invalidFile) {
                  setFileError(`Only PDF files can be uploaded: ${invalidFile.name}`)
                } else if (files.length > 0) {
                  setFileError(undefined)
                  onUpload(files)
                }
                event.currentTarget.value = ''
              }}
            />
          </label>
        </div>
      </div>

      <form
        className="flex flex-col gap-2 border-b border-border-subtle bg-surface-muted px-4 py-3 sm:flex-row sm:items-end"
        onSubmit={(event) => {
          event.preventDefault()
          const code = referenceCode.trim()
          if (!code) {
            setReferenceError('Enter an expired quotation Code.')
            return
          }
          setReferenceError(undefined)
          void onAddReference(code).then((message) => {
            setReferenceError(message)
            if (!message) setReferenceCode('')
          })
        }}
      >
        <label className="min-w-0 flex-1">
          <span className="mb-1 block text-caption font-medium uppercase text-ink-muted">Expired quotation Code</span>
          <input
            value={referenceCode}
            onChange={(event) => setReferenceCode(event.target.value)}
            placeholder="QC-YYYYMMDD-NNN"
            disabled={disabled}
            className={appInputClassName('sm', 'w-full')}
          />
        </label>
        <button
          type="submit"
          disabled={disabled || !referenceCode.trim()}
          className="inline-flex h-8 shrink-0 items-center justify-center gap-2 rounded-sm border border-border-subtle bg-white px-3 text-body font-medium hover:bg-surface-panel focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:cursor-not-allowed disabled:opacity-50"
        >
          <Link2 className="size-3.5" aria-hidden />
          Add reference
        </button>
      </form>

      {displayedError && <p className="border-b border-border-subtle px-4 py-2 text-caption text-danger">{displayedError}</p>}

      {documents.length === 0 ? (
        <p className="px-4 py-6 text-center text-body text-ink-muted">No documents are attached to this request.</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[680px] border-collapse text-left text-body">
            <thead className="bg-surface-muted text-caption uppercase text-ink-muted">
              <tr>
                <th className="w-28 px-3 py-2 font-semibold">Order</th>
                <th className="px-3 py-2 font-semibold">File name</th>
                <th className="w-64 px-3 py-2 font-semibold">Type</th>
                <th className="w-24 px-3 py-2 text-right font-semibold">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border-subtle">
              {documents.map((document, index) => (
                <tr key={document.id}>
                  <td className="px-2 py-1.5">
                    <div className="flex items-center gap-0.5">
                      <span className="w-6 text-center text-caption tabular-nums text-ink-muted">{index + 1}</span>
                      <IconButton size="sm" label={`Move ${document.fileName} up`} disabled={disabled || index === 0} onClick={() => moveDocument(index, -1)}>
                        <ArrowUp className="size-3.5" aria-hidden />
                      </IconButton>
                      <IconButton size="sm" label={`Move ${document.fileName} down`} disabled={disabled || index === documents.length - 1} onClick={() => moveDocument(index, 1)}>
                        <ArrowDown className="size-3.5" aria-hidden />
                      </IconButton>
                    </div>
                  </td>
                  <td className="px-3 py-2">
                    <div className="flex min-w-0 items-center gap-2">
                      {document.referenceCode
                        ? <Link2 className="size-4 shrink-0 text-accent" aria-hidden />
                        : <FileText className="size-4 shrink-0 text-ink-soft" aria-hidden />}
                      <div className="min-w-0">
                        <p className="truncate font-medium">{document.fileName}</p>
                        <p className="text-caption text-ink-muted">
                          {document.referenceCode ? `${document.referenceCode} · ` : ''}{formatFileSize(document.fileSize)}
                        </p>
                      </div>
                    </div>
                  </td>
                  <td className="px-3 py-2">
                    <select
                      aria-label={`Document type for ${document.fileName}`}
                      value={document.documentTypeId}
                      disabled={disabled || Boolean(document.referenceCode)}
                      onChange={(event) => onUpdate(documents.map((item) => item.id === document.id
                        ? { ...item, documentTypeId: Number(event.target.value) }
                        : item))}
                      className={appInputClassName('sm', 'w-full')}
                    >
                      {documentTypes.map((type) => <option key={type.id} value={type.id}>{type.name}</option>)}
                    </select>
                  </td>
                  <td className="px-2 py-1.5">
                    <div className="flex justify-end">
                      <IconButton size="sm" label={`View ${document.fileName}`} disabled={disabled} onClick={() => onView(document)}>
                        <ExternalLink className="size-3.5" aria-hidden />
                      </IconButton>
                      <IconButton size="sm" label={`Delete ${document.fileName}`} tone="danger" disabled={disabled} onClick={() => onRemove(document)}>
                        <Trash2 className="size-3.5" aria-hidden />
                      </IconButton>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
import { Eye, Save, Send, Trash2 } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useNavigate, useParams, useBeforeUnload } from 'react-router'
import { AppButton } from '@/components/ui/AppButton'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { Field } from '@/components/ui/Field'
import { appInputClassName, appTextareaClassName } from '@/components/ui/inputStyles'
import { ErrorSurface, LoadingSurface } from '@/components/ui/Surfaces'
import { toApiError, type ApiError } from '@/lib/apiClient'
import { toast } from '@/lib/toast'
import { QrsSourceLookup } from './QrsSourceLookup'
import { TypedDocumentEditor } from './TypedDocumentEditor'
import { VendorLookup } from './VendorLookup'
import { PdfViewer, type PdfPreview } from '@/features/quotations/PdfViewer'
import { WorkflowRoutePreview } from './WorkflowRoutePreview'
import { createPortalDraft, deletePortalAttachment, deletePortalDraft, getPortalRequestById, previewPortalRequest, submitPortalRequest, updatePortalDraft, uploadPortalAttachment } from './requestApi'
import { createEmptyRequest, mapServerFieldErrors, validateRequest, type RequestFormErrors } from './requestFormValidation'
import type { PortalDocument, PortalRequestDetail, SavePortalRequest } from './types'

type Action = 'save' | 'submit' | 'preview' | 'delete' | 'upload' | 'remove'
function fromDetail(detail: PortalRequestDetail): SavePortalRequest {
  return {
    title: detail.title ?? '',
    vendorCode: detail.vendorCode ?? '',
    vendorName: detail.vendorName ?? '',
    sourceSystem: detail.sourceSystem ?? '',
    sourceCode: detail.sourceCode ?? '',
    validFrom: detail.validFrom?.slice(0, 10) ?? '',
    validUntil: detail.validUntil?.slice(0, 10) ?? '',
    remark: detail.remark ?? '',
  }
}
function focusFirstInvalid() {
  requestAnimationFrame(() => {
    const target = document.querySelector<HTMLElement>('[data-invalid="true"]')
    target?.scrollIntoView({ block: 'center' })
    target?.querySelector<HTMLElement>('input, textarea, select, button')?.focus()
  })
}

export function RequestFormPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const requestId = id ? Number(id) : undefined
  const [form, setForm] = useState(createEmptyRequest)
  const [request, setRequest] = useState<PortalRequestDetail>()
  const [loading, setLoading] = useState(Boolean(requestId))
  const [error, setError] = useState<ApiError>()
  const [errors, setErrors] = useState<RequestFormErrors>({})
  const [dirty, setDirty] = useState(false)
  const [busy, setBusy] = useState<Action>()
  // One preview surface for both an attachment and the merged PDF — same modal the detail pages
  // use. The merged PDF is a blob, which is why this holds a url and a name rather than a
  // document row.
  const [preview, setPreview] = useState<PdfPreview>()
  const [confirmDelete, setConfirmDelete] = useState(false)
  useBeforeUnload((event) => {
    if (dirty) event.preventDefault()
  })
  useEffect(() => {
    if (!requestId || !Number.isInteger(requestId) || requestId <= 0) return undefined
    const controller = new AbortController()
    void getPortalRequestById(requestId, controller.signal)
      .then((detail) => {
        setRequest(detail)
        setForm(fromDetail(detail))
      })
      .catch((reason: unknown) => {
        if (!controller.signal.aborted) setError(toApiError(reason))
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [requestId])
  // Blob urls from the merged preview must be released; attachment urls are plain paths and
  // startsWith keeps this from trying to revoke those.
  useEffect(
    () => () => {
      if (preview?.url.startsWith('blob:')) URL.revokeObjectURL(preview.url)
    },
    [preview],
  )
  const patch = (next: Partial<SavePortalRequest>) => {
    setForm((current) => ({ ...current, ...next }))
    setDirty(true)
  }
  const save = async () => {
    const nextErrors = validateRequest(form, 'draft', Boolean(request?.documents.some((document) => document.documentTypeId === 10)))
    setErrors(nextErrors)
    if (Object.keys(nextErrors).length) {
      focusFirstInvalid()
      return
    }
    setBusy('save')
    setError(undefined)
    try {
      const result = requestId ? await updatePortalDraft(requestId, form) : await createPortalDraft(form)
      setDirty(false)
      toast.success('Draft saved.')
      navigate(`/requests/${result.id}/edit`, { replace: true })
    } catch (reason) {
      const apiError = toApiError(reason)
      setError(apiError)
      setErrors(mapServerFieldErrors(apiError.fieldErrors))
      focusFirstInvalid()
    } finally {
      setBusy(undefined)
    }
  }
  const submit = async () => {
    if (!requestId) {
      await save()
      return
    }
    const nextErrors = validateRequest(form, 'submit', Boolean(request?.documents.some((document) => document.documentTypeId === 10)))
    setErrors(nextErrors)
    if (Object.keys(nextErrors).length) {
      focusFirstInvalid()
      return
    }
    setBusy('submit')
    try {
      await updatePortalDraft(requestId, form)
      await submitPortalRequest(requestId)
      setDirty(false)
      toast.success('Request submitted.')
      navigate(`/requests/${requestId}`)
    } catch (reason) {
      const apiError = toApiError(reason)
      setError(apiError)
      setErrors(mapServerFieldErrors(apiError.fieldErrors))
      focusFirstInvalid()
    } finally {
      setBusy(undefined)
    }
  }
  const upload = async (file: File, documentTypeId: number) => {
    if (!requestId) {
      toast.warning('Save the draft before uploading attachments.')
      return
    }
    setBusy('upload')
    try {
      await uploadPortalAttachment(requestId, file, documentTypeId)
      const detail = await getPortalRequestById(requestId)
      setRequest(detail)
      toast.success('Attachment uploaded.')
    } catch (reason) {
      setError(toApiError(reason))
    } finally {
      setBusy(undefined)
    }
  }
  const remove = async (document: PortalDocument) => {
    if (!requestId) return
    setBusy('remove')
    try {
      await deletePortalAttachment(requestId, document.id)
      setRequest(await getPortalRequestById(requestId))
      toast.success('Attachment removed.')
    } catch (reason) {
      setError(toApiError(reason))
    } finally {
      setBusy(undefined)
    }
  }
  if (loading) return <LoadingSurface />
  if (error && !request && requestId) return <ErrorSurface>{error.detail ?? error.title}</ErrorSurface>
  const documents = request?.documents ?? []
  const disabled = Boolean(busy)
  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <header>
        <h1 className="text-title font-semibold">{requestId ? `Edit ${request?.code ?? 'request'}` : 'New request'}</h1>
        <p className="mt-1 text-body text-ink-muted">Save a draft at any time. Required fields apply when submitting.</p>
      </header>
      {error && <ErrorSurface>{error.detail ?? error.title}</ErrorSurface>}
      <section className="space-y-4 rounded-sm border border-border-subtle bg-white p-4">
        <Field label="Title" required error={errors.title}>
          <input value={form.title} onChange={(event) => patch({ title: event.target.value })} className={appInputClassName('md', 'w-full')} />
        </Field>
        <VendorLookup value={form} errors={errors} onChange={patch} />
        <QrsSourceLookup value={form} onChange={patch} />
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Valid from" required error={errors.validFrom}>
            <input type="date" value={form.validFrom} onChange={(event) => patch({ validFrom: event.target.value })} className={appInputClassName('md', 'w-full')} />
          </Field>
          <Field label="Valid until" required error={errors.validUntil}>
            <input type="date" value={form.validUntil} onChange={(event) => patch({ validUntil: event.target.value })} className={appInputClassName('md', 'w-full')} />
          </Field>
        </div>
        <Field label="Remark" error={errors.remark}>
          <textarea value={form.remark} onChange={(event) => patch({ remark: event.target.value })} className={appTextareaClassName('min-h-24 w-full')} />
        </Field>
        <TypedDocumentEditor documents={documents} disabled={disabled} error={errors.attachments} onUpload={upload} onView={(document) => setPreview({ url: document.viewUrl, fileName: document.fileName })} onRemove={remove} />
        {request && <WorkflowRoutePreview steps={request.workflowSteps} />}
      </section>
      <div className="flex flex-wrap justify-end gap-2">
        <AppButton
          variant="secondary"
          onClick={async () => {
            if (!requestId) return
            setBusy('preview')
            try {
              const blob = await previewPortalRequest(requestId)
              setPreview({ url: URL.createObjectURL(blob), fileName: `${form.title || "Request"} — merged preview.pdf` })
            } catch (reason) {
              setError(toApiError(reason))
            } finally {
              setBusy(undefined)
            }
          }}
          disabled={disabled || !requestId}
        >
          <Eye className="size-4" aria-hidden /> Preview
        </AppButton>
        <AppButton onClick={() => void save()} disabled={disabled}>
          <Save className="size-4" aria-hidden />
          {busy === 'save' ? 'Saving...' : 'Save draft'}
        </AppButton>
        <AppButton onClick={() => void submit()} disabled={disabled}>
          <Send className="size-4" aria-hidden />
          {busy === 'submit' ? 'Submitting...' : 'Submit'}
        </AppButton>
        {requestId && request?.permissions.canDelete && (
          <AppButton variant="danger" onClick={() => setConfirmDelete(true)} disabled={disabled}>
            <Trash2 className="size-4" aria-hidden />
            Delete
          </AppButton>
        )}
      </div>
      <PdfViewer document={preview} onClose={() => { if (preview?.url.startsWith("blob:")) URL.revokeObjectURL(preview.url); setPreview(undefined) }} />
      <ConfirmDialog
        open={confirmDelete}
        title="Delete draft"
        confirmText="Delete"
        onClose={() => setConfirmDelete(false)}
        onConfirm={() => {
          if (!requestId) return
          void (async () => {
            setBusy('delete')
            try {
              await deletePortalDraft(requestId)
              setDirty(false)
              toast.success('Draft deleted.')
              navigate('/')
            } catch (reason) {
              setError(toApiError(reason))
            } finally {
              setBusy(undefined)
              setConfirmDelete(false)
            }
          })()
        }}
      >
        Delete this draft permanently?
      </ConfirmDialog>
    </div>
  )
}

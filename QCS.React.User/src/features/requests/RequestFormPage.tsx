import { ArrowLeft, Eye, Save, Send, Trash2 } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams, useBeforeUnload } from 'react-router'
import { AppButton } from '@/components/ui/AppButton'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { ErrorSurface, LoadingSurface } from '@/components/ui/Surfaces'
import { toApiError, type ApiError } from '@/lib/apiClient'
import { toast } from '@/lib/toast'
import { ApprovalActionDialog } from './ApprovalActionDialog'
import { QrsSourceLookup } from './QrsSourceLookup'
import { TypedAttachmentEditor } from './TypedAttachmentEditor'
import { VendorLookup } from './VendorLookup'
import { WorkflowRoutePreview } from './WorkflowRoutePreview'
import { approvePortalRequest, createPortalDraft, deletePortalAttachment, deletePortalDraft, getPortalRequestById, previewPortalRequest, rejectPortalRequest, submitPortalRequest, updatePortalDraft, uploadPortalAttachment } from './requestApi'
import { createEmptyRequest, mapServerFieldErrors, validateRequest, type RequestFormErrors } from './requestFormValidation'
import type { PortalDocument, PortalRequestDetail, SavePortalRequest } from './types'

type Action = 'save' | 'submit' | 'preview' | 'delete' | 'upload' | 'remove' | 'approve' | 'reject'
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
  const [previewUrl, setPreviewUrl] = useState('')
  const [confirmDelete, setConfirmDelete] = useState(false)
  const [approvalAction, setApprovalAction] = useState<'approve' | 'reject'>()
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
  useEffect(
    () => () => {
      if (previewUrl) URL.revokeObjectURL(previewUrl)
    },
    [previewUrl],
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
    <div className="mx-auto grid max-w-5xl gap-6">
      <Link to={requestId ? `/requests/${requestId}` : '/requests'} className="inline-flex w-fit items-center gap-2 text-body text-accent hover:underline">
        <ArrowLeft size={16} aria-hidden />
        Back
      </Link>
      <header>
        <h1 className="text-title font-semibold">{requestId ? `Edit ${request?.code ?? 'request'}` : 'New request'}</h1>
        <p className="mt-1 text-body text-ink-muted">Save a draft at any time. Required fields apply when submitting.</p>
      </header>
      {error && <ErrorSurface>{error.detail ?? error.title}</ErrorSurface>}
      <section className="grid gap-5 border border-border-subtle bg-white p-4 md:p-6">
        <label className="grid gap-1.5 text-body" data-invalid={errors.title ? 'true' : undefined}>
          Title <span className="text-danger">*</span>
          <input value={form.title} onChange={(event) => patch({ title: event.target.value })} className="rounded-sm border border-border-subtle px-3 py-2" />
          {errors.title && <span className="text-caption text-danger">{errors.title}</span>}
        </label>
        <VendorLookup value={form} errors={errors} onChange={patch} />
        <QrsSourceLookup value={form} onChange={patch} />
        <div className="grid gap-4 sm:grid-cols-2">
          <label className="grid gap-1.5 text-body" data-invalid={errors.validFrom ? 'true' : undefined}>
            Valid from <span className="text-danger">*</span>
            <input type="date" value={form.validFrom} onChange={(event) => patch({ validFrom: event.target.value })} className="rounded-sm border border-border-subtle px-3 py-2" />
            {errors.validFrom && <span className="text-caption text-danger">{errors.validFrom}</span>}
          </label>
          <label className="grid gap-1.5 text-body" data-invalid={errors.validUntil ? 'true' : undefined}>
            Valid until <span className="text-danger">*</span>
            <input type="date" value={form.validUntil} onChange={(event) => patch({ validUntil: event.target.value })} className="rounded-sm border border-border-subtle px-3 py-2" />
            {errors.validUntil && <span className="text-caption text-danger">{errors.validUntil}</span>}
          </label>
        </div>
        <label className="grid gap-1.5 text-body" data-invalid={errors.remark ? 'true' : undefined}>
          Remark
          <textarea value={form.remark} onChange={(event) => patch({ remark: event.target.value })} className="min-h-24 rounded-sm border border-border-subtle px-3 py-2" />
          {errors.remark && <span className="text-caption text-danger">{errors.remark}</span>}
        </label>
        <TypedAttachmentEditor documents={documents} disabled={disabled} error={errors.attachments} onUpload={upload} onView={(document) => window.open(document.viewUrl, '_blank', 'noopener,noreferrer')} onRemove={remove} />
        {request && <WorkflowRoutePreview steps={request.workflowSteps} />}
      </section>
      <div className="flex flex-wrap justify-end gap-2">
        <AppButton
          tone="secondary"
          onClick={async () => {
            if (!requestId) return
            setBusy('preview')
            try {
              const blob = await previewPortalRequest(requestId)
              setPreviewUrl(URL.createObjectURL(blob))
            } catch (reason) {
              setError(toApiError(reason))
            } finally {
              setBusy(undefined)
            }
          }}
          disabled={disabled || !requestId}
        >
          <Eye size={16} aria-hidden /> Preview
        </AppButton>
        <AppButton onClick={() => void save()} disabled={disabled}>
          <Save size={16} aria-hidden />
          {busy === 'save' ? 'Saving...' : 'Save draft'}
        </AppButton>
        <AppButton onClick={() => void submit()} disabled={disabled}>
          <Send size={16} aria-hidden />
          {busy === 'submit' ? 'Submitting...' : 'Submit'}
        </AppButton>
        {requestId && request?.permissions.canDelete && (
          <AppButton tone="danger" onClick={() => setConfirmDelete(true)} disabled={disabled}>
            <Trash2 size={16} aria-hidden />
            Delete
          </AppButton>
        )}
        {request?.permissions.canApprove && (
          <AppButton onClick={() => setApprovalAction('approve')} disabled={disabled}>
            Approve
          </AppButton>
        )}
        {request?.permissions.canReject && (
          <AppButton tone="danger" onClick={() => setApprovalAction('reject')} disabled={disabled}>
            Reject
          </AppButton>
        )}
      </div>
      {previewUrl && <iframe title="Merged PDF preview" src={previewUrl} sandbox="allow-same-origin" className="h-[70vh] w-full border border-border-subtle" />}
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
      <ApprovalActionDialog
        action={approvalAction}
        busy={busy === 'approve' || busy === 'reject'}
        onClose={() => setApprovalAction(undefined)}
        onConfirm={(comment) => {
          if (!requestId || !approvalAction) return
          void (async () => {
            const action = approvalAction
            setBusy(action)
            try {
              await (action === 'approve' ? approvePortalRequest(requestId, { comment }) : rejectPortalRequest(requestId, { comment }))
              setDirty(false)
              toast.success(action === 'approve' ? 'Request approved.' : 'Request rejected.')
              navigate(`/requests/${requestId}`)
            } catch (reason) {
              setError(toApiError(reason))
            } finally {
              setBusy(undefined)
              setApprovalAction(undefined)
            }
          })()
        }}
      />
    </div>
  )
}

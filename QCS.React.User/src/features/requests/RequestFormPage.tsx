import { ExternalLink, Eye, Save, Send, Trash2 } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useNavigate, useParams, useBeforeUnload } from 'react-router'
import { AppButton } from '@/components/ui/AppButton'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { Field } from '@/components/ui/Field'
import { appInputClassName, appTextareaClassName } from '@/components/ui/inputStyles'
import { StatusBadge } from '@/components/ui/StatusBadge'
import { ErrorSurface, LoadingSurface } from '@/components/ui/Surfaces'
import { qrsRequestUrl } from '@/config/appConfig'
import { toApiError, type ApiError } from '@/lib/apiClient'
import { toast } from '@/lib/toast'
import { ApprovalActionDialog, type ApprovalActionKind } from './ApprovalActionDialog'
import { ApprovalSteps } from './ApprovalSteps'
import { QrsSourceLookup } from './QrsSourceLookup'
import { TypedDocumentEditor } from './TypedDocumentEditor'
import { VendorLookup } from './VendorLookup'
import { PdfViewer, type PdfPreview } from '@/features/quotations/PdfViewer'
import { WorkflowRoutePreview } from './WorkflowRoutePreview'
import { addExpiredQuotationReference, approvePortalRequest, cancelPortalRequest, createPortalDraft, deletePortalAttachment, deletePortalDraft, getPortalRequestById, getRoutePreview, previewPortalRequest, rejectPortalRequest, returnPortalRequest, submitPortalRequest, updatePortalDocuments, updatePortalDraft, uploadPortalAttachment } from './requestApi'
import { createEmptyRequest, mapServerFieldErrors, validateRequest, type RequestFormErrors } from './requestFormValidation'
import type { PortalApprovalAction, PortalDocument, PortalRequestDetail, RoutePreview, SavePortalRequest } from './types'

type Action = 'save' | 'submit' | 'preview' | 'delete' | 'upload' | 'remove' | 'documents' | 'reference'
const defaultUploadDocumentTypeId = 40

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
  const [routePreview, setRoutePreview] = useState<RoutePreview>()
  const [routeLoading, setRouteLoading] = useState(false)
  const [routeError, setRouteError] = useState<string>()
  const [confirmDelete, setConfirmDelete] = useState(false)
  const [approvalAction, setApprovalAction] = useState<ApprovalActionKind>()
  const [busyApprovalAction, setBusyApprovalAction] = useState<ApprovalActionKind>()
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
      navigate(`/requests/${result.id}`, { replace: true })
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
      const detail = await getPortalRequestById(requestId)
      setRequest(detail)
      setForm(fromDetail(detail))
    } catch (reason) {
      const apiError = toApiError(reason)
      setError(apiError)
      setErrors(mapServerFieldErrors(apiError.fieldErrors))
      focusFirstInvalid()
    } finally {
      setBusy(undefined)
    }
  }
  const upload = async (files: File[]) => {
    const nextErrors = validateRequest(
      form,
      'draft',
      Boolean(request?.documents.some((document) => document.documentTypeId === 10)),
    )
    setErrors(nextErrors)
    if (Object.keys(nextErrors).length) {
      focusFirstInvalid()
      return
    }

    setBusy('upload')
    setError(undefined)
    let targetRequestId = requestId
    try {
      if (!targetRequestId) {
        const draft = await createPortalDraft(form)
        targetRequestId = draft.id
        setDirty(false)
        navigate(`/requests/${draft.id}`, { replace: true })
      }

      for (const file of files) {
        await uploadPortalAttachment(targetRequestId, file, defaultUploadDocumentTypeId)
      }
      const detail = await getPortalRequestById(targetRequestId)
      setRequest(detail)
      toast.success(files.length === 1 ? 'Document uploaded.' : `${files.length} documents uploaded.`)
    } catch (reason) {
      if (targetRequestId) {
        setRequest(await getPortalRequestById(targetRequestId).catch(() => request))
      }
      setError(toApiError(reason))
    } finally {
      setBusy(undefined)
    }
  }
  const updateDocuments = async (nextDocuments: PortalDocument[]) => {
    if (!requestId) return
    setBusy('documents')
    try {
      await updatePortalDocuments(requestId, nextDocuments)
      setRequest(await getPortalRequestById(requestId))
      toast.success('Documents updated.')
    } catch (reason) {
      setError(toApiError(reason))
    } finally {
      setBusy(undefined)
    }
  }
  const addReference = async (code: string): Promise<string | undefined> => {
    if (!requestId) {
      toast.warning('Save the draft before referencing an expired quotation.')
      return 'Save the draft before adding a quotation reference.'
    }
    setBusy('reference')
    try {
      await updatePortalDraft(requestId, form)
      await addExpiredQuotationReference(requestId, code)
      setRequest(await getPortalRequestById(requestId))
      setDirty(false)
      toast.success(`Expired quotation ${code.trim().toUpperCase()} referenced.`)
      return undefined
    } catch (reason) {
      const apiError = toApiError(reason)
      setError(apiError)
      return apiError.detail ?? apiError.title
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
  const runApprovalAction = async (input: PortalApprovalAction) => {
    const action = approvalAction
    if (!action || !requestId) return
    const runners: Record<ApprovalActionKind, () => Promise<void>> = {
      submit: () => submitPortalRequest(requestId),
      approve: () => approvePortalRequest(requestId, input),
      reject: () => rejectPortalRequest(requestId, input),
      return: () => returnPortalRequest(requestId, input),
      cancel: () => cancelPortalRequest(requestId, input),
    }
    setBusyApprovalAction(action)
    setError(undefined)
    try {
      await runners[action]()
      const detail = await getPortalRequestById(requestId)
      setRequest(detail)
      setForm(fromDetail(detail))
      setApprovalAction(undefined)
    } catch (reason) {
      setError(toApiError(reason))
    } finally {
      setBusyApprovalAction(undefined)
    }
  }
  if (loading) return <LoadingSurface />
  if (error && !request && requestId) return <ErrorSurface>{error.detail ?? error.title}</ErrorSurface>
  const documents = request?.documents ?? []
  const disabled = Boolean(busy || busyApprovalAction)
  const canEdit = !requestId || request?.permissions.canEdit === true
  const formDisabled = disabled || !canEdit
  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="text-title font-semibold">{requestId ? `${canEdit ? 'Edit ' : ''}${request?.code ?? 'request'}` : 'New request'}</h1>
            {request && <StatusBadge status={request.statusName} />}
          </div>
          <p className="mt-1 text-body text-ink-muted">
            {canEdit ? 'Save a draft at any time. Required fields apply when submitting.' : 'Request details and approval progress.'}
          </p>
        </div>
        {request?.sourceSystem === 'QRS' && request.sourceCode && (
          <a href={qrsRequestUrl(request.sourceCode)} target="_blank" rel="noreferrer" className="inline-flex items-center gap-2 rounded-sm text-body text-accent hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">
            Open source request {request.sourceCode}
            <ExternalLink className="size-3.5" aria-hidden />
          </a>
        )}
      </header>
      {error && <ErrorSurface>{error.detail ?? error.title}</ErrorSurface>}
      <section className="space-y-4 rounded-sm border border-border-subtle bg-white p-4">
        <Field label="Title" required error={errors.title}>
          <input value={form.title} disabled={formDisabled} onChange={(event) => patch({ title: event.target.value })} className={appInputClassName('md', 'w-full')} />
        </Field>
        <VendorLookup value={form} errors={errors} disabled={formDisabled} onChange={patch} />
        <QrsSourceLookup value={form} disabled={formDisabled} onChange={patch} />
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Valid from" required error={errors.validFrom}>
            <input type="date" value={form.validFrom} disabled={formDisabled} onChange={(event) => patch({ validFrom: event.target.value })} className={appInputClassName('md', 'w-full')} />
          </Field>
          <Field label="Valid until" required error={errors.validUntil}>
            <input type="date" value={form.validUntil} disabled={formDisabled} onChange={(event) => patch({ validUntil: event.target.value })} className={appInputClassName('md', 'w-full')} />
          </Field>
        </div>
        <Field label="Remark" error={errors.remark}>
          <textarea value={form.remark} disabled={formDisabled} onChange={(event) => patch({ remark: event.target.value })} className={appTextareaClassName('min-h-24 w-full')} />
        </Field>
        <TypedDocumentEditor documents={documents} disabled={formDisabled} uploading={busy === 'upload'} error={errors.attachments} onUpload={upload} onAddReference={addReference} onUpdate={updateDocuments} onView={(document) => setPreview({ url: document.viewUrl, fileName: document.fileName })} onRemove={remove} />
        {canEdit && <WorkflowRoutePreview
          preview={routePreview}
          loading={routeLoading}
          error={routeError}
          onLoad={() => {
            setRouteLoading(true)
            setRouteError(undefined)
            // Sends the form as it stands, not the saved draft: the point is to answer "who will
            // approve what I am about to submit", and unsaved edits can change the route.
            void getRoutePreview(form)
              .then(setRoutePreview)
              .catch((reason: unknown) => setRouteError(toApiError(reason).detail ?? toApiError(reason).title))
              .finally(() => setRouteLoading(false))
          }}
        />}
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
        {canEdit && <AppButton onClick={() => void save()} disabled={disabled}>
          <Save className="size-4" aria-hidden />
          {busy === 'save' ? 'Saving...' : 'Save draft'}
        </AppButton>}
        {canEdit && <AppButton onClick={() => void submit()} disabled={disabled}>
          <Send className="size-4" aria-hidden />
          {busy === 'submit' ? 'Submitting...' : 'Submit'}
        </AppButton>}
        {!canEdit && request?.permissions.canSubmit && <AppButton disabled={disabled} onClick={() => setApprovalAction('submit')}>Submit</AppButton>}
        {request?.permissions.canApprove && <AppButton disabled={disabled} onClick={() => setApprovalAction('approve')}>Approve</AppButton>}
        {request?.permissions.canReject && <AppButton variant="danger" disabled={disabled} onClick={() => setApprovalAction('reject')}>Reject</AppButton>}
        {request?.permissions.canReturn && <AppButton variant="secondary" disabled={disabled} onClick={() => setApprovalAction('return')}>Return</AppButton>}
        {request?.permissions.canCancel && <AppButton variant="secondary" disabled={disabled} onClick={() => setApprovalAction('cancel')}>Cancel request</AppButton>}
        {requestId && request?.permissions.canDelete && (
          <AppButton variant="danger" onClick={() => setConfirmDelete(true)} disabled={disabled}>
            <Trash2 className="size-4" aria-hidden />
            Delete
          </AppButton>
        )}
      </div>
      {requestId && !canEdit && <ApprovalSteps steps={request?.workflowSteps ?? []} histories={request?.histories ?? []} />}
      <PdfViewer document={preview} onClose={() => { if (preview?.url.startsWith("blob:")) URL.revokeObjectURL(preview.url); setPreview(undefined) }} />
      <ApprovalActionDialog
        action={approvalAction}
        busy={Boolean(busyApprovalAction)}
        steps={(request?.workflowSteps ?? []).filter((step) => !step.isCurrentStep && Boolean(step.actionDate))}
        onClose={() => { if (!busyApprovalAction) setApprovalAction(undefined) }}
        onConfirm={(input) => void runApprovalAction(input)}
      />
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

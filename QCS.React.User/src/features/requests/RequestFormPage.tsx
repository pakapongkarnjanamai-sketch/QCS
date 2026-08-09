import { Eye, Save, Send, Trash2, Settings2 } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams, useBeforeUnload, useSearchParams } from 'react-router'
import { AppButton } from '@/components/ui/AppButton'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { Field } from '@/components/ui/Field'
import { ExternalActionLink } from '@/components/ui/ExternalActionLink'
import { FormActions, FormPage, FormPageHeader, FormSection, FormSummary, FormSummaryItem } from '@/components/ui/FormPage'
import { appInputClassName, appTextareaClassName } from '@/components/ui/inputStyles'
import { StatusBadge } from '@/components/ui/StatusBadge'
import { ErrorSurface, LoadingSurface } from '@/components/ui/Surfaces'
import { qrsRequestUrl } from '@/config/appConfig'
import { toApiError, type ApiError } from '@/lib/apiClient'
import { toast } from '@/lib/toast'
import { ApprovalActionDialog, type ApprovalActionKind } from './ApprovalActionDialog'
import { ApprovalSteps } from './ApprovalSteps'
import { RequestSetupPanel } from './RequestSetupPanel'
import { RenewQuotationLink } from './RenewQuotationLink'
import { TypedDocumentEditor } from './TypedDocumentEditor'
import { VendorLookup } from './VendorLookup'
import { PdfViewer, type PdfPreview } from '@/features/quotations/PdfViewer'
import { WorkflowRoutePreview } from './WorkflowRoutePreview'
import { addExpiredQuotationReference, approvePortalRequest, cancelPortalRequest, createPortalDraft, deletePortalAttachment, deletePortalDraft, getPortalRequestById, getRoutePreview, previewPortalRequest, rejectPortalRequest, resolveSetupFromQcs, resolveSetupFromQrs, returnPortalRequest, submitPortalRequest, updatePortalDocuments, updatePortalDraft, uploadPortalAttachment } from './requestApi'
import { createEmptyRequest, mapServerFieldErrors, validateRequest, validateSetup, type RequestFormErrors } from './requestFormValidation'
import { setupErrorMessage } from './setupErrors'
import type { DiscriminatedSetupState, PortalApprovalAction, PortalDocument, PortalRequestDetail, PortalSetupResolution, RoutePreview, SavePortalRequest, SetupFlow } from './types'

type Action = 'save' | 'submit' | 'preview' | 'delete' | 'upload' | 'remove' | 'documents' | 'reference'
const defaultUploadDocumentTypeId = 40

function formatDate(value?: string): string {
  return value
    ? new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium' }).format(new Date(value))
    : '-'
}

function fromDetail(detail: PortalRequestDetail): SavePortalRequest {
  return {
    intent: detail.intent ?? 0,
    renewedFromRequestId: detail.renewedFromRequestId,
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

function fromSetup(setup: DiscriminatedSetupState): Partial<SavePortalRequest> {
  if (setup.intent === 'New' && setup.origin === 'QCS') {
    return {
      intent: 0,
      renewedFromRequestId: undefined,
      title: '',
      vendorCode: '',
      vendorName: '',
      sourceSystem: '',
      sourceCode: '',
    }
  }

  if (setup.intent === 'New') {
    return {
      intent: 0,
      renewedFromRequestId: undefined,
      title: setup.qrsTitle ?? '',
      vendorCode: '',
      vendorName: '',
      sourceSystem: 'QRS',
      sourceCode: setup.qrsSourceCode,
    }
  }

  return {
    intent: 1,
    renewedFromRequestId: setup.renewedFromRequestId,
    title: setup.origin === 'QRS' ? setup.qrsTitle ?? '' : setup.title,
    vendorCode: setup.vendorCode,
    vendorName: setup.vendorName,
    sourceSystem: setup.origin === 'QRS' ? 'QRS' : '',
    sourceCode: setup.origin === 'QRS' ? setup.qrsSourceCode : '',
  }
}

function getSetupFlow(searchParams: URLSearchParams): SetupFlow | undefined {
  const intent = searchParams.get('intent')
  const origin = searchParams.get('origin')
  if (intent === 'new' && origin === 'qcs') return 'new-qcs'
  if (intent === 'new' && origin === 'qrs') return 'new-qrs'
  if (intent === 'renewal' && origin === 'qcs') return 'renewal-qcs'
  if (intent === 'renewal' && origin === 'qrs') return 'renewal-qrs'
  return undefined
}

// A field the resolver's own contract guarantees for this flow. Missing means the
// response is malformed, which belongs on the error surface — not half-applied to
// the form, where it would show up later as a create rejected for reasons the user
// cannot see.
function required<T>(value: T | undefined | null, field: string, flow: string): T {
  if (value === undefined || value === null) throw new Error(`The setup response for ${flow} is missing ${field}.`)
  return value
}

function fromResolution(resolution: PortalSetupResolution): DiscriminatedSetupState {
  const { flow } = resolution
  if (flow === 'NewQrs') {
    return { intent: 'New', origin: 'QRS', qrsSourceCode: required(resolution.sourceCode, 'sourceCode', flow), qrsTitle: resolution.sourceTitle }
  }
  const predecessor = {
    renewedFromRequestId: required(resolution.renewedFromRequestId, 'renewedFromRequestId', flow),
    renewedFromCode: required(resolution.renewedFromCode, 'renewedFromCode', flow),
    vendorCode: required(resolution.vendorCode, 'vendorCode', flow),
    vendorName: required(resolution.vendorName, 'vendorName', flow),
  }
  if (flow === 'RenewalQcs') {
    return { intent: 'Renewal', origin: 'QCS', ...predecessor, title: resolution.sourceTitle ?? '' }
  }
  return { intent: 'Renewal', origin: 'QRS', ...predecessor, qrsSourceCode: required(resolution.sourceCode, 'sourceCode', flow), qrsTitle: resolution.sourceTitle }
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
  const [searchParams, setSearchParams] = useSearchParams()
  const requestId = id ? Number(id) : undefined
  const qrsCode = requestId ? undefined : searchParams.get('qrsCode')?.trim()
  const directRenewedFromCode = requestId ? undefined : searchParams.get('renewedFromCode')?.trim()
  const selectedFlow = requestId ? undefined : getSetupFlow(searchParams)
  const [form, setForm] = useState(createEmptyRequest)
  const [request, setRequest] = useState<PortalRequestDetail>()
  const [loading, setLoading] = useState(Boolean(requestId))
  const [error, setError] = useState<ApiError>()
  const [errors, setErrors] = useState<RequestFormErrors>({})
  const [dirty, setDirty] = useState(false)
  const [busy, setBusy] = useState<Action>()
  const [setupCompleted, setSetupCompleted] = useState(Boolean(requestId))
  const [setupSummary, setSetupSummary] = useState<DiscriminatedSetupState>()
  const [confirmSetupChange, setConfirmSetupChange] = useState(false)
  const [retryToken, setRetryToken] = useState(0)
  const [setupResolving, setSetupResolving] = useState(Boolean(qrsCode || directRenewedFromCode))
  const [setupResolutionError, setSetupResolutionError] = useState<ApiError>()

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
    setLoading(true)
    setError(undefined)
    void getPortalRequestById(requestId, controller.signal)
      .then((detail) => {
        setRequest(detail)
        setForm(fromDetail(detail))
        setSetupCompleted(true)
      })
      .catch((reason: unknown) => {
        if (!controller.signal.aborted) setError(toApiError(reason))
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
    }, [requestId, retryToken])

  useEffect(
    () => () => {
      if (preview?.url.startsWith('blob:')) URL.revokeObjectURL(preview.url)
    },
    [preview],
  )

  useEffect(() => {
    if (requestId || (!qrsCode && !directRenewedFromCode)) return undefined
    const controller = new AbortController()
    setSetupResolving(true)
    setSetupResolutionError(undefined)
    const resolve = qrsCode ? resolveSetupFromQrs(qrsCode, controller.signal) : resolveSetupFromQcs(directRenewedFromCode!, controller.signal)
    void resolve
      .then((resolution) => completeSetup(fromResolution(resolution)))
      .catch((reason: unknown) => { if (!controller.signal.aborted) setSetupResolutionError(toApiError(reason)) })
      .finally(() => { if (!controller.signal.aborted) setSetupResolving(false) })
    return () => controller.abort()
  }, [directRenewedFromCode, qrsCode, requestId, retryToken])

  const patch = (next: Partial<SavePortalRequest>) => {
    setForm((current) => ({ ...current, ...next }))
    setDirty(true)
  }

  const completeSetup = (setup: DiscriminatedSetupState) => {
    setForm((current) => ({ ...current, ...fromSetup(setup) }))
    setSetupSummary(setup)
    setSetupCompleted(true)
    setErrors({})
    setDirty(true)
  }

  const selectFlow = (flow?: SetupFlow) => {
    if (!flow) {
      setSearchParams(new URLSearchParams(), { replace: true })
      return
    }
    const [intent, origin] = flow.split('-')
    const nextSearchParams = new URLSearchParams(searchParams)
    nextSearchParams.set('intent', intent)
    nextSearchParams.set('origin', origin)
    setSearchParams(nextSearchParams)
  }

  const resolveQrsSetup = async (code: string) => completeSetup(fromResolution(await resolveSetupFromQrs(code)))
  const resolveQcsSetup = async (code: string) => completeSetup(fromResolution(await resolveSetupFromQcs(code)))

  const changeSetup = () => {
    setForm((current) => ({
      ...current,
      intent: 0,
      renewedFromRequestId: undefined,
      title: '',
      vendorCode: '',
      vendorName: '',
      sourceSystem: '',
      sourceCode: '',
    }))
    setSetupSummary(undefined)
    setSetupCompleted(false)
    setErrors({})
    setDirty(true)
    setConfirmSetupChange(false)
  }

  const save = async () => {
    if (!requestId && !setupCompleted) {
      toast.warning('Complete request setup before saving the draft.')
      return
    }

    const setupErrors = validateSetup(form)
    if (Object.keys(setupErrors).length) {
      setErrors(setupErrors)
      setSetupCompleted(false)
      focusFirstInvalid()
      return
    }

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
    if (!requestId) {
      const setupErrors = validateSetup(form)
      if (!setupCompleted || Object.keys(setupErrors).length) {
        setErrors(setupErrors)
        toast.error('Complete request setup before uploading documents.')
        return
      }
    }

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
  if (setupResolving) return <LoadingSurface />
  if (setupResolutionError) return <ErrorSurface><div className="space-y-3"><p>{setupErrorMessage(setupResolutionError)}</p><AppButton variant="secondary" onClick={() => { const next = new URLSearchParams(searchParams); next.delete('qrsCode'); next.delete('renewedFromCode'); setSearchParams(next, { replace: true }); setSetupResolutionError(undefined) }}>Back</AppButton>{setupResolutionError.status !== 409 && <AppButton variant="secondary" onClick={() => setRetryToken((token) => token + 1)}>Try again</AppButton>}</div></ErrorSurface>
  if (error && !request && requestId) {
    return (
      <ErrorSurface>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <span>{error.detail ?? error.title}</span>
          <AppButton
            variant="secondary"
            onClick={() => setRetryToken((value) => value + 1)}
          >
            Try again
          </AppButton>
        </div>
      </ErrorSurface>
    )
  }

  const documents = request?.documents ?? []
  const disabled = Boolean(busy || busyApprovalAction)
  const canEdit = !requestId || request?.permissions.canEdit === true
  const formDisabled = disabled || !canEdit
  const isRenewal = form.intent === 1 || request?.intent === 1
  const isQrsOrigin = request?.originName === 'QRS' || form.sourceSystem.toUpperCase() === 'QRS'
  const renewedFromCode = request?.renewedFromCode ?? (setupSummary?.intent === 'Renewal' ? setupSummary.renewedFromCode : undefined)
  const renewedFromRequestId = request?.renewedFromRequestId ?? (setupSummary?.intent === 'Renewal' ? setupSummary.renewedFromRequestId : undefined)

  return (
    <FormPage>
      <FormPageHeader
        title={requestId ? `${canEdit ? 'Edit ' : ''}${request?.code ?? 'request'}` : 'New request'}
        description={canEdit ? 'Save a draft at any time. Required fields apply when submitting.' : 'Request details and approval progress.'}
        status={request && <StatusBadge status={request.statusName} />}
        actions={request && ((isQrsOrigin && request.sourceCode) || request.canRenew) ? <>
          {isQrsOrigin && request.sourceCode && (
            <ExternalActionLink href={qrsRequestUrl(request.sourceCode)}>
              Open source request {request.sourceCode}
            </ExternalActionLink>
          )}
          {request.canRenew && <RenewQuotationLink code={request.code} />}
        </> : undefined}
      />

      {error && <ErrorSurface>{error.detail ?? error.title}</ErrorSurface>}

      {requestId && request && (
        <FormSummary>
          <FormSummaryItem label="Requester" title={`${request.requesterName} (${request.requesterNId})`} truncate>
            {request.requesterName || request.requesterNId}
            {request.requesterName && request.requesterNId ? ` (${request.requesterNId})` : ''}
          </FormSummaryItem>
          <FormSummaryItem label="Request date">{formatDate(request.requestDate)}</FormSummaryItem>
          <FormSummaryItem label="Current step" title={request.currentStepName ?? 'Not submitted'} truncate>
            {request.currentStepName ?? 'Not submitted'}
          </FormSummaryItem>
        </FormSummary>
      )}

      {!requestId && !setupCompleted ? (
        <RequestSetupPanel
          selectedFlow={selectedFlow}
          onFlowChange={selectFlow}
          onComplete={completeSetup}
          onResolveQrs={resolveQrsSetup}
          onResolveQcs={resolveQcsSetup}
        />
      ) : (
        <>
          <div className="flex items-center justify-between rounded-sm border border-border-subtle bg-surface-panel px-4 py-3 text-caption">
            <div className="flex flex-wrap items-center gap-x-4 gap-y-1">
              <div>
                <span className="text-ink-muted font-medium">Intent:</span>{' '}
                <span className="font-semibold text-ink-strong">
                  {isRenewal ? 'Renewal' : 'New'}
                </span>
              </div>
              <div>
                <span className="text-ink-muted font-medium">Origin:</span>{' '}
                <span className="font-semibold text-ink-strong">
                  {isQrsOrigin ? 'QRS' : 'QCS'}
                </span>
              </div>
              {isRenewal && renewedFromRequestId && (
                <div>
                  <span className="text-ink-muted font-medium">Predecessor:</span>{' '}
                  {requestId && renewedFromCode ? (
                    <Link
                      to={`/requests/${renewedFromRequestId}`}
                      className="rounded-sm font-mono font-semibold text-accent underline underline-offset-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                    >
                      {renewedFromCode}
                    </Link>
                  ) : (
                    <span className="font-mono font-semibold text-accent">
                      {renewedFromCode ?? `ID #${renewedFromRequestId}`}
                    </span>
                  )}
                </div>
              )}
              {(form.sourceCode || request?.sourceCode) && (
                <div>
                  <span className="text-ink-muted font-medium">QRS Code:</span>{' '}
                  <span className="font-mono font-semibold text-accent">
                    {request?.sourceCode ?? form.sourceCode}
                  </span>
                </div>
              )}
            </div>
            {!requestId && canEdit && (
              <AppButton variant="ghost" size="sm" onClick={() => setConfirmSetupChange(true)}>
                <Settings2 className="size-3.5" aria-hidden />
                <span>Change setup</span>
              </AppButton>
            )}
          </div>

          <FormSection>
            <Field label="Title" required error={errors.title}>
              <input
                value={form.title}
                disabled={formDisabled}
                onChange={(event) => patch({ title: event.target.value })}
                className={appInputClassName('md', 'w-full')}
              />
            </Field>

            <VendorLookup
              value={form}
              errors={errors}
              disabled={formDisabled || isRenewal}
              onChange={patch}
            />

            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Valid from" required error={errors.validFrom}>
                <input
                  type="date"
                  value={form.validFrom}
                  disabled={formDisabled}
                  onChange={(event) => patch({ validFrom: event.target.value })}
                  className={appInputClassName('md', 'w-full')}
                />
              </Field>
              <Field label="Valid until" required error={errors.validUntil}>
                <input
                  type="date"
                  value={form.validUntil}
                  disabled={formDisabled}
                  onChange={(event) => patch({ validUntil: event.target.value })}
                  className={appInputClassName('md', 'w-full')}
                />
              </Field>
            </div>

            <Field label="Remark" error={errors.remark}>
              <textarea
                value={form.remark}
                disabled={formDisabled}
                onChange={(event) => patch({ remark: event.target.value })}
                className={appTextareaClassName('min-h-24 w-full')}
              />
            </Field>
          </FormSection>

          <TypedDocumentEditor
            documents={documents}
            disabled={formDisabled}
            uploading={busy === 'upload'}
            error={errors.attachments}
            onUpload={upload}
            onAddReference={addReference}
            onUpdate={updateDocuments}
            onView={(document) => setPreview({ url: document.viewUrl, fileName: document.fileName })}
            onRemove={remove}
          />

          {canEdit && (
            <WorkflowRoutePreview
              preview={routePreview}
              loading={routeLoading}
              error={routeError}
              onLoad={() => {
                setRouteLoading(true)
                setRouteError(undefined)
                void getRoutePreview(form)
                  .then(setRoutePreview)
                  .catch((reason: unknown) => setRouteError(toApiError(reason).detail ?? toApiError(reason).title))
                  .finally(() => setRouteLoading(false))
              }}
            />
          )}
        </>
      )}

      {(requestId || setupCompleted) && (
        <FormActions>
          <AppButton
            variant="secondary"
            onClick={async () => {
              if (!requestId) return
              setBusy('preview')
              try {
                const blob = await previewPortalRequest(requestId)
                setPreview({ url: URL.createObjectURL(blob), fileName: `${form.title || 'Request'} — merged preview.pdf` })
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
          {canEdit && (
            <AppButton onClick={() => void save()} disabled={disabled}>
              <Save className="size-4" aria-hidden />
              {busy === 'save' ? 'Saving...' : 'Save draft'}
            </AppButton>
          )}
          {canEdit && (
            <AppButton onClick={() => void submit()} disabled={disabled}>
              <Send className="size-4" aria-hidden />
              {busy === 'submit' ? 'Submitting...' : 'Submit'}
            </AppButton>
          )}
          {!canEdit && request?.permissions.canSubmit && (
            <AppButton disabled={disabled} onClick={() => setApprovalAction('submit')}>
              Submit
            </AppButton>
          )}
          {request?.permissions.canApprove && (
            <AppButton disabled={disabled} onClick={() => setApprovalAction('approve')}>
              Approve
            </AppButton>
          )}
          {request?.permissions.canReject && (
            <AppButton variant="danger" disabled={disabled} onClick={() => setApprovalAction('reject')}>
              Reject
            </AppButton>
          )}
          {request?.permissions.canReturn && (
            <AppButton variant="secondary" disabled={disabled} onClick={() => setApprovalAction('return')}>
              Return
            </AppButton>
          )}
          {request?.permissions.canCancel && (
            <AppButton variant="secondary" disabled={disabled} onClick={() => setApprovalAction('cancel')}>
              Cancel request
            </AppButton>
          )}
          {requestId && request?.permissions.canDelete && (
            <AppButton variant="danger" onClick={() => setConfirmDelete(true)} disabled={disabled}>
              <Trash2 className="size-4" aria-hidden />
              Delete
            </AppButton>
          )}
        </FormActions>
      )}

      {requestId && !canEdit && <ApprovalSteps steps={request?.workflowSteps ?? []} histories={request?.histories ?? []} />}
      <PdfViewer
        document={preview}
        onClose={() => {
          if (preview?.url.startsWith('blob:')) URL.revokeObjectURL(preview.url)
          setPreview(undefined)
        }}
      />
      <ApprovalActionDialog
        action={approvalAction}
        busy={Boolean(busyApprovalAction)}
        steps={(request?.workflowSteps ?? []).filter((step) => !step.isCurrentStep && Boolean(step.actionDate))}
        onClose={() => {
          if (!busyApprovalAction) setApprovalAction(undefined)
        }}
        onConfirm={(input) => void runApprovalAction(input)}
      />
      <ConfirmDialog
        open={confirmSetupChange}
        title="Change request setup"
        confirmText="Change setup"
        onClose={() => setConfirmSetupChange(false)}
        onConfirm={changeSetup}
      >
        Changing setup clears the Title, Vendor, QRS source and renewal predecessor selected from the current setup. Dates and Remark are retained.
      </ConfirmDialog>
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
    </FormPage>
  )
}

import { useEffect, useState } from 'react'
import { AppButton } from '@/components/ui/AppButton'
import { appInputClassName, appTextareaClassName } from '@/components/ui/inputStyles'
import { Modal } from '@/components/ui/Modal'
import type { PortalApprovalAction, PortalWorkflowStep } from './types'

export type ApprovalActionKind = 'submit' | 'approve' | 'reject' | 'return' | 'cancel'

/**
 * Whether a reason is required is a property of the action, not of the user. Reject and Cancel end
 * the document; Return sends it back to someone who has to be told why. Submit and Approve move it
 * forward and need no justification.
 */
const ACTIONS: Record<ApprovalActionKind, { title: string; confirm: string; danger: boolean; reasonRequired: boolean }> = {
  submit: { title: 'Submit request', confirm: 'Submit', danger: false, reasonRequired: false },
  approve: { title: 'Approve request', confirm: 'Approve', danger: false, reasonRequired: false },
  reject: { title: 'Reject request', confirm: 'Reject', danger: true, reasonRequired: true },
  return: { title: 'Return request', confirm: 'Return', danger: false, reasonRequired: true },
  cancel: { title: 'Cancel request', confirm: 'Cancel request', danger: true, reasonRequired: true },
}

export function ApprovalActionDialog({
  action,
  busy,
  steps = [],
  onClose,
  onConfirm,
}: {
  action?: ApprovalActionKind
  busy: boolean
  /** Steps this document may be returned to. Empty means the service chooses. */
  steps?: PortalWorkflowStep[]
  onClose: () => void
  onConfirm: (input: PortalApprovalAction) => void
}) {
  const [comment, setComment] = useState('')
  const [returnTo, setReturnTo] = useState('')
  useEffect(() => {
    if (action) {
      setComment('')
      setReturnTo('')
    }
  }, [action])

  const config = action ? ACTIONS[action] : undefined
  const trimmed = comment.trim()
  // Only Return targets a step, and only when the server offered steps. Leaving it blank is valid:
  // it means "wherever the service sends it", which is the service's decision to make.
  const showReturnTarget = action === 'return' && steps.length > 0

  return (
    <Modal open={Boolean(action)} title={config?.title ?? ''} onClose={onClose}>
      <div className="grid gap-5">
        {showReturnTarget && (
          <label className="block">
            <span className="mb-1 block text-caption font-medium uppercase tracking-[0.08em] text-ink-muted">Return to step</span>
            <select value={returnTo} onChange={(event) => setReturnTo(event.target.value)} className={appInputClassName('md', 'w-full')}>
              <option value="">Previous step (service decides)</option>
              {steps.map((step) => (
                <option key={step.id} value={step.sequenceNo}>{step.sequenceNo}. {step.stepName}</option>
              ))}
            </select>
          </label>
        )}
        <label className="block">
          <span className="mb-1 block text-caption font-medium uppercase tracking-[0.08em] text-ink-muted">
            Comment {config?.reasonRequired ? '(required)' : '(optional)'}
          </span>
          <textarea autoFocus value={comment} onChange={(event) => setComment(event.target.value)} rows={4} className={appTextareaClassName('w-full')} />
        </label>
        <div className="flex justify-end gap-2">
          <AppButton variant="secondary" onClick={onClose} disabled={busy}>Close</AppButton>
          <AppButton
            variant={config?.danger ? 'danger' : 'primary'}
            onClick={() => onConfirm({ comment: trimmed, returnToStepSequence: returnTo ? Number(returnTo) : undefined })}
            disabled={busy || (config?.reasonRequired === true && !trimmed)}
          >
            {busy ? 'Working...' : config?.confirm}
          </AppButton>
        </div>
      </div>
    </Modal>
  )
}

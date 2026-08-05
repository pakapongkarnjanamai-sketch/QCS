import { useEffect, useState } from 'react'
import { AppButton } from '@/components/ui/AppButton'
import { appTextareaClassName } from '@/components/ui/inputStyles'
import { Modal } from '@/components/ui/Modal'

export function ApprovalActionDialog({ action, busy, onClose, onConfirm }: { action?: 'approve' | 'reject'; busy: boolean; onClose: () => void; onConfirm: (comment: string) => void }) {
  const [comment, setComment] = useState('')
  const label = action === 'approve' ? 'Approve request' : 'Reject request'
  const reasonRequired = action === 'reject'
  const trimmed = comment.trim()
  useEffect(() => { if (action) setComment('') }, [action])
  return <Modal open={Boolean(action)} title={label} onClose={onClose}><div className="grid gap-5"><label className="block"><span className="mb-1 block text-caption font-medium uppercase tracking-[0.08em] text-ink-muted">Comment {reasonRequired ? '(required)' : '(optional)'}</span><textarea autoFocus value={comment} onChange={(event) => setComment(event.target.value)} rows={4} className={appTextareaClassName('w-full')} /></label><div className="flex justify-end gap-2"><AppButton variant="secondary" onClick={onClose} disabled={busy}>Cancel</AppButton><AppButton variant={action === 'reject' ? 'danger' : 'primary'} onClick={() => onConfirm(trimmed)} disabled={busy || (reasonRequired && !trimmed)}>{busy ? 'Working...' : action === 'approve' ? 'Approve' : 'Reject'}</AppButton></div></div></Modal>
}
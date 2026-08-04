import { useState } from 'react'
import { AppButton } from '@/components/ui/AppButton'
import { Modal } from '@/components/ui/Modal'

export function ApprovalActionDialog({ action, busy, onClose, onConfirm }: { action?: 'approve' | 'reject'; busy: boolean; onClose: () => void; onConfirm: (comment: string) => void }) {
  const [comment, setComment] = useState('')
  const label = action === 'approve' ? 'Approve request' : 'Reject request'
  return <Modal open={Boolean(action)} title={label} onClose={onClose}><div className="grid gap-5"><label className="grid gap-1.5 text-body">Comment<textarea value={comment} onChange={(event) => setComment(event.target.value)} className="min-h-24 rounded-sm border border-border-subtle px-3 py-2" /></label><div className="flex justify-end gap-2"><AppButton tone="secondary" onClick={onClose} disabled={busy}>Cancel</AppButton><AppButton tone={action === 'reject' ? 'danger' : 'primary'} onClick={() => onConfirm(comment)} disabled={busy}>{busy ? 'Working...' : action === 'approve' ? 'Approve' : 'Reject'}</AppButton></div></div></Modal>
}
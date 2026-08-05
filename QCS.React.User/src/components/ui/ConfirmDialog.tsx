import type { ReactNode } from 'react'
import { AppButton } from './AppButton'
import { Modal } from './Modal'

interface ConfirmDialogProps { open: boolean; title: string; children: ReactNode; confirmText?: string; onConfirm: () => void; onClose: () => void }

export function ConfirmDialog({ open, title, children, confirmText = 'Confirm', onConfirm, onClose }: ConfirmDialogProps) {
  return <Modal open={open} title={title} onClose={onClose}><div className="grid gap-5"><div className="text-body text-ink-muted">{children}</div><div className="flex justify-end gap-2"><AppButton variant="secondary" onClick={onClose}>Cancel</AppButton><AppButton onClick={onConfirm}>{confirmText}</AppButton></div></div></Modal>
}
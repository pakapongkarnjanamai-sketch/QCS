import { useEffect, type ReactNode } from 'react'
import { X } from 'lucide-react'
import { IconButton } from './IconButton'

interface ModalProps { open: boolean; title: string; onClose: () => void; children: ReactNode }

export function Modal({ open, title, onClose, children }: ModalProps) {
  useEffect(() => {
    if (!open) return undefined
    const onKeyDown = (event: KeyboardEvent) => { if (event.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [open, onClose])

  if (!open) return null
  return <div className="fixed inset-0 z-50 grid place-items-center bg-black/30 p-4" role="presentation" onMouseDown={onClose}>
    <section role="dialog" aria-modal="true" aria-label={title} className="w-full max-w-lg rounded-sm border border-border-subtle bg-white" onMouseDown={(event) => event.stopPropagation()}>
      <header className="flex items-center justify-between border-b border-border-subtle px-5 py-3"><h2 className="text-heading font-semibold">{title}</h2><IconButton label="Close dialog" onClick={onClose}><X size={18} /></IconButton></header>
      <div className="p-5">{children}</div>
    </section>
  </div>
}
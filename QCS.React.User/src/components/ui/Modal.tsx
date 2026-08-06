import { useEffect, useRef, type ReactNode } from 'react'
import { X } from 'lucide-react'
import { IconButton } from './IconButton'

interface ModalProps { open: boolean; title: string; onClose: () => void; children: ReactNode; className?: string }

export function Modal({ open, title, onClose, children, className = 'max-w-lg' }: ModalProps) {
  const dialogRef = useRef<HTMLElement>(null)
  const returnFocusRef = useRef<HTMLElement | null>(null)

  useEffect(() => {
    if (!open) return undefined
    returnFocusRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null
    dialogRef.current?.focus()
    const onKeyDown = (event: KeyboardEvent) => { if (event.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKeyDown)
    return () => { window.removeEventListener('keydown', onKeyDown); returnFocusRef.current?.focus() }
  }, [open, onClose])

  if (!open) return null
  return <div className="fixed inset-0 z-50 grid place-items-center bg-black/30 p-4" role="presentation" onMouseDown={onClose}>
    <section ref={dialogRef} tabIndex={-1} role="dialog" aria-modal="true" aria-label={title} className={`flex w-full flex-col rounded-sm border border-border-subtle bg-white ${className}`} onMouseDown={(event) => event.stopPropagation()}>
      <header className="flex items-center justify-between border-b border-border-subtle px-5 py-3"><h2 className="text-heading font-semibold">{title}</h2><IconButton label="Close dialog" onClick={onClose}><X className="size-5" /></IconButton></header>
      <div className="p-5">{children}</div>
    </section>
  </div>
}
import { useEffect, useRef, type ReactNode } from 'react'
import { X } from 'lucide-react'
import { IconButton } from './IconButton'

interface ModalProps {
  open: boolean
  title: string
  onClose: () => void
  children: ReactNode
  /** Sizing for the dialog itself. Defaults to the confirm-dialog width. */
  className?: string
  /**
   * Sizing for the body. Separate from `className`, and opt-in, because a body that fills the
   * dialog needs `flex-1` — and `flex-1` inside a column flex container of automatic height
   * resolves against a zero flex-basis, collapsing the body to nothing. Every other modal here is
   * content-height, so growth has to be asked for rather than applied to all of them.
   */
  contentClassName?: string
}

export function Modal({ open, title, onClose, children, className = 'max-w-lg', contentClassName = 'p-5' }: ModalProps) {
  const dialogRef = useRef<HTMLElement>(null)
  const returnFocusRef = useRef<HTMLElement | null>(null)

  useEffect(() => {
    if (!open) return undefined
    returnFocusRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null
    dialogRef.current?.focus()
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onClose()
        return
      }
      if (event.key !== 'Tab' || !dialogRef.current) return

      const focusable = Array.from(dialogRef.current.querySelectorAll<HTMLElement>('a[href], button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex]:not([tabindex="-1"])'))
      if (focusable.length === 0) {
        event.preventDefault()
        dialogRef.current.focus()
        return
      }

      const first = focusable[0]
      const last = focusable[focusable.length - 1]
      if (event.shiftKey && (document.activeElement === first || document.activeElement === dialogRef.current)) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first.focus()
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => { window.removeEventListener('keydown', onKeyDown); returnFocusRef.current?.focus() }
  }, [open, onClose])

  if (!open) return null
  return <div className="fixed inset-0 z-50 grid place-items-center bg-black/30 p-4" role="presentation" onMouseDown={onClose}>
    <section ref={dialogRef} tabIndex={-1} role="dialog" aria-modal="true" aria-label={title} className={`flex w-full flex-col rounded-sm border border-border-subtle bg-surface-panel ${className}`} onMouseDown={(event) => event.stopPropagation()}>
      <header className="flex shrink-0 items-center justify-between border-b border-border-subtle px-5 py-3"><h2 className="text-heading font-semibold">{title}</h2><IconButton label="Close dialog" onClick={onClose}><X className="size-5" /></IconButton></header>
      <div className={contentClassName}>{children}</div>
    </section>
  </div>
}
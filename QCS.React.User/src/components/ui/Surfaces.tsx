import type { ReactNode } from 'react'

export function LoadingSurface() { return <div className="grid min-h-48 place-items-center border border-border-subtle bg-white text-body text-ink-muted">Loading...</div> }
export function EmptySurface({ children }: { children: ReactNode }) { return <div className="grid min-h-48 place-items-center border border-dashed border-border-subtle bg-white p-6 text-center text-body text-ink-muted">{children}</div> }
export function ErrorSurface({ children }: { children: ReactNode }) { return <div className="border border-danger/30 bg-red-50 p-4 text-body text-danger">{children}</div> }
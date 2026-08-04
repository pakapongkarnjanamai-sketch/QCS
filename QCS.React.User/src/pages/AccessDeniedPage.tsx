import { Link } from 'react-router'
import { appConfig } from '@/config/appConfig'

export function AccessDeniedPage() {
  return <main className="grid min-h-dvh place-items-center bg-surface-app p-6"><section className="grid max-w-md gap-4 border border-border-subtle bg-white p-6"><h1 className="text-title font-semibold">Access denied</h1><p className="text-body text-ink-muted">Your account does not have access to this portal.</p><Link className="text-body font-medium text-accent underline decoration-1 underline-offset-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent" to={appConfig.appBasePath}>Return to the portal</Link></section></main>
}
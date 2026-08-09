import { AppLinkButton } from '@/components/ui/AppLinkButton'
import { appConfig } from '@/config/appConfig'

export function AccessDeniedPage() {
  return <main className="grid min-h-dvh place-items-center bg-surface-app p-6"><section className="grid max-w-md gap-4 rounded-sm border border-border-subtle bg-surface-panel p-6"><h1 className="text-title font-semibold">Access denied</h1><p className="text-body text-ink-muted">Your account does not have access to this portal.</p><AppLinkButton variant="secondary" to={appConfig.appBasePath}>Return to the portal</AppLinkButton></section></main>
}
const DEV_API_FALLBACK = 'https://localhost:7101/QCS/Service'

function normalizePath(value: string | undefined, fallback: string): string {
  const raw = (value ?? '').trim() || fallback
  const withLeadingSlash = raw.startsWith('/') ? raw : `/${raw}`
  return withLeadingSlash.length > 1 ? withLeadingSlash.replace(/\/+$/, '') : '/'
}

function resolveApiBaseUrl(): string {
  const configured = import.meta.env.VITE_QCS_API_BASE_URL?.trim()
  if (configured) return configured.replace(/\/+$/, '')
  return import.meta.env.DEV ? DEV_API_FALLBACK : '/QCS/Service'
}

const apiBaseUrl = resolveApiBaseUrl()

export const appConfig = {
  appBasePath: normalizePath(import.meta.env.VITE_QCS_USER_APP_BASE_PATH, '/QCS/User'),
  apiBaseUrl,
  hubUrl: import.meta.env.VITE_QCS_HUB_URL?.trim() || `${apiBaseUrl}/notificationHub`,
  legacyPortalBaseUrl: normalizePath(import.meta.env.VITE_QCS_LEGACY_PORTAL_BASE_URL, '/QCS'),
  qrsRequestBaseUrl: (import.meta.env.VITE_QRS_REQUEST_BASE_URL?.trim() || '/QRS/requests').replace(/\/+$/, ''),
} as const

/**
 * Deep link to a QRS request from its business code.
 *
 * QRS routes request detail as /requests/:id by integer id, and QCS only ever holds the code —
 * the cross-system key is deliberately the code, not the id. So this targets the QRS request
 * LIST filtered to that one code, which QRS already supports, rather than a by-code route that
 * would have to be built there. `view=all` is required: the QRS list defaults to the caller's
 * own requests, and a QCS purchaser is usually not the QRS requester.
 */
export function qrsRequestUrl(code: string): string {
  return `${appConfig.qrsRequestBaseUrl}?q=${encodeURIComponent(code)}&view=all`
}
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
 * Deep link straight to the QRS request's detail page, from its business code.
 *
 * QRS's detail route now accepts a code as well as an integer id: it resolves the code through
 * its own list search and replaces the URL with the canonical /requests/{id}. That change lives
 * in QRS, and it is what lets this be a link to the document rather than to a filtered list.
 *
 * The code stays the cross-system key — see DOC/INTEGRATION-QCS.md. QCS is not given a QRS id,
 * because ids are per-database and break on a restore.
 */
export function qrsRequestUrl(code: string): string {
  return `${appConfig.qrsRequestBaseUrl}/${encodeURIComponent(code)}`
}
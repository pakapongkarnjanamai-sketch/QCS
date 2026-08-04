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
  qrsRequestBaseUrl: (import.meta.env.VITE_QRS_REQUEST_BASE_URL?.trim() || '/QRS/QuotationRequests').replace(/\/+$/, ''),
} as const
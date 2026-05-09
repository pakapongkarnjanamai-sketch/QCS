const normalizeBasePath = (value?: string) => {
  const trimmedValue = value?.trim()

  if (!trimmedValue || trimmedValue === '/') {
    return '/'
  }

  return `/${trimmedValue.replace(/^\/+|\/+$/g, '')}`
}

const normalizeUrl = (value?: string, fallback = '') => {
  const trimmed = value?.trim()
  if (!trimmed) return fallback
  // Strip trailing slash for consistency; callers append their own path segments
  return trimmed.replace(/\/+$/, '')
}

function isLocalhost() {
  if (typeof window === 'undefined') return false
  const host = window.location.hostname.toLowerCase()
  return host === 'localhost' || host === '127.0.0.1'
}

// Vite proxy cannot relay Windows Auth (NTLM) — Node.js does not participate in
// the NTLM handshake, so every proxied request arrives unauthenticated (401).
// Fix: use an absolute API URL in local dev so the browser handles NTLM directly.
// In production (IIS) the SPA and API are on the same origin, so relative paths work.
function resolveApiBaseUrl() {
  const configured = import.meta.env.VITE_QCS_API_BASE_URL?.trim()

  if (configured && !configured.startsWith('/')) {
    // Absolute URL configured (e.g. https://localhost:7127) — use as-is
    return normalizeUrl(configured)
  }

  if (isLocalhost()) {
    // On localhost with no absolute URL configured, fall back to the dev API port.
    // Override by setting VITE_QCS_API_BASE_URL=http://localhost:5157 in .env.local
    return 'http://localhost:5157'
  }

  // Production: use the configured relative path (e.g. /api) or empty string
  return normalizeUrl(configured)
}

function resolveHubUrl(apiBaseUrl: string) {
  const configured = import.meta.env.VITE_QCS_HUB_URL?.trim()

  if (configured && !configured.startsWith('/')) {
    return normalizeUrl(configured)
  }

  if (isLocalhost()) {
    return `${apiBaseUrl}/hubs/qcs`
  }

  return normalizeUrl(configured) || `${apiBaseUrl}/hubs/qcs`
}

const apiBaseUrl = resolveApiBaseUrl()

const normalizePortalUrl = (value?: string) => {
  const trimmed = value?.trim()
  if (!trimmed) return ''
  return trimmed.replace(/\/+$/, '')
}

function resolvePortalBaseUrl() {
  const configured = import.meta.env.VITE_QCS_PORTAL_BASE_URL?.trim()

  if (configured) {
    return normalizePortalUrl(configured)
  }

  // Legacy MVC base URL fallback for quotation detail links.
  return 'https://ap-ntc2137-prwb/QCS'
}

export const appConfig = {
  appBasePath: normalizeBasePath(import.meta.env.VITE_QCS_ADMIN_APP_BASE_PATH),
  apiBaseUrl,
  hubUrl: resolveHubUrl(apiBaseUrl),
  portalBaseUrl: resolvePortalBaseUrl(),
} as const
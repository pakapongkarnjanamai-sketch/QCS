const normalizeBasePath = (value?: string) => {
  const trimmedValue = value?.trim()

  if (!trimmedValue || trimmedValue === '/') {
    return '/'
  }

  return `/${trimmedValue.replace(/^\/+|\/+$/g, '')}`
}

const trimSlashes = (value: string) => value.replace(/^\/+|\/+$/g, '')

const joinRelativePath = (basePath: string, segment: string) => {
  const normalizedSegment = trimSlashes(segment)

  if (!normalizedSegment) {
    return basePath
  }

  if (!basePath || basePath === '/') {
    return `/${normalizedSegment}`
  }

  return `${basePath}/${normalizedSegment}`
}

const getParentBasePath = (basePath: string) => {
  if (!basePath || basePath === '/') {
    return '/'
  }

  const segments = trimSlashes(basePath).split('/').filter(Boolean)
  if (segments.length <= 1) {
    return '/'
  }

  return `/${segments.slice(0, -1).join('/')}`
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
function resolveApiBaseUrl(siteBasePath: string) {
  const configured = import.meta.env.VITE_QCS_API_BASE_URL?.trim()

  if (configured && !configured.startsWith('/')) {
    // Absolute URL configured (e.g. https://localhost:7127) — use as-is
    return normalizeUrl(configured)
  }

  if (isLocalhost()) {
    // On localhost with no absolute URL configured, fall back to the dev API port.
    // Use the same hostname as the browser to avoid cross-origin CORS issues
    // (e.g. 127.0.0.1 vs localhost are treated as different origins).
    // Override by setting VITE_QCS_API_BASE_URL=http://localhost:5157 in .env.local
    return `http://${window.location.hostname}:5157`
  }

  // Production default for the IIS layout: <site>/QCS/Admin -> API at <site>/QCS/Service
  return normalizeUrl(configured) || joinRelativePath(siteBasePath, 'Service')
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

const appBasePath = normalizeBasePath(import.meta.env.VITE_QCS_ADMIN_APP_BASE_PATH)
const siteBasePath = getParentBasePath(appBasePath)
const apiBaseUrl = resolveApiBaseUrl(siteBasePath)

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

  if (isLocalhost()) {
    return ''
  }

  return siteBasePath === '/' ? '' : siteBasePath
}

export const appConfig = {
  appBasePath,
  apiBaseUrl,
  hubUrl: resolveHubUrl(apiBaseUrl),
  portalBaseUrl: resolvePortalBaseUrl(),
} as const
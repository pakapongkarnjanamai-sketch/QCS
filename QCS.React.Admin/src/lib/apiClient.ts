import { appConfig } from '../config/appConfig.ts'
import { toast } from './toast.ts'

const ACCESS_DENIED_PATH = '/access-denied'

export function redirectToAccessDenied() {
  if (typeof window === 'undefined') {
    return
  }

  const basePath = appConfig.appBasePath === '/' ? '' : appConfig.appBasePath
  window.location.assign(`${window.location.origin}${basePath}${ACCESS_DENIED_PATH}`)
}

export async function fetchWithAccessControl(input: RequestInfo | URL, init?: RequestInit) {
  const response = await fetch(input, init)

  if (response.status === 403) {
    toast.warning('You do not have permission to access this resource.')
    redirectToAccessDenied()
    throw new Error('Access denied.')
  }

  return response
}

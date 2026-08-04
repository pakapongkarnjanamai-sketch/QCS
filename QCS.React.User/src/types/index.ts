// Mirrors QCS.Api.Controllers.SessionResponse.
export interface SessionResponse {
  displayName: string
  windowsIdentity: string
  isAuthenticated: boolean
  nId: string
  accessLevel: string[]
}

export interface ProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}
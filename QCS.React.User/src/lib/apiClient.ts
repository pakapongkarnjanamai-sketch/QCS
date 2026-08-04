import axios, { type AxiosError } from 'axios'
import { appConfig } from '@/config/appConfig'
import { toast } from './toast'
import type { ProblemDetails } from '@/types'

export const apiClient = axios.create({
  baseURL: `${appConfig.apiBaseUrl}/api`,
  withCredentials: true,
  headers: { 'Content-Type': 'application/json' },
})

export interface ApiError {
  status: number
  title: string
  detail?: string
  fieldErrors?: Record<string, string[]>
}

export function toApiError(error: unknown): ApiError {
  const axiosError = error as AxiosError<ProblemDetails>
  if (axiosError?.isAxiosError && axiosError.response) {
    const { status, data } = axiosError.response
    return { status, title: data?.title ?? axiosError.message, detail: data?.detail, fieldErrors: data?.errors }
  }
  return { status: 0, title: 'The server could not be reached.', detail: axiosError?.message }
}

apiClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    if (error.response?.status === 401) {
      console.error(`QCS: 401 from the API. Check Windows Authentication and VITE_QCS_API_BASE_URL (${appConfig.apiBaseUrl}).`)
    }
    if (error.response?.status === 403 && window.location.pathname !== `${appConfig.appBasePath}/access-denied`) {
      toast.warning('You do not have permission to view this page.')
      window.location.assign(`${appConfig.appBasePath}/access-denied`)
    }
    return Promise.reject(error)
  },
)
import { apiClient } from '@/lib/apiClient'
import { appConfig } from '@/config/appConfig'
import type { PortalRequestDetail } from './types'

function resolveDocumentUrls(request: PortalRequestDetail): PortalRequestDetail {
  return {
    ...request,
    documents: request.documents.map((document) => ({
      ...document,
      viewUrl: document.viewUrl.startsWith('/api/') ? `${appConfig.apiBaseUrl}${document.viewUrl}` : document.viewUrl,
    })),
  }
}

export async function getPortalRequestById(id: number, signal?: AbortSignal): Promise<PortalRequestDetail> {
  const { data } = await apiClient.get<PortalRequestDetail>(`/Portal/Requests/${id}`, { signal })
  return resolveDocumentUrls(data)
}

export async function getPortalRequestByCode(code: string, signal?: AbortSignal): Promise<PortalRequestDetail> {
  const { data } = await apiClient.get<PortalRequestDetail>(`/Portal/Requests/by-code/${encodeURIComponent(code)}`, { signal })
  return resolveDocumentUrls(data)
}
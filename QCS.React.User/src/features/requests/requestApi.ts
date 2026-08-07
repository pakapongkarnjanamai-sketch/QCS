import { apiClient } from '@/lib/apiClient'
import { appConfig } from '@/config/appConfig'
import type { AddExpiredQuotationReference, PortalApprovalAction, PortalAttachment, PortalDocument, PortalPage, PortalRequestDetail, PortalSaveResult, QrsSourcingPage, QrsSourcingRequest, RenewalCandidate, RoutePreview, SavePortalRequest, SavePortalRequestPayload, UpdatePortalDocuments } from './types'

function toSavePortalRequestPayload(input: SavePortalRequest): SavePortalRequestPayload {
  return {
    ...input,
    validFrom: input.validFrom || null,
    validUntil: input.validUntil || null,
  }
}

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

export async function createPortalDraft(input: SavePortalRequest): Promise<PortalSaveResult> {
  const { data } = await apiClient.post<PortalSaveResult>('/Portal/Requests', toSavePortalRequestPayload(input))
  return data
}

export async function updatePortalDraft(id: number, input: SavePortalRequest): Promise<PortalSaveResult> {
  const { data } = await apiClient.put<PortalSaveResult>(`/Portal/Requests/${id}`, toSavePortalRequestPayload(input))
  return data
}

export async function submitPortalRequest(id: number): Promise<void> { await apiClient.post(`/Portal/Requests/${id}/submit`) }

export async function deletePortalDraft(id: number): Promise<void> { await apiClient.delete(`/Portal/Requests/${id}`) }

export async function uploadPortalAttachment(id: number, file: File, documentTypeId: number): Promise<PortalAttachment> {
  const formData = new FormData()
  formData.append('file', file)
  formData.append('documentTypeId', String(documentTypeId))
  const { data } = await apiClient.post<PortalAttachment>(`/Portal/Requests/${id}/attachments`, formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
  return data
}

export async function deletePortalAttachment(id: number, attachmentId: number): Promise<void> {
  await apiClient.delete(`/Portal/Requests/${id}/attachments/${attachmentId}`)
}

export async function addExpiredQuotationReference(id: number, code: string): Promise<void> {
  const input: AddExpiredQuotationReference = { code }
  await apiClient.post(`/Portal/Requests/${id}/expired-quotation-references`, input)
}

export async function updatePortalDocuments(id: number, documents: PortalDocument[]): Promise<void> {
  const input: UpdatePortalDocuments = {
    documents: documents.map((document) => ({ id: document.id, documentTypeId: document.documentTypeId })),
  }
  await apiClient.put(`/Portal/Requests/${id}/attachments`, input)
}

export async function previewPortalRequest(id: number): Promise<Blob> {
  const { data } = await apiClient.post(`/Portal/Requests/${id}/preview`, undefined, { responseType: 'blob' })
  return data
}

export async function approvePortalRequest(id: number, input: PortalApprovalAction): Promise<void> { await apiClient.post(`/Portal/Requests/${id}/approve`, input) }

export async function rejectPortalRequest(id: number, input: PortalApprovalAction): Promise<void> { await apiClient.post(`/Portal/Requests/${id}/reject`, input) }

export async function returnPortalRequest(id: number, input: PortalApprovalAction): Promise<void> { await apiClient.post(`/Portal/Requests/${id}/return`, input) }

export async function cancelPortalRequest(id: number, input: PortalApprovalAction): Promise<void> { await apiClient.post(`/Portal/Requests/${id}/cancel`, input) }

export async function getRenewalCandidates(
  params?: { search?: string; page?: number; pageSize?: number },
  signal?: AbortSignal,
): Promise<PortalPage<RenewalCandidate>> {
  const query = new URLSearchParams()
  if (params?.search) query.set('search', params.search)
  if (params?.page) query.set('page', String(params.page))
  if (params?.pageSize) query.set('pageSize', String(params.pageSize))
  const qs = query.toString()
  const { data } = await apiClient.get<PortalPage<RenewalCandidate>>(`/Portal/Requests/renewal-candidates${qs ? `?${qs}` : ''}`, { signal })
  return data
}

export async function getQrsSourcingRequests(
  params: { search?: string; page: number; pageSize: number },
  signal?: AbortSignal,
): Promise<QrsSourcingPage<QrsSourcingRequest>> {
  const { data } = await apiClient.get<QrsSourcingPage<QrsSourcingRequest>>('/QrsSourcing/Requests', {
    params,
    signal,
  })
  return data
}

/**
 * Asks the server which route this request would take if submitted now. It writes nothing, and it
 * is the only way the form may show a route — the graph lives in the central workflow, not here.
 */
export async function getRoutePreview(input: SavePortalRequest, signal?: AbortSignal): Promise<RoutePreview> {
  const { data } = await apiClient.post<RoutePreview>('/Portal/Requests/route-preview', toSavePortalRequestPayload(input), { signal })
  return data
}
import { apiClient } from '@/lib/apiClient'
import { portalViewByWorkspaceView, type PortalPage, type PortalRequestListItem, type PortalRequestQuery, type WorkspaceSummaryData } from './types'

export async function getWorkspaceSummary(signal?: AbortSignal): Promise<WorkspaceSummaryData> {
  const { data } = await apiClient.get<WorkspaceSummaryData>('/Dashboard/Summary', { signal })
  return data
}

export async function getPortalRequests(query: PortalRequestQuery, signal?: AbortSignal): Promise<PortalPage<PortalRequestListItem>> {
  const { data } = await apiClient.get<PortalPage<PortalRequestListItem>>('/Portal/Requests', {
    signal,
    params: {
      view: portalViewByWorkspaceView[query.view],
      search: query.search || undefined,
      page: query.page,
      pageSize: query.pageSize,
      sortBy: query.sortBy || undefined,
      sortDescending: query.sortDescending,
    },
  })
  return data
}
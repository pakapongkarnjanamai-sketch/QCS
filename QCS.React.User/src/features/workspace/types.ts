// Mirrors QCS.Domain.DTOs.Portal.PortalPage<T>.
export interface PortalPage<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  hasNextPage: boolean
}

// Mirrors QCS.Domain.DTOs.Portal.PortalRequestListItemDto.
export interface PortalRequestListItem {
  id: number
  code: string
  title: string
  vendorCode: string
  vendorName: string
  requestDate: string
  currentStepId: number
  status: number
  statusName: string
  requesterName: string
  requesterNId: string
  remark: string
  validFrom?: string
  validUntil?: string
}

// Mirrors QCS.Domain.DTOs.DashboardDto.
export interface WorkspaceSummaryData {
  myTaskCount: number
  myApprovedCount: number
  myRejectedCount: number
  myRequestCount: number
}

export const workspaceViews = ['my-tasks', 'my-requests', 'my-approved', 'rejected', 'all-approved'] as const
export type WorkspaceView = typeof workspaceViews[number]

export type PortalRequestView = 'MyTasks' | 'MyRequests' | 'MyApproved' | 'Rejected' | 'AllApproved'

export const portalViewByWorkspaceView: Record<WorkspaceView, PortalRequestView> = {
  'my-tasks': 'MyTasks',
  'my-requests': 'MyRequests',
  'my-approved': 'MyApproved',
  rejected: 'Rejected',
  'all-approved': 'AllApproved',
}

export const workspaceViewLabels: Record<WorkspaceView, string> = {
  'my-tasks': 'My tasks',
  'my-requests': 'My requests',
  'my-approved': 'My approved',
  rejected: 'Rejected',
  'all-approved': 'All approved',
}

export interface PortalRequestQuery {
  view: WorkspaceView
  search: string
  page: number
  pageSize: number
  sortBy?: string
  sortDescending: boolean
}
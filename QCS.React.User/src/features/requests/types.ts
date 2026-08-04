// Mirrors QCS.Domain.DTOs.Portal.PortalRequestDetailDto.
export interface PortalRequestDetail {
  id: number
  code: string
  title: string
  requestDate: string
  status: number
  statusName: string
  requesterNId: string
  requesterName: string
  vendorCode: string
  vendorName: string
  sourceSystem?: string
  sourceCode?: string
  validFrom?: string
  validUntil?: string
  remark?: string
  currentStepId: number
  currentStepName?: string
  permissions: PortalRequestPermissions
  workflowSteps: PortalWorkflowStep[]
  documents: PortalDocument[]
  histories: PortalHistory[]
}

// Mirrors QCS.Domain.DTOs.Portal.PermissionDto.
export interface PortalRequestPermissions { [key: string]: boolean }

// Mirrors QCS.Domain.DTOs.Portal.PortalDocumentDto.
export interface PortalDocument { id: number; fileName: string; documentTypeId: number; documentTypeName: string; viewUrl: string }

// Mirrors QCS.Domain.DTOs.Portal.PortalWorkflowStepDto.
export interface PortalWorkflowStep { id: number; sequenceNo: number; stepName: string; status?: number; statusName?: string; actionDate?: string; approverNId?: string; approverName?: string; comment?: string; assignments: PortalAssignment[] }

// Mirrors QCS.Domain.DTOs.Portal.PortalAssignmentDto.
export interface PortalAssignment { nId: string; employeeName: string; assignmentType: string; isCurrentUser: boolean }

// Mirrors QCS.Domain.DTOs.Portal.PortalHistoryDto.
export interface PortalHistory { sequenceNo: number; stepName: string; status: number; statusName: string; approverNId?: string; approverName?: string; actionDate?: string; comment?: string }
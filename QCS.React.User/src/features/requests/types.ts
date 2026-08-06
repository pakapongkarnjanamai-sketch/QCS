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

// Mirrors QCS.Domain.DTOs.Portal.SavePortalRequestDto.
export interface SavePortalRequest {
  title: string
  vendorCode: string
  vendorName: string
  sourceSystem: string
  sourceCode: string
  validFrom: string
  validUntil: string
  remark: string
}

// Mirrors QCS.Domain.DTOs.Portal.PortalSaveResultDto.
export interface PortalSaveResult { id: number; code: string }

// Mirrors QCS.Domain.DTOs.Portal.PortalAttachmentDto.
export interface PortalAttachment { id: number; fileName: string; documentTypeId: number; documentTypeName: string; viewUrl: string }

// Mirrors QCS.Domain.DTOs.Portal.PortalApprovalActionDto.
export interface PortalApprovalAction { comment: string }

// Mirrors QCS.Domain.DTOs.PermissionDto.
export interface PortalRequestPermissions {
  canApprove: boolean
  canReject: boolean
  canEdit: boolean
  canDelete: boolean
}

// Mirrors QCS.Domain.DTOs.Portal.PortalDocumentDto.
// fileSize was missing here while the DTO has always sent it. It is always present in the
// response — WhenWritingNull omits nulls, not zeros — but it is 0 on the generated FinalPdf row,
// which is built without one. Callers must treat 0 as "unknown", not as an empty file.
export interface PortalDocument { id: number; fileName: string; documentTypeId: number; documentTypeName: string; fileSize: number; viewUrl: string }

// Mirrors QCS.Domain.DTOs.Portal.PortalWorkflowStepDto.
export interface PortalWorkflowStep { id: number; sequenceNo: number; stepName: string; status?: number; statusName?: string; actionDate?: string; approverNId?: string; approverName?: string; comment?: string; assignments: PortalAssignment[] }

// Mirrors QCS.Domain.DTOs.Portal.PortalAssignmentDto.
export interface PortalAssignment { nId: string; employeeName: string; assignmentType: string; isCurrentUser: boolean }

// Mirrors QCS.Domain.DTOs.Portal.PortalHistoryDto.
export interface PortalHistory { sequenceNo: number; stepName: string; status: number; statusName: string; approverNId?: string; approverName?: string; actionDate?: string; comment?: string }
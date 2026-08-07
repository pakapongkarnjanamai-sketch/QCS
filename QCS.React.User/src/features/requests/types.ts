export type RequestIntent = 0 | 1
export type SetupIntent = 'New' | 'Renewal'
export type SetupOrigin = 'QCS' | 'QRS'
export type SetupFlow = 'new-qcs' | 'new-qrs' | 'renewal-qcs' | 'renewal-qrs'

// Mirrors QCS.Domain.DTOs.Portal.PortalPage.
export interface PortalPage<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  hasNextPage: boolean
}

// Mirrors QCS.Domain.DTOs.Portal.RenewalCandidateDto.
export interface RenewalCandidate {
  id: number
  code: string
  title: string
  vendorCode: string
  vendorName: string
  validFrom?: string
  validUntil?: string
  sourceSystem?: string
  sourceCode?: string
  requestDate: string
  originalQuotationCount: number
  renewalWindowStatus: 'Expired' | 'ExpiringSoon'
}

// Mirrors QRS.Application.Quotations.Dtos.SourcingRequestDto.
export interface QrsSourcingRequest {
  code: string
  title: string
  requestType: number
  requestTypeName: string
  intent: RequestIntent
  intentName: string
  requesterNId: string
  requesterName: string
  requesterDepartment?: string
  currency: string
  estimatedTotal: number
  isUrgent: boolean
  requiredBy?: string
  submittedAt?: string
  completedAt?: string
  itemCount: number
  attachmentCount: number
}

// Mirrors QRS.Application.Common.Models.PaginatedList<T>.
export interface QrsSourcingPage<T> {
  items: T[]
  pageNumber: number
  pageSize: number
  totalPages: number
  totalCount: number
  hasPreviousPage: boolean
  hasNextPage: boolean
}

export type DiscriminatedSetupState =
  | { intent: 'New'; origin: 'QCS' }
  | { intent: 'New'; origin: 'QRS'; qrsSourceCode: string; qrsTitle?: string }
  | { intent: 'Renewal'; origin: 'QCS'; renewedFromRequestId: number; renewedFromCode: string; vendorCode: string; vendorName: string; title: string }
  | { intent: 'Renewal'; origin: 'QRS'; renewedFromRequestId: number; renewedFromCode: string; vendorCode: string; vendorName: string; qrsSourceCode: string; qrsTitle?: string }

// Mirrors QCS.Domain.DTOs.Portal.PortalSetupResolutionDto.
export interface PortalSetupResolution {
  flow: 'NewQrs' | 'RenewalQcs' | 'RenewalQrs'
  intent: RequestIntent
  origin: SetupOrigin
  /** Null for RenewalQcs — that flow renews a QC directly and has no QRS source. */
  sourceCode?: string
  sourceTitle?: string
  renewedFromRequestId?: number
  renewedFromCode?: string
  vendorCode?: string
  vendorName?: string
}

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
  intent: RequestIntent
  intentName: string
  renewedFromRequestId?: number
  renewedFromCode?: string
  canRenew: boolean
  originName: string
  validFrom?: string
  validUntil?: string
  remark?: string
  approvalDocumentId?: string
  approvalDocumentNumber?: string
  // currentStepSequence is the central sequence and is null before submit. currentStepId is the
  // legacy field, kept on the DTO for existing callers — prefer currentStepSequence for anything
  // new, and never compare either against a sentinel like 99.
  currentStepSequence?: number
  currentStepId: number
  currentStepName?: string
  permissions: PortalRequestPermissions
  workflowSteps: PortalWorkflowStep[]
  documents: PortalDocument[]
  histories: PortalHistory[]
}

// Browser form state. Date inputs require strings; requestApi maps blanks to null on the wire.
export interface SavePortalRequest {
  intent: RequestIntent
  renewedFromRequestId?: number
  title: string
  vendorCode: string
  vendorName: string
  sourceSystem: string
  sourceCode: string
  validFrom: string
  validUntil: string
  remark: string
}

// Mirrors QCS.Domain.DTOs.Portal.SavePortalRequestDto.
export interface SavePortalRequestPayload extends Omit<SavePortalRequest, 'validFrom' | 'validUntil'> {
  validFrom: string | null
  validUntil: string | null
}

// Mirrors QCS.Domain.DTOs.Portal.PortalSaveResultDto.
export interface PortalSaveResult { id: number; code: string }

// Mirrors QCS.Domain.DTOs.Portal.PortalAttachmentDto.
export interface PortalAttachment { id: number; fileName: string; originalFileName: string; documentTypeId: number; documentTypeName: string; sortOrder: number; fileSize: number; uploadDate: string; viewUrl: string }

// Mirrors QCS.Domain.DTOs.Portal.UpdatePortalDocumentsDto.
export interface UpdatePortalDocuments { documents: PortalDocumentUpdate[] }

// Mirrors QCS.Domain.DTOs.Portal.PortalDocumentUpdateDto.
export interface PortalDocumentUpdate { id: number; documentTypeId: number }

// Mirrors QCS.Domain.DTOs.Portal.PortalApprovalActionDto.
export interface PortalApprovalAction { comment: string; returnToStepSequence?: number }

/**
 * Mirrors QCS.Domain.DTOs.PermissionDto.
 *
 * canSubmit/canApprove/canReject/canReturn/canCancel come from the central Approval Service and
 * are the ONLY thing that may decide whether an action is offered. Do not infer a right from the
 * status, the step number or the signed-in NID — the service owns that decision and this mirror
 * is what it reported. canEdit/canDelete stay local and apply to a local Draft only.
 */
export interface PortalRequestPermissions {
  canSubmit: boolean
  canApprove: boolean
  canReject: boolean
  canReturn: boolean
  canCancel: boolean
  canEdit: boolean
  canDelete: boolean
  isCreator: boolean
  isCurrentAssignee: boolean
  availableActions: string[]
}

// Mirrors QCS.Domain.DTOs.Portal.PortalDocumentDto.
// fileSize was missing here while the DTO has always sent it. It is always present in the
// response — WhenWritingNull omits nulls, not zeros — but it is 0 on the generated FinalPdf row,
// which is built without one. Callers must treat 0 as "unknown", not as an empty file.
export interface PortalDocument { id: number; fileName: string; documentTypeId: number; documentTypeName: string; sortOrder: number; referenceCode?: string; fileSize: number; viewUrl: string }

// Mirrors QCS.Domain.DTOs.Portal.AddExpiredQuotationReferenceDto.
export interface AddExpiredQuotationReference { code: string }

// Mirrors QCS.Domain.DTOs.Portal.PortalWorkflowStepDto.
export interface PortalWorkflowStep { id: number; sequenceNo: number; stepName: string; status?: number; statusName?: string; actionDate?: string; isCurrentStep: boolean; approverNId?: string; approverName?: string; comment?: string; assignments: PortalAssignment[] }

// Mirrors QCS.Application.Abstractions.ApprovalAssigneeView.
export interface RoutePreviewAssignee { username: string; employeeName?: string; displayStatus?: string; actedAt?: string; comment?: string }

// Mirrors QCS.Application.Abstractions.ApprovalStepView.
export interface RoutePreviewStep { sequenceNo: number; stepName: string; status?: string; isFinalStep: boolean; assignees: RoutePreviewAssignee[] }

// Mirrors QCS.Application.Abstractions.ApprovalPreviewResult.
// The step list is whatever the published workflow currently resolves to — one step or forty.
// Nothing here may assume a count.
export interface RoutePreview { steps: RoutePreviewStep[]; workflowName?: string; workflowVersion?: string }

// Mirrors QCS.Domain.DTOs.Portal.PortalAssignmentDto.
export interface PortalAssignment { nId: string; employeeName: string; assignmentType: string; isCurrentUser: boolean }

// Mirrors QCS.Domain.DTOs.Portal.PortalHistoryDto.
export interface PortalHistory { sequenceNo: number; stepName: string; status: number; statusName: string; approverNId?: string; approverName?: string; actionDate?: string; comment?: string }
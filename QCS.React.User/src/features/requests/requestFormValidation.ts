import type { SavePortalRequest } from './types'

export type RequestValidationMode = 'draft' | 'submit'
export type RequestFormErrors = Record<string, string>

export function createEmptyRequest(): SavePortalRequest {
  return {
    intent: 0,
    renewedFromRequestId: undefined,
    title: '',
    vendorCode: '',
    vendorName: '',
    sourceSystem: '',
    sourceCode: '',
    validFrom: '',
    validUntil: '',
    remark: '',
  }
}

export function validateSetup(input: SavePortalRequest): RequestFormErrors {
  const errors: RequestFormErrors = {}
  if (input.intent === 0) {
    if (input.sourceSystem === 'QRS' && !input.sourceCode.trim()) {
      errors.sourceCode = 'Select or enter a QRS source code.'
    }
  } else if (input.intent === 1) {
    if (!input.renewedFromRequestId) {
      errors.renewedFromRequestId = 'Select an expired QCS request to renew.'
    }
    if (input.sourceSystem === 'QRS' && !input.sourceCode.trim()) {
      errors.sourceCode = 'Select or enter a QRS source code.'
    }
  }
  return errors
}

export function validateRequest(input: SavePortalRequest, mode: RequestValidationMode, hasOriginalQuotation: boolean): RequestFormErrors {
  const errors: RequestFormErrors = {}
  if (input.validFrom && input.validUntil && input.validUntil < input.validFrom) errors.validUntil = 'Valid until must be on or after valid from.'
  if (mode === 'draft') return errors

  if (!input.title.trim()) errors.title = 'Enter a title before submitting this request.'
  if (!input.vendorCode.trim() && !input.vendorName.trim()) errors.vendor = 'Select or enter a vendor before submitting this request.'
  if (!input.validFrom) errors.validFrom = 'Enter a valid-from date before submitting this request.'
  if (!input.validUntil) errors.validUntil = 'Enter a valid-until date before submitting this request.'
  if (!hasOriginalQuotation) errors.attachments = 'Attach an Original Quotation before submitting this request.'
  return errors
}

export function mapServerFieldErrors(fieldErrors?: Record<string, string[]>): RequestFormErrors {
  const fields: RequestFormErrors = {}
  const keyMap: Record<string, string> = {
    Title: 'title', VendorCode: 'vendor', VendorName: 'vendor', ValidFrom: 'validFrom', ValidUntil: 'validUntil', Remark: 'remark', Intent: 'intent', RenewedFromRequestId: 'renewedFromRequestId', SourceCode: 'sourceCode',
  }
  for (const [serverKey, messages] of Object.entries(fieldErrors ?? {})) {
    const key = keyMap[serverKey] ?? serverKey
    fields[key] = messages.join(' ')
  }
  return fields
}
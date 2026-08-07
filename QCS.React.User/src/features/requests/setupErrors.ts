import type { ApiError } from '@/lib/apiClient'

/**
 * Turns a setup-resolver failure into something a user can act on.
 *
 * The 409 case is the one that matters. The server's detail already names the
 * quotation that was consumed — that is why it carries both codes — so it leads,
 * and the recovery follows it. Retrying a consumed predecessor can never succeed,
 * so callers must not offer "Try again" on this status.
 */
export function setupErrorMessage(error: ApiError): string {
  if (error.status === 409) {
    const named = error.detail?.trim()
    const recovery = 'Raise a new QRS request selecting a different quotation, or raise it as New.'
    return named ? `${named} ${recovery}` : `This previous quotation has already been renewed. ${recovery}`
  }

  return error.detail ?? error.title
}

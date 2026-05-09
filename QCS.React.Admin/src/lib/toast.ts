import notify from 'devextreme/ui/notify'

export type ToastType = 'info' | 'success' | 'warning' | 'error'

export interface ShowToastOptions {
  type?: ToastType
  displayTime?: number
}

const DEFAULT_DISPLAY_TIME = 3000

export function showToast(message: string, options: ShowToastOptions = {}) {
  if (!message.trim()) {
    return
  }

  notify(message, options.type ?? 'info', options.displayTime ?? DEFAULT_DISPLAY_TIME)
}

export const toast = {
  info: (message: string, displayTime?: number) =>
    showToast(message, { type: 'info', displayTime }),
  success: (message: string, displayTime?: number) =>
    showToast(message, { type: 'success', displayTime }),
  warning: (message: string, displayTime?: number) =>
    showToast(message, { type: 'warning', displayTime }),
  error: (message: string, displayTime?: number) =>
    showToast(message, { type: 'error', displayTime }),
}
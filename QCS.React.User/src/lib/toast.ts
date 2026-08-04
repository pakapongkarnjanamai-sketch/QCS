export type ToastKind = 'error' | 'warning' | 'success'

type ToastListener = (message: string, kind: ToastKind) => void
let listener: ToastListener | undefined

export const toast = {
  subscribe(nextListener: ToastListener) {
    listener = nextListener
    return () => { listener = undefined }
  },
  error(message: string) { listener?.(message, 'error') },
  warning(message: string) { listener?.(message, 'warning') },
  success(message: string) { listener?.(message, 'success') },
}
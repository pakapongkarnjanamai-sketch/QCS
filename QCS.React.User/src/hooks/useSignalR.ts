import { useEffect } from 'react'
import { startSignalR } from '@/lib/signalrClient'

export function useSignalR(eventName: string, handler: () => void): void {
  useEffect(() => {
    let active = true
    void startSignalR().then((connection) => {
      if (active) connection.on(eventName, handler)
    }).catch((error: unknown) => console.warn('QCS SignalR connection failed.', error))

    return () => {
      active = false
      void startSignalR().then((connection) => connection.off(eventName, handler)).catch(() => undefined)
    }
  }, [eventName, handler])
}
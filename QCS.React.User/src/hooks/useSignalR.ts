import { useEffect, useEffectEvent } from 'react'
import { getSignalRConnection, startSignalR } from '@/lib/signalrClient'

export function useSignalR(eventName: string, handler: () => void): void {
  const onEvent = useEffectEvent(handler)

  useEffect(() => {
    let active = true
    void startSignalR().then((connection) => {
      if (active) connection.on(eventName, onEvent)
    }).catch((error: unknown) => console.warn('QCS SignalR connection failed.', error))

    return () => {
      active = false
      getSignalRConnection().off(eventName, onEvent)
    }
  }, [eventName])
}
import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr'
import { appConfig } from '@/config/appConfig'

let connection: HubConnection | undefined

export function getSignalRConnection(): HubConnection {
  connection ??= new HubConnectionBuilder()
    .withUrl(appConfig.hubUrl, { withCredentials: true })
    .withAutomaticReconnect([0, 2_000, 5_000, 10_000])
    .configureLogging(LogLevel.Warning)
    .build()
  return connection
}

export async function startSignalR(): Promise<HubConnection> {
  const client = getSignalRConnection()
  if (client.state === HubConnectionState.Disconnected) await client.start()
  return client
}
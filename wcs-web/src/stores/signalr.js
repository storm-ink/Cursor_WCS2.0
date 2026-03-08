import { ref } from 'vue'
import * as signalR from '@microsoft/signalr'

const connection = ref(null)
const connected = ref(false)
const currentGroups = ref([])

function createConnection() {
  if (connection.value) return connection.value

  const conn = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/wcs')
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(signalR.LogLevel.Warning)
    .build()

  conn.onreconnecting(() => {
    connected.value = false
    console.log('[SignalR] Reconnecting...')
  })

  conn.onreconnected(async () => {
    connected.value = true
    console.log('[SignalR] Reconnected, rejoining groups...')
    for (const group of currentGroups.value) {
      await conn.invoke('JoinGroup', group)
    }
  })

  conn.onclose(() => {
    connected.value = false
    console.log('[SignalR] Connection closed')
  })

  connection.value = conn
  return conn
}

async function start() {
  const conn = createConnection()
  if (conn.state === signalR.HubConnectionState.Disconnected) {
    try {
      await conn.start()
      connected.value = true
      console.log('[SignalR] Connected')
    } catch (err) {
      console.error('[SignalR] Connection failed:', err)
      setTimeout(start, 5000)
    }
  }
}

async function joinGroup(groupName) {
  if (!connection.value || !connected.value) return
  await connection.value.invoke('JoinGroup', groupName)
  if (!currentGroups.value.includes(groupName)) {
    currentGroups.value.push(groupName)
  }
}

async function leaveGroup(groupName) {
  if (!connection.value || !connected.value) return
  await connection.value.invoke('LeaveGroup', groupName)
  currentGroups.value = currentGroups.value.filter(g => g !== groupName)
}

function on(event, callback) {
  if (connection.value) {
    connection.value.on(event, callback)
  }
}

function off(event, callback) {
  if (connection.value) {
    connection.value.off(event, callback)
  }
}

export function useSignalR() {
  return {
    connection,
    connected,
    currentGroups,
    start,
    joinGroup,
    leaveGroup,
    on,
    off
  }
}

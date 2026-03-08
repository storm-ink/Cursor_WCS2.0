<template>
  <div class="device-management">
    <div class="panel" style="margin-bottom: 16px;">
      <div class="panel-title">设备列表</div>
      <el-table :data="devices" stripe style="width: 100%" @row-click="onSelectDevice" highlight-current-row>
        <el-table-column prop="code" label="设备编号" width="120" />
        <el-table-column prop="type" label="设备类型" width="120" />
        <el-table-column label="连接状态" width="120">
          <template #default="{ row }">
            <el-tag :type="row.isConnected ? 'success' : 'danger'" size="small">
              {{ row.isConnected ? '已连接' : '断开' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="state" label="设备状态" width="120" />
        <el-table-column prop="currentTaskNo" label="当前任务" width="160" />
        <el-table-column prop="lastUpdate" label="最后更新" width="180">
          <template #default="{ row }">
            {{ formatTime(row.lastUpdate) }}
          </template>
        </el-table-column>
      </el-table>
    </div>

    <div v-if="selectedDevice" class="device-detail">
      <div class="detail-row">
        <div class="panel" style="flex: 1;">
          <div class="panel-title">解析数据 - {{ selectedDevice.code }}</div>
          <div class="parsed-data">
            <div class="parsed-row"><span class="label">设备名：</span><span class="value">{{ selectedDevice.code }}</span></div>
            <div class="parsed-row"><span class="label">设备类型：</span><span class="value">{{ selectedDevice.type }}</span></div>
            <div class="parsed-row"><span class="label">任务号：</span><span class="value">{{ selectedDevice.currentTaskNo || '—' }}</span></div>
            <div class="parsed-row"><span class="label">设备状态：</span><span class="value">{{ selectedDevice.state || 'Idle' }}</span></div>
            <div class="parsed-row"><span class="label">连接状态：</span>
              <span class="value" :style="{ color: selectedDevice.isConnected ? '#66bb6a' : '#ef5350' }">
                {{ selectedDevice.isConnected ? '已连接' : '断开' }}
              </span>
            </div>
            <div class="parsed-row"><span class="label">最后更新：</span><span class="value">{{ formatTime(selectedDevice.lastUpdate) }}</span></div>
            <div v-if="lastParsedMessage" style="margin-top: 12px; border-top: 1px solid #1e3a5f; padding-top: 12px;">
              <div class="parsed-row"><span class="label">指令：</span><span class="value">{{ lastParsedMessage.command || '—' }}</span></div>
              <div class="parsed-row"><span class="label">结果：</span><span class="value">{{ lastParsedMessage.result || '—' }}</span></div>
              <div class="parsed-row"><span class="label">消息：</span><span class="value">{{ lastParsedMessage.message || '—' }}</span></div>
              <div class="parsed-row"><span class="label">方向：</span><span class="value">{{ lastParsedMessage.direction }}</span></div>
              <div class="parsed-row"><span class="label">时间：</span><span class="value">{{ lastParsedMessage.time }}</span></div>
            </div>
          </div>
        </div>

        <div class="panel" style="flex: 1;">
          <div class="panel-title">原始数据（最近 500 条）</div>
          <div class="raw-messages">
            <div v-for="(msg, idx) in messages" :key="idx" class="raw-msg"
                :class="{ sent: msg.direction === '发送' }">
              <span class="msg-time">{{ formatTime(msg.timestamp) }}</span>
              <span class="msg-dir">[{{ msg.direction }}]</span>
              <span class="msg-data">{{ msg.rawData }}</span>
            </div>
            <div v-if="messages.length === 0" style="color: #556677; text-align: center; padding: 20px;">
              暂无通讯数据
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { deviceApi } from '../api'
import { useSignalR } from '../stores/signalr'

const devices = ref([])
const selectedDevice = ref(null)
const messages = ref([])
const { joinGroup, leaveGroup, on, off } = useSignalR()

let currentDeviceGroup = null

const lastParsedMessage = computed(() => {
  if (messages.value.length === 0) return null
  const last = messages.value[messages.value.length - 1]
  const parsed = parseRawMessage(last.rawData)
  return {
    command: parsed.CMD || parsed.Cmd || '',
    result: parsed.TaskState || parsed.HandShake || '',
    message: last.rawData,
    direction: last.direction,
    time: formatTime(last.timestamp)
  }
})

function parseRawMessage(raw) {
  const result = {}
  const content = raw.replace(/^\[?\[?/, '').replace(/\]?\]?$/, '')
  content.split(';').forEach(pair => {
    const eqIdx = pair.indexOf('=')
    if (eqIdx > 0) {
      result[pair.substring(0, eqIdx).trim()] = pair.substring(eqIdx + 1).trim()
    }
  })
  return result
}

async function loadDevices() {
  try {
    devices.value = await deviceApi.getAll()
  } catch (e) {
    console.error('Failed to load devices:', e)
  }
}

async function onSelectDevice(row) {
  if (currentDeviceGroup) {
    leaveGroup(currentDeviceGroup)
  }

  selectedDevice.value = row
  currentDeviceGroup = `view:messages:${row.code}`
  joinGroup(currentDeviceGroup)

  try {
    messages.value = await deviceApi.getMessages(row.code)
  } catch (e) {
    console.error('Failed to load messages:', e)
  }
}

function handleDeviceMessage(data) {
  messages.value.push(data)
  if (messages.value.length > 500) {
    messages.value = messages.value.slice(-500)
  }
}

function handleDeviceStatus(data) {
  if (Array.isArray(data)) {
    devices.value = data
    if (selectedDevice.value) {
      const updated = data.find(d => d.code === selectedDevice.value.code)
      if (updated) selectedDevice.value = updated
    }
  }
}

function formatTime(val) {
  if (!val) return '-'
  return new Date(val).toLocaleString('zh-CN')
}

let refreshTimer

onMounted(() => {
  loadDevices()
  joinGroup('view:devices')
  on('DeviceMessage', handleDeviceMessage)
  on('DeviceStatusUpdated', handleDeviceStatus)
  refreshTimer = setInterval(loadDevices, 5000)
})

onUnmounted(() => {
  leaveGroup('view:devices')
  if (currentDeviceGroup) leaveGroup(currentDeviceGroup)
  off('DeviceMessage', handleDeviceMessage)
  off('DeviceStatusUpdated', handleDeviceStatus)
  clearInterval(refreshTimer)
})
</script>

<style scoped>
.device-detail {
  margin-top: 0;
}

.detail-row {
  display: flex;
  gap: 16px;
}

.parsed-data {
  font-family: 'Courier New', monospace;
  font-size: 13px;
  line-height: 2;
}

.parsed-row {
  display: flex;
}

.parsed-row .label {
  color: #8899aa;
  width: 100px;
  flex-shrink: 0;
  text-align: right;
  padding-right: 12px;
}

.parsed-row .value {
  color: #e0e6ed;
}

.raw-messages {
  max-height: 400px;
  overflow-y: auto;
  font-family: 'Courier New', monospace;
  font-size: 12px;
}

.raw-msg {
  padding: 4px 8px;
  border-bottom: 1px solid #0d1a2f;
  display: flex;
  gap: 8px;
  align-items: flex-start;
}

.raw-msg.sent {
  background: rgba(21, 101, 192, 0.08);
}

.msg-time {
  color: #556677;
  white-space: nowrap;
  flex-shrink: 0;
}

.msg-dir {
  color: #4fc3f7;
  white-space: nowrap;
  flex-shrink: 0;
}

.raw-msg.sent .msg-dir {
  color: #ffa726;
}

.msg-data {
  color: #c0ccd8;
  word-break: break-all;
}
</style>

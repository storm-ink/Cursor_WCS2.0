<template>
   <div class="sub-tabs">
        <router-link to="/devices/deviceManagement">设备列表</router-link>
        <router-link to="/devices/deviceControlCommand">控制指令管理</router-link>
        <router-link to="/devices/deviceCurrentTasks">当前状态</router-link>
         <router-link to="/devices/deviceHistoryTasks">历史任务</router-link>
        <router-link to="/devices/devicePerformanceAnalysis">性能分析</router-link>
        <router-link to="/devices/deviceProfiles">设备档案</router-link>

  </div>
  <div>
    <div class="panel">
      <div class="panel-title">设备列表</div>
      <el-table :data="devices"  @row-click="onSelectDevice" highlight-current-row
                v-loading="loading" element-loading-background="rgba(0,0,0,0.3)">
        <el-table-column prop="code" label="设备编号" min-width="100" />
        <el-table-column prop="type" label="设备类型" min-width="90" align="center">
          <template #default="{ row }">{{ row.type === 'Crane' ? '堆垛机' : '输送线' }}</template>
        </el-table-column>
        <el-table-column label="连接状态" min-width="90" align="center">
          <template #default="{ row }">
            <el-tag :type="row.isConnected ? 'success' : 'danger'" size="small">
              {{ row.isConnected ? '在线' : '离线' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="启用状态" min-width="90" align="center">
          <template #default="{ row }">
            <el-tag :type="row.isEnabled ? 'primary' : 'info'" size="small">
              {{ row.isEnabled ? '启用' : '禁用' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="state" label="设备状态" min-width="90" align="center">
          <template #default="{ row }">
            <span :class="row.state === 'Idle' ? 'state-idle' : 'state-busy'">{{ row.state || 'Idle' }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="currentTaskNo" label="当前任务" min-width="160" show-overflow-tooltip />
        <el-table-column prop="lastUpdate" label="最后更新" min-width="150">
          <template #default="{ row }">{{ formatTime(row.lastUpdate) }}</template>
        </el-table-column>
        <el-table-column label="操作" min-width="100" align="center" fixed="right">
          <template #default="{ row }">
            <el-button v-if="row.isEnabled" size="small" type="danger" plain @click.stop="toggleDevice(row, false)">
              禁用
            </el-button>
            <el-button v-else size="small" type="success" plain @click.stop="toggleDevice(row, true)">
              启用
            </el-button>
          </template>
        </el-table-column>
        <template #empty>
          <div style="padding: 40px 0; color: var(--text-muted);">暂无设备</div>
        </template>
      </el-table>
    </div>

    <div v-if="selectedDevice" class="device-detail-area">
      <div class="detail-grid">
        <div class="panel-detail">
          <div class="panel-title">解析数据</div>
          <div class="kv-list">
            <div class="kv-row"><span class="kv-key">设备名</span><span class="kv-val accent">{{ selectedDevice.code }}</span></div>
            <div class="kv-row"><span class="kv-key">设备类型</span><span class="kv-val">{{ selectedDevice.type === 'Crane' ? '堆垛机' : '输送线' }}</span></div>
            <div class="kv-row"><span class="kv-key">任务号</span><span class="kv-val">{{ selectedDevice.currentTaskNo || '—' }}</span></div>
            <div class="kv-row"><span class="kv-key">设备状态</span><span class="kv-val">{{ selectedDevice.state || 'Idle' }}</span></div>
            <div class="kv-row"><span class="kv-key">连接状态</span><span class="kv-val" :class="selectedDevice.isConnected ? 'val-ok' : 'val-err'">{{ selectedDevice.isConnected ? '已连接' : '已断开' }}</span></div>
            <div class="kv-row"><span class="kv-key">启用状态</span><span class="kv-val" :class="selectedDevice.isEnabled ? 'val-ok' : 'val-err'">{{ selectedDevice.isEnabled ? '启用' : '禁用' }}</span></div>
            <div class="kv-row"><span class="kv-key">最后更新</span><span class="kv-val dim">{{ formatTime(selectedDevice.lastUpdate) }}</span></div>
          </div>
          <template v-if="lastParsedMessage">
            <div class="kv-divider"></div>
            <div class="kv-list">
              <div class="kv-row"><span class="kv-key">指令</span><span class="kv-val">{{ lastParsedMessage.command || '—' }}</span></div>
              <div class="kv-row"><span class="kv-key">结果</span><span class="kv-val">{{ lastParsedMessage.result || '—' }}</span></div>
              <div class="kv-row"><span class="kv-key">方向</span><span class="kv-val" :class="lastParsedMessage.direction === '发送' ? 'val-send' : 'val-recv'">{{ lastParsedMessage.direction }}</span></div>
              <div class="kv-row"><span class="kv-key">时间</span><span class="kv-val dim">{{ lastParsedMessage.time }}</span></div>
            </div>
          </template>
        </div>

        <div class="panel">
          <div class="panel-title">原始报文 <span class="msg-count">{{ messages.length }} 条</span></div>
          <div class="raw-messages" ref="msgListRef">
            <div v-for="(msg, idx) in messages" :key="idx"
                 class="raw-msg" :class="msg.direction === '发送' ? 'msg-send' : 'msg-recv'">
              <span class="msg-time">{{ formatShortTime(msg.timestamp) }}</span>
              <span class="msg-dir">{{ msg.direction === '发送' ? 'TX' : 'RX' }}</span>
              <span class="msg-data">{{ msg.rawData }}</span>
            </div>
            <div v-if="messages.length === 0" class="msg-empty">暂无通讯数据</div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, nextTick, watch } from 'vue'
import { deviceApi } from '../../api'
import { useSignalR } from '../../stores/signalr'
import { ElMessage } from 'element-plus'

const devices = ref([])
const selectedDevice = ref(null)
const messages = ref([])
const loading = ref(false)
const msgListRef = ref(null)
const { joinGroup, leaveGroup, on, off } = useSignalR()
let currentDeviceGroup = null

const lastParsedMessage = computed(() => {
  if (messages.value.length === 0) return null
  const last = messages.value[messages.value.length - 1]
  const parsed = parseRawMessage(last.rawData)
  return { command: parsed.CMD || parsed.Cmd || '', result: parsed.TaskState || parsed.HandShake || '', direction: last.direction, time: formatTime(last.timestamp) }
})

watch(messages, () => { nextTick(() => { const el = msgListRef.value; if (el) el.scrollTop = el.scrollHeight }) }, { deep: true })

function parseRawMessage(raw) {
  const result = {}
  raw.replace(/^\[?\[?/, '').replace(/\]?\]?$/, '').split(';').forEach(pair => {
    const i = pair.indexOf('=')
    if (i > 0) result[pair.substring(0, i).trim()] = pair.substring(i + 1).trim()
  })
  return result
}

async function loadDevices() {
  loading.value = true
  try { devices.value = await deviceApi.getAll() } catch (e) { console.error(e) }
  finally { loading.value = false }
}

async function onSelectDevice(row) {
  if (currentDeviceGroup) leaveGroup(currentDeviceGroup)
  selectedDevice.value = row
  currentDeviceGroup = `view:messages:${row.code}`
  joinGroup(currentDeviceGroup)
  try { messages.value = await deviceApi.getMessages(row.code) } catch (e) { console.error(e) }
}

async function toggleDevice(row, enable) {
  try {
    if (enable) await deviceApi.enable(row.code)
    else await deviceApi.disable(row.code)
    ElMessage.success(`设备 ${row.code} 已${enable ? '启用' : '禁用'}`)
  } catch (e) {
    ElMessage.error(e.response?.data?.error || '操作失败')
  }
}

function handleDeviceMessage(data) {
  messages.value.push(data)
  if (messages.value.length > 500) messages.value = messages.value.slice(-500)
}

function handleDeviceStatus(data) {
  if (!Array.isArray(data)) return
  devices.value = data
  if (selectedDevice.value) {
    const updated = data.find(d => d.code === selectedDevice.value.code)
    if (updated) selectedDevice.value = updated
  }
}

function formatTime(val) { return val ? new Date(val).toLocaleString('zh-CN', { hour12: false }) : '—' }
function formatShortTime(val) { return val ? new Date(val).toLocaleTimeString('zh-CN', { hour12: false, hour: '2-digit', minute: '2-digit', second: '2-digit' }) : '' }

onMounted(() => {
  loadDevices()
  joinGroup('view:devices')
  on('DeviceMessage', handleDeviceMessage)
  on('DeviceStatusUpdated', handleDeviceStatus)
})

onUnmounted(() => {
  leaveGroup('view:devices')
  if (currentDeviceGroup) leaveGroup(currentDeviceGroup)
  off('DeviceMessage', handleDeviceMessage)
  off('DeviceStatusUpdated', handleDeviceStatus)
})
</script>

<style scoped>
.device-detail-area { margin-top: 16px; }
.detail-grid { display: grid; grid-template-columns: 320px 1fr; gap: 16px; }
.kv-list { display: flex; flex-direction: column; gap: 2px; }
.kv-row { display: flex; align-items: center; padding: 6px 0; font-size: 12px; }
.kv-key { width: 70px; flex-shrink: 0; color: var(--text-muted); text-align: right; padding-right: 14px; }
.kv-val { color: var(--text-primary); }
.kv-val.accent { color: var(--accent); font-weight: 600; }
.kv-val.dim { color: var(--text-muted); }
.kv-val.val-ok { color: var(--success); }
.kv-val.val-err { color: var(--danger); }
.kv-val.val-send { color: #f59e0b; }
.kv-val.val-recv { color: #3b9eff; }
.kv-divider { height: 1px; background: var(--border); margin: 10px 0; }
.msg-count { font-size: 11px; font-weight: 400; color: var(--text-muted); margin-left: 6px; }
.raw-messages { max-height: 420px; overflow-y: auto; font-family: var(--font-mono); font-size: 11px; background: var(--bg-deep); border-radius: 6px; padding: 6px; }
.raw-msg { padding: 3px 8px; display: flex; gap: 8px; align-items: baseline; border-radius: 3px; line-height: 1.7; }
.raw-msg:hover { background: rgba(255,255,255,0.02); }
.msg-time { color: var(--text-muted); white-space: nowrap; flex-shrink: 0; }
.msg-dir { font-weight: 700; font-size: 10px; width: 20px; text-align: center; flex-shrink: 0; border-radius: 3px; padding: 1px 0; }
.msg-recv .msg-dir { color: #3b9eff; }
.msg-send .msg-dir { color: #f59e0b; }
.msg-data { color: var(--text-secondary); word-break: break-all; }
.msg-empty { color: var(--text-muted); text-align: center; padding: 40px; }
.state-idle { color: var(--text-muted); }
.state-busy { color: var(--accent); font-weight: 600; }
</style>

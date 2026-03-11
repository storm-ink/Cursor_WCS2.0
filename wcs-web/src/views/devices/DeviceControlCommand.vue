<template>
  <div>
     <div class="sub-tabs">
      <router-link to="/devices/deviceManagement">设备列表</router-link>
      <router-link to="/devices/deviceControlCommand">控制指令</router-link>
      <router-link to="/devices/deviceCurrentTasks">当前状态</router-link>
      <router-link to="/devices/deviceHistoryTasks">历史任务</router-link>
      <router-link to="/devices/devicePerformanceAnalysis">性能分析</router-link>
      <router-link to="/devices/deviceProfiles">设备档案</router-link>
    </div>

    <div class="panel">
      <div class="panel-title">设备列表</div>
      <el-table :data="devices" stripe @row-click="onSelectDevice" highlight-current-row
                v-loading="loading" element-loading-background="rgba(0,0,0,0.3)" size="small">
        <el-table-column prop="code" label="设备编号" min-width="100" />
        <el-table-column prop="type" label="设备类型" min-width="90" align="center">
          <template #default="{ row }">{{ row.type === 'Crane' ? '堆垛机' : '输送线' }}</template>
        </el-table-column>
        <el-table-column label="连接" min-width="70" align="center">
          <template #default="{ row }">
            <el-tag :type="row.isConnected ? 'success' : 'danger'" size="small">{{ row.isConnected ? '在线' : '离线' }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="启用" min-width="70" align="center">
          <template #default="{ row }">
            <el-tag :type="row.isEnabled ? 'primary' : 'info'" size="small">{{ row.isEnabled ? '启用' : '禁用' }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="currentTaskNo" label="当前任务" min-width="120" show-overflow-tooltip />
      </el-table>
    </div>

    <div v-if="selectedDevice" class="command-area">
      <div class="command-grid">
        <div class="panel">
          <div class="panel-title">任务命令 — {{ selectedDevice.code }}</div>
          <el-form label-width="70px" label-position="left" size="small">
            <el-form-item label="指令类型">
              <el-select v-model="commandForm.cmd" style="width: 100%;">
                <el-option label="CV_TASK (输送线任务)" value="CV_TASK" />
                <el-option label="CRANE_TASK (堆垛机任务)" value="CRANE_TASK" />
              </el-select>
            </el-form-item>
            <el-form-item label="HandShake">
              <el-input v-model="commandForm.handShake" placeholder="1" />
            </el-form-item>
            <el-form-item label="TaskNo">
              <el-input v-model="commandForm.taskNo" placeholder="任务号" />
            </el-form-item>
            <el-form-item label="From">
              <el-input v-model="commandForm.from" placeholder="起点" />
            </el-form-item>
            <el-form-item label="To">
              <el-input v-model="commandForm.to" placeholder="终点" />
            </el-form-item>
            <el-form-item label="RoutingNo" v-if="commandForm.cmd === 'CV_TASK'">
              <el-input v-model="commandForm.routingNo" placeholder="路由号" />
            </el-form-item>
            <el-form-item label="TUID">
              <el-input v-model="commandForm.tuid" placeholder="托盘号" />
            </el-form-item>
          </el-form>
          <div class="command-preview">
            <div class="preview-label">预览命令</div>
            <div class="preview-text">{{ buildCommandPreview() }}</div>
          </div>
          <div class="command-note">
            <el-icon><WarningFilled /></el-icon>
            此功能用于调试，命令需通过 PLC 通信层发送
          </div>
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
import { ref, onMounted, onUnmounted, nextTick, watch } from 'vue'
import { deviceApi } from '../../api'
import { useSignalR } from '../../stores/signalr'
import { WarningFilled } from '@element-plus/icons-vue'

const devices = ref([])
const selectedDevice = ref(null)
const messages = ref([])
const loading = ref(false)
const msgListRef = ref(null)
const { joinGroup, leaveGroup, on, off } = useSignalR()
let currentDeviceGroup = null

const commandForm = ref({
  cmd: 'CV_TASK', handShake: '1', taskNo: '', from: '', to: '', routingNo: '', tuid: ''
})

watch(messages, () => { nextTick(() => { const el = msgListRef.value; if (el) el.scrollTop = el.scrollHeight }) }, { deep: true })

function buildCommandPreview() {
  const f = commandForm.value
  const parts = [`CMD=${f.cmd}`, `HandShake=${f.handShake}`]
  if (f.taskNo) parts.push(`TaskNo=${f.taskNo}`)
  if (f.tuid) parts.push(`TUID=${f.tuid}`)
  if (f.from) parts.push(`From=${f.from}`)
  if (f.to) parts.push(`To=${f.to}`)
  if (f.routingNo && f.cmd === 'CV_TASK') parts.push(`RoutingNo=${f.routingNo}`)
  return `[${parts.join(';')}]`
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
.command-area { margin-top: 16px; }
.command-grid { display: grid; grid-template-columns: 360px 1fr; gap: 16px; }
.command-preview {
  margin-top: 12px; padding: 10px; background: var(--bg-deep); border-radius: 6px;
  font-family: var(--font-mono); font-size: 11px;
}
.preview-label { font-size: 10px; color: var(--text-muted); margin-bottom: 4px; text-transform: uppercase; letter-spacing: 0.5px; }
.preview-text { color: var(--accent); word-break: break-all; }
.command-note {
  margin-top: 10px; font-size: 11px; color: var(--text-muted);
  display: flex; align-items: center; gap: 4px;
}
.msg-count { font-size: 11px; font-weight: 400; color: var(--text-muted); margin-left: 6px; }
.raw-messages { max-height: 420px; overflow-y: auto; font-family: var(--font-mono); font-size: 11px; background: var(--bg-deep); border-radius: 6px; padding: 6px; }
.raw-msg { padding: 3px 8px; display: flex; gap: 8px; align-items: baseline; border-radius: 3px; line-height: 1.7; }
.raw-msg:hover { background: rgba(255,255,255,0.02); }
.msg-time { color: var(--text-muted); white-space: nowrap; flex-shrink: 0; }
.msg-dir { font-weight: 700; font-size: 10px; width: 20px; text-align: center; flex-shrink: 0; }
.msg-recv .msg-dir { color: #3b9eff; }
.msg-send .msg-dir { color: #f59e0b; }
.msg-data { color: var(--text-secondary); word-break: break-all; }
.msg-empty { color: var(--text-muted); text-align: center; padding: 40px; }
</style>

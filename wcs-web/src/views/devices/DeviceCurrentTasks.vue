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
        <el-table-column label="状态" min-width="70" align="center">
          <template #default="{ row }">
            <span :class="row.state === 'Idle' ? 'state-idle' : 'state-busy'">{{ row.state || 'Idle' }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="currentTaskNo" label="当前任务" min-width="140" show-overflow-tooltip />
      </el-table>
    </div>

    <div v-if="selectedDevice" class="panel" style="margin-top: 16px;">
      <div class="panel-title">设备任务 — {{ selectedDevice.code }}
        <span class="title-hint">（当前库 DeviceTaskEntity）</span>
      </div>

      <div class="task-section">
        <div class="section-label running-label">
          <span class="section-dot running"></span>
          运行中 / 下发中 <span class="count-badge">{{ runningTasks.length }}</span>
        </div>
        <el-table :data="runningTasks" stripe size="small" v-loading="taskLoading"
                  element-loading-background="rgba(0,0,0,0.3)">
          <el-table-column prop="taskCode" label="任务号" min-width="140" show-overflow-tooltip />
          <el-table-column prop="stepOrder" label="步骤" min-width="50" align="center" />
          <el-table-column prop="segmentSource" label="起点" min-width="80" align="center" />
          <el-table-column prop="segmentDest" label="终点" min-width="80" align="center" />
          <el-table-column prop="status" label="状态" min-width="80" align="center">
            <template #default="{ row }">
              <el-tag :type="deviceStatusTagType(row.status)" size="small">{{ deviceStatusLabel(row.status) }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="sendCount" label="发送次数" min-width="70" align="center" />
          <el-table-column prop="createdAt" label="创建时间" min-width="140">
            <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
          </el-table-column>
          <template #empty>
            <div style="padding: 20px 0; color: var(--text-muted);">暂无运行中任务</div>
          </template>
        </el-table>
      </div>

      <div class="task-section" style="margin-top: 16px;">
        <div class="section-label waiting-label">
          <span class="section-dot waiting"></span>
          未执行 <span class="count-badge">{{ waitingTasks.length }}</span>
        </div>
        <el-table :data="waitingTasks" stripe size="small">
          <el-table-column prop="taskCode" label="任务号" min-width="140" show-overflow-tooltip />
          <el-table-column prop="stepOrder" label="步骤" min-width="50" align="center" />
          <el-table-column prop="segmentSource" label="起点" min-width="80" align="center" />
          <el-table-column prop="segmentDest" label="终点" min-width="80" align="center" />
          <el-table-column prop="status" label="状态" min-width="80" align="center">
            <template #default="{ row }">
              <el-tag type="info" size="small">{{ deviceStatusLabel(row.status) }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="未执行原因" min-width="200">
            <template #default="{ row }">
              <span class="reason-text">{{ getWaitReason(row) }}</span>
            </template>
          </el-table-column>
          <el-table-column prop="createdAt" label="创建时间" min-width="140">
            <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
          </el-table-column>
          <template #empty>
            <div style="padding: 20px 0; color: var(--text-muted);">暂无待执行任务</div>
          </template>
        </el-table>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { deviceApi, deviceTaskApi } from '../../api'
import { useSignalR } from '../../stores/signalr'

const devices = ref([])
const selectedDevice = ref(null)
const allDeviceTasks = ref([])
const loading = ref(false)
const taskLoading = ref(false)
const { joinGroup, leaveGroup, on, off } = useSignalR()

const runningTasks = computed(() =>
  allDeviceTasks.value.filter(d => ['SendingToPlc', 'Running'].includes(d.status))
)
const waitingTasks = computed(() =>
  allDeviceTasks.value.filter(d => ['Waiting', 'Error'].includes(d.status))
)

function getWaitReason(task) {
  if (task.status === 'Error') return `异常: ${task.errorMessage || '未知错误'}`
  if (task.status === 'Waiting') {
    if (task.stepOrder > 1) return '等待前序步骤完成'
    return '等待设备空闲或调度分配'
  }
  return '—'
}

async function loadDevices() {
  loading.value = true
  try { devices.value = await deviceApi.getAll() } catch (e) { console.error(e) }
  finally { loading.value = false }
}

async function onSelectDevice(row) {
  selectedDevice.value = row
  taskLoading.value = true
  try { allDeviceTasks.value = await deviceTaskApi.getByDevice(row.code) } catch (e) { console.error(e) }
  finally { taskLoading.value = false }
}

function handleDeviceStatus(data) {
  if (!Array.isArray(data)) return
  devices.value = data
  if (selectedDevice.value) {
    const updated = data.find(d => d.code === selectedDevice.value.code)
    if (updated) selectedDevice.value = updated
  }
}

let debounceTimer = null
function handleTasksChanged() {
  if (debounceTimer) clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => {
    if (selectedDevice.value) onSelectDevice(selectedDevice.value)
  }, 500)
}

const deviceStatusLabel = s => ({ Waiting: '等待', SendingToPlc: '下发中', Running: '运行中', Finished: '已完成', Error: '异常' }[s] || s)
const deviceStatusTagType = s => ({ Waiting: 'info', SendingToPlc: 'warning', Running: 'primary', Finished: 'success', Error: 'danger' }[s] || 'info')
function formatTime(val) { return val ? new Date(val).toLocaleString('zh-CN', { hour12: false }) : '—' }

onMounted(() => {
  loadDevices()
  joinGroup('view:devices')
  joinGroup('view:tasks')
  on('DeviceStatusUpdated', handleDeviceStatus)
  on('TasksChanged', handleTasksChanged)
})

onUnmounted(() => {
  leaveGroup('view:devices')
  leaveGroup('view:tasks')
  off('DeviceStatusUpdated', handleDeviceStatus)
  off('TasksChanged', handleTasksChanged)
})
</script>

<style scoped>
.title-hint { font-size: 11px; font-weight: 400; color: var(--text-muted); }
.task-section { }
.section-label {
  font-size: 12px; font-weight: 600; margin-bottom: 8px;
  display: flex; align-items: center; gap: 6px;
}
.section-dot { width: 8px; height: 8px; border-radius: 50%; }
.section-dot.running { background: var(--accent); box-shadow: 0 0 6px var(--accent-glow); }
.section-dot.waiting { background: var(--text-muted); }
.running-label { color: var(--accent); }
.waiting-label { color: var(--text-secondary); }
.count-badge { font-size: 10px; font-weight: 400; color: var(--text-muted); }
.reason-text { font-size: 11px; color: var(--warning); }
.state-idle { color: var(--text-muted); }
.state-busy { color: var(--accent); font-weight: 600; }
</style>

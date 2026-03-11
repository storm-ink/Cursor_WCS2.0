<template>
  <div>
    <div class="sub-tabs">
      <router-link to="/devices/list">设备列表</router-link>
      <router-link to="/devices/commands">控制指令</router-link>
      <router-link to="/devices/status">当前状态</router-link>
      <router-link to="/devices/history">历史任务</router-link>
      <router-link to="/devices/performance">性能分析</router-link>
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
        <el-table-column prop="state" label="状态" min-width="70" align="center" />
        <el-table-column prop="currentTaskNo" label="当前任务" min-width="140" show-overflow-tooltip />
      </el-table>
    </div>

    <div v-if="selectedDevice" class="panel" style="margin-top: 16px;">
      <div class="panel-title">历史设备任务 — {{ selectedDevice.code }}
        <span class="title-hint">（历史库 DeviceTaskEntity — 已完成/已取消）</span>
      </div>
      <el-table :data="historyTasks" stripe size="small" v-loading="taskLoading"
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
        <el-table-column prop="finishedAt" label="完成时间" min-width="140">
          <template #default="{ row }">{{ formatTime(row.finishedAt) }}</template>
        </el-table-column>
        <el-table-column prop="errorMessage" label="错误信息" min-width="120" show-overflow-tooltip />
        <template #empty>
          <div style="padding: 30px 0; color: var(--text-muted);">暂无历史设备任务</div>
        </template>
      </el-table>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import { deviceApi, deviceTaskApi } from '../../api'
import { useSignalR } from '../../stores/signalr'

const devices = ref([])
const selectedDevice = ref(null)
const historyTasks = ref([])
const loading = ref(false)
const taskLoading = ref(false)
const { joinGroup, leaveGroup, on, off } = useSignalR()

async function loadDevices() {
  loading.value = true
  try { devices.value = await deviceApi.getAll() } catch (e) { console.error(e) }
  finally { loading.value = false }
}

async function onSelectDevice(row) {
  selectedDevice.value = row
  taskLoading.value = true
  try { historyTasks.value = await deviceTaskApi.getHistoryByDevice(row.code) } catch (e) { console.error(e) }
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

const deviceStatusLabel = s => ({ Waiting: '等待', SendingToPlc: '下发中', Running: '运行中', Finished: '已完成', Error: '异常' }[s] || s)
const deviceStatusTagType = s => ({ Waiting: 'info', SendingToPlc: 'warning', Running: 'primary', Finished: 'success', Error: 'danger' }[s] || 'info')
function formatTime(val) { return val ? new Date(val).toLocaleString('zh-CN', { hour12: false }) : '—' }

onMounted(() => {
  loadDevices()
  joinGroup('view:devices')
  on('DeviceStatusUpdated', handleDeviceStatus)
})

onUnmounted(() => {
  leaveGroup('view:devices')
  off('DeviceStatusUpdated', handleDeviceStatus)
})
</script>

<style scoped>
.title-hint { font-size: 11px; font-weight: 400; color: var(--text-muted); }
</style>

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
      <div class="panel-title">性能分析 — {{ selectedDevice.code }}</div>

      <div class="stats-grid">
        <div class="stat-card">
          <div class="stat-label">稼动率</div>
          <div class="stat-value">
            <el-progress type="dashboard" :percentage="stats.utilization" :width="100"
                         :color="stats.utilization > 80 ? '#ef4444' : stats.utilization > 50 ? '#f59e0b' : '#22c55e'" />
          </div>
          <div class="stat-desc">设备运行时间 / 总在线时间</div>
        </div>

        <div class="stat-card">
          <div class="stat-label">任务吞吐量</div>
          <div class="stat-number">{{ stats.completedCount }}</div>
          <div class="stat-desc">已完成设备任务数</div>
        </div>

        <div class="stat-card">
          <div class="stat-label">平均执行时间</div>
          <div class="stat-number">{{ stats.avgDuration }}<span class="stat-unit">秒</span></div>
          <div class="stat-desc">每个设备任务平均耗时</div>
        </div>

        <div class="stat-card">
          <div class="stat-label">异常率</div>
          <div class="stat-number" :class="stats.errorRate > 10 ? 'val-danger' : ''">{{ stats.errorRate }}<span class="stat-unit">%</span></div>
          <div class="stat-desc">异常任务 / 总任务数</div>
        </div>

        <div class="stat-card">
          <div class="stat-label">总任务数</div>
          <div class="stat-number">{{ stats.totalCount }}</div>
          <div class="stat-desc">包含所有状态</div>
        </div>

        <div class="stat-card">
          <div class="stat-label">异常任务数</div>
          <div class="stat-number val-danger">{{ stats.errorCount }}</div>
          <div class="stat-desc">执行出错的任务</div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import { deviceApi, deviceTaskApi } from '../../api'
import { useSignalR } from '../../stores/signalr'

const devices = ref([])
const selectedDevice = ref(null)
const loading = ref(false)
const { joinGroup, leaveGroup, on, off } = useSignalR()

const stats = ref({
  utilization: 0,
  completedCount: 0,
  avgDuration: 0,
  errorRate: 0,
  totalCount: 0,
  errorCount: 0
})

async function loadDevices() {
  loading.value = true
  try { devices.value = await deviceApi.getAll() } catch (e) { console.error(e) }
  finally { loading.value = false }
}

async function onSelectDevice(row) {
  selectedDevice.value = row
  await calculateStats(row.code)
}

async function calculateStats(deviceCode) {
  try {
    // 用当前库的设备任务计算性能
    const currentTasks = await deviceTaskApi.getByDevice(deviceCode)
    // 也获取历史库的
    let historyTasks = []
    try { historyTasks = await deviceTaskApi.getHistoryByDevice(deviceCode) } catch (e) { /* history may be empty */ }

    const allTasks = [...currentTasks, ...historyTasks]
    const totalCount = allTasks.length
    const completedTasks = allTasks.filter(t => t.status === 'Finished')
    const errorTasks = allTasks.filter(t => t.status === 'Error')
    const completedCount = completedTasks.length
    const errorCount = errorTasks.length

    // 平均执行时间
    let avgDuration = 0
    if (completedTasks.length > 0) {
      const durations = completedTasks
        .filter(t => t.startedAt && t.finishedAt)
        .map(t => (new Date(t.finishedAt) - new Date(t.startedAt)) / 1000)
      avgDuration = durations.length > 0 ? Math.round(durations.reduce((a, b) => a + b, 0) / durations.length) : 0
    }

    // 异常率
    const errorRate = totalCount > 0 ? Math.round((errorCount / totalCount) * 100) : 0

    // 稼动率（简化计算：有完成的任务时间占比）
    let utilization = 0
    if (completedTasks.length > 0) {
      const busySeconds = completedTasks
        .filter(t => t.startedAt && t.finishedAt)
        .map(t => (new Date(t.finishedAt) - new Date(t.startedAt)) / 1000)
        .reduce((a, b) => a + b, 0)
      const times = allTasks.map(t => new Date(t.createdAt).getTime())
      const finished = completedTasks
        .filter(t => t.finishedAt)
        .map(t => new Date(t.finishedAt).getTime())
      if (times.length > 0 && finished.length > 0) {
        const span = (Math.max(...finished) - Math.min(...times)) / 1000
        utilization = span > 0 ? Math.min(Math.round((busySeconds / span) * 100), 100) : 0
      }
    }

    stats.value = { utilization, completedCount, avgDuration, errorRate, totalCount, errorCount }
  } catch (e) {
    console.error(e)
    stats.value = { utilization: 0, completedCount: 0, avgDuration: 0, errorRate: 0, totalCount: 0, errorCount: 0 }
  }
}

function handleDeviceStatus(data) {
  if (!Array.isArray(data)) return
  devices.value = data
  if (selectedDevice.value) {
    const updated = data.find(d => d.code === selectedDevice.value.code)
    if (updated) selectedDevice.value = updated
  }
}

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
.stats-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
  margin-top: 8px;
}
.stat-card {
  background: var(--bg-base);
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 20px;
  text-align: center;
}
.stat-label {
  font-size: 12px;
  font-weight: 600;
  color: var(--text-secondary);
  margin-bottom: 12px;
}
.stat-number {
  font-size: 32px;
  font-weight: 700;
  color: var(--accent);
  font-family: var(--font-mono);
  line-height: 1;
  margin-bottom: 8px;
}
.stat-number.val-danger { color: var(--danger); }
.stat-unit {
  font-size: 14px;
  font-weight: 400;
  color: var(--text-muted);
  margin-left: 2px;
}
.stat-desc {
  font-size: 11px;
  color: var(--text-muted);
}
.stat-value {
  display: flex;
  justify-content: center;
  margin-bottom: 8px;
}
</style>

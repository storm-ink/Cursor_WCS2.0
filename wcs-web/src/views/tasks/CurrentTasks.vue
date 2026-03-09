<template>
  <div>
    <div class="sub-tabs">
      <router-link to="/tasks/current">当前任务</router-link>
      <router-link to="/tasks/history">历史任务</router-link>
      <router-link to="/tasks/create">手动下任务</router-link>
    </div>

    <div class="panel">
      <div class="panel-title">当前任务列表</div>
      <el-table :data="tasks" stripe @row-click="onRowClick" highlight-current-row>
        <el-table-column prop="taskCode" label="任务编号" min-width="160" show-overflow-tooltip />
        <el-table-column prop="source" label="来源" min-width="70" align="center">
          <template #default="{ row }">{{ sourceLabel(row.source) }}</template>
        </el-table-column>
        <el-table-column prop="type" label="类型" min-width="70" align="center">
          <template #default="{ row }">
            <span :class="'type-' + row.type?.toLowerCase()">{{ typeLabel(row.type) }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="palletCode" label="托盘号" min-width="100" show-overflow-tooltip />
        <el-table-column prop="startLocationCode" label="起点" min-width="80" align="center" />
        <el-table-column prop="endLocationCode" label="终点" min-width="80" align="center" />
        <el-table-column prop="status" label="状态" min-width="100" align="center">
          <template #default="{ row }">
            <el-tag :type="statusTagType(row.status)" size="small">{{ statusLabel(row.status) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="进度" min-width="80" align="center">
          <template #default="{ row }">
            <span class="step-progress">{{ row.currentStepOrder }} / {{ row.totalSteps }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="priority" label="优先级" min-width="70" align="center">
          <template #default="{ row }">
            <span :class="row.priority >= 5 ? 'priority-high' : ''">{{ row.priority }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="createdAt" label="创建时间" min-width="140">
          <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
        </el-table-column>
      </el-table>
    </div>

    <div v-if="selectedTask" class="panel-detail">
      <div class="panel-title">设备任务明细 — {{ selectedTask.taskCode }}</div>
      <el-table :data="deviceTasks" stripe>
        <el-table-column prop="stepOrder" label="步骤" min-width="50" align="center" />
        <el-table-column prop="deviceType" label="设备类型" min-width="90" align="center">
          <template #default="{ row }">{{ deviceTypeLabel(row.deviceType) }}</template>
        </el-table-column>
        <el-table-column prop="deviceCode" label="设备号" min-width="80" align="center" />
        <el-table-column prop="segmentSource" label="起点" min-width="90" align="center" />
        <el-table-column prop="segmentDest" label="终点" min-width="90" align="center" />
        <el-table-column prop="status" label="状态" min-width="90" align="center">
          <template #default="{ row }">
            <el-tag :type="deviceStatusTagType(row.status)" size="small">{{ deviceStatusLabel(row.status) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="sendCount" label="发送次数" min-width="80" align="center" />
        <el-table-column prop="createdAt" label="创建时间" min-width="140">
          <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
        </el-table-column>
        <el-table-column prop="errorMessage" label="错误信息" min-width="120" show-overflow-tooltip />
      </el-table>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import { taskApi } from '../../api'
import { useSignalR } from '../../stores/signalr'

const tasks = ref([])
const selectedTask = ref(null)
const deviceTasks = ref([])
const { joinGroup, leaveGroup, on, off } = useSignalR()

async function loadTasks() {
  try { tasks.value = await taskApi.getCurrent() } catch (e) { console.error(e) }
}

async function onRowClick(row) {
  selectedTask.value = row
  try { deviceTasks.value = await taskApi.getDeviceTasks(row.taskCode) } catch (e) { console.error(e) }
}

function handleTaskUpdate(data) {
  if (Array.isArray(data)) tasks.value = data
  else loadTasks()
}

const statusMap = { Created: '已创建', SendingToPlc: '下发中', Running: '运行中', Finished: '已完成', Error: '异常', Cancelled: '已取消' }
const statusLabel = s => statusMap[s] || s
const statusTagType = s => ({ Created: 'info', SendingToPlc: 'warning', Running: 'primary', Finished: 'success', Error: 'danger', Cancelled: 'info' }[s] || 'info')

const deviceStatusMap = { Waiting: '等待', SendingToPlc: '下发中', Running: '运行中', Finished: '已完成', Error: '异常' }
const deviceStatusLabel = s => deviceStatusMap[s] || s
const deviceStatusTagType = s => ({ Waiting: 'info', SendingToPlc: 'warning', Running: 'primary', Finished: 'success', Error: 'danger' }[s] || 'info')

const sourceLabel = s => ({ Manual: '手动', Wms: 'WMS' }[s] || s)
const typeLabel = t => ({ Inbound: '入库', Outbound: '出库', Transfer: '移库' }[t] || t)
const deviceTypeLabel = t => ({ Conveyor: '输送线', Crane: '堆垛机' }[t] || t)

function formatTime(val) {
  if (!val) return '—'
  return new Date(val).toLocaleString('zh-CN', { hour12: false })
}

let refreshTimer

onMounted(() => {
  loadTasks()
  joinGroup('view:tasks')
  on('TasksUpdated', handleTaskUpdate)
  refreshTimer = setInterval(loadTasks, 5000)
})

onUnmounted(() => {
  leaveGroup('view:tasks')
  off('TasksUpdated', handleTaskUpdate)
  clearInterval(refreshTimer)
})
</script>

<style scoped>
.step-progress {
  font-family: var(--font-mono);
  font-size: 11px;
  color: var(--accent);
}
.priority-high {
  color: var(--warning);
  font-weight: 700;
}
.type-inbound { color: #4ade80; }
.type-outbound { color: #f97316; }
.type-transfer { color: #a78bfa; }
</style>

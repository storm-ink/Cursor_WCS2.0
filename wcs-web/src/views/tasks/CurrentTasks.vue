<template>
  <div>
    <div class="sub-tabs">
      <router-link to="/tasks/board">任务看板</router-link>
      <router-link to="/tasks/current">当前任务</router-link>
      <router-link to="/tasks/history">历史任务</router-link>
      <router-link to="/tasks/create">手动下任务</router-link>
    </div>

    <div class="panel">
      <div class="panel-title">当前任务列表</div>
      <el-table :data="tasks" stripe @row-click="onRowClick" highlight-current-row
                v-loading="loading" element-loading-background="rgba(0,0,0,0.3)">
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
        <el-table-column label="操作" min-width="200" align="center" fixed="right">
          <template #default="{ row }">
            <el-button v-if="row.status === 'Error'" size="small" type="warning" plain @click.stop="retryTask(row)">
              重试
            </el-button>
            <el-button v-if="canCancel(row.status)" size="small" type="danger" plain @click.stop="cancelTask(row)">
              取消
            </el-button>
            <el-button v-if="canComplete(row.status)" size="small" type="success" plain @click.stop="completeTask(row)">
              完成
            </el-button>
          </template>
        </el-table-column>
        <template #empty>
          <div style="padding: 40px 0; color: var(--text-muted);">暂无进行中的任务</div>
        </template>
      </el-table>
    </div>

    <div v-if="selectedTask" class="panel-detail">
      <div class="panel-title">设备任务明细 — {{ selectedTask.taskCode }}</div>
      <el-table :data="deviceTasks" stripe v-loading="detailLoading" element-loading-background="rgba(0,0,0,0.3)">
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
        <el-table-column label="操作" min-width="200" align="center" fixed="right">
          <template #default="{ row }">
            <el-button v-if="canResendDevice(row.status)" size="small" type="warning" plain @click.stop="resendDeviceTask(row)">
              重发
            </el-button>
            <el-button v-if="canCancelDevice(row.status)" size="small" type="danger" plain @click.stop="cancelDeviceTask(row)">
              取消
            </el-button>
            <el-button v-if="canCompleteDevice(row.status)" size="small" type="success" plain @click.stop="completeDeviceTask(row)">
              完成
            </el-button>
          </template>
        </el-table-column>
        <template #empty>
          <div style="padding: 20px 0; color: var(--text-muted);">暂无设备任务</div>
        </template>
      </el-table>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import { taskApi, deviceTaskApi } from '../../api'
import { useSignalR } from '../../stores/signalr'
import { ElMessage, ElMessageBox } from 'element-plus'

const tasks = ref([])
const selectedTask = ref(null)
const deviceTasks = ref([])
const loading = ref(false)
const detailLoading = ref(false)
const { joinGroup, leaveGroup, on, off } = useSignalR()

async function loadTasks() {
  loading.value = true
  try { tasks.value = await taskApi.getCurrent() } catch (e) { console.error(e) }
  finally { loading.value = false }
}

async function onRowClick(row) {
  selectedTask.value = row
  detailLoading.value = true
  try { deviceTasks.value = await taskApi.getDeviceTasks(row.taskCode) } catch (e) { console.error(e) }
  finally { detailLoading.value = false }
}

async function cancelTask(row) {
  try {
    await ElMessageBox.confirm(`确定要取消任务 ${row.taskCode}？`, '确认取消', { type: 'warning' })
    await taskApi.cancel(row.id)
    ElMessage.success('任务已取消')
    await loadTasks()
  } catch (e) {
    if (e !== 'cancel') ElMessage.error(e.response?.data?.error || '取消失败')
  }
}

async function retryTask(row) {
  try {
    await taskApi.retry(row.id)
    ElMessage.success('任务已重试')
    await loadTasks()
  } catch (e) {
    ElMessage.error(e.response?.data?.error || '重试失败')
  }
}

async function completeTask(row) {
  try {
    await ElMessageBox.confirm(`确定要手动完成任务 ${row.taskCode}？`, '确认完成', { type: 'warning' })
    await taskApi.complete(row.id)
    ElMessage.success('任务已完成')
    await loadTasks()
  } catch (e) {
    if (e !== 'cancel') ElMessage.error(e.response?.data?.error || '完成失败')
  }
}

async function resendDeviceTask(row) {
  try {
    await deviceTaskApi.resend(row.id)
    ElMessage.success('设备任务已重发')
    if (selectedTask.value) await onRowClick(selectedTask.value)
  } catch (e) { ElMessage.error(e.response?.data?.error || '重发失败') }
}

async function cancelDeviceTask(row) {
  try {
    await ElMessageBox.confirm(`确定要取消设备任务？`, '确认取消', { type: 'warning' })
    await deviceTaskApi.cancel(row.id)
    ElMessage.success('设备任务已取消')
    if (selectedTask.value) await onRowClick(selectedTask.value)
  } catch (e) {
    if (e !== 'cancel') ElMessage.error(e.response?.data?.error || '取消失败')
  }
}

async function completeDeviceTask(row) {
  try {
    await ElMessageBox.confirm(`确定要手动完成设备任务？`, '确认完成', { type: 'warning' })
    await deviceTaskApi.complete(row.id)
    ElMessage.success('设备任务已完成')
    if (selectedTask.value) await onRowClick(selectedTask.value)
    await loadTasks()
  } catch (e) {
    if (e !== 'cancel') ElMessage.error(e.response?.data?.error || '完成失败')
  }
}

function canCancel(status) {
  return ['Created', 'SendingToPlc', 'Running', 'Error'].includes(status)
}
function canComplete(status) {
  return ['Created', 'SendingToPlc', 'Running', 'Error'].includes(status)
}
function canResendDevice(status) {
  return ['Error', 'Waiting'].includes(status)
}
function canCancelDevice(status) {
  return ['Waiting', 'SendingToPlc', 'Running', 'Error'].includes(status)
}
function canCompleteDevice(status) {
  return ['Waiting', 'SendingToPlc', 'Running', 'Error'].includes(status)
}

let debounceTimer = null
function handleTasksChanged() {
  if (debounceTimer) clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => loadTasks(), 500)
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
function formatTime(val) { return val ? new Date(val).toLocaleString('zh-CN', { hour12: false }) : '—' }

onMounted(() => {
  loadTasks()
  joinGroup('view:tasks')
  on('TasksChanged', handleTasksChanged)
})

onUnmounted(() => {
  leaveGroup('view:tasks')
  off('TasksChanged', handleTasksChanged)
})
</script>

<style scoped>
.step-progress { font-family: var(--font-mono); font-size: 11px; color: var(--accent); }
.priority-high { color: var(--warning); font-weight: 700; }
.type-inbound { color: #4ade80; }
.type-outbound { color: #f97316; }
.type-transfer { color: #a78bfa; }
</style>

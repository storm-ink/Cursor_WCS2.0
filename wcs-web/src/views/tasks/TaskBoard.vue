<template>
  <div>
    <div class="sub-tabs">
      <router-link to="/tasks/board">任务看板</router-link>
      <router-link to="/tasks/current">当前任务</router-link>
      <router-link to="/tasks/history">历史任务</router-link>
      <router-link to="/tasks/create">手动下任务</router-link>
    </div>

    <div class="board-container">
      <div class="board-column">
        <div class="column-header pending">
          <span class="column-dot"></span>
          未执行 <span class="column-count">{{ pendingTasks.length }}</span>
        </div>
        <div class="column-body">
          <div v-for="task in pendingTasks" :key="task.id" class="board-card" @click="onSelectTask(task)">
            <div class="card-code">{{ task.taskCode }}</div>
            <div class="card-info">
              <span class="card-type" :class="'type-' + task.type?.toLowerCase()">{{ typeLabel(task.type) }}</span>
              <span class="card-source">{{ sourceLabel(task.source) }}</span>
            </div>
            <div class="card-route">{{ task.startLocationCode }} → {{ task.endLocationCode }}</div>
            <div class="card-meta">
              <span>托盘: {{ task.palletCode }}</span>
              <span>优先级: {{ task.priority }}</span>
            </div>
            <div class="card-time">{{ formatTime(task.createdAt) }}</div>
          </div>
          <div v-if="pendingTasks.length === 0" class="column-empty">暂无未执行任务</div>
        </div>
      </div>

      <div class="board-column">
        <div class="column-header running">
          <span class="column-dot"></span>
          执行中 <span class="column-count">{{ runningTasks.length }}</span>
        </div>
        <div class="column-body">
          <div v-for="task in runningTasks" :key="task.id" class="board-card" @click="onSelectTask(task)">
            <div class="card-code">{{ task.taskCode }}</div>
            <div class="card-info">
              <span class="card-type" :class="'type-' + task.type?.toLowerCase()">{{ typeLabel(task.type) }}</span>
              <el-tag type="primary" size="small">{{ statusLabel(task.status) }}</el-tag>
            </div>
            <div class="card-route">{{ task.startLocationCode }} → {{ task.endLocationCode }}</div>
            <div class="card-progress">
              <el-progress :percentage="Math.round((task.currentStepOrder / task.totalSteps) * 100)" :stroke-width="6" />
            </div>
            <div class="card-time">{{ formatTime(task.startedAt || task.createdAt) }}</div>
          </div>
          <div v-if="runningTasks.length === 0" class="column-empty">暂无执行中任务</div>
        </div>
      </div>

      <div class="board-column">
        <div class="column-header error">
          <span class="column-dot"></span>
          异常任务 <span class="column-count">{{ errorTasks.length }}</span>
        </div>
        <div class="column-body">
          <div v-for="task in errorTasks" :key="task.id" class="board-card card-error" @click="onSelectTask(task)">
            <div class="card-code">{{ task.taskCode }}</div>
            <div class="card-info">
              <span class="card-type" :class="'type-' + task.type?.toLowerCase()">{{ typeLabel(task.type) }}</span>
              <el-tag type="danger" size="small">{{ statusLabel(task.status) }}</el-tag>
            </div>
            <div class="card-route">{{ task.startLocationCode }} → {{ task.endLocationCode }}</div>
            <div class="card-error-msg" v-if="task.errorMessage">{{ task.errorMessage }}</div>
            <div class="card-meta">
              <span>步骤: {{ task.currentStepOrder }} / {{ task.totalSteps }}</span>
            </div>
            <div class="card-actions">
              <el-button size="small" type="warning" plain @click.stop="retryTask(task)">重试</el-button>
              <el-button size="small" type="danger" plain @click.stop="cancelTask(task)">取消</el-button>
            </div>
          </div>
          <div v-if="errorTasks.length === 0" class="column-empty">暂无异常任务</div>
        </div>
      </div>
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
        <el-table-column prop="errorMessage" label="错误信息" min-width="120" show-overflow-tooltip />
      </el-table>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { taskApi } from '../../api'
import { useSignalR } from '../../stores/signalr'
import { ElMessage, ElMessageBox } from 'element-plus'

const allTasks = ref([])
const selectedTask = ref(null)
const deviceTasks = ref([])
const loading = ref(false)
const detailLoading = ref(false)
const { joinGroup, leaveGroup, on, off } = useSignalR()

const pendingTasks = computed(() =>
  allTasks.value.filter(t => t.status === 'Created')
)
const runningTasks = computed(() =>
  allTasks.value.filter(t => ['SendingToPlc', 'Running'].includes(t.status))
)
const errorTasks = computed(() =>
  allTasks.value.filter(t => t.status === 'Error')
)

async function loadTasks() {
  loading.value = true
  try { allTasks.value = await taskApi.getCurrent(1, 200) } catch (e) { console.error(e) }
  finally { loading.value = false }
}

async function onSelectTask(task) {
  selectedTask.value = task
  detailLoading.value = true
  try { deviceTasks.value = await taskApi.getDeviceTasks(task.taskCode) } catch (e) { console.error(e) }
  finally { detailLoading.value = false }
}

async function retryTask(row) {
  try {
    await taskApi.retry(row.id)
    ElMessage.success('任务已重试')
    await loadTasks()
  } catch (e) { ElMessage.error(e.response?.data?.error || '重试失败') }
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

let debounceTimer = null
function handleTasksChanged() {
  if (debounceTimer) clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => loadTasks(), 500)
}

const statusLabel = s => ({ Created: '已创建', SendingToPlc: '下发中', Running: '运行中', Finished: '已完成', Error: '异常', Cancelled: '已取消' }[s] || s)
const sourceLabel = s => ({ Manual: '手动', Wms: 'WMS' }[s] || s)
const typeLabel = t => ({ Inbound: '入库', Outbound: '出库', Transfer: '移库' }[t] || t)
const deviceTypeLabel = t => ({ Conveyor: '输送线', Crane: '堆垛机' }[t] || t)
const deviceStatusLabel = s => ({ Waiting: '等待', SendingToPlc: '下发中', Running: '运行中', Finished: '已完成', Error: '异常' }[s] || s)
const deviceStatusTagType = s => ({ Waiting: 'info', SendingToPlc: 'warning', Running: 'primary', Finished: 'success', Error: 'danger' }[s] || 'info')
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
.board-container {
  display: grid;
  grid-template-columns: 1fr 1fr 1fr;
  gap: 16px;
  min-height: 400px;
}
.board-column {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 10px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.column-header {
  padding: 12px 16px;
  font-size: 13px;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 8px;
  border-bottom: 1px solid var(--border);
}
.column-header.pending { color: var(--text-secondary); }
.column-header.running { color: var(--accent); }
.column-header.error { color: var(--danger); }
.column-dot {
  width: 8px; height: 8px; border-radius: 50%;
}
.column-header.pending .column-dot { background: var(--text-muted); }
.column-header.running .column-dot { background: var(--accent); box-shadow: 0 0 6px var(--accent-glow); }
.column-header.error .column-dot { background: var(--danger); }
.column-count {
  font-size: 11px; font-weight: 400; color: var(--text-muted);
  margin-left: 4px;
}
.column-body {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.column-empty {
  text-align: center; color: var(--text-muted); padding: 40px 0; font-size: 12px;
}
.board-card {
  background: var(--bg-base);
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 12px;
  cursor: pointer;
  transition: border-color 0.15s;
}
.board-card:hover { border-color: var(--accent); }
.board-card.card-error { border-left: 3px solid var(--danger); }
.card-code { font-size: 12px; font-weight: 600; color: var(--accent); margin-bottom: 6px; font-family: var(--font-mono); }
.card-info { display: flex; align-items: center; gap: 6px; margin-bottom: 4px; }
.card-route { font-size: 11px; color: var(--text-secondary); margin-bottom: 4px; }
.card-meta { font-size: 11px; color: var(--text-muted); display: flex; gap: 12px; margin-bottom: 4px; }
.card-time { font-size: 10px; color: var(--text-muted); }
.card-error-msg { font-size: 11px; color: var(--danger); margin-bottom: 4px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.card-progress { margin-bottom: 4px; }
.card-actions { display: flex; gap: 6px; margin-top: 6px; }
.card-type { font-size: 11px; font-weight: 500; }
.type-inbound { color: #4ade80; }
.type-outbound { color: #f97316; }
.type-transfer { color: #a78bfa; }
</style>

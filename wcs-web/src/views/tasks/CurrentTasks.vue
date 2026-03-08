<template>
  <div>
    <div class="sub-tabs">
      <router-link to="/tasks/current">当前任务</router-link>
      <router-link to="/tasks/history">历史任务</router-link>
      <router-link to="/tasks/create">手动下任务</router-link>
    </div>

    <div class="panel">
      <div class="panel-title">当前任务列表</div>
      <el-table :data="tasks" stripe style="width: 100%" @row-click="onRowClick" highlight-current-row>
        <el-table-column prop="taskCode" label="任务编号" width="180" />
        <el-table-column prop="source" label="来源" width="80" />
        <el-table-column prop="type" label="类型" width="80" />
        <el-table-column prop="palletCode" label="托盘号" width="120" />
        <el-table-column prop="startLocationCode" label="起点" width="100" />
        <el-table-column prop="endLocationCode" label="终点" width="100" />
        <el-table-column prop="status" label="状态" width="120">
          <template #default="{ row }">
            <el-tag :type="statusTagType(row.status)" size="small">{{ row.status }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="currentStepOrder" label="当前步骤" width="90" />
        <el-table-column prop="totalSteps" label="总步骤" width="80" />
        <el-table-column prop="priority" label="优先级" width="80" />
        <el-table-column prop="createdAt" label="创建时间" width="170">
          <template #default="{ row }">
            {{ formatTime(row.createdAt) }}
          </template>
        </el-table-column>
      </el-table>
    </div>

    <div v-if="selectedTask" class="panel" style="margin-top: 16px;">
      <div class="panel-title">设备任务明细 - {{ selectedTask.taskCode }}</div>
      <el-table :data="deviceTasks" stripe style="width: 100%">
        <el-table-column prop="stepOrder" label="步骤" width="60" />
        <el-table-column prop="deviceType" label="设备类型" width="100" />
        <el-table-column prop="deviceCode" label="设备号" width="100" />
        <el-table-column prop="segmentSource" label="起点" width="100" />
        <el-table-column prop="segmentDest" label="终点" width="100" />
        <el-table-column prop="status" label="状态" width="120">
          <template #default="{ row }">
            <el-tag :type="deviceStatusTagType(row.status)" size="small">{{ row.status }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="sendCount" label="发送次数" width="90" />
        <el-table-column prop="createdAt" label="创建时间" width="170">
          <template #default="{ row }">
            {{ formatTime(row.createdAt) }}
          </template>
        </el-table-column>
        <el-table-column prop="errorMessage" label="错误信息" />
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
  try {
    tasks.value = await taskApi.getCurrent()
  } catch (e) {
    console.error('Failed to load tasks:', e)
  }
}

async function onRowClick(row) {
  selectedTask.value = row
  try {
    deviceTasks.value = await taskApi.getDeviceTasks(row.taskCode)
  } catch (e) {
    console.error('Failed to load device tasks:', e)
  }
}

function handleTaskUpdate(data) {
  if (Array.isArray(data)) {
    tasks.value = data
  } else {
    loadTasks()
  }
}

function statusTagType(status) {
  const map = { Created: 'info', SendingToPlc: 'warning', Running: '', Finished: 'success', Error: 'danger', Cancelled: 'info' }
  return map[status] || 'info'
}

function deviceStatusTagType(status) {
  const map = { Waiting: 'info', SendingToPlc: 'warning', Running: '', Finished: 'success', Error: 'danger' }
  return map[status] || 'info'
}

function formatTime(val) {
  if (!val) return '-'
  return new Date(val).toLocaleString('zh-CN')
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

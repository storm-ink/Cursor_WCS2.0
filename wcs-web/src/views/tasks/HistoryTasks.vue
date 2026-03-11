<template>
  <div>
    <div class="sub-tabs">
      <router-link to="/tasks/board">任务看板</router-link>
      <router-link to="/tasks/current">当前任务</router-link>
      <router-link to="/tasks/history">历史任务</router-link>
      <router-link to="/tasks/create">手动下任务</router-link>
    </div>

    <div class="panel">
      <div class="panel-title">历史任务查询</div>

      <div class="filter-bar">
        <el-form :inline="true" :model="filters">
          <el-form-item label="开始时间">
            <el-date-picker v-model="filters.startDate" type="datetime" placeholder="开始" value-format="YYYY-MM-DDTHH:mm:ss" format="YYYY-MM-DD HH:mm" />
          </el-form-item>
          <el-form-item label="结束时间">
            <el-date-picker v-model="filters.endDate" type="datetime" placeholder="结束" value-format="YYYY-MM-DDTHH:mm:ss" format="YYYY-MM-DD HH:mm" />
          </el-form-item>
          <el-form-item label="状态">
            <el-select v-model="filters.status" placeholder="全部" clearable style="width: 110px;">
              <el-option label="已完成" value="Finished" />
              <el-option label="已取消" value="Cancelled" />
            </el-select>
          </el-form-item>
          <el-form-item label="类型">
            <el-select v-model="filters.type" placeholder="全部" clearable style="width: 100px;">
              <el-option label="入库" value="Inbound" />
              <el-option label="出库" value="Outbound" />
              <el-option label="移库" value="Transfer" />
            </el-select>
          </el-form-item>
          <el-form-item>
            <el-button type="primary" @click="search">查 询</el-button>
          </el-form-item>
        </el-form>
      </div>

      <el-table :data="tasks" stripe @row-click="onRowClick" highlight-current-row
                v-loading="loading" element-loading-background="rgba(0,0,0,0.3)">
        <el-table-column prop="taskCode" label="任务编号" min-width="160" show-overflow-tooltip />
        <el-table-column prop="source" label="来源" min-width="70" align="center">
          <template #default="{ row }">{{ sourceLabel(row.source) }}</template>
        </el-table-column>
        <el-table-column prop="type" label="类型" min-width="70" align="center">
          <template #default="{ row }">{{ typeLabel(row.type) }}</template>
        </el-table-column>
        <el-table-column prop="palletCode" label="托盘号" min-width="100" show-overflow-tooltip />
        <el-table-column prop="startLocationCode" label="起点" min-width="80" align="center" />
        <el-table-column prop="endLocationCode" label="终点" min-width="80" align="center" />
        <el-table-column prop="status" label="状态" min-width="90" align="center">
          <template #default="{ row }">
            <el-tag :type="statusTagType(row.status)" size="small">{{ statusLabel(row.status) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="priority" label="优先级" min-width="70" align="center" />
        <el-table-column prop="createdAt" label="创建时间" min-width="140">
          <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
        </el-table-column>
        <el-table-column prop="finishedAt" label="完成时间" min-width="140">
          <template #default="{ row }">{{ formatTime(row.finishedAt) }}</template>
        </el-table-column>
        <el-table-column prop="errorMessage" label="错误信息" min-width="120" show-overflow-tooltip />
        <template #empty>
          <div style="padding: 40px 0; color: var(--text-muted);">暂无历史任务数据</div>
        </template>
      </el-table>

      <div class="pagination-bar">
        <el-pagination
          v-model:current-page="page"
          v-model:page-size="pageSize"
          :total="total"
          :page-sizes="[20, 50, 100]"
          layout="total, sizes, prev, pager, next"
          @size-change="search"
          @current-change="search"
        />
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
        <el-table-column prop="sendCount" label="发送次数" min-width="80" align="center" />
        <el-table-column prop="createdAt" label="创建时间" min-width="140">
          <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
        </el-table-column>
        <el-table-column prop="finishedAt" label="完成时间" min-width="140">
          <template #default="{ row }">{{ formatTime(row.finishedAt) }}</template>
        </el-table-column>
        <el-table-column prop="errorMessage" label="错误信息" min-width="120" show-overflow-tooltip />
        <template #empty>
          <div style="padding: 20px 0; color: var(--text-muted);">暂无设备任务</div>
        </template>
      </el-table>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { taskApi } from '../../api'

const tasks = ref([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)
const loading = ref(false)
const selectedTask = ref(null)
const deviceTasks = ref([])
const detailLoading = ref(false)
const filters = ref({ startDate: null, endDate: null, status: null, type: null })

async function search() {
  loading.value = true
  selectedTask.value = null
  deviceTasks.value = []
  try {
    const params = { page: page.value, pageSize: pageSize.value, ...filters.value }
    Object.keys(params).forEach(k => { if (params[k] === null || params[k] === '') delete params[k] })
    const result = await taskApi.getHistoryArchive(params)
    tasks.value = result.items
    total.value = result.total
  } catch (e) { console.error(e) }
  finally { loading.value = false }
}

async function onRowClick(row) {
  selectedTask.value = row
  detailLoading.value = true
  try {
    // 先尝试历史库，再回退到当前库
    deviceTasks.value = await taskApi.getHistoryDeviceTasks(row.taskCode)
    if (!deviceTasks.value || deviceTasks.value.length === 0) {
      deviceTasks.value = await taskApi.getDeviceTasks(row.taskCode)
    }
  } catch (e) { console.error(e) }
  finally { detailLoading.value = false }
}

const statusMap = { Created: '已创建', SendingToPlc: '下发中', Running: '运行中', Finished: '已完成', Error: '异常', Cancelled: '已取消' }
const statusLabel = s => statusMap[s] || s
const statusTagType = s => ({ Created: 'info', SendingToPlc: 'warning', Running: 'primary', Finished: 'success', Error: 'danger', Cancelled: 'info' }[s] || 'info')
const sourceLabel = s => ({ Manual: '手动', Wms: 'WMS' }[s] || s)
const typeLabel = t => ({ Inbound: '入库', Outbound: '出库', Transfer: '移库' }[t] || t)
const deviceTypeLabel = t => ({ Conveyor: '输送线', Crane: '堆垛机' }[t] || t)
const deviceStatusLabel = s => ({ Waiting: '等待', SendingToPlc: '下发中', Running: '运行中', Finished: '已完成', Error: '异常' }[s] || s)
const deviceStatusTagType = s => ({ Waiting: 'info', SendingToPlc: 'warning', Running: 'primary', Finished: 'success', Error: 'danger' }[s] || 'info')

function formatTime(val) {
  if (!val) return '—'
  return new Date(val).toLocaleString('zh-CN', { hour12: false })
}

onMounted(() => search())
</script>

<style scoped>
.filter-bar {
  margin-bottom: 16px;
  padding-bottom: 16px;
  border-bottom: 1px solid var(--border);
}
.pagination-bar {
  margin-top: 16px;
  padding-top: 12px;
  border-top: 1px solid var(--border);
  display: flex;
  justify-content: flex-end;
}
</style>

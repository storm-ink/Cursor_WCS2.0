<template>
  <div>
    <div class="sub-tabs">
      <router-link to="/tasks/current">当前任务</router-link>
      <router-link to="/tasks/history">历史任务</router-link>
      <router-link to="/tasks/create">手动下任务</router-link>
    </div>

    <div class="panel">
      <div class="panel-title">历史任务查询</div>
      <el-form :inline="true" :model="filters" style="margin-bottom: 16px;">
        <el-form-item label="开始日期">
          <el-date-picker v-model="filters.startDate" type="date" placeholder="选择开始日期" value-format="YYYY-MM-DD" />
        </el-form-item>
        <el-form-item label="结束日期">
          <el-date-picker v-model="filters.endDate" type="date" placeholder="选择结束日期" value-format="YYYY-MM-DD" />
        </el-form-item>
        <el-form-item label="状态">
          <el-select v-model="filters.status" placeholder="全部" clearable style="width: 130px;">
            <el-option label="已创建" value="Created" />
            <el-option label="发送中" value="SendingToPlc" />
            <el-option label="运行中" value="Running" />
            <el-option label="已完成" value="Finished" />
            <el-option label="错误" value="Error" />
            <el-option label="已取消" value="Cancelled" />
          </el-select>
        </el-form-item>
        <el-form-item label="类型">
          <el-select v-model="filters.type" placeholder="全部" clearable style="width: 120px;">
            <el-option label="入库" value="Inbound" />
            <el-option label="出库" value="Outbound" />
            <el-option label="移库" value="Transfer" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="search">查询</el-button>
        </el-form-item>
      </el-form>

      <el-table :data="tasks" stripe style="width: 100%">
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
        <el-table-column prop="priority" label="优先级" width="80" />
        <el-table-column prop="createdAt" label="创建时间" width="170">
          <template #default="{ row }">
            {{ formatTime(row.createdAt) }}
          </template>
        </el-table-column>
        <el-table-column prop="finishedAt" label="完成时间" width="170">
          <template #default="{ row }">
            {{ formatTime(row.finishedAt) }}
          </template>
        </el-table-column>
        <el-table-column prop="errorMessage" label="错误信息" />
      </el-table>

      <div style="margin-top: 16px; display: flex; justify-content: flex-end;">
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
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { taskApi } from '../../api'

const tasks = ref([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)
const filters = ref({
  startDate: null,
  endDate: null,
  status: null,
  type: null
})

async function search() {
  try {
    const params = {
      page: page.value,
      pageSize: pageSize.value,
      ...filters.value
    }
    Object.keys(params).forEach(k => {
      if (params[k] === null || params[k] === '') delete params[k]
    })
    const result = await taskApi.getHistory(params)
    tasks.value = result.items
    total.value = result.total
  } catch (e) {
    console.error('Failed to load history:', e)
  }
}

function statusTagType(status) {
  const map = { Created: 'info', SendingToPlc: 'warning', Running: '', Finished: 'success', Error: 'danger', Cancelled: 'info' }
  return map[status] || 'info'
}

function formatTime(val) {
  if (!val) return '-'
  return new Date(val).toLocaleString('zh-CN')
}

onMounted(() => search())
</script>

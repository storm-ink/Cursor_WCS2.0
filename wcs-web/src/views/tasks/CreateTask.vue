<template>
  <div>
    <div class="sub-tabs">
      <router-link to="/tasks/current">当前任务</router-link>
      <router-link to="/tasks/history">历史任务</router-link>
      <router-link to="/tasks/create">手动下任务</router-link>
    </div>

    <div class="panel create-panel">
      <div class="panel-title">手动创建任务</div>
      <el-form :model="form" label-width="90px" label-position="left" @submit.prevent="submitTask">
        <div class="form-grid">
          <el-form-item label="任务编号">
            <el-input v-model="form.taskCode" placeholder="留空自动生成" />
          </el-form-item>
          <el-form-item label="任务类型">
            <el-select v-model="form.type" style="width: 100%;">
              <el-option label="入库" value="Inbound" />
              <el-option label="出库" value="Outbound" />
              <el-option label="移库" value="Transfer" />
            </el-select>
          </el-form-item>
          <el-form-item label="托盘号">
            <el-input v-model="form.palletCode" placeholder="请输入托盘号" />
          </el-form-item>
          <el-form-item label="优先级">
            <el-input-number v-model="form.priority" :min="0" :max="99" style="width: 100%;" />
          </el-form-item>
          <el-form-item label="起点位置">
            <el-input v-model="form.startLocationCode" placeholder="如 1001" />
          </el-form-item>
          <el-form-item label="终点位置">
            <el-input v-model="form.endLocationCode" placeholder="如 01-02-03" />
          </el-form-item>
        </div>
        <el-form-item label="备注">
          <el-input v-model="form.description" type="textarea" :rows="2" placeholder="选填" />
        </el-form-item>
        <div class="form-actions">
          <el-button type="primary" @click="submitTask" :loading="loading">创建任务</el-button>
          <el-button @click="resetForm">重置</el-button>
        </div>
      </el-form>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { taskApi } from '../../api'
import { ElMessage } from 'element-plus'

const loading = ref(false)
const form = ref({
  taskCode: '', type: 'Inbound', palletCode: '',
  startLocationCode: '', endLocationCode: '', priority: 0, description: ''
})

async function submitTask() {
  if (!form.value.palletCode || !form.value.startLocationCode || !form.value.endLocationCode) {
    ElMessage.warning('请填写托盘号、起点和终点')
    return
  }
  loading.value = true
  try {
    const payload = { ...form.value, source: 'Manual' }
    if (!payload.taskCode) delete payload.taskCode
    await taskApi.create(payload)
    ElMessage.success('任务创建成功')
    resetForm()
  } catch (e) {
    ElMessage.error(e.response?.data?.error || '创建失败')
  } finally {
    loading.value = false
  }
}

function resetForm() {
  form.value = {
    taskCode: '', type: 'Inbound', palletCode: '',
    startLocationCode: '', endLocationCode: '', priority: 0, description: ''
  }
}
</script>

<style scoped>
.create-panel { max-width: 680px; }
.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0 24px;
}
.form-actions {
  padding-top: 12px;
  border-top: 1px solid var(--border);
  margin-top: 8px;
  display: flex;
  gap: 10px;
}
</style>

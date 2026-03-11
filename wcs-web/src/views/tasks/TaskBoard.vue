<template>
  <div>
    <div class="sub-tabs">
      <router-link to="/tasks/taskboard">任务看板</router-link>
      <router-link to="/tasks/current">任务更改</router-link>
      <router-link to="/tasks/history">历史任务</router-link>
      <router-link to="/tasks/create">手动任务</router-link>
      <router-link to="/tasks/changeTaskMode">切换模式</router-link>
    </div>

    <div class="container">
       
         <div class="item">
            <!-- <div class="item-title" >未执行</div> -->
                <el-table :data="tasks"  @row-click="onRowClick" 
                        v-loading="loading" element-loading-background="rgba(0,1.0,0.6,0.6)" >
                <el-table-column prop="taskCode" label="未执行任务编号" min-width="160" show-overflow-tooltip />
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
                
                <template #empty>
                    <div style="padding: 40px 0; color: var(--text-muted);">暂无进行中的任务</div>
                </template>
                </el-table>
        </div>

         <div class="item">
                <el-table :data="tasks"  @row-click="onRowClick" 
                        v-loading="loading" element-loading-background="rgba(0,1.0,0.6,0.6)">
                <el-table-column prop="taskCode" label="执行中任务编号" min-width="160" show-overflow-tooltip />
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
                
                <template #empty>
                    <div style="padding: 40px 0; color: var(--text-muted);">暂无进行中的任务</div>
                </template>
                </el-table>
        </div>
         <div class="item">
                <el-table :data="tasks"  @row-click="onRowClick" 
                        v-loading="loading" element-loading-background="rgba(0,1.0,0.6,0.6)">
                <el-table-column prop="taskCode" label="异常任务编号" min-width="160" show-overflow-tooltip />
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
                
                <template #empty>
                    <div style="padding: 40px 0; color: var(--text-muted);">暂无进行中的任务</div>
                </template>
                </el-table>
        </div>
        
    </div>

   
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import { taskApi } from '../../api'
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
  
  if (selectedTask.value && selectedTask.value.taskCode === row.taskCode) {
    // 清空选中状态，这会触发 panel-detail 的 v-if 变为 false，从而收起
    selectedTask.value = null;
     detailLoading.value = false;
     deviceTasks.value = [];
    // 不需要继续执行后面的加载逻辑了
    return;
  }

  
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

function canCancel(status) {
  return ['Created', 'SendingToPlc', 'Running', 'Error'].includes(status)
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



<style scoped>
/* 定义过渡动画的属性 */
.slide-up-enter-active,
.slide-up-leave-active {
  transition: all 0.3s ease-out; /* 0.3秒的动画时长，ease-out 让结束更柔和 */
  transform: translateY(0); /* 最终状态：在正常位置 */
}

/* 定义进入时的初始状态 (离开时的结束状态) */
.slide-up-enter-from,
.slide-up-leave-to {
  transform: translateY(100%); /* 初始状态：在屏幕底部以下 (隐藏) */
  opacity: 0; /* 同时可以加一个透明度变化 */
}

/* 基础样式：确保 div 固定在底部 */
.panel-detail {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  /* 其他样式如 background, margin 等 */
  background: #1a2d4d;
  margin: 10px;
  border-radius: 8px 8px 0 0;
  box-shadow: 0 -2px 10px rgba(0,0,0,0.1);
   z-index: 9999; 
  /* 注意：不要在这里写 transition，要写在上面的 enter/leave 类里 */
}

.container {
  display: grid;
  grid-template-columns: 1fr 1fr 1fr; /* 将容器分为三列，每列宽度相等 */
  /* 或者使用简写： grid-template-columns: repeat(3, 1fr); */
}


.item {
  flex: 1; /* 关键：占据等分的剩余空间 */
  /* 以下是为了防止表格溢出出现横向滚动条 */
  min-width: 0; /* 关键：防止内容撑开导致 flex 失效 */
  margin: 0 5px; /* 可选：增加一点间距 */
  display: flex;
  flex-direction: column; /* 如果你希望标题和表格在内部也是垂直排列 */

   overflow: auto; 
    &::-webkit-scrollbar {
    width: 0;  /* 隐藏滚动条宽度 */
    height: 0; /* 隐藏底部滚动条高度 */
  }
}


/* 第一个 .item */
.item:nth-child(1) {
  background-color:  #1c3560; /* 浅黄 */
}

/* 第二个 .item */
.item:nth-child(2) {
  background-color:  #102a58; /* 浅蓝 */
}

/* 第三个 .item */
.item:nth-child(3) {
  background-color:  #071b3d; /* 浅绿 */
}

.item-title {
  /* 1. 调整字体大小 */
  font-size: 20px;     /* 你可以根据需要修改数值，例如 14px, 18px 等 */
  font-weight: 600;    /* 加粗，可选 */

  /* 2. 水平居中 */
  text-align: center;  


  /* 4. 容器基础样式 (可选) */
  height: 40px;        /* 设置一个固定高度，以便垂直居中效果明显 */
}
</style>
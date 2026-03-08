<template>
  <div>
    <div class="panel">
      <div class="panel-title">实时日志</div>

      <div style="margin-bottom: 12px; display: flex; gap: 12px; align-items: center;">
        <el-select v-model="filterCategory" placeholder="类型" clearable style="width: 120px;" size="small">
          <el-option label="全部" value="" />
          <el-option label="Api" value="Api" />
          <el-option label="Plc" value="Plc" />
          <el-option label="Task" value="Task" />
          <el-option label="Device" value="Device" />
          <el-option label="System" value="System" />
        </el-select>
        <el-select v-model="filterLevel" placeholder="级别" clearable style="width: 120px;" size="small">
          <el-option label="全部" value="" />
          <el-option label="Information" value="Information" />
          <el-option label="Warning" value="Warning" />
          <el-option label="Error" value="Error" />
          <el-option label="Debug" value="Debug" />
        </el-select>
        <el-button size="small" @click="clearLogs">清空</el-button>
        <el-checkbox v-model="autoScroll" size="small">自动滚动</el-checkbox>
        <span style="color: #556677; font-size: 12px; margin-left: auto;">
          共 {{ filteredLogs.length }} 条
        </span>
      </div>

      <div class="log-list" ref="logListRef">
        <div v-for="(log, idx) in filteredLogs" :key="idx" class="log-entry" :class="'level-' + log.level?.toLowerCase()">
          <span class="log-time">{{ log.timestamp }}</span>
          <span class="log-level">{{ log.level }}</span>
          <span class="log-category">[{{ log.category }}]</span>
          <span class="log-message">{{ log.message }}</span>
          <span v-if="log.exception" class="log-exception">{{ log.exception }}</span>
        </div>
        <div v-if="filteredLogs.length === 0" style="color: #556677; text-align: center; padding: 40px;">
          等待日志数据...
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, nextTick, watch } from 'vue'
import { useSignalR } from '../stores/signalr'

const logs = ref([])
const filterCategory = ref('')
const filterLevel = ref('')
const autoScroll = ref(true)
const logListRef = ref(null)
const { joinGroup, leaveGroup, on, off } = useSignalR()
const MAX_LOGS = 500

const filteredLogs = computed(() => {
  return logs.value.filter(log => {
    if (filterCategory.value && log.category !== filterCategory.value) return false
    if (filterLevel.value && log.level !== filterLevel.value) return false
    return true
  })
})

function handleLog(data) {
  logs.value.push(data)
  if (logs.value.length > MAX_LOGS) {
    logs.value = logs.value.slice(-MAX_LOGS)
  }
  if (autoScroll.value) {
    nextTick(() => {
      const el = logListRef.value
      if (el) el.scrollTop = el.scrollHeight
    })
  }
}

function clearLogs() {
  logs.value = []
}

watch(filteredLogs, () => {
  if (autoScroll.value) {
    nextTick(() => {
      const el = logListRef.value
      if (el) el.scrollTop = el.scrollHeight
    })
  }
})

onMounted(() => {
  joinGroup('view:logs')
  on('LogReceived', handleLog)
})

onUnmounted(() => {
  leaveGroup('view:logs')
  off('LogReceived', handleLog)
})
</script>

<style scoped>
.log-list {
  max-height: calc(100vh - 240px);
  overflow-y: auto;
  font-family: 'Courier New', monospace;
  font-size: 12px;
  background: #080e1a;
  border-radius: 6px;
  padding: 8px;
}

.log-entry {
  padding: 3px 6px;
  display: flex;
  gap: 8px;
  align-items: flex-start;
  border-bottom: 1px solid #0d1520;
}

.log-entry:hover {
  background: rgba(79, 195, 247, 0.05);
}

.log-time {
  color: #556677;
  white-space: nowrap;
  flex-shrink: 0;
}

.log-level {
  white-space: nowrap;
  flex-shrink: 0;
  width: 80px;
}

.level-information .log-level { color: #4fc3f7; }
.level-warning .log-level { color: #ffa726; }
.level-error .log-level { color: #ef5350; }
.level-debug .log-level { color: #78909c; }

.log-category {
  color: #8899aa;
  white-space: nowrap;
  flex-shrink: 0;
}

.log-message {
  color: #c0ccd8;
  word-break: break-all;
}

.log-exception {
  color: #ef5350;
  word-break: break-all;
}
</style>

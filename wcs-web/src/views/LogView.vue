<template>
  <div>
    <div class="panel">
      <div class="panel-title">实时日志</div>

      <div class="log-toolbar">
        <el-select v-model="filterCategory" placeholder="类型" clearable size="small" style="width: 100px;">
          <el-option label="全部" value="" />
          <el-option label="Api" value="Api" />
          <el-option label="Plc" value="Plc" />
          <el-option label="Task" value="Task" />
          <el-option label="Device" value="Device" />
          <el-option label="System" value="System" />
        </el-select>
        <el-select v-model="filterLevel" placeholder="级别" clearable size="small" style="width: 110px;">
          <el-option label="全部" value="" />
          <el-option label="Information" value="Information" />
          <el-option label="Warning" value="Warning" />
          <el-option label="Error" value="Error" />
          <el-option label="Debug" value="Debug" />
        </el-select>
        <el-button size="small" @click="clearLogs">清空</el-button>
        <el-checkbox v-model="autoScroll" size="small">自动滚动</el-checkbox>
        <span class="log-count">{{ filteredLogs.length }} 条</span>
      </div>

      <div class="log-list" ref="logListRef">
        <div v-for="(log, idx) in filteredLogs" :key="idx"
             class="log-row" :class="'log-' + log.level?.toLowerCase()">
          <span class="log-time">{{ log.timestamp }}</span>
          <span class="log-level">{{ levelShort(log.level) }}</span>
          <span class="log-category">{{ log.category }}</span>
          <span class="log-msg">{{ log.message }}</span>
          <span v-if="log.exception" class="log-exc">{{ log.exception }}</span>
        </div>
        <div v-if="filteredLogs.length === 0" class="log-empty">等待日志数据...</div>
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

const filteredLogs = computed(() => {
  return logs.value.filter(log => {
    if (filterCategory.value && log.category !== filterCategory.value) return false
    if (filterLevel.value && log.level !== filterLevel.value) return false
    return true
  })
})

const levelShort = l => ({ Information: 'INF', Warning: 'WRN', Error: 'ERR', Debug: 'DBG' }[l] || l)

function handleLog(data) {
  logs.value.push(data)
  if (logs.value.length > 500) logs.value = logs.value.slice(-500)
}

function clearLogs() { logs.value = [] }

watch(filteredLogs, () => {
  if (autoScroll.value) {
    nextTick(() => {
      const el = logListRef.value
      if (el) el.scrollTop = el.scrollHeight
    })
  }
})

onMounted(() => { joinGroup('view:logs'); on('LogReceived', handleLog) })
onUnmounted(() => { leaveGroup('view:logs'); off('LogReceived', handleLog) })
</script>

<style scoped>
.log-toolbar {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 12px;
  padding-bottom: 12px;
  border-bottom: 1px solid var(--border);
}

.log-count {
  margin-left: auto;
  font-size: 11px;
  color: var(--text-muted);
  font-family: var(--font-mono);
}

.log-list {
  height: calc(100vh - 240px);
  overflow-y: auto;
  font-family: var(--font-mono);
  font-size: 11px;
  background: var(--bg-deep);
  border-radius: 6px;
  padding: 4px;
}

.log-row {
  padding: 3px 8px;
  display: flex;
  gap: 10px;
  align-items: baseline;
  border-radius: 3px;
  line-height: 1.8;
}
.log-row:hover { background: rgba(255,255,255,0.015); }

.log-time { color: var(--text-muted); white-space: nowrap; flex-shrink: 0; }

.log-level {
  font-weight: 700;
  width: 30px;
  text-align: center;
  flex-shrink: 0;
  font-size: 10px;
}
.log-information .log-level { color: #3b9eff; }
.log-warning .log-level { color: #f59e0b; }
.log-error .log-level { color: #ef4444; }
.log-debug .log-level { color: #6b7280; }

.log-warning { background: rgba(245, 158, 11, 0.04); }
.log-error { background: rgba(239, 68, 68, 0.06); }

.log-category {
  color: var(--text-muted);
  white-space: nowrap;
  flex-shrink: 0;
  min-width: 48px;
}

.log-msg { color: var(--text-secondary); word-break: break-all; }
.log-exc { color: var(--danger); word-break: break-all; }

.log-empty { color: var(--text-muted); text-align: center; padding: 60px 0; }
</style>

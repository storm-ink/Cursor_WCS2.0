<template>
  <div class="app-container">
    <header class="app-header">
      <div class="logo">
        WCS
        <span class="logo-sub">智能仓储控制系统</span>
      </div>
      <nav class="app-nav">
        <router-link to="/monitor3d">
          <el-icon><Monitor /></el-icon>
          实时监控
        </router-link>
        <router-link to="/tasks" :class="{ active: $route.path.startsWith('/tasks') }">
          <el-icon><List /></el-icon>
          任务监控
        </router-link>
        <router-link to="/devices">
          <el-icon><SetUp /></el-icon>
          设备管理
        </router-link>
        <router-link to="/logs">
          <el-icon><Document /></el-icon>
          日志
        </router-link>
      </nav>
      <div class="connection-status">
        <span class="dot" :class="{ connected }"></span>
        {{ connected ? '已连接' : '未连接' }}
      </div>
    </header>
    <main class="app-main">
      <router-view />
    </main>
  </div>
</template>

<script setup>
import { onMounted } from 'vue'
import { useSignalR } from './stores/signalr'

const { connected, start } = useSignalR()

onMounted(() => {
  start()
})
</script>

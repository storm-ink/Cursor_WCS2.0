<template>
  <div class="app-container" v-if="isLoggedIn">
    <header class="app-header">
      <div class="logo">
        WCS
      </div>
      <nav class="app-nav">
        <router-link to="/tasks" :class="{ active: $route.path.startsWith('/tasks') }">
          <el-icon><List /></el-icon>
          任务
        </router-link>
        <router-link v-if="isAdmin || isUser" to="/devices">
          <el-icon><SetUp /></el-icon>
          设备
        </router-link>
        <router-link v-if="isAdmin" to="/logs">
          <el-icon><Document /></el-icon>
          日志
        </router-link>
        <router-link v-if="isAdmin" to="/monitor3d">
          <el-icon><Monitor /></el-icon>
          实时监控
        </router-link>
        <router-link v-if="isAdmin" to="/admin/users">
          <el-icon><UserFilled /></el-icon>
          用户管理
        </router-link>
      </nav>
      <div class="header-right">
        <div class="connection-status">
          <span class="dot" :class="{ connected }"></span>
          {{ connected ? '连接' : '断连' }}
        </div>
        <div class="user-info">
          <el-icon><User /></el-icon>
          <span class="user-name">{{ username }}</span>
          <el-tag :type="roleTagType" size="small" class="role-tag">{{ roleLabel }}</el-tag>
          <el-button
            v-if="isUser"
            size="small"
            class="pwd-btn"
            @click="showChangePassword = true"
          >
            改密码
          </el-button>
          <el-button
            size="small"
            class="logout-btn"
            @click="handleLogout"
          >
            <el-icon><SwitchButton /></el-icon>
            退出
          </el-button>
        </div>
      </div>
    </header>
    <main class="app-main">
      <router-view />
    </main>

    <!-- Change Password Dialog (for user role) -->
    <el-dialog
      v-model="showChangePassword"
      title="修改密码"
      width="360px"
      :close-on-click-modal="false"
    >
      <el-form ref="pwdFormRef" :model="pwdForm" :rules="pwdRules" label-width="80px">
        <el-form-item label="原密码" prop="oldPassword">
          <el-input v-model="pwdForm.oldPassword" type="password" show-password />
        </el-form-item>
        <el-form-item label="新密码" prop="newPassword">
          <el-input v-model="pwdForm.newPassword" type="password" show-password />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showChangePassword = false">取消</el-button>
        <el-button type="primary" :loading="changingPwd" @click="handleChangePwd">确认修改</el-button>
      </template>
    </el-dialog>
  </div>

  <!-- Show login page via router-view when not logged in -->
  <router-view v-else />
</template>

<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useSignalR } from './stores/signalr'
import { useAuth } from './stores/auth'
import { userApi } from './api/index'

const { connected, start } = useSignalR()
const { isLoggedIn, isAdmin, isUser, username, role, clearAuth } = useAuth()

const router = useRouter()

const showChangePassword = ref(false)
const changingPwd = ref(false)
const pwdFormRef = ref(null)
const pwdForm = reactive({ oldPassword: '', newPassword: '' })
const pwdRules = {
  oldPassword: [{ required: true, message: '请输入原密码', trigger: 'blur' }],
  newPassword: [{ required: true, message: '请输入新密码', trigger: 'blur' }, { min: 6, message: '密码至少6位', trigger: 'blur' }]
}

const roleTagType = computed(() => {
  if (role.value === 'admin') return 'danger'
  if (role.value === 'user') return 'primary'
  return 'info'
})

const roleLabel = computed(() => {
  if (role.value === 'admin') return '管理员'
  if (role.value === 'user') return '操作员'
  return '游客'
})

onMounted(() => {
  if (isLoggedIn.value) start()
})

function handleLogout() {
  clearAuth()
  router.replace('/login')
  ElMessage.success('已退出登录')
}

async function handleChangePwd() {
  const valid = await pwdFormRef.value.validate().catch(() => false)
  if (!valid) return
  changingPwd.value = true
  try {
    await userApi.changeMyPassword(pwdForm.oldPassword, pwdForm.newPassword)
    ElMessage.success('密码修改成功')
    showChangePassword.value = false
    pwdForm.oldPassword = ''
    pwdForm.newPassword = ''
  } catch (err) {
    ElMessage.error(err.response?.data?.error || '修改失败')
  } finally {
    changingPwd.value = false
  }
}
</script>

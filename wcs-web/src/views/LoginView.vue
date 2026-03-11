<template>
  <div class="login-page">
    <div class="login-card">
      <div class="login-logo">
        <span class="logo-text">WCS</span>
        <span class="logo-sub">智能仓储控制系统</span>
      </div>

      <el-form
        ref="formRef"
        :model="form"
        :rules="rules"
        class="login-form"
        @keyup.enter="handleLogin"
      >
        <el-form-item prop="username">
          <el-input
            v-model="form.username"
            placeholder="用户名"
            prefix-icon="User"
            size="large"
            clearable
          />
        </el-form-item>
        <el-form-item prop="password">
          <el-input
            v-model="form.password"
            type="password"
            placeholder="密码"
            prefix-icon="Lock"
            size="large"
            show-password
          />
        </el-form-item>
        <el-form-item>
          <el-button
            type="primary"
            size="large"
            class="login-btn"
            :loading="loading"
            @click="handleLogin"
          >
            登 录
          </el-button>
        </el-form-item>
        <div class="guest-divider">
          <span>或</span>
        </div>
        <el-button
          size="large"
          class="guest-btn"
          :loading="guestLoading"
          @click="handleGuest"
        >
          <el-icon><View /></el-icon>
          游客模式
        </el-button>
      </el-form>

      <div class="login-hint">
        <el-icon><InfoFilled /></el-icon>
        游客模式仅可查看任务看板
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { authApi } from '../api/index'
import { useAuth } from '../stores/auth'

const router = useRouter()
const { setAuth } = useAuth()

const formRef = ref(null)
const loading = ref(false)
const guestLoading = ref(false)

const form = reactive({
  username: '',
  password: ''
})

const rules = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }]
}

async function handleLogin() {
  if (!formRef.value) return
  const valid = await formRef.value.validate().catch(() => false)
  if (!valid) return

  loading.value = true
  try {
    const data = await authApi.login(form.username, form.password)
    setAuth(data)
    ElMessage.success(`欢迎，${data.username}！`)
    router.replace(getDefaultRoute())
  } catch (err) {
    ElMessage.error(err.response?.data?.error || '登录失败')
  } finally {
    loading.value = false
  }
}

async function handleGuest() {
  guestLoading.value = true
  try {
    const data = await authApi.guest()
    setAuth(data)
    router.replace('/tasks/taskboard')
  } catch (err) {
    ElMessage.error('游客登录失败')
  } finally {
    guestLoading.value = false
  }
}

function getDefaultRoute() {
  return '/tasks/taskboard'
}
</script>

<style scoped>
.login-page {
  width: 100%;
  height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--bg-deep);
  background-image:
    radial-gradient(ellipse at 20% 50%, rgba(59,158,255,0.06) 0%, transparent 60%),
    radial-gradient(ellipse at 80% 20%, rgba(59,158,255,0.04) 0%, transparent 50%);
}

.login-card {
  width: 380px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 16px;
  padding: 40px 36px 32px;
  box-shadow: 0 20px 60px rgba(0,0,0,0.5);
}

.login-logo {
  text-align: center;
  margin-bottom: 32px;
}

.logo-text {
  display: block;
  font-size: 36px;
  font-weight: 800;
  color: var(--accent);
  letter-spacing: 4px;
  margin-bottom: 6px;
}

.logo-sub {
  font-size: 13px;
  color: var(--text-muted);
  letter-spacing: 1px;
}

.login-form {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.login-btn {
  width: 100%;
  height: 42px;
  font-size: 15px;
  font-weight: 600;
  letter-spacing: 2px;
}

.guest-divider {
  text-align: center;
  color: var(--text-muted);
  font-size: 12px;
  position: relative;
  margin: 4px 0;
}

.guest-divider::before,
.guest-divider::after {
  content: '';
  position: absolute;
  top: 50%;
  width: 42%;
  height: 1px;
  background: var(--border);
}

.guest-divider::before { left: 0; }
.guest-divider::after { right: 0; }

.guest-btn {
  width: 100%;
  height: 42px;
  font-size: 13px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
}

.login-hint {
  margin-top: 24px;
  text-align: center;
  font-size: 11px;
  color: var(--text-muted);
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
}
</style>

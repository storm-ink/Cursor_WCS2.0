<template>
  <div class="panel">
    <div class="panel-title">
      <el-icon><UserFilled /></el-icon>
      用户管理
      <el-button
        type="primary"
        size="small"
        style="margin-left: auto"
        @click="showCreate = true"
      >
        <el-icon><Plus /></el-icon>
        新增用户
      </el-button>
    </div>

    <el-table :data="users" stripe style="width:100%">
      <el-table-column prop="id" label="ID" width="60" />
      <el-table-column prop="username" label="用户名" />
      <el-table-column prop="role" label="角色" width="100">
        <template #default="{ row }">
          <el-tag :type="roleTagType(row.role)" size="small">{{ roleLabel(row.role) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="createdAt" label="创建时间" width="180">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="100">
        <template #default="{ row }">
          <el-button
            type="danger"
            size="small"
            :disabled="row.username === currentUsername"
            @click="handleDelete(row)"
          >
            删除
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- Create User Dialog -->
    <el-dialog
      v-model="showCreate"
      title="新增用户"
      width="380px"
      :close-on-click-modal="false"
    >
      <el-form ref="createFormRef" :model="createForm" :rules="createRules" label-width="70px">
        <el-form-item label="用户名" prop="username">
          <el-input v-model="createForm.username" placeholder="请输入用户名" />
        </el-form-item>
        <el-form-item label="密码" prop="password">
          <el-input v-model="createForm.password" type="password" show-password placeholder="请输入密码" />
        </el-form-item>
        <el-form-item label="角色" prop="role">
          <el-select v-model="createForm.role" style="width:100%">
            <el-option label="管理员 (admin)" value="admin" />
            <el-option label="操作员 (user)" value="user" />
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showCreate = false">取消</el-button>
        <el-button type="primary" :loading="creating" @click="handleCreate">确认创建</el-button>
      </template>
    </el-dialog>

    <!-- Change Password Dialog -->
    <el-dialog
      v-model="showPassword"
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
        <el-button @click="showPassword = false">取消</el-button>
        <el-button type="primary" :loading="changingPwd" @click="handleChangePwd">确认修改</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { userApi } from '../../api/index'
import { useAuth } from '../../stores/auth'

const { username: currentUsername } = useAuth()

const users = ref([])
const showCreate = ref(false)
const showPassword = ref(false)
const creating = ref(false)
const changingPwd = ref(false)

const createFormRef = ref(null)
const createForm = reactive({ username: '', password: '', role: 'user' })
const createRules = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }, { min: 6, message: '密码至少6位', trigger: 'blur' }],
  role: [{ required: true, message: '请选择角色', trigger: 'change' }]
}

const pwdFormRef = ref(null)
const pwdForm = reactive({ oldPassword: '', newPassword: '' })
const pwdRules = {
  oldPassword: [{ required: true, message: '请输入原密码', trigger: 'blur' }],
  newPassword: [{ required: true, message: '请输入新密码', trigger: 'blur' }, { min: 6, message: '密码至少6位', trigger: 'blur' }]
}

onMounted(loadUsers)

async function loadUsers() {
  try {
    users.value = await userApi.getAll()
  } catch {
    ElMessage.error('加载用户列表失败')
  }
}

async function handleCreate() {
  const valid = await createFormRef.value.validate().catch(() => false)
  if (!valid) return
  creating.value = true
  try {
    await userApi.create(createForm.username, createForm.password, createForm.role)
    ElMessage.success('用户创建成功')
    showCreate.value = false
    createForm.username = ''
    createForm.password = ''
    createForm.role = 'user'
    loadUsers()
  } catch (err) {
    ElMessage.error(err.response?.data?.error || '创建失败')
  } finally {
    creating.value = false
  }
}

async function handleDelete(row) {
  try {
    await ElMessageBox.confirm(`确认删除用户 "${row.username}"？`, '删除确认', {
      type: 'warning',
      confirmButtonText: '删除',
      cancelButtonText: '取消'
    })
    await userApi.remove(row.id)
    ElMessage.success('删除成功')
    loadUsers()
  } catch (err) {
    if (err !== 'cancel') ElMessage.error(err.response?.data?.error || '删除失败')
  }
}

async function handleChangePwd() {
  const valid = await pwdFormRef.value.validate().catch(() => false)
  if (!valid) return
  changingPwd.value = true
  try {
    await userApi.changeMyPassword(pwdForm.oldPassword, pwdForm.newPassword)
    ElMessage.success('密码修改成功')
    showPassword.value = false
    pwdForm.oldPassword = ''
    pwdForm.newPassword = ''
  } catch (err) {
    ElMessage.error(err.response?.data?.error || '修改失败')
  } finally {
    changingPwd.value = false
  }
}

// Expose showPassword for external use
defineExpose({ showPassword })

function roleTagType(role) {
  return role === 'admin' ? 'danger' : role === 'user' ? 'primary' : 'info'
}

function roleLabel(role) {
  return role === 'admin' ? '管理员' : role === 'user' ? '操作员' : '游客'
}

function formatDate(dt) {
  if (!dt) return '-'
  return new Date(dt).toLocaleString('zh-CN')
}
</script>

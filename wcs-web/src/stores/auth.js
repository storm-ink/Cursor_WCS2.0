import { ref, computed } from 'vue'

const TOKEN_KEY = 'wcs_token'
const USER_KEY = 'wcs_user'

const token = ref(localStorage.getItem(TOKEN_KEY) || '')
const userInfo = ref(JSON.parse(localStorage.getItem(USER_KEY) || 'null'))

const isLoggedIn = computed(() => !!token.value)
const role = computed(() => userInfo.value?.role || '')
const username = computed(() => userInfo.value?.username || '')

const isAdmin = computed(() => role.value === 'admin')
const isUser = computed(() => role.value === 'user')
const isGuest = computed(() => role.value === 'guest')

function setAuth(data) {
  token.value = data.token
  userInfo.value = { username: data.username, role: data.role }
  localStorage.setItem(TOKEN_KEY, data.token)
  localStorage.setItem(USER_KEY, JSON.stringify(userInfo.value))
}

function clearAuth() {
  token.value = ''
  userInfo.value = null
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(USER_KEY)
}

export function useAuth() {
  return {
    token,
    userInfo,
    isLoggedIn,
    role,
    username,
    isAdmin,
    isUser,
    isGuest,
    setAuth,
    clearAuth
  }
}

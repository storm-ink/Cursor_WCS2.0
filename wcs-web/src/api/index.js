import axios from 'axios'
import { useAuth } from '../stores/auth'

const api = axios.create({
  baseURL: '/api',
  timeout: 10000
})

api.interceptors.request.use(config => {
  const { token } = useAuth()
  if (token.value) {
    config.headers.Authorization = `Bearer ${token.value}`
  }
  return config
})

api.interceptors.response.use(
  response => response.data,
  error => {
    if (error.response?.status === 401) {
      const { clearAuth } = useAuth()
      clearAuth()
      window.location.href = '/login'
    }
    const msg = error.response?.data?.error || error.message
    console.error('API Error:', msg)
    return Promise.reject(error)
  }
)

export const taskApi = {
  getCurrent: (page = 1, pageSize = 20) =>
    api.get('/tasks', { params: { page, pageSize } }),
  getHistory: (params) =>
    api.get('/tasks/history', { params }),
  getHistoryArchive: (params) =>
    api.get('/tasks/history/archive', { params }),
  create: (data) =>
    api.post('/tasks', data),
  getDeviceTasks: (taskCode) =>
    api.get('/devicetasks', { params: { taskCode } }),
  getHistoryDeviceTasks: (taskCode) =>
    api.get('/devicetasks/history', { params: { taskCode } }),
  cancel: (id) =>
    api.post(`/tasks/${id}/cancel`),
  retry: (id) =>
    api.post(`/tasks/${id}/retry`),
  complete: (id) =>
    api.post(`/tasks/${id}/complete`),
  cleanup: (retainDays = 30) =>
    api.delete('/tasks/cleanup', { params: { retainDays } }),
  archive: (retainDays = 30) =>
    api.post('/tasks/archive', null, { params: { retainDays } }),
  getArchiveConfig: () =>
    api.get('/tasks/archive/config')
}

export const deviceTaskApi = {
  getByDevice: (code) =>
    api.get(`/devicetasks/bydevice/${code}`),
  getHistoryByDevice: (code) =>
    api.get(`/devicetasks/bydevice/${code}/history`),
  resend: (id) =>
    api.post(`/devicetasks/${id}/resend`),
  cancel: (id) =>
    api.post(`/devicetasks/${id}/cancel`),
  complete: (id) =>
    api.post(`/devicetasks/${id}/complete`)
}

export const deviceApi = {
  getAll: () => api.get('/devices'),
  getMessages: (code) => api.get(`/devices/${code}/messages`),
  enable: (code) => api.post(`/devices/${code}/enable`),
  disable: (code) => api.post(`/devices/${code}/disable`)
}

export const wmsApi = {
  inbound: (data) => api.post('/wms/inbound-orders', data),
  outbound: (data) => api.post('/wms/outbound-orders', data)
}

export const configApi = {
  importPaths: (data) => api.post('/config/import-paths', data)
}

export const healthApi = {
  check: () => api.get('/health')
}

export const authApi = {
  login: (username, password) => api.post('/auth/login', { username, password }),
  guest: () => api.post('/auth/guest')
}

export const userApi = {
  getAll: () => api.get('/users'),
  create: (username, password, role) => api.post('/users', { username, password, role }),
  remove: (id) => api.delete(`/users/${id}`),
  changeMyPassword: (oldPassword, newPassword) =>
    api.put('/users/me/password', { oldPassword, newPassword })
}

export default api

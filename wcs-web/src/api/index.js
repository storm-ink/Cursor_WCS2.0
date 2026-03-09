import axios from 'axios'

const api = axios.create({
  baseURL: '/api',
  timeout: 10000
})

api.interceptors.response.use(
  response => response.data,
  error => {
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
  create: (data) =>
    api.post('/tasks', data),
  getDeviceTasks: (taskCode) =>
    api.get('/devicetasks', { params: { taskCode } }),
  cancel: (id) =>
    api.post(`/tasks/${id}/cancel`),
  retry: (id) =>
    api.post(`/tasks/${id}/retry`),
  cleanup: (retainDays = 30) =>
    api.delete('/tasks/cleanup', { params: { retainDays } })
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

export default api

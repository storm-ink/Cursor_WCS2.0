import { createRouter, createWebHistory } from 'vue-router'
import { useAuth } from '../stores/auth'

const routes = [
  {
    path: '/login',
    name: 'Login',
    component: () => import('../views/LoginView.vue'),
    meta: { public: true }
  },
  {
    path: '/',
    redirect: '/tasks/taskboard'
  },
  {
    path: '/tasks',
    name: 'Tasks',
    redirect: '/tasks/taskboard',
    children: [
      {
        path: 'current',
        name: 'CurrentTasks',
        component: () => import('../views/tasks/CurrentTasks.vue')
      },
      {
        path: 'history',
        name: 'HistoryTasks',
        component: () => import('../views/tasks/HistoryTasks.vue')
      },
      {
        path: 'create',
        name: 'CreateTask',
        component: () => import('../views/tasks/CreateTask.vue')
      },
      {
        path: 'changeTaskMode',
        name: 'ChangeTaskMode',
        component: () => import('../views/tasks/ChangeTaskMode.vue')
      },
      {
        path: 'taskboard',
        name: 'TaskBoard',
        component: () => import('../views/tasks/TaskBoard.vue')
      }
    ]
  },
  {
    path: '/devices',
    name: 'Devices',
    redirect: '/devices/deviceManagement',
    meta: { roles: ['admin', 'user'] },
    children: [
      {
        path: 'deviceManagement',
        name: 'DeviceManagement',
        component: () => import('../views/devices/DeviceManagement.vue')
      },
      {
        path: 'deviceControlCommand',
        name: 'DeviceControlCommand',
        component: () => import('../views/devices/DeviceControlCommand.vue')
      },
      {
        path: 'deviceCurrentTasks',
        name: 'DeviceCurrentTasks',
        component: () => import('../views/devices/DeviceCurrentTasks.vue')
      },
      {
        path: 'deviceHistoryTasks',
        name: 'DeviceHistoryTasks',
        component: () => import('../views/devices/DeviceHistoryTasks.vue')
      },
      {
        path: 'devicePerformanceAnalysis',
        name: 'DevicePerformanceAnalysis',
        component: () => import('../views/devices/DevicePerformanceAnalysis.vue')
      },
      {
        path: 'deviceProfiles',
        name: 'DeviceProfiles',
        component: () => import('../views/devices/DeviceProfiles.vue')
      }
    ]
  },
  {
    path: '/logs',
    name: 'Logs',
    component: () => import('../views/LogView.vue'),
    meta: { roles: ['admin'] }
  },
  {
    path: '/monitor3d',
    name: 'Monitor3D',
    component: () => import('../views/Monitor3D.vue'),
    meta: { roles: ['admin'] }
  },
  {
    path: '/admin/users',
    name: 'UserManagement',
    component: () => import('../views/admin/UserManagement.vue'),
    meta: { roles: ['admin'] }
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

router.beforeEach((to) => {
  const { isLoggedIn, role } = useAuth()

  // Redirect logged-in users away from login page
  if (to.meta.public && isLoggedIn.value) {
    return { path: '/tasks/taskboard' }
  }

  // Allow public routes
  if (to.meta.public) return true

  // Redirect to login if not authenticated
  if (!isLoggedIn.value) {
    return { path: '/login', query: { redirect: to.fullPath } }
  }

  // Check role restrictions
  const allowedRoles = to.meta.roles
  if (allowedRoles && !allowedRoles.includes(role.value)) {
    // Redirect to taskboard
    return { path: '/tasks/taskboard' }
  }

  return true
})

export default router


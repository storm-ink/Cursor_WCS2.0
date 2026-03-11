import { createRouter, createWebHistory } from 'vue-router'

const routes = [
  {
    path: '/',
    redirect: '/monitor3d'
  },
  {
    path: '/monitor3d',
    name: 'Monitor3D',
    component: () => import('../views/Monitor3D.vue')
  },
  {
    path: '/tasks',
    name: 'Tasks',
    redirect: '/tasks/board',
    children: [
      {
        path: 'board',
        name: 'TaskBoard',
        component: () => import('../views/tasks/TaskBoard.vue')
      },
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
      }
    ]
  },
  {
    path: '/devices',
    name: 'Devices',
    redirect: '/devices/list',
    children: [
      {
        path: 'list',
        name: 'DeviceList',
        component: () => import('../views/devices/DeviceList.vue')
      },
      {
        path: 'commands',
        name: 'ControlCommands',
        component: () => import('../views/devices/ControlCommands.vue')
      },
      {
        path: 'status',
        name: 'DeviceStatus',
        component: () => import('../views/devices/DeviceStatus.vue')
      },
      {
        path: 'history',
        name: 'DeviceHistory',
        component: () => import('../views/devices/DeviceHistory.vue')
      },
      {
        path: 'performance',
        name: 'DevicePerformance',
        component: () => import('../views/devices/DevicePerformance.vue')
      }
    ]
  },
  {
    path: '/logs',
    name: 'Logs',
    component: () => import('../views/LogView.vue')
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

export default router

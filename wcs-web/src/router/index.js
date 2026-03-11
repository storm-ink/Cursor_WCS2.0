import { createRouter, createWebHistory } from 'vue-router'

const routes = [
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
      }
      ,
      {
        path: 'changeTaskMode',
        name: 'ChangeTaskMode',
        component: () => import('../views/tasks/ChangeTaskMode.vue')
      }
      ,
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
      }
      ,
      {
        path: 'deviceCurrentTasks',
        name: 'DeviceCurrentTasks',
        component: () => import('../views/devices/DeviceCurrentTasks.vue')
      },
      {
        path: 'deviceHistoryTasks',
        name: 'DeviceHistoryTasks',
        component: () => import('../views/devices/DeviceHistoryTasks.vue')
      }
      ,
      {
        path: 'devicePerformanceAnalysis',
        name: 'DevicePerformanceAnalysis',
        component: () => import('../views/devices/DevicePerformanceAnalysis.vue')
      }
      ,
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
    component: () => import('../views/LogView.vue')
  },
  {
    path: '/monitor3d',
    name: 'Monitor3D',
    component: () => import('../views/Monitor3D.vue')
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

export default router

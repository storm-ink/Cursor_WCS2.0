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
    redirect: '/tasks/current',
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
    ]
  },
  {
    path: '/devices',
    name: 'Devices',
    component: () => import('../views/DeviceManagement.vue')
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

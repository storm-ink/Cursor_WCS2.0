# Vue 3 + Vite

This template should help get you started developing with Vue 3 in Vite. The template uses Vue 3 `<script setup>` SFCs, check out the [script setup docs](https://v3.vuejs.org/api/sfc-script-setup.html#sfc-script-setup) to learn more.

Learn more about IDE Support for Vue in the [Vue Docs Scaling up Guide](https://vuejs.org/guide/scaling-up/tooling.html#ide-support).



# WCS‑Web Frontend (Vue 3 + Vite)

这是一个用于**智能仓储控制系统**的前端项目，基于 Vue 3 和 Vite 搭建。主要功能包括实时监控、任务调度、设备管理及三维可视化。

## 📁 项目结构

```
wcs-web/
├─ index.html
├─ package.json, package-lock.json
├─ vite.config.js
├─ README.md         ← 当前文档
└─ src/
   ├─ main.js            ← 应用入口
   ├─ App.vue            ← 根组件，包含导航和 SignalR 连接状态
   ├─ style.css          ← 全局主题/样式
   ├─ api/               ← 封装的后端 HTTP 接口
   │   └─ index.js
   ├─ router/
   │   └─ index.js       ← 路由定义
   ├─ stores/
   │   └─ signalr.js     ← SignalR 连接及事件管理
   └─ views/             ← 页面组件
       ├─ Monitor3D.vue       ← 三维仓库可视化
       ├─ DeviceManagement.vue← 设备管理界面
       ├─ LogView.vue         ← 日志查看（暂未启用）
       └─ tasks/
           ├─ CurrentTasks.vue ← 当前任务监控
           ├─ HistoryTasks.vue ← 历史任务查询
           └─ CreateTask.vue   ← 手动下发任务
```

## ⚙️ 技术栈

- Vue 3（`<script setup>`）
- Vite 作为构建工具
- Element Plus UI 组件库
- Three.js 用于 3D 场景绘制
- SignalR（@microsoft/signalr）实现实时通信
- Axios 封装 HTTP 请求
- Vue Router 管理页面路由

## 🔗 接口封装

所有后端接口通过 `src/api/index.js` 暴露，示例模块：

- `taskApi`、`deviceApi`、`wmsApi`、`configApi`、`healthApi`

统一 `baseURL: '/api'`，并对响应和错误做拦截处理。

## 🚀 启动项目

```bash
npm install      # 安装依赖
npm run dev      # 启动开发服务器，默认 http://localhost:3000/
```

## 📘 说明

- 应用启动后会在 `App.vue` 中通过 `useSignalR()` 连接服务器，其他页面根据需要加入不同 SignalR 组获取实时更新。
- 全局主题定义在 `src/style.css`，对 Element Plus 做了暗色模式覆盖。

欢迎在此基础上扩展功能或适配具体后端业务。


# WCS.Bs 后端代码技术分析文档

> 文档版本：V1.0 | 基于现有代码梳理，面向后续优化  
> 技术栈：.NET 9 / ASP.NET Core Web API / EF Core / SQL Server / SignalR / Serilog

---

## 目录

1. [系统总览](#一系统总览)
2. [目录结构与职责划分](#二目录结构与职责划分)
3. [领域模型（Domain）](#三领域模型domain)
4. [数据库与持久层（Infrastructure）](#四数据库与持久层infrastructure)
5. [服务层（Services）业务逻辑](#五服务层services业务逻辑)
   - [TaskService](#51-taskservice--任务管理核心)
   - [PathConfigService](#52-pathconfigservice--路径配置与匹配)
   - [DeviceService](#53-deviceservice--设备注册与状态管理)
   - [PlcDispatchService](#54-plcdispatchservice--plc调度主循环)
   - [DataCleanupService](#55-datacleanupservice--历史数据清理)
   - [Pipeline 扩展点](#56-pipeline-扩展点)
6. [PLC 通讯层（Plc）](#六plc-通讯层plc)
7. [API 层（Controllers）](#七api-层controllers)
8. [实时推送（SignalR Hubs）](#八实时推送signalr-hubs)
9. [启动流程（Program.cs）](#九启动流程programcs)
10. [完整数据流分析](#十完整数据流分析)
11. [当前代码问题](#十一当前代码问题)
12. [优化建议](#十二优化建议)

---

## 一、系统总览

WCS.Bs（仓储控制系统 BS 版）是一个介于 **WMS（仓储管理系统）** 与 **PLC（可编程控制器）** 之间的中间件系统，核心职责是：

```
WMS / 手工操作
       │  HTTP POST
       ▼
  ┌─────────────┐
  │   WCS.Bs    │  ← 任务接收、路径规划、状态跟踪
  │  ASP.NET    │
  │  Core API   │
  └──────┬──────┘
         │  TCP Socket (自定义文本协议)
    ┌────┴────┐
    ▼         ▼
  CV PLC    Crane PLC
 (输送线)   (堆垛机)
```

**关键功能：**
- 接收 WMS 或手工下发的入库/出库/移库任务
- 根据路径配置将主任务拆解为按设备的子任务（DeviceTask）
- 通过 TCP 与 PLC 通信，发送指令并接收回报
- 超时重试、状态跟踪、任务恢复
- SignalR 实时推送设备状态、任务状态、日志

---

## 二、目录结构与职责划分

```
Wcs.Bs/
├── Controllers/               # HTTP API 端点（薄层，仅做参数校验和调用 Service）
│   ├── TasksController.cs     # 任务 CRUD、取消、重试
│   ├── DeviceTasksController.cs  # 查询子任务
│   ├── DevicesController.cs   # 设备状态、启用/禁用
│   ├── WmsController.cs       # WMS 入库/出库接入
│   ├── ConfigController.cs    # 路径配置导入
│   └── HealthController.cs    # 系统健康检查
│
├── Domain/                    # 领域模型和枚举（纯 POCO，无业务逻辑）
│   ├── TaskEntity.cs          # 主任务实体
│   ├── DeviceTaskEntity.cs    # 设备子任务实体
│   ├── PathConfigEntity.cs    # 路径配置表实体
│   ├── CraneReachableConfigEntity.cs  # 堆垛机可达范围实体
│   ├── PathConfigModels.cs    # 路径配置 JSON 模型
│   ├── DeviceConfig.cs        # 设备/PLC/输送线配置 POCO
│   ├── PipelineConfig.cs      # Pipeline 开关配置
│   ├── LocationInfo.cs        # 位置信息辅助类
│   └── Enums.cs               # 所有枚举
│
├── Infrastructure/
│   └── WcsDbContext.cs        # EF Core DbContext，定义索引和关系
│
├── Services/                  # 核心业务逻辑
│   ├── TaskService.cs         # 任务创建、推进、取消、重试、查询
│   ├── PathConfigService.cs   # 路径导入、匹配、堆垛机可达检查
│   ├── DeviceService.cs       # 设备注册、状态管理、报文缓冲（内存）
│   ├── PlcDispatchService.cs  # PLC 连接、调度循环、超时重试、报文处理
│   ├── DataCleanupService.cs  # 定时清理历史数据
│   └── Pipeline/              # 扩展钩子（策略模式）
│       ├── IDeviceTaskDispatchFilter.cs   # 下发前过滤器接口
│       ├── IDeviceTaskCompletedHandler.cs # 子任务完成后钩子接口
│       ├── ITaskCompletedHandler.cs       # 主任务完成后钩子接口
│       ├── DefaultDispatchFilter.cs       # 默认实现（直接放行）
│       ├── DefaultDeviceTaskCompletedHandler.cs
│       └── DefaultTaskCompletedHandler.cs
│
├── Plc/                       # PLC 通讯（协议编解码 + TCP 客户端）
│   ├── PlcClient.cs           # TCP 长连接、自动重连、接收循环
│   ├── PlcMessage.cs          # 报文序列化/反序列化（[Key=Value;...] 格式）
│   ├── ConveyorProtocol.cs    # 输送线指令构建 + 上报解析
│   └── CraneProtocol.cs       # 堆垛机指令构建 + 上报解析
│
├── Hubs/
│   ├── WcsHub.cs              # SignalR Hub（Group 管理）
│   └── SerilogSignalRSink.cs  # Serilog → SignalR 实时日志 Sink
│
├── Config/
│   └── paths.json             # 路径配置文件（启动时加载）
│
├── Program.cs                 # 依赖注入注册 + 启动初始化
├── appsettings.json           # 应用配置
└── Wcs.Bs.csproj              # 项目文件（.NET 9）
```

---

## 三、领域模型（Domain）

### 3.1 核心枚举

```
TaskSource:   Manual(0) | Wms(1)
TaskType:     Inbound(1) | Outbound(2) | Transfer(3)
TaskStatus:   Created(1) → SendingToPlc(2) → Running(3) → Finished(4)
                                                        → Error(5)
                                                        → Cancelled(6)
DeviceTaskStatus: Waiting(1) → SendingToPlc(2) → Running(3) → Finished(4) → Error(5)
DeviceType:   Conveyor(1) | Crane(2)
```

### 3.2 任务状态机

```
主任务（TaskEntity）:
  Created ──► SendingToPlc ──► Running ──► Finished
                │                │
                └────────────────┴──► Error
                            (手动取消) ──► Cancelled

设备子任务（DeviceTaskEntity）:
  Waiting ──► SendingToPlc ──► Running ──► Finished
               │
               └──(超时重试)──► Waiting（SendCount < MaxRetry）
               └──(超时超限)──► Error
```

### 3.3 路径配置模型

`PathConfigJson` 存储在数据库 `PathConfigs.ConfigJson` 字段中：

```json
{
  "pathCode": "P001",
  "source": "1001",                    // 起点匹配（支持 * 通配符和 a:b 范围）
  "destinationPattern": "01-*-*",      // 终点匹配
  "steps": [
    {
      "stepOrder": 1,
      "deviceType": "Conveyor",
      "deviceCode": "CV01",
      "routingNo": "1",
      "segmentSource": "1001",
      "segmentDest": "1004"
    },
    {
      "stepOrder": 2,
      "deviceType": "Crane",
      "deviceCode": "CR01",
      "segmentSource": "1004",
      "segmentDest": "{Destination}"   // 占位符，运行时替换为任务终点
    }
  ]
}
```

占位符：`{Source}` 和 `{Destination}` 在生成 DeviceTask 时替换为主任务的起/终点。

---

## 四、数据库与持久层（Infrastructure）

### 4.1 表结构概览

| 表名 | 主键 | 核心索引 |
|------|------|----------|
| `Tasks` | `Id` (BIGINT IDENTITY) | `TaskCode` (唯一), `Status`, `CreatedAt` |
| `DeviceTasks` | `Id` (BIGINT IDENTITY) | `(TaskId, StepOrder)`, `Status` |
| `PathConfigs` | `Id` (BIGINT IDENTITY) | `PathCode` |
| `CraneReachableConfigs` | `Id` (BIGINT IDENTITY) | `DeviceCode` |

### 4.2 主要关系

```
Tasks (1) ──────< DeviceTasks (N)
  TaskEntity.Id = DeviceTaskEntity.TaskId
  级联删除：DeleteBehavior.Cascade
```

### 4.3 数据库初始化方式

当前使用 `db.Database.EnsureCreated()`（Program.cs 第 56 行），**不使用 EF Core Migrations**。此方式仅适合开发阶段，生产环境存在风险（见优化建议第 1 条）。

---

## 五、服务层（Services）业务逻辑

### 5.1 TaskService — 任务管理核心

**注入方式：** `AddScoped`（请求级别生命周期）

#### CreateTaskAsync

```
输入: CreateTaskRequest（TaskCode可选, Source, Type, PalletCode, 起终点, Priority）
  │
  ├─ 1. TaskCode 为空时自动生成（"T" + yyyyMMdd + GUID前8位大写）
  ├─ 2. 检查 TaskCode 唯一性
  ├─ 3. PathConfigService.MatchPathAsync(Start, End) → 找到路径配置
  ├─ 4. 检查路径中每个 Crane 步骤的终点是否在堆垛机可达范围内
  ├─ 5. 创建并保存 TaskEntity（Status=Created）
  └─ 6. CreateDeviceTaskForStepAsync(task, pathConfig, stepOrder=1) → 生成第一个子任务
```

#### OnDeviceTaskCompletedAsync（设备任务完成推进）

```
输入: deviceTaskId
  │
  ├─ 1. 标记 DeviceTask.Status = Finished
  ├─ 2. （可选）触发 IDeviceTaskCompletedHandler Pipeline 钩子
  ├─ 3. task.CurrentStepOrder = dt.StepOrder + 1
  │
  ├─ 若 stepOrder >= TotalSteps（最后一步）:
  │    ├─ task.Status = Finished
  │    └─ （可选）触发 ITaskCompletedHandler Pipeline 钩子
  │
  └─ 若还有下一步:
       └─ CreateDeviceTaskForStepAsync(task, pathConfig, nextStep)
```

#### 其他方法

| 方法 | 功能 |
|------|------|
| `CancelTaskAsync` | 主任务 → Cancelled；未完成子任务 → Error |
| `RetryTaskAsync` | Error 状态任务：子任务重置为 Waiting；主任务重置为 Created |
| `GetCurrentTasksAsync` | 查询非终态任务（分页，按 Priority 降序） |
| `GetHistoryTasksAsync` | 全量历史查询（日期/状态/类型过滤，分页） |
| `GetDeviceTasksAsync` | 按 TaskCode 查询所有子任务 |
| `CleanupOldTasksAsync` | 删除超期的 Finished/Cancelled 任务及其子任务 |
| `RecoverOnRestartAsync` | 重启时将中间状态恢复（SendingToPlc/Running → Created/Waiting） |

---

### 5.2 PathConfigService — 路径配置与匹配

**注入方式：** `AddScoped`

#### 路径匹配算法

`MatchPathAsync(source, destination)` 遍历所有激活路径，按以下规则匹配：

```
规则1（精确）:   pattern == value
规则2（通配符）: pattern 含 * → 转 Regex，* 对应 .*
规则3（范围）:   pattern 含 : → 分割为 start:end，多段比较（数字或字符串）
```

示例：
- `"01-*-*"` 匹配 `"01-05-03"`
- `"01-01-01:01-10-05"` 匹配 `"01-03-02"`（排1,列3,层2，在范围内）

#### ImportFromJsonAsync

**当前实现**：删除所有现有配置 → 批量插入。  
**问题**：无事务包裹，删除成功但插入失败会导致数据丢失（见优化建议）。

---

### 5.3 DeviceService — 设备注册与状态管理

**注入方式：** `AddSingleton`（贯穿整个应用生命周期）

内存维护以下并发字典：
- `_clients`：`DeviceCode → PlcClient`（TCP 连接）
- `_configs`：`DeviceCode → DeviceConfig`（配置）
- `_statuses`：`DeviceCode → DeviceStatus`（连接状态、当前任务、设备状态）
- `_messages`：`DeviceCode → List<DeviceMessage>`（最近 500 条收发报文）

**注意：** `DeviceStatus` 和 `DeviceMessage` 类定义在 `DeviceService.cs` 文件末尾，未放入 Domain 层（见优化建议）。

---

### 5.4 PlcDispatchService — PLC 调度主循环

**注入方式：** `AddHostedService`（后台 `BackgroundService`）

#### 启动流程

```
ExecuteAsync() 启动后：
  1. 等待 2 秒（等待 Web 服务就绪）
  2. TaskService.RecoverOnRestartAsync()（恢复中间状态）
  3. InitPlcConnectionsAsync()（建立所有设备 TCP 连接）
  4. 进入 1 秒轮询主循环:
     ├─ DispatchPendingTasksAsync()  // 下发等待中的子任务
     ├─ CheckTimeoutsAsync()         // 超时检查与重试
     └─ FlushTasksNotification()     // 推送 SignalR（节流 800ms）
```

#### DispatchPendingTasksAsync — 下发逻辑

```
查询所有 Status=Waiting 的 DeviceTask（按主任务 Priority 降序）
  │
  对每个等待任务：
  ├─ 检查同设备是否有 SendingToPlc 或 Running 的任务 → 有则跳过
  ├─ 检查设备是否 Enabled → 未启用则跳过
  ├─ 检查 PlcClient 是否已连接 → 未连接则跳过
  ├─ （可选）IDeviceTaskDispatchFilter.CheckAsync() → 不通过则跳过
  ├─ 根据 DeviceType 构建指令字符串：
  │    Conveyor → ConveyorProtocol.BuildTaskCommand()
  │    Crane    → CraneProtocol.BuildTaskCommand()
  ├─ PlcClient.SendAsync(command)
  └─ 更新状态：DeviceTask.Status=SendingToPlc, Task.Status=SendingToPlc
```

#### CheckTimeoutsAsync — 超时重试逻辑

```
查询所有 Status=SendingToPlc 且 LastSendTime 不为空的子任务
  │
  对每个超时任务（当前时间 > LastSendTime + TimeoutSeconds）：
  ├─ SendCount < MaxRetryCount → 重置为 Waiting（触发重发）
  └─ SendCount >= MaxRetryCount → 标记为 Error，主任务也标记为 Error
```

#### HandleReportAsync — PLC 上报处理

由 `PlcClient.OnReportReceived` 事件触发，使用 `SemaphoreSlim(1,1)` 串行化处理：

```
CV_REPORT（输送线上报）:
  └─ ConveyorProtocol.ParseReport() → 解析多个任务槽
       ├─ HandShake=1 → ConfirmSendSuccess → DeviceTask.Status=Running
       └─ HandShake=2 → CompleteDeviceTaskByTaskNo → OnDeviceTaskCompletedAsync

CRANE_REPORT（堆垛机上报）:
  └─ CraneProtocol.ParseReport() → 解析单任务
       ├─ TaskState=1 → ConfirmSendSuccess → DeviceTask.Status=Running
       └─ TaskState=2 → CompleteDeviceTaskByTaskNo → OnDeviceTaskCompletedAsync
```

#### 任务号格式

PLC 侧使用 `{TaskCode}-{StepOrder}` 作为任务标识符（如 `T20250308ABCD1234-1`）。
`ParseTaskNo` 方法将其拆分回 `(TaskCode, StepOrder)`。

---

### 5.5 DataCleanupService — 历史数据清理

**注入方式：** `AddHostedService`

- 每 **6 小时** 触发一次
- 删除 `FinishedAt` 早于 `retainDays` 天且状态为 Finished 或 Cancelled 的任务及其子任务
- `retainDays` 读取自配置 `DataCleanup:RetainDays`（默认 30 天）

---

### 5.6 Pipeline 扩展点

三个可选钩子，通过配置开关 `Pipeline:Enable*` 控制是否启用：

| 接口 | 触发时机 | 默认实现 | 典型用途 |
|------|----------|----------|----------|
| `IDeviceTaskDispatchFilter` | 子任务下发前 | 直接放行 | 检查货位锁定、设备维护状态 |
| `IDeviceTaskCompletedHandler` | 子任务完成后 | 仅日志 | WMS 分段完工回报、货位状态更新 |
| `ITaskCompletedHandler` | 主任务完成后 | 仅日志 | WMS 完工回报、库存更新 |

---

## 六、PLC 通讯层（Plc）

### 6.1 PlcMessage — 报文格式

所有报文格式为 `[Key=Value;Key=Value;...]`，全部字段为 ASCII 字符串。

```csharp
// 序列化
var msg = new PlcMessage();
msg.Fields["CMD"] = "CV_TASK";
msg.Serialize(); // → "[CMD=CV_TASK]"

// 反序列化
var msg = PlcMessage.Parse("[CMD=CV_REPORT;Task1_HandShake=1]");
msg.GetField("CMD"); // → "CV_REPORT"
```

### 6.2 PlcClient — TCP 长连接

```
ConnectAsync() 启动后台 Task：
  └─ 无限重连循环（失败等待 5 秒重试）：
       ├─ TcpClient.ConnectAsync() → 建立连接
       ├─ 触发 OnConnectionChanged(true)
       └─ ReceiveLoopAsync()：
            ├─ 读取字节流到 StringBuilder 缓冲
            ├─ 找到 [...] 完整报文
            ├─ 触发 OnMessageReceived（原始字符串）
            └─ 触发 OnReportReceived（PlcMessage 对象）
```

**注意：** 当前实现中 `sb` 缓冲区每次读取后清空再追加剩余数据，边界处理逻辑正确，但受限于 `byte[4096]` 缓冲区大小，超大报文可能需要多次读取（见优化建议）。

### 6.3 输送线协议（ConveyorProtocol）

**WCS → PLC（下发）：**
```
[CMD=CV_TASK;HandShake=1;TaskNo=T20250308ABCD1234-1;TUID=P001                ;RotingNo=1;From=1001;To=1004]
```
- `TUID` 固定 20 字符（右填充空格）
- `HandShake=1` 新任务，`2` 删除任务

**PLC → WCS（上报）：**
```
[CMD=CV_REPORT;Task1_HandShake=1;Task1_TaskNo=T20250308ABCD1234-1;...;Loc1_PosNo=1001;Loc1_HaveGoods=1;...]
```
- 最多 `TaskReportCount`（默认5）个任务槽 + `LocationCount`（默认10）个位置
- `HandShake=1` 已接收，`2` 已完成

### 6.4 堆垛机协议（CraneProtocol）

**WCS → PLC（下发）：**
```
[CMD=CRANE_TASK;Cmd=1;EquipmentTaskId=T20250308ABCD1234-2;PickCVNO=1004;PutCVNO=;ForkPickRow=;ForkPickColumn=;ForkPickLevel=;ForkPutRow=01;ForkPutColumn=05;ForkPutLevel=03]
```
- 货架位置（`row-col-level` 格式如 `01-05-03`）→ 填 Fork 字段
- 输送线节号（如 `1004`）→ 填 CVNO 字段

**PLC → WCS（上报）：**
```
[CMD=CRANE_REPORT;DeviceNo=1;EquipmentTaskId=T20250308ABCD1234-2;TaskState=2;DeviceState=1;IsLoaded=1;XColumn=5;YLevel=3;ZRow=1]
```
- `TaskState=1` 已接收，`2` 已完成

---

## 七、API 层（Controllers）

### 完整 API 端点列表

| 方法 | 路径 | 说明 | 关键参数 |
|------|------|------|----------|
| `GET` | `/api/tasks` | 当前任务列表（非终态） | `page`, `pageSize` |
| `GET` | `/api/tasks/history` | 历史任务（全量，含终态） | `startDate`, `endDate`, `status`, `type`, `page`, `pageSize` |
| `POST` | `/api/tasks` | 手工创建任务 | Body: `CreateTaskRequest` |
| `POST` | `/api/tasks/{id}/cancel` | 取消任务 | 路径参数 `id` |
| `POST` | `/api/tasks/{id}/retry` | 重试 Error 任务 | 路径参数 `id` |
| `DELETE` | `/api/tasks/cleanup` | 手工触发历史清理 | `retainDays`（默认30） |
| `GET` | `/api/devicetasks` | 按任务号查询子任务 | `taskCode`（必填） |
| `GET` | `/api/devices` | 所有设备状态 | — |
| `GET` | `/api/devices/{code}/messages` | 设备最近报文 | 路径参数 `code` |
| `POST` | `/api/devices/{code}/enable` | 启用设备 | 路径参数 `code` |
| `POST` | `/api/devices/{code}/disable` | 禁用设备 | 路径参数 `code` |
| `POST` | `/api/wms/inbound-orders` | WMS 入库指令 | Body: `WmsOrderRequest` |
| `POST` | `/api/wms/outbound-orders` | WMS 出库指令 | Body: `WmsOrderRequest` |
| `POST` | `/api/config/import-paths` | 导入路径配置 | Body: 路径配置 JSON |
| `GET` | `/api/health` | 系统健康检查 | — |

### SignalR 端点

| 地址 | 说明 |
|------|------|
| `/hubs/wcs` | WCS 统一 SignalR Hub |

#### 客户端需加入的 Group 及推送事件

| Group | 事件 | 触发时机 |
|-------|------|----------|
| `view:tasks` | `TasksChanged` | 任务状态变化（节流800ms） |
| `view:devices` | `DeviceStatusUpdated` | 设备连接/状态变化 |
| `view:messages:{code}` | `DeviceMessage` | 收到 PLC 原始报文 |
| `view:logs` | `LogReceived` | 任意 Serilog 日志事件 |

---

## 八、实时推送（SignalR Hubs）

### WcsHub

仅提供 Group 管理（`JoinGroup` / `LeaveGroup`），无业务逻辑。

### SerilogSignalRSink

Serilog 自定义 Sink，将日志事件实时推送到 `view:logs` Group。通过 `SourceContext` 属性自动分类：

| SourceContext 关键字 | category |
|---------------------|----------|
| 含 `Plc` | `Plc` |
| 含 `Task` | `Task` |
| 含 `Device` | `Device` |
| 含 `Controller` | `Api` |
| 其他 | `System` |

---

## 九、启动流程（Program.cs）

```
1. 配置 Serilog（读取配置文件，初始化日志）
2. 注册 EF Core DbContext（SQL Server）
3. 注册 Services：
   - PathConfigService (Scoped)
   - TaskService (Scoped)
   - DeviceService (Singleton)
   - PlcDispatchService (HostedService)
   - DataCleanupService (HostedService)
   - Pipeline 接口实现（Scoped）
4. 注册 SignalR、Controllers（JSON 枚举字符串、引用循环忽略）
5. 注册 OpenAPI（仅开发环境）
6. 注册 CORS（开放所有 Origin）
7. 构建 app
8. 启动时初始化：
   a. db.Database.EnsureCreated()
   b. PathConfigService.ImportFromFileAsync("Config/paths.json")
   c. 读取 Devices 配置，注册到 DeviceService
9. 重新配置 Serilog（增加 SignalR Sink）
10. 挂载中间件：CORS → Serilog请求日志 → Controllers → SignalR Hub
11. app.Run()
```

---

## 十、完整数据流分析

### 场景一：WMS 入库

```
1. WMS → POST /api/wms/inbound-orders
2. WmsController → TaskService.CreateTaskAsync(Source=Wms, Type=Inbound)
3. 路径匹配（PathConfigService.MatchPathAsync）
4. 堆垛机可达检查（IsDestinationReachableAsync）
5. 保存 Tasks 记录（Status=Created）
6. 保存 DeviceTasks Step1 记录（Status=Waiting）
7. 响应 WMS: { taskCode, status="Created" }

--- 后台（PlcDispatchService 1秒轮询） ---

8. DispatchPendingTasksAsync：发现 Step1.Waiting
9. 检查同设备无忙任务 → 构建 CV_TASK 指令 → PlcClient.SendAsync
10. DeviceTask.Status=SendingToPlc，Task.Status=SendingToPlc
11. SignalR 推送 TasksChanged

--- PLC 上报（异步） ---

12. 输送线回报 HandShake=1 → ConfirmSendSuccess → Step1.Status=Running
13. 输送线回报 HandShake=2 → CompleteDeviceTaskByTaskNo
14. OnDeviceTaskCompletedAsync：Step1.Finished → 创建 Step2(Crane)
15. Step2.Waiting → DispatchPendingTasksAsync → 构建 CRANE_TASK → 发送
16. 堆垛机回报 TaskState=1 → Step2.Running
17. 堆垛机回报 TaskState=2 → CompleteDeviceTaskByTaskNo
18. OnDeviceTaskCompletedAsync：Step2 是最后步骤 → Task.Status=Finished
19. （可选）ITaskCompletedHandler.HandleAsync(task) → 回报 WMS
20. SignalR 推送 TasksChanged
```

### 场景二：任务超时重试

```
1. Step1 发送后超过 TaskTimeoutSeconds 未收到 HandShake=1
2. CheckTimeoutsAsync：发现 SendingToPlc + 超时
3. SendCount < MaxRetryCount → Step1.Status=Waiting，触发重发
4. 达到 MaxRetryCount → Step1.Status=Error，Task.Status=Error
5. 运维人员通过前端 POST /api/tasks/{id}/retry
6. RetryTaskAsync：重置 Error 子任务为 Waiting，主任务为 Created
7. 重新进入调度循环
```

---

## 十一、当前代码问题

> 以下问题按严重程度排列，供优化参考

### P0 - 高危问题

#### 1. 路径配置导入无事务保护
**文件：** `Services/PathConfigService.cs`，`ImportFromJsonAsync` 方法

```csharp
// 当前：删除后批量插入，无事务
_db.PathConfigs.RemoveRange(_db.PathConfigs);
// ...
await _db.SaveChangesAsync();  // 若此处失败，数据已丢失
```

**风险：** 若 `SaveChangesAsync` 中途异常，旧路径配置已删除但新配置未写入，系统将无法创建任何任务。

#### 2. 字段名拼写错误（`RotingNo` 应为 `RoutingNo`）
**文件：** `Plc/ConveyorProtocol.cs`

```csharp
msg.Fields["RotingNo"] = dt.RoutingNo ?? "1";  // "Roting" 缺少字母 u
```

`DeviceTaskEntity` 中字段是 `RoutingNo`，但协议报文用 `RotingNo`。若 PLC 协议已定义为 `RoutingNo`，此处会导致 PLC 读不到正确字段。**需与 PLC 协议确认一致性。**

#### 3. CORS 完全开放
**文件：** `Program.cs`

```csharp
policy.SetIsOriginAllowed(_ => true)  // 任何来源都可访问
```

生产环境应限制为特定域名。

---

### P1 - 中等问题

#### 4. 使用 EnsureCreated 而非 Migrations
**文件：** `Program.cs`

`EnsureCreated()` 只在数据库不存在时建表，无法处理后续字段变更。生产环境应使用 EF Core Migrations。

#### 5. `CurrentLocationCode` 从未更新
**文件：** `Domain/TaskEntity.cs`

`CurrentLocationCode` 和 `CurrentLocationDeviceName` 在创建任务时设为起点，但在子任务推进过程中**从未更新**，始终停留在起点。

#### 6. 类定义位置不规范
以下类应移至 `Domain/` 文件夹，但目前散落在 Service/Controller 层：

| 类名 | 当前位置 | 应移至 |
|------|----------|--------|
| `DeviceStatus` | `Services/DeviceService.cs` | `Domain/DeviceStatus.cs` |
| `DeviceMessage` | `Services/DeviceService.cs` | `Domain/DeviceMessage.cs` |
| `WmsOrderRequest` | `Controllers/WmsController.cs` | `Domain/Dto/WmsOrderRequest.cs` |
| `CreateTaskRequest` | `Services/TaskService.cs` | `Domain/Dto/CreateTaskRequest.cs` |
| `CvReport`, `CvTaskReport`, `CvLocationReport` | `Plc/ConveyorProtocol.cs` | `Domain/Plc/CvReport.cs` |
| `CraneReport` | `Plc/CraneProtocol.cs` | `Domain/Plc/CraneReport.cs` |

#### 7. CV 输送线货位信息（Locations）从未使用
**文件：** `Services/PlcDispatchService.cs`，`HandleCvReportAsync`

`ConveyorProtocol.ParseReport()` 解析了 `CvLocationReport` 列表（货位状态），但解析结果 `report.Locations` 被完全忽略，没有任何后续处理或推送。这是一个潜在的功能缺失。

#### 8. PlcDispatchService 类过大
**文件：** `Services/PlcDispatchService.cs`（约 416 行）

单个类承担了：连接管理、任务调度、超时检测、报文处理、SignalR 推送等多个职责，违反单一职责原则。

---

### P2 - 低优先级问题

#### 9. 配置文件中含明文密码
**文件：** `appsettings.json`

```json
"Password=Sineva@123"
```

即使是示例项目，也不应将密码提交到代码仓库。应使用环境变量或 User Secrets。

#### 10. PlcClient 缓冲区固定 4096 字节
**文件：** `Plc/PlcClient.cs`

若单次 PLC 上报报文超过 4096 字节（多任务槽 + 多货位），需多次 ReadAsync 拼接。当前 `sb` 拼接逻辑正确，但缓冲区大小建议可配置，或使用 `PipeReader` 等更健壮的流式处理方案。

#### 11. GetCurrentTasksAsync 不返回子任务
`GET /api/tasks` 返回的 `TaskEntity` 不包含 `DeviceTasks` 集合（未 Include），前端若需要显示子任务信息需要额外请求 `/api/devicetasks?taskCode=xxx`，存在 N+1 请求问题。

#### 12. SerilogSignalRSink 中 ConfigureAwait(false) 未被等待
**文件：** `Hubs/SerilogSignalRSink.cs`

```csharp
hubContext.Clients.Group("view:logs")
    .SendAsync("LogReceived", logEntry)
    .ConfigureAwait(false);  // 返回值未被 await，异常会静默丢失
```

---

## 十二、优化建议

> 按实施优先级排序，标注预期收益和工作量

### 🔴 高优先级

#### 建议1：路径配置导入加事务
**工作量：** 小（1小时）  
**收益：** 防止配置更新期间数据丢失

```csharp
public async Task ImportFromJsonAsync(string json)
{
    // ...
    await using var transaction = await _db.Database.BeginTransactionAsync();
    try
    {
        _db.PathConfigs.RemoveRange(_db.PathConfigs);
        // ... 添加新配置
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

#### 建议2：改用 EF Core Migrations
**工作量：** 中（半天）  
**收益：** 支持生产环境平滑升级数据库结构

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

Program.cs 改为：
```csharp
db.Database.Migrate();  // 代替 EnsureCreated
```

#### 建议3：更新 CurrentLocationCode
**工作量：** 小（2小时）  
**收益：** 任务进度跟踪更准确，前端可展示货物当前位置

在 `OnDeviceTaskCompletedAsync` 中，完成每步子任务后更新主任务当前位置：
```csharp
// 子任务完成后，更新主任务当前位置为子任务的终点
task.CurrentLocationCode = deviceTask.SegmentDest;
```

---

### 🟡 中优先级

#### 建议4：拆解 PlcDispatchService
**工作量：** 中（1天）  
**收益：** 提升可维护性和可测试性

建议拆分为：
- `PlcConnectionManager`：管理 TCP 连接、重连逻辑
- `PlcDispatchLoop`：调度主循环（仅下发逻辑）
- `PlcReportHandler`：处理 PLC 上报（CV + Crane 分离）

#### 建议5：将 DTO/视图模型移至独立层
**工作量：** 小（2小时）

建议新建 `Dto/` 目录，集中管理请求/响应模型：
```
Domain/
├── Dto/
│   ├── CreateTaskRequest.cs
│   ├── WmsOrderRequest.cs
│   └── TaskResponse.cs   ← 新增：专用于 API 响应，避免直接暴露实体
```

#### 建议6：GET /api/tasks 支持返回子任务概要
**工作量：** 小（1小时）  
**收益：** 减少前端 N+1 请求

```csharp
// 查询时 Include DeviceTasks
return await _db.Tasks
    .Include(t => t.DeviceTasks)
    .Where(...)
    .ToListAsync();
```

或新增独立的 Response DTO 包含子任务摘要（仅返回 Status 和 StepOrder，不返回完整字段）。

#### 建议7：添加全局异常处理中间件
**工作量：** 小（1小时）

```csharp
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        await context.Response.WriteAsJsonAsync(new { error = ex?.Message ?? "Internal Server Error" });
    });
});
```

#### 建议8：处理输送线货位状态（Locations）
**工作量：** 中（半天）  
**收益：** 实现 3D 监控界面所需的货位实时状态

将 `report.Locations` 通过 SignalR 推送给 `view:realtime` Group，前端 Three.js 据此更新货位状态显示。

---

### 🟢 低优先级 / 架构改进

#### 建议9：使用 Repository 模式（可选）
**工作量：** 大（2天）  
**收益：** 业务逻辑与数据库解耦，便于单元测试

为 `Tasks` 和 `DeviceTasks` 抽取 `ITaskRepository` / `IDeviceTaskRepository` 接口，在测试中可 Mock 数据库。

#### 建议10：为 PlcClient 引入接口 IPlcClient
**工作量：** 小（1小时）  
**收益：** 便于单元测试（Mock PLC 通信）和后续支持不同通信协议

```csharp
public interface IPlcClient
{
    bool IsConnected { get; }
    Task SendAsync(string message);
    Task ConnectAsync(CancellationToken cancellationToken);
}
```

#### 建议11：确认并修复 RoutingNo/RotingNo 字段名
**工作量：** 极小（15分钟）  
**收益：** 消除隐患

与 PLC 工程师确认报文字段名后，统一改为正确拼写（`RoutingNo`），同时更新协议文档。

#### 建议12：配置敏感信息管理
**工作量：** 小（1小时）

开发环境使用 User Secrets：
```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."
```
生产环境使用环境变量或 Azure Key Vault。

---

## 附录：配置参考

### appsettings.json 完整说明

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=cursor_wcs;..."
  },
  "Devices": [
    {
      "Code": "CV01",          // 设备唯一标识
      "Type": "Conveyor",      // Conveyor | Crane
      "PlcIp": "127.0.0.1",   // PLC IP
      "PlcPort": 2001          // PLC 端口
    }
  ],
  "Plc": {
    "TaskTimeoutSeconds": 60,  // 下发超时（秒）
    "MaxRetryCount": 3         // 最大重试次数
  },
  "CvPlc": {
    "TaskReportCount": 5,      // 输送线上报任务槽数量
    "LocationCount": 10        // 输送线上报货位数量
  },
  "Pipeline": {
    "EnableDispatchFilter": false,             // 开启下发前过滤器
    "EnableDeviceTaskCompletedHandler": false, // 开启子任务完成钩子
    "EnableTaskCompletedHandler": false        // 开启主任务完成钩子
  },
  "DataCleanup": {
    "RetainDays": 30           // 历史数据保留天数
  },
  "PathConfig": {
    "JsonPath": "Config/paths.json"  // 路径配置文件路径
  }
}
```

---

*文档生成时间：2026-03-11 | 基于 Wcs.Bs 现有代码梳理*

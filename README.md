# Cursor_WCS2.0
# WCS 智能仓储控制系统 BS 版 — V2 技术开发文档

本文档基于 V1 及讨论结果整理，用于 V2 开发实施。

---

## 一、项目概览

- **项目名称**：WCS.Bs（WCS 智能仓储控制系统，BS 架构）
- **技术栈**：
  - 后端：.NET 10、ASP.NET Core Web API、Entity Framework Core 8、SQL Server、SignalR、Serilog
  - 前端：Vue 3、Vite、Three.js、Bootstrap 5 / Element Plus
- **目标**：任务管理、配置化任务分解、PLC 通讯（输送线/堆垛机）、WMS 对接、3D 实时监控、设备管理、日志系统

---

## 二、解决方案与项目结构

### 2.1 后端（Wcs.Bs）

```
Wcs.Bs/
├── Controllers/
├── Domain/
├── Infrastructure/
├── Services/
├── Plc/
├── Hubs/
├── wwwroot/
├── appsettings.json
├── Program.cs
└── Wcs.Bs.csproj
```

### 2.2 前端（Vue）

```
wcs-web/
├── src/
│   ├── views/
│   ├── components/
│   ├── api/
│   ├── stores/
│   └── ...
├── package.json
└── vite.config.js
```

---

## 三、数据库

### 3.1 表 Tasks（主任务）

| 列名 | 类型 | 说明 |
|------|------|------|
| Id | BIGINT, PK, IDENTITY | 主键 |
| TaskCode | NVARCHAR(50) | 任务编号，唯一业务标识 |
| Source | INT | 任务来源：0=Manual, 1=Wms |
| Type | INT | 任务类型：1=Inbound, 2=Outbound, 3=Transfer |
| PalletCode | NVARCHAR(50) | 托盘号 |
| StartLocationCode | NVARCHAR(50) | 起点位置编码 |
| StartLocationDeviceName | NVARCHAR(50) | 起点对应设备名 |
| EndLocationCode | NVARCHAR(50) | 终点位置编码 |
| EndLocationDeviceName | NVARCHAR(50) | 终点对应设备名 |
| CurrentLocationCode | NVARCHAR(50) | 当前位置编码 |
| CurrentLocationDeviceName | NVARCHAR(50) | 当前位置对应设备名 |
| Status | INT | 1=Created, 2=SendingToPlc, 3=Running, 4=Finished, 5=Error, 6=Cancelled |
| PathCode | NVARCHAR(50) | 匹配到的路径编码 |
| CurrentStepOrder | INT | 当前执行到的步骤序号 |
| TotalSteps | INT | 路径总步骤数 |
| Priority | INT | 优先级，值越大越高 |
| CreatedAt | DATETIME2 | 创建时间 |
| StartedAt | DATETIME2 NULL | 开始时间 |
| FinishedAt | DATETIME2 NULL | 结束时间 |
| ErrorMessage | NVARCHAR(500) NULL | 错误信息 |
| Description | NVARCHAR(200) NULL | 备注 |

### 3.2 表 DeviceTasks（设备任务）

| 列名 | 类型 | 说明 |
|------|------|------|
| Id | BIGINT, PK, IDENTITY | 主键 |
| TaskId | BIGINT FK | 关联主任务 |
| TaskCode | NVARCHAR(50) | 主任务号 |
| StepOrder | INT | 在主任务路径中的步骤序号 |
| DeviceType | INT | 1=Conveyor, 2=Crane |
| DeviceCode | NVARCHAR(50) | 设备号 |
| SegmentSource | NVARCHAR(50) | 本段起点 |
| SegmentDest | NVARCHAR(50) | 本段终点 |
| Status | INT | 1=Waiting, 2=SendingToPlc, 3=Running, 4=Finished, 5=Error |
| SendCount | INT | 已发送次数 |
| LastSendTime | DATETIME2 NULL | 最近一次发送时间 |
| TimeoutSeconds | INT | 超时秒数 |
| CreatedAt | DATETIME2 | 创建时间 |
| StartedAt | DATETIME2 NULL | 开始时间 |
| FinishedAt | DATETIME2 NULL | 完成时间 |
| ErrorMessage | NVARCHAR(500) NULL | 错误信息 |

### 3.3 表 PathConfig（路径配置）

| 列名 | 类型 | 说明 |
|------|------|------|
| Id | BIGINT, PK, IDENTITY | 主键 |
| PathCode | NVARCHAR(50) | 路径编码 |
| Source | NVARCHAR(100) | 起点（支持范围/通配符） |
| DestinationPattern | NVARCHAR(100) | 终点匹配规则 |
| ConfigJson | NVARCHAR(MAX) | 完整 JSON（含 steps） |
| IsActive | BIT | 是否启用 |

### 3.4 表 CraneReachableConfig（堆垛机可达范围）

| 列名 | 类型 | 说明 |
|------|------|------|
| Id | BIGINT, PK, IDENTITY | 主键 |
| DeviceCode | NVARCHAR(50) | 设备号 |
| ReachablePattern | NVARCHAR(100) | 可达货架范围（如 01-01-01:01-10-05） |
| IsActive | BIT | 是否启用 |

### 3.5 建表方式

使用 **EF Core Migrations**，不使用 EnsureCreated。

---

## 四、领域模型

### 4.1 LocationInfo

```csharp
public class LocationInfo
{
    public string Code { get; set; }
    public string DeviceName { get; set; }
}
```

### 4.2 TaskEntity

见第三节表结构，对应实体含 PathCode、CurrentStepOrder、TotalSteps、Priority 等。

### 4.3 PathConfig JSON 结构

```json
{
  "pathCode": "P001",
  "source": "1001",
  "destinationPattern": "01-*-*",
  "steps": [
    {
      "stepOrder": 1,
      "deviceType": "Conveyor",
      "deviceCode": "CV01",
      "rotingNo": "1",
      "segmentSource": "1001",
      "segmentDest": "1004"
    },
    {
      "stepOrder": 2,
      "deviceType": "Crane",
      "deviceCode": "CR01",
      "segmentSource": "01-01-01",
      "segmentDest": "{Destination}"
    }
  ]
}
```

占位符：`{Source}`、`{Destination}` 由主任务填充。

---

## 五、任务策略

### 5.1 分解逻辑

1. 主任务创建后，根据 Source、Destination 匹配 PathConfig
2. 校验 Destination 是否在对应 Crane 的 CraneReachableConfig 内
3. 仅生成 StepOrder=1 的设备任务
4. 设备任务完成后，再生成下一步设备任务，直至全部完成

### 5.2 设备忙控制

- 同一设备同一时刻只允许一个任务处于 SendingToPlc 或 Running
- 新任务进入 Waiting，按主任务 Priority 排序
- 下发前检查设备是否忙，忙则不下发

### 5.3 重试与超时

- 配置：TaskTimeoutSeconds、MaxRetryCount
- 超时未收到回报则重发，达到最大重试次数后标记 Error

### 5.4 重启恢复

- 将 SendingToPlc/Running 的主任务、设备任务恢复为可继续状态
- 设备任务置为 Waiting，SendCount 清零
- 重发时使用相同 TaskNo/EquipmentTaskId，PLC 按任务 ID 幂等

### 5.5 优先级

主任务 Priority 越高越先执行，设备任务按主任务优先级排队。

---

## 六、PLC 协议

### 6.1 通用格式

- 键值对，`Key=Value`，分号分隔
- 报文以 `[` 开头，`]` 结尾
- 全部字段为 string

### 6.2 CV 输送线 — WCS 下发

```
[CMD=CV_TASK;HandShake=1;TaskNo=T001-1;TUID=P001               ;RotingNo=1;From=1001;To=1004]
```

| 字段 | 说明 |
|------|------|
| HandShake | 1=新任务，2=删除任务 |
| TaskNo | 任务号 |
| TUID | 托盘号，20 字符 |
| RotingNo | 路径号 |
| From | 起点节号 |
| To | 终点节号 |

### 6.3 CV 输送线 — PLC 上报

```
[CMD=CV_REPORT;Task1_HandShake=1;Task1_TaskNo=T001;...;Loc1_PosNo=1001;Loc1_TaskNo=T001;Loc1_HaveGoods=1;Loc1_Alarms=0;Loc1_State=1;...]
```

- 任务多条、货位多条，数量由配置 TaskReportCount、LocationCount 决定
- 前缀：Task{N}_、Loc{N}_，N 从 1 开始

### 6.4 堆垛机 — WCS 下发

```
[CMD=CRANE_TASK;Cmd=1;EquipmentTaskId=T001-2;PickCVNO=1004;PutCVNO=1005;ForkPickRow=01;ForkPickColumn=01;ForkPickLevel=01;ForkPutRow=01;ForkPutColumn=02;ForkPutLevel=03]
```

| 字段 | 说明 |
|------|------|
| PickCVNO/PutCVNO | 输送线节号 |
| ForkPickRow/Column/Level | 取货排-列-层 |
| ForkPutRow/Column/Level | 放货排-列-层 |

### 6.5 堆垛机 — PLC 上报

```
[CMD=CRANE_REPORT;DeviceNo=1;EquipmentTaskId=T001-2;TaskState=2;DeviceState=1;IsLoaded=1;XColumn=2;YLevel=3;ZRow=10;...]
```

---

## 七、配置（appsettings.json）

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=cursor_wcs;User Id=sa;Password=xxx;TrustServerCertificate=True;"
  },
  "Devices": [
    { "Code": "CV01", "Type": "Conveyor", "PlcIp": "192.168.158.1", "PlcPort": 2001 },
    { "Code": "CR01", "Type": "Crane", "PlcIp": "192.168.158.2", "PlcPort": 2002 }
  ],
  "Plc": {
    "TaskTimeoutSeconds": 60,
    "MaxRetryCount": 3
  },
  "CvPlc": {
    "TaskReportCount": 5,
    "LocationCount": 10
  },
  "PathConfig": {
    "JsonPath": "Config/paths.json"
  },
  "Serilog": {
    "MinimumLevel": "Information"
  }
}
```

---

## 八、路径配置导入

- 仅支持 JSON 导入
- 启动时从 `Config/paths.json` 加载到 PathConfig、CraneReachableConfig 表
- 或提供一次性导入接口 `POST /api/config/import-paths`

---

## 九、SignalR

### 9.1 Groups

| Group | 说明 |
|-------|------|
| view:realtime | 实时监控 3D |
| view:tasks | 当前任务 |
| view:devices | 设备列表 |
| view:messages:{DeviceCode} | 指定设备报文 |
| view:logs | 实时日志 |

### 9.2 推送策略

- 仅向当前视图对应 Group 推送
- 报文推送可节流（如 200–300ms 或 3–5 次/秒）
- 浏览器重连后需重新 JoinGroup

---

## 十、日志系统

### 10.1 后端

- Serilog
- 输出：Console、按日期文件（logs/wcs-20250308.log）
- 自定义 Sink 推送到 SignalR（view:logs）

### 10.2 分类

Api、Plc、Task、Device、System

### 10.3 前端

- 日志 Tab，实时日志子 Tab
- 按类型、级别筛选
- 限制最近 500 条

---

## 十一、Web API

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | /api/tasks | 当前任务列表（支持分页） |
| GET | /api/tasks/history | 历史任务（startDate, endDate, status, type, page, pageSize） |
| POST | /api/tasks | 手工创建任务 |
| GET | /api/devicetasks | 按任务号查询设备任务 |
| POST | /api/wms/inbound-orders | WMS 入库 |
| POST | /api/wms/outbound-orders | WMS 出库 |
| GET | /api/devices | 设备列表及状态 |
| GET | /api/devices/{code}/messages | 指定设备报文 |
| POST | /api/config/import-paths | 导入路径配置 |

---

## 十二、前端

### 12.1 技术栈

Vue 3、Vite、Three.js、Bootstrap 5 / Element Plus、Axios、@microsoft/signalr

### 12.2 主 Tab

| 主 Tab | 子 Tab | 内容 |
|--------|--------|------|
| 实时监控 3D | — | Three.js 3D 数字孪生 |
| 任务监控 | 当前任务 | 任务列表、设备任务明细 |
| 任务监控 | 历史任务 | 日期/状态/类型筛选、分页 |
| 任务监控 | 手动下任务 | 创建任务表单 |
| 设备管理 | — | 设备列表 + 选中后 50% 解析数据（固定格式）+ 50% 原始数据（最近 500 条） |
| 日志 | 实时日志 | 日志列表、类型/级别筛选 |

### 12.3 设备管理解析区固定格式

```
设备名：    CV01
任务号：    T001-1
指令：      TASK_START
结果：      OK
消息：      —
方向：      接收
时间：      2025-03-08 10:30:01
```

### 12.4 数据刷新

SignalR 按当前视图 Group 推送，仅更新当前界面数据。

---

## 十三、NuGet 包（后端）

- Microsoft.NET.Sdk.Web（.NET 10）
- Microsoft.AspNetCore.OpenApi
- Microsoft.EntityFrameworkCore.SqlServer
- Microsoft.EntityFrameworkCore.Tools
- Microsoft.AspNetCore.SignalR
- Serilog.AspNetCore
- Serilog.Sinks.File

---

## 十四、开发顺序建议

1. 后端：Domain、Infrastructure、数据库 Migrations
2. 后端：PathConfig、CraneReachableConfig、任务分解服务
3. 后端：PLC 通讯（CV、Crane 协议）
4. 后端：SignalR Hub、API、日志
5. 前端：Vue 项目、路由、主/子 Tab 布局
6. 前端：任务监控、设备管理、日志
7. 前端：3D 实时监控（可先占位或模拟数据）
8. 联调与测试

---

*文档版本：V2 | 基于讨论定稿*

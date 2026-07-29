# API 端点索引

默认地址为 `http://127.0.0.1:5080`。除健康检查、文档页面和 OpenAPI 定义外，
所有端点都需要 `X-API-Key`。完整请求/响应模型以
[`openapi.yaml`](openapi.yaml) 为准。

运行服务后可访问：

- Swagger UI：`http://127.0.0.1:5080/docs/`
- OpenAPI 3.0：`http://127.0.0.1:5080/openapi.yaml`

Swagger UI 从 jsDelivr 加载前端资源；在完全离线的环境中仍可直接下载
`/openapi.yaml` 并导入 Postman、Insomnia 或 Swagger Editor。

## 服务与设备会话

| 方法 | 路径 | 用途 | 详细文档 |
|---|---|---|---|
| `GET` | `/health/live` | 仅检查进程存活 | 本页 |
| `GET` | `/health`、`/health/ready` | COM、SQLite、事件库、STA readiness | 本页 |
| `GET` | `/api/v1/version` | 获取程序集与协议版本（需 API Key） | 本页 |
| `GET` | `/api/v1/capabilities` | 获取稳定全局能力标识（需 API Key） | 本页 |
| `GET` | `/api/v1/diagnostics/health` | 获取分项健康诊断（需 API Key） | 本页 |
| `GET` | `/api/v1/devices` | 获取当前设备会话列表 | [基础 API](API.md#5-获取设备列表) |
| `GET` | `/api/v1/devices/{deviceId}` | 获取单个设备状态 | [基础 API](API.md#6-获取单个设备状态) |
| `POST` | `/api/v1/devices/{deviceId}/connect` | 连接设备并保存配置 | [基础 API](API.md#7-连接设备) |
| `POST` | `/api/v1/devices/{deviceId}/disconnect` | 断开设备并关闭自动连接 | [基础 API](API.md#8-断开设备) |
| `POST` | `/api/v1/devices/{deviceId}/restart` | 重启设备 | [基础 API](API.md#10-重启设备) |

## 考勤

| 方法 | 路径 | 用途 | 详细文档 |
|---|---|---|---|
| `GET` | `/api/v1/devices/{deviceId}/attendance` | 拉取全部考勤，兼容旧调用 | [考勤 API](ATTENDANCE_API.md#拉取全部考勤记录) |
| `GET` | `/api/v1/devices/{deviceId}/attendance/query` | 时间范围查询与分页 | [考勤 API](ATTENDANCE_API.md#按时间范围查询并分页) |
| `POST` | `/api/v1/devices/{deviceId}/attendance/clear` | 全量、范围或时间点前清除 | [考勤 API](ATTENDANCE_API.md#清除考勤记录) |

## 持久化配置

| 方法 | 路径 | 用途 | 详细文档 |
|---|---|---|---|
| `GET` | `/api/v1/device-configurations` | 获取全部持久化配置 | [设备持久化](DEVICE_PERSISTENCE.md#配置列表) |
| `GET` | `/api/v1/device-configurations/{deviceId}` | 获取单个持久化配置 | [设备持久化](DEVICE_PERSISTENCE.md) |
| `PUT` | `/api/v1/device-configurations/{deviceId}` | 新建或更新持久化配置 | [设备持久化](DEVICE_PERSISTENCE.md#更新配置) |
| `DELETE` | `/api/v1/device-configurations/{deviceId}` | 删除持久化配置 | [设备持久化](DEVICE_PERSISTENCE.md#删除配置) |

## 人员、凭证与生物特征

| 方法 | 路径 | 用途 | 详细文档 |
|---|---|---|---|
| `GET` | `/api/v1/devices/{deviceId}/users` | 获取人员列表 | [扩展 API](EXTENDED_API.md#人员管理) |
| `GET` | `/api/v1/devices/{deviceId}/users/{enrollNumber}` | 获取人员 | [扩展 API](EXTENDED_API.md#人员管理) |
| `PUT` | `/api/v1/devices/{deviceId}/users/{enrollNumber}` | 新建或更新姓名、密码、卡号、权限 | [扩展 API](EXTENDED_API.md#人员管理) |
| `DELETE` | `/api/v1/devices/{deviceId}/users/{enrollNumber}` | 删除人员及登记数据 | [扩展 API](EXTENDED_API.md#人员管理) |
| `GET` | `/api/v1/devices/{deviceId}/users/{enrollNumber}/fingerprints/{fingerIndex}` | 获取指纹模板 | [扩展 API](EXTENDED_API.md#指纹模板) |
| `PUT` | `/api/v1/devices/{deviceId}/users/{enrollNumber}/fingerprints/{fingerIndex}` | 写入指纹模板 | [扩展 API](EXTENDED_API.md#指纹模板) |
| `DELETE` | `/api/v1/devices/{deviceId}/users/{enrollNumber}/fingerprints/{fingerIndex}` | 删除指纹模板 | [扩展 API](EXTENDED_API.md#指纹模板) |
| `GET` | `/api/v1/devices/{deviceId}/users/{enrollNumber}/face` | 获取人脸模板 | [扩展 API](EXTENDED_API.md#人脸模板) |
| `PUT` | `/api/v1/devices/{deviceId}/users/{enrollNumber}/face` | 写入人脸模板 | [扩展 API](EXTENDED_API.md#人脸模板) |
| `DELETE` | `/api/v1/devices/{deviceId}/users/{enrollNumber}/face` | 删除人脸模板 | [扩展 API](EXTENDED_API.md#人脸模板) |
| `PUT` | `/api/v1/devices/{deviceId}/users/{enrollNumber}/photo` | 上传用户照片或可见光人脸照片 | [扩展 API](EXTENDED_API.md#人员照片与可见光人脸照片) |
| `GET` | `/api/v1/devices/{deviceId}/users/{enrollNumber}/photo` | 下载设备原始 JPG 用户照片（非人脸模板） | [扩展 API](EXTENDED_API.md#人员照片与可见光人脸照片) |

人员接口的 `password` 与 `cardNumber` 字段即密码和卡号管理入口；查询响应不会返回
密码明文，只返回 `hasPassword`。

## 门禁

| 方法 | 路径 | 用途 | 详细文档 |
|---|---|---|---|
| `GET` | `/api/v1/devices/{deviceId}/capabilities` | 探测人员 ID、门禁、常开、门状态和照片能力 | [扩展 API](EXTENDED_API.md#设备能力探测) |
| `GET` | `/api/v1/devices/{deviceId}/access/door-state` | 获取当前门开关状态 | [扩展 API](EXTENDED_API.md#门状态与常开) |
| `POST` | `/api/v1/devices/{deviceId}/access/unlock` | 远程开门 | [扩展 API](EXTENDED_API.md#远程开门) |
| `POST` | `/api/v1/devices/{deviceId}/access/normally-open/start` | 开始常开并返回原锁驱动时长 | [扩展 API](EXTENDED_API.md#门状态与常开) |
| `POST` | `/api/v1/devices/{deviceId}/access/normally-open/end` | 结束常开并恢复调用方保存的锁驱动时长 | [扩展 API](EXTENDED_API.md#门状态与常开) |
| `GET` | `/api/v1/devices/{deviceId}/access/time-zones/{index}` | 获取门禁时间段 | [扩展 API](EXTENDED_API.md#门禁时间段) |
| `PUT` | `/api/v1/devices/{deviceId}/access/time-zones/{index}` | 设置门禁时间段 | [扩展 API](EXTENDED_API.md#门禁时间段) |
| `GET` | `/api/v1/devices/{deviceId}/access/groups/{groupNumber}` | 获取权限组 | [扩展 API](EXTENDED_API.md#权限组) |
| `PUT` | `/api/v1/devices/{deviceId}/access/groups/{groupNumber}` | 设置权限组 | [扩展 API](EXTENDED_API.md#权限组) |
| `GET` | `/api/v1/devices/{deviceId}/access/users/{enrollNumber}` | 获取人员门禁权限 | [扩展 API](EXTENDED_API.md#人员权限分配) |
| `PUT` | `/api/v1/devices/{deviceId}/access/users/{enrollNumber}` | 设置人员门禁权限 | [扩展 API](EXTENDED_API.md#人员权限分配) |
| `GET` | `/api/v1/devices/{deviceId}/access/unlock-combinations/{number}` | 获取多人开锁组合 | [扩展 API](EXTENDED_API.md#多人开锁组合) |
| `PUT` | `/api/v1/devices/{deviceId}/access/unlock-combinations/{number}` | 设置多人开锁组合 | [扩展 API](EXTENDED_API.md#多人开锁组合) |

## 实时事件

| 协议 | 路径 | 用途 | 详细文档 |
|---|---|---|---|
| WebSocket | `/api/v1/events/ws` | 订阅考勤、门、报警等实时事件 | [实时事件](REALTIME_EVENTS.md) |

可通过 `deviceId` 和逗号分隔的 `eventType` 查询参数过滤订阅，并通过
`afterSequence` 从 SQLite 断点续传。服务端客户端应使用 `X-API-Key` 请求头；
浏览器客户端可使用 `apiKey` 查询参数。

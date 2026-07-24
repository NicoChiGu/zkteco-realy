# 考勤记录 API

本文档对应 SDK 手册中的 `ReadAllGLogData`、`ReadTimeGLogData`、
`SSR_GetGeneralLogData`、`ClearGLog`、`DeleteAttlogBetweenTheDate` 和
`DeleteAttlogByTime`。

所有时间参数均使用 ISO 8601。Relay 会将时间转换为 Windows 主机本地时间后传给设备，
因此部署时应确保 Relay 主机与设备使用相同的时区并已正确校时。

## 拉取全部考勤记录

```http
GET /api/v1/devices/{deviceId}/attendance
X-API-Key: your-secret-key
```

响应体保持为 `AttendanceRecord[]`，用于兼容已有调用方。该端点每次读取设备当前可用的
全部普通考勤记录，不提供服务端游标。

```json
[
  {
    "enrollNumber": "10001",
    "verifyMode": 1,
    "inOutMode": 0,
    "timestamp": "2026-07-24T09:15:30+08:00",
    "workCode": 0
  }
]
```

## 按时间范围查询并分页

```http
GET /api/v1/devices/{deviceId}/attendance/query?from=2026-07-01T00:00:00%2B08:00&to=2026-07-31T23:59:59%2B08:00&page=1&pageSize=100
X-API-Key: your-secret-key
```

查询参数：

| 参数 | 类型 | 必填 | 默认值 | 说明 |
|---|---|---:|---:|---|
| `from` | ISO 8601 date-time | 否 | 无 | 包含该时刻；可单独使用 |
| `to` | ISO 8601 date-time | 否 | 无 | 包含该时刻；可单独使用 |
| `page` | integer | 否 | `1` | 从 1 开始 |
| `pageSize` | integer | 否 | `100` | 范围 `1-1000` |

当 `from` 和 `to` 同时存在时，Relay 优先调用厂商的 `ReadTimeGLogData`。
该函数只适用于部分新架构固件；如果设备不支持，Relay 会回退到
`ReadAllGLogData` 并在本地过滤。分页发生在设备数据读取完成之后，
因此分页可限制 HTTP 响应大小，但不能减少旧固件的设备传输量。

成功响应：

```json
{
  "items": [
    {
      "enrollNumber": "10001",
      "verifyMode": 1,
      "inOutMode": 0,
      "timestamp": "2026-07-24T09:15:30+08:00",
      "workCode": 0
    }
  ],
  "page": 1,
  "pageSize": 100,
  "totalItems": 1,
  "totalPages": 1,
  "from": "2026-07-01T00:00:00+08:00",
  "to": "2026-07-31T23:59:59+08:00"
}
```

## 清除考勤记录

```http
POST /api/v1/devices/{deviceId}/attendance/clear
Content-Type: application/json
X-API-Key: your-secret-key
```

这是不可恢复的设备写操作。所有模式都必须显式传入 `"confirm": true`。

### 清除全部

对应 SDK `ClearGLog`：

```json
{
  "confirm": true
}
```

### 清除时间范围

对应 SDK `DeleteAttlogBetweenTheDate`，`from` 和 `to` 必须同时存在：

```json
{
  "confirm": true,
  "from": "2026-07-01T00:00:00+08:00",
  "to": "2026-07-31T23:59:59+08:00"
}
```

### 清除指定时刻之前的记录

对应 SDK `DeleteAttlogByTime`：

```json
{
  "confirm": true,
  "before": "2026-07-01T00:00:00+08:00"
}
```

`before` 不能与 `from` 或 `to` 混用。按时间删除接口只适用于部分新架构固件；
不支持时会返回厂商错误，不会自动退化为全量清除。

成功响应：

```json
{
  "success": true,
  "vendorErrorCode": null,
  "message": null
}
```

未确认：

```http
HTTP/1.1 400 Bad Request
```

```json
{
  "code": "invalid_request",
  "message": "confirm must be true before attendance records can be deleted."
}
```

设备会话不存在时返回 `404`，设备未连接时返回 `409`，厂商 SDK 拒绝操作时返回
`400` 并在 `vendorErrorCode` 中提供原始错误码。

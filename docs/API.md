# ZKTeco Relay REST API 开发文档

本文档对应当前 `ZktecoRelay` 实现，用于帮助 Node.js、Java、Python、Go、C# 等后端服务接入运行在 Windows 上的 ZKTeco Relay。

> 当前 API 设计为服务器到服务器调用。服务未启用 CORS，不建议浏览器前端直接访问。Web 前端应调用自己的业务后端，再由业务后端调用 Relay。

## 1. 基本信息

默认服务地址：

```text
http://127.0.0.1:5080
```

API 前缀：

```text
/api/v1
```

环境变量：

| 环境变量 | 必填 | 默认值 | 说明 |
|---|---:|---|---|
| `ZKTECO_API_KEY` | 是 | 无 | 固定 API 密钥，至少 16 个字符，建议 32～64 个随机字符 |
| `ZKTECO_BIND_URL` | 否 | `http://127.0.0.1:5080` | HTTP 监听地址 |

示例 `.env`：

```dotenv
ZKTECO_API_KEY=replace-with-a-long-random-secret
ZKTECO_BIND_URL=http://127.0.0.1:5080
```

## 2. 鉴权

除健康检查接口外，所有请求都必须包含：

```http
X-API-Key: replace-with-a-long-random-secret
```

缺少密钥：

```http
HTTP/1.1 401 Unauthorized
Content-Type: application/json
```

```json
{
  "code": "missing_api_key",
  "message": "X-API-Key header is required."
}
```

密钥错误：

```http
HTTP/1.1 401 Unauthorized
Content-Type: application/json
```

```json
{
  "code": "invalid_api_key",
  "message": "The supplied API key is invalid."
}
```

## 3. 通用约定

### 3.1 Content-Type

带请求体的接口使用：

```http
Content-Type: application/json
```

响应通常为：

```http
Content-Type: application/json; charset=utf-8
```

健康检查接口是纯文本响应。

### 3.2 deviceId

`deviceId` 是由调用方定义的设备唯一标识，不是 ZKTeco 设备自动返回的序列号。

建议格式：

```text
office-a-gate-01
warehouse-attendance-02
```

限制：

- 必填。
- 最长 64 个字符。
- 建议只使用字母、数字、短横线和下划线。
- 同一个 `deviceId` 对应一个设备会话。

### 3.3 时间格式

时间字段使用 ISO 8601：

```text
2026-07-16T08:30:12+08:00
```

### 3.4 错误对象

大部分失败响应使用：

```json
{
  "code": "device_not_found",
  "message": "Device was not found."
}
```

字段：

| 字段 | 类型 | 说明 |
|---|---|---|
| `code` | string | 稳定的机器可读错误码 |
| `message` | string | 面向开发者的错误说明 |

连接失败是一个例外：它返回 `DeviceConnectionResult`，因为其中包含厂商错误码。

## 4. 健康检查

### 请求

```http
GET /health
```

不需要 `X-API-Key`。

### curl

```bash
curl http://127.0.0.1:5080/health
```

### 成功响应

```http
HTTP/1.1 200 OK
Content-Type: text/plain; charset=utf-8
```

```text
Healthy
```

> 该接口只表示 HTTP 服务正在运行，不代表设备已经连接。

## 5. 获取设备列表

### 请求

```http
GET /api/v1/devices
X-API-Key: your-secret-key
```

### curl

```bash
curl \
  -H "X-API-Key: your-secret-key" \
  http://127.0.0.1:5080/api/v1/devices
```

### 成功响应

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
```

```json
[
  {
    "deviceId": "office-gate-01",
    "ipAddress": "192.168.1.100",
    "port": 4370,
    "connected": true,
    "connectedAt": "2026-07-16T08:15:22.4567890+08:00",
    "lastError": null
  },
  {
    "deviceId": "warehouse-01",
    "ipAddress": "192.168.1.101",
    "port": 4370,
    "connected": false,
    "connectedAt": null,
    "lastError": "Connect_Net failed. Vendor error: -1."
  }
]
```

当没有设备会话时：

```json
[]
```

### DeviceStatus 字段

| 字段 | 类型 | 可空 | 说明 |
|---|---|---:|---|
| `deviceId` | string | 否 | 调用方定义的设备标识 |
| `ipAddress` | string | 否 | 设备 IP |
| `port` | integer | 否 | 设备通信端口，通常为 4370 |
| `connected` | boolean | 否 | 当前会话是否标记为已连接 |
| `connectedAt` | string | 是 | 最近连接成功时间 |
| `lastError` | string | 是 | 最近一次设备操作错误 |

## 6. 获取单个设备状态

### 请求

```http
GET /api/v1/devices/{deviceId}
X-API-Key: your-secret-key
```

### 示例

```bash
curl \
  -H "X-API-Key: your-secret-key" \
  http://127.0.0.1:5080/api/v1/devices/office-gate-01
```

### 成功响应

```http
HTTP/1.1 200 OK
```

```json
{
  "deviceId": "office-gate-01",
  "ipAddress": "192.168.1.100",
  "port": 4370,
  "connected": true,
  "connectedAt": "2026-07-16T08:15:22.4567890+08:00",
  "lastError": null
}
```

### 设备不存在

```http
HTTP/1.1 404 Not Found
```

```json
{
  "code": "device_not_found",
  "message": "Device was not found."
}
```

## 7. 连接设备

### 请求

```http
POST /api/v1/devices/{deviceId}/connect
Content-Type: application/json
X-API-Key: your-secret-key
```

请求体：

```json
{
  "ipAddress": "192.168.1.100",
  "port": 4370,
  "communicationPassword": ""
}
```

### 请求字段

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|---|---|---:|---|---|
| `ipAddress` | string | 是 | 无 | ZKTeco 设备 IP 地址 |
| `port` | integer | 否 | `4370` | 设备通信端口，范围 1～65535 |
| `communicationPassword` | string | 否 | 空字符串 | 设备通信密码，不是 API Key |

> `communicationPassword` 是设备侧配置的通信密码。它不会出现在状态查询响应中。

### curl

```bash
curl -X POST \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your-secret-key" \
  -d '{
    "ipAddress": "192.168.1.100",
    "port": 4370,
    "communicationPassword": ""
  }' \
  http://127.0.0.1:5080/api/v1/devices/office-gate-01/connect
```

### 连接成功

```http
HTTP/1.1 200 OK
```

```json
{
  "deviceId": "office-gate-01",
  "connected": true,
  "error": null,
  "vendorErrorCode": null
}
```

### 连接失败

```http
HTTP/1.1 400 Bad Request
```

```json
{
  "deviceId": "office-gate-01",
  "connected": false,
  "error": "Connect_Net failed. Vendor error: -1.",
  "vendorErrorCode": -1
}
```

字段：

| 字段 | 类型 | 可空 | 说明 |
|---|---|---:|---|
| `deviceId` | string | 否 | 设备标识 |
| `connected` | boolean | 否 | 是否连接成功 |
| `error` | string | 是 | 连接失败说明 |
| `vendorErrorCode` | integer | 是 | ZKTeco SDK 返回的厂商错误码 |

### IP 地址无效

```http
HTTP/1.1 400 Bad Request
```

```json
{
  "code": "invalid_request",
  "message": "IpAddress must be a valid IPv4 or IPv6 address."
}
```

### 端口无效

```json
{
  "code": "invalid_request",
  "message": "Port must be between 1 and 65535."
}
```

### deviceId 无效

```json
{
  "code": "invalid_request",
  "message": "deviceId is required and must not exceed 64 characters."
}
```

### 会话行为

- 相同 `deviceId` 和相同 IP/端口再次连接时，会复用现有会话并重新建立连接。
- 相同 `deviceId` 但 IP 或端口改变时，会断开并销毁旧会话，再创建新会话。
- 同一设备上的命令由 Relay 串行执行。

## 8. 断开设备

### 请求

```http
POST /api/v1/devices/{deviceId}/disconnect
X-API-Key: your-secret-key
```

无请求体。

### curl

```bash
curl -X POST \
  -H "X-API-Key: your-secret-key" \
  http://127.0.0.1:5080/api/v1/devices/office-gate-01/disconnect
```

### 成功响应

```http
HTTP/1.1 200 OK
```

```json
{
  "deviceId": "office-gate-01",
  "connected": false
}
```

断开后，该会话会从内存中移除。

### 设备不存在

```http
HTTP/1.1 404 Not Found
```

```json
{
  "code": "device_not_found",
  "message": "Device was not found."
}
```

## 9. 拉取考勤记录

### 请求

```http
GET /api/v1/devices/{deviceId}/attendance
X-API-Key: your-secret-key
```

### curl

```bash
curl \
  -H "X-API-Key: your-secret-key" \
  http://127.0.0.1:5080/api/v1/devices/office-gate-01/attendance
```

### 成功响应

```http
HTTP/1.1 200 OK
```

```json
[
  {
    "enrollNumber": "10001",
    "verifyMode": 1,
    "inOutMode": 0,
    "timestamp": "2026-07-16T08:31:12+08:00",
    "workCode": 0
  },
  {
    "enrollNumber": "10002",
    "verifyMode": 4,
    "inOutMode": 1,
    "timestamp": "2026-07-16T08:32:09+08:00",
    "workCode": 0
  }
]
```

没有记录时：

```json
[]
```

### AttendanceRecord 字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `enrollNumber` | string | 设备中的人员编号 |
| `verifyMode` | integer | 厂商 SDK 返回的验证方式代码 |
| `inOutMode` | integer | 厂商 SDK 返回的进出/考勤状态代码 |
| `timestamp` | string | 打卡时间，ISO 8601 |
| `workCode` | integer | 工作代码 |

> `verifyMode` 和 `inOutMode` 当前保留 SDK 原始整数值。不同型号或固件可能有不同含义，业务系统应结合所用设备的数据字典建立映射，不建议直接把数字展示给终端用户。

### 设备会话不存在

```http
HTTP/1.1 404 Not Found
```

```json
{
  "code": "device_not_found",
  "message": "Device 'office-gate-01' was not found."
}
```

### 设备未连接

```http
HTTP/1.1 409 Conflict
```

```json
{
  "code": "device_unavailable",
  "message": "Device 'office-gate-01' is not connected."
}
```

### 当前读取语义

当前接口调用 SDK 的“读取全部普通考勤记录”流程：

- 它不是分页 API。
- 它没有 `from`、`to`、`limit` 参数。
- 返回记录数量取决于设备中保存的数据。
- 调用方应根据 `deviceId + enrollNumber + timestamp + verifyMode` 等字段做幂等去重。
- 大量历史记录可能导致请求耗时较长，业务端应设置合理超时。

## 10. 重启设备

### 请求

```http
POST /api/v1/devices/{deviceId}/restart
X-API-Key: your-secret-key
```

无请求体。

### curl

```bash
curl -X POST \
  -H "X-API-Key: your-secret-key" \
  http://127.0.0.1:5080/api/v1/devices/office-gate-01/restart
```

### 成功响应

```http
HTTP/1.1 202 Accepted
```

```json
{
  "deviceId": "office-gate-01",
  "restarted": true
}
```

重启命令发出后，Relay 会把当前会话标记为未连接。设备恢复后需要重新调用连接接口。

### 设备会话不存在

```http
HTTP/1.1 404 Not Found
```

```json
{
  "code": "device_not_found",
  "message": "Device 'office-gate-01' was not found."
}
```

### 设备未连接

```http
HTTP/1.1 409 Conflict
```

```json
{
  "code": "device_unavailable",
  "message": "Device 'office-gate-01' is not connected."
}
```

## 11. HTTP 状态码汇总

| 状态码 | 场景 |
|---:|---|
| `200` | 查询成功、连接成功、断开成功、考勤读取成功 |
| `202` | 已接受重启命令 |
| `400` | 请求参数错误，或设备连接失败 |
| `401` | 缺少 API Key 或 API Key 错误 |
| `404` | 设备会话不存在 |
| `409` | 设备存在但当前未连接或不可执行操作 |
| `500` | 未处理异常，例如 COM 注册、DLL 加载或 SDK 内部异常 |

## 12. Node.js / TypeScript SDK 示例

```ts
type ApiError = {
  code: string;
  message: string;
};

type DeviceConnectionResult = {
  deviceId: string;
  connected: boolean;
  error: string | null;
  vendorErrorCode: number | null;
};

type DeviceStatus = {
  deviceId: string;
  ipAddress: string;
  port: number;
  connected: boolean;
  connectedAt: string | null;
  lastError: string | null;
};

type AttendanceRecord = {
  enrollNumber: string;
  verifyMode: number;
  inOutMode: number;
  timestamp: string;
  workCode: number;
};

export class ZktecoRelayClient {
  constructor(
    private readonly baseUrl: string,
    private readonly apiKey: string,
  ) {}

  private async request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const response = await fetch(`${this.baseUrl}${path}`, {
      ...init,
      headers: {
        accept: 'application/json',
        'x-api-key': this.apiKey,
        ...(init.body ? { 'content-type': 'application/json' } : {}),
        ...init.headers,
      },
    });

    const contentType = response.headers.get('content-type') ?? '';
    const body = contentType.includes('application/json')
      ? await response.json()
      : await response.text();

    if (!response.ok) {
      throw new Error(
        typeof body === 'string'
          ? `Relay ${response.status}: ${body}`
          : `Relay ${response.status}: ${JSON.stringify(body)}`,
      );
    }

    return body as T;
  }

  connect(
    deviceId: string,
    input: {
      ipAddress: string;
      port?: number;
      communicationPassword?: string;
    },
  ): Promise<DeviceConnectionResult> {
    return this.request(`/api/v1/devices/${encodeURIComponent(deviceId)}/connect`, {
      method: 'POST',
      body: JSON.stringify(input),
    });
  }

  listDevices(): Promise<DeviceStatus[]> {
    return this.request('/api/v1/devices');
  }

  getDevice(deviceId: string): Promise<DeviceStatus> {
    return this.request(`/api/v1/devices/${encodeURIComponent(deviceId)}`);
  }

  readAttendance(deviceId: string): Promise<AttendanceRecord[]> {
    return this.request(
      `/api/v1/devices/${encodeURIComponent(deviceId)}/attendance`,
    );
  }

  restart(deviceId: string): Promise<{ deviceId: string; restarted: boolean }> {
    return this.request(`/api/v1/devices/${encodeURIComponent(deviceId)}/restart`, {
      method: 'POST',
    });
  }

  disconnect(deviceId: string): Promise<{ deviceId: string; connected: false }> {
    return this.request(
      `/api/v1/devices/${encodeURIComponent(deviceId)}/disconnect`,
      { method: 'POST' },
    );
  }
}
```

使用：

```ts
const client = new ZktecoRelayClient(
  process.env.ZKTECO_RELAY_URL ?? 'http://127.0.0.1:5080',
  process.env.ZKTECO_RELAY_KEY!,
);

const connection = await client.connect('office-gate-01', {
  ipAddress: '192.168.1.100',
  port: 4370,
  communicationPassword: '',
});

if (!connection.connected) {
  throw new Error(
    `设备连接失败，厂商错误码：${connection.vendorErrorCode ?? 'unknown'}`,
  );
}

const records = await client.readAttendance('office-gate-01');
console.log(records);
```

## 13. Python 示例

```python
import requests

BASE_URL = "http://127.0.0.1:5080"
API_KEY = "your-secret-key"

headers = {
    "X-API-Key": API_KEY,
    "Content-Type": "application/json",
}

response = requests.post(
    f"{BASE_URL}/api/v1/devices/office-gate-01/connect",
    headers=headers,
    json={
        "ipAddress": "192.168.1.100",
        "port": 4370,
        "communicationPassword": "",
    },
    timeout=15,
)
response.raise_for_status()
print(response.json())

records = requests.get(
    f"{BASE_URL}/api/v1/devices/office-gate-01/attendance",
    headers={"X-API-Key": API_KEY},
    timeout=120,
)
records.raise_for_status()
print(records.json())
```

## 14. 推荐的业务集成流程

```text
1. 业务服务启动
2. 调用 GET /health 确认 Relay 存活
3. 调用 POST /connect 建立设备会话
4. 定时调用 GET /attendance 拉取记录
5. 在业务数据库中做幂等去重
6. 失败时记录 HTTP 状态、错误 code、vendorErrorCode
7. 网络恢复后重新连接
8. 程序退出或设备下线时调用 POST /disconnect
```

建议不要在每次读取考勤前都创建新的 `deviceId`。应为同一台物理设备使用稳定标识。

## 15. 超时、重试和幂等建议

### 连接接口

- 建议超时：10～20 秒。
- 网络错误可重试 2～3 次。
- 建议指数退避，例如 1 秒、3 秒、10 秒。

### 考勤读取

- 建议超时：60～180 秒，取决于设备历史记录数量。
- 不要无间隔高频调用。
- 建议每 1～5 分钟拉取一次，具体按业务要求调整。
- 当前接口可能重复返回历史记录，数据库必须去重。

### 重启接口

- 返回 `202` 只表示命令已发出。
- 不应立即连续重复发送。
- 重启后等待设备恢复，再重新连接。

## 16. 安全建议

- Relay 端口只开放给业务服务器。
- 优先绑定 `127.0.0.1` 或具体内网 IP。
- 不要把 API Key 放入前端代码。
- 不要通过 URL Query 传递 API Key。
- 使用 Windows 防火墙限制来源 IP。
- 跨主机或跨网段时，建议使用 VPN、mTLS 或 HTTPS 反向代理。
- 不要记录设备通信密码。
- 定期轮换 `ZKTECO_API_KEY`。

## 17. 当前未实现能力

当前版本尚未提供：

- 分页和按时间范围查询考勤。
- 清除设备考勤记录。
- 实时事件 WebSocket。
- 用户增删改查。
- 卡号、密码、指纹模板管理。
- 人脸照片管理。
- 门禁开门控制。
- OpenAPI UI 页面。

仓库中提供了静态 `docs/openapi.yaml`，可导入 Swagger Editor、Postman、Insomnia 或代码生成工具。
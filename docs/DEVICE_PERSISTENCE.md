# 设备配置持久化与自动重连

## 数据库

默认 SQLite 数据库：

```text
data/zkteco-relay.db
```

自定义路径：

```dotenv
ZKTECO_DATABASE_PATH=D:\ZktecoRelayData\zkteco-relay.db
```

设备通信密码使用 Windows DPAPI（LocalMachine）加密后保存。API 查询不会返回密码内容。

## 自动保存

调用连接接口时会保存或更新设备配置，并启用自动重连：

```http
POST /api/v1/devices/front-door/connect
X-API-Key: your-key
Content-Type: application/json

{
  "ipAddress": "192.168.1.100",
  "port": 4370,
  "communicationPassword": "1234"
}
```

服务重新启动后会自动连接 `autoConnect=true` 的设备。

调用断开接口会关闭该设备的自动重连：

```http
POST /api/v1/devices/front-door/disconnect
X-API-Key: your-key
```

## 配置列表

```http
GET /api/v1/device-configurations
X-API-Key: your-key
```

响应：

```json
[
  {
    "deviceId": "front-door",
    "ipAddress": "192.168.1.100",
    "port": 4370,
    "hasCommunicationPassword": true,
    "autoConnect": true,
    "updatedAt": "2026-07-16T18:20:00+00:00"
  }
]
```

## 更新配置

`communicationPassword` 为 `null` 时保留原密码。

```http
PUT /api/v1/device-configurations/front-door
X-API-Key: your-key
Content-Type: application/json

{
  "ipAddress": "192.168.1.101",
  "port": 4370,
  "communicationPassword": null,
  "autoConnect": true
}
```

## 删除配置

```http
DELETE /api/v1/device-configurations/front-door
X-API-Key: your-key
```

删除配置不会主动断开当前内存会话。需要同时断开时，先调用 `/disconnect`，再删除配置。

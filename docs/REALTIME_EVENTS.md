# 实时事件 WebSocket

## 地址

```text
ws://127.0.0.1:5080/api/v1/events/ws
```

服务端 WebSocket 客户端应优先使用请求头：

```http
X-API-Key: your-secret-key
```

浏览器原生 `WebSocket` 不能设置自定义请求头，可使用查询参数：

```text
ws://127.0.0.1:5080/api/v1/events/ws?apiKey=your-secret-key
```

查询参数可能出现在代理访问日志中。生产环境建议通过后端服务建立 WebSocket，或在可信内网中使用。

## 过滤

只订阅一个设备：

```text
/api/v1/events/ws?deviceId=front-door
```

只订阅指定事件：

```text
/api/v1/events/ws?eventType=attendance,door,alarm
```

可以组合使用：

```text
/api/v1/events/ws?deviceId=front-door&eventType=attendance,door
```

## 通用消息

```json
{
  "eventId": "da3cf34e4cbc40dfbec576a628052fb6",
  "deviceId": "front-door",
  "eventType": "attendance",
  "occurredAt": "2026-07-16T10:22:31.125Z",
  "data": {}
}
```

## 考勤事件

```json
{
  "eventId": "da3cf34e4cbc40dfbec576a628052fb6",
  "deviceId": "front-door",
  "eventType": "attendance",
  "occurredAt": "2026-07-16T10:22:31.125Z",
  "data": {
    "enrollNumber": "10001",
    "isInvalid": false,
    "attendanceState": 0,
    "verifyMethod": 1,
    "timestamp": "2026-07-16T18:22:31+08:00",
    "year": 2026,
    "month": 7,
    "day": 16,
    "hour": 18,
    "minute": 22,
    "second": 31,
    "workCode": 0
  }
}
```

## 其他事件类型

- `websocket_connected`
- `events_registered`
- `events_registration_failed`
- `connected`
- `finger_detected`
- `verification`
- `finger_feature`
- `card_swiped`
- `door`
- `alarm`
- `template_deleted`
- `finger_enrolled`
- `attendance`

不同设备型号和固件可能只支持其中一部分事件。实时事件注册失败不会阻止设备的普通 REST API 连接。

## 浏览器示例

```js
const url = new URL("ws://127.0.0.1:5080/api/v1/events/ws");
url.searchParams.set("apiKey", "your-secret-key");
url.searchParams.set("deviceId", "front-door");
url.searchParams.set("eventType", "attendance,door,alarm");

const socket = new WebSocket(url);
socket.onmessage = event => {
  const message = JSON.parse(event.data);
  console.log(message.eventType, message.deviceId, message.data);
};
```

## Node.js 示例

```ts
import WebSocket from "ws";

const socket = new WebSocket(
  "ws://127.0.0.1:5080/api/v1/events/ws?eventType=attendance,door",
  {
    headers: {
      "X-API-Key": process.env.ZKTECO_RELAY_KEY!,
    },
  },
);

socket.on("message", payload => {
  console.log(JSON.parse(payload.toString()));
});
```

每个 WebSocket 客户端使用独立的有界缓冲区。客户端消费过慢时会丢弃最旧事件，避免慢客户端拖垮设备线程；业务系统应及时消费并自行持久化关键事件。

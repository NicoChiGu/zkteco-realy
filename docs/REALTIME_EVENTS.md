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
  "eventSequence": "1025",
  "deviceId": "front-door",
  "eventType": "attendance",
  "occurredAt": "2026-07-16T10:22:31.125Z",
  "data": {}
}
```

`eventSequence` 是 SQLite 中单调递增的 64 位整数，并始终以十进制字符串传输，
避免 JavaScript `number` 精度丢失。`websocket_connected` 和
`event_replay_gap` 是连接控制消息，不带序号。

## 断点续传

客户端只应在事件及其业务副作用已在同一事务中提交后保存检查点。重连时传入：

```text
/api/v1/events/ws?afterSequence=1025
```

Relay 会从 SQLite 顺序补发 `1025` 之后的事件，再继续推送实时事件。即使客户端
消费速度较慢，也会继续从事件库分批读取，不依赖会丢旧数据的内存消息队列。

事件默认保留 30 天且最多 100,000 条；达到任一上限时删除最旧记录。如果
`afterSequence` 早于最早可用事件，Relay 先发送 `event_replay_gap`，其中包含
请求序号和最早可用序号，随后从最早记录继续。调用方必须记录运维告警；可通过
批量读取补账的考勤不应与门状态、报警等不可恢复缺口混为一谈。

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
- `device_status_changed`
- `event_replay_gap`

不同设备型号和固件可能只支持其中一部分事件。实时事件注册失败不会阻止设备的普通 REST API 连接。

## 浏览器示例

```js
const url = new URL("ws://127.0.0.1:5080/api/v1/events/ws");
url.searchParams.set("apiKey", "your-secret-key");
url.searchParams.set("deviceId", "front-door");
url.searchParams.set("eventType", "attendance,door,alarm");
url.searchParams.set("afterSequence", localStorage.getItem("relaySequence") ?? "0");

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
  const event = JSON.parse(payload.toString());
  console.log(event.eventSequence, event.eventType);
});
```

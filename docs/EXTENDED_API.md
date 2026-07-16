# 人员、生物特征与门禁 API

所有接口都要求 `X-API-Key`，并且设备必须先通过 `/connect` 建立会话。

## 人员管理

```http
GET    /api/v1/devices/{deviceId}/users
GET    /api/v1/devices/{deviceId}/users/{enrollNumber}
PUT    /api/v1/devices/{deviceId}/users/{enrollNumber}
DELETE /api/v1/devices/{deviceId}/users/{enrollNumber}
```

新增或更新人员：

```json
{
  "name": "张三",
  "password": "1234",
  "privilege": 0,
  "enabled": true,
  "cardNumber": "98653214"
}
```

`password` 与 `cardNumber` 省略或传 `null` 时，更新已有人员会尽量保留设备上的原值。查询人员时不会返回密码明文，只返回 `hasPassword`。

成功响应：

```json
{
  "success": true,
  "vendorErrorCode": null,
  "message": null
}
```

人员示例：

```json
{
  "enrollNumber": "10001",
  "name": "张三",
  "privilege": 0,
  "enabled": true,
  "cardNumber": "98653214",
  "hasPassword": true
}
```

## 指纹模板

```http
GET    /api/v1/devices/{deviceId}/users/{enrollNumber}/fingerprints/{fingerIndex}
PUT    /api/v1/devices/{deviceId}/users/{enrollNumber}/fingerprints/{fingerIndex}
DELETE /api/v1/devices/{deviceId}/users/{enrollNumber}/fingerprints/{fingerIndex}
```

`fingerIndex` 通常为 `0-9`。

```json
{
  "fingerIndex": 0,
  "templateData": "厂商SDK字符串模板"
}
```

模板必须来自兼容的 ZKTeco 指纹算法版本。不同算法版本的模板不能假定可互换。

## 人脸模板

```http
GET    /api/v1/devices/{deviceId}/users/{enrollNumber}/face?faceIndex=50
PUT    /api/v1/devices/{deviceId}/users/{enrollNumber}/face
DELETE /api/v1/devices/{deviceId}/users/{enrollNumber}/face?faceIndex=50
```

```json
{
  "templateData": "厂商SDK字符串人脸模板",
  "faceIndex": 50
}
```

文档规定人脸索引通常为 `50`，该接口主要适用于支持 7.0 人脸算法的 IFACE 设备。

## 人员照片与可见光人脸照片

```http
PUT /api/v1/devices/{deviceId}/users/{enrollNumber}/photo
```

普通人员照片：

```json
{
  "base64Jpeg": "/9j/4AAQSkZJRgABAQ...",
  "visibleLightFacePhoto": false
}
```

可见光比对照片：

```json
{
  "base64Jpeg": "/9j/4AAQSkZJRgABAQ...",
  "visibleLightFacePhoto": true
}
```

服务会临时生成 `{工号}.jpg` 或 `verify_biophoto_9_{工号}.jpg`，调用 SDK 后删除临时文件。

## 远程开门

```http
POST /api/v1/devices/{deviceId}/access/unlock
```

```json
{
  "delayTenthsOfSecond": 30
}
```

`30` 表示约 3 秒，SDK 的单位是十分之一秒。

## 门禁时间段

```http
GET /api/v1/devices/{deviceId}/access/time-zones/{index}
PUT /api/v1/devices/{deviceId}/access/time-zones/{index}
```

```json
{
  "timeZoneIndex": 1,
  "schedule": "08001800080018000800180008001800080018000800180008001800"
}
```

`schedule` 固定为 56 位数字，按周日到周六排列，每天 8 位，格式为 `HHmmHHmm`。

## 权限组

```http
GET /api/v1/devices/{deviceId}/access/groups/{groupNumber}
PUT /api/v1/devices/{deviceId}/access/groups/{groupNumber}
```

```json
{
  "groupNumber": 2,
  "timeZone1": 1,
  "timeZone2": 2,
  "timeZone3": 0,
  "holidayValid": false,
  "verifyStyle": 0
}
```

TFT/IFACE 设备通常支持 `1-99` 组。`verifyStyle` 保留厂商整数编码。

## 人员权限分配

```http
GET /api/v1/devices/{deviceId}/access/users/{enrollNumber}
PUT /api/v1/devices/{deviceId}/access/users/{enrollNumber}
```

使用组时间段：

```json
{
  "groupNumber": 2,
  "timeZone1": 0,
  "timeZone2": 0,
  "timeZone3": 0,
  "useGroupTimeZone": true
}
```

使用个人时间段：

```json
{
  "groupNumber": 2,
  "timeZone1": 1,
  "timeZone2": 2,
  "timeZone3": 3,
  "useGroupTimeZone": false
}
```

此组接口底层使用旧式门禁函数，`enrollNumber` 必须是纯数字。

## 多人开锁组合

```http
GET /api/v1/devices/{deviceId}/access/unlock-combinations/{number}
PUT /api/v1/devices/{deviceId}/access/unlock-combinations/{number}
```

```json
{
  "combinationNumber": 1,
  "group1": 2,
  "group2": 23,
  "group3": 14,
  "group4": 56,
  "group5": 0
}
```

组合编号通常为 `1-10`，每个组合最多包含 5 个组。

## 错误响应

设备未连接：

```json
{
  "code": "device_operation_failed",
  "message": "Device 'office-gate-01' is not connected.",
  "vendorErrorCode": null
}
```

厂商 SDK 返回失败：

```json
{
  "success": false,
  "vendorErrorCode": -1,
  "message": "SSR_SetUserInfo failed."
}
```

不同型号、固件架构和算法版本并不支持所有接口。调用方应把厂商错误码、设备型号和固件版本一起记录，不能仅依据 HTTP 状态判断设备能力。

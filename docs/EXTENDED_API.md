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

所有后续设备写入和照片文件名统一要求 `enrollNumber` 匹配
`[A-Za-z0-9_-]+`，并继续受设备 `pinWidth` 和字母编号能力约束。升级不会自动
改写已有人员；不合规编号应由上层提示并等待人工重新映射。

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
GET /api/v1/devices/{deviceId}/users/{enrollNumber}/photo
GET /api/v1/devices/{deviceId}/users/{enrollNumber}/visible-light-face-photo
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

下载接口调用厂商 `DownloadUserPhoto`，返回设备中的原始 JPG 文件，不是
`GetUserFaceStr` 返回的加密/算法人脸模板：

```json
{
  "enrollNumber": "10001",
  "fileName": "10001.jpg",
  "base64Jpeg": "/9j/4AAQSkZJRgABAQ...",
  "byteLength": 18342
}
```

Relay 校验文件名、10 MB 大小上限和 JPG 文件头/文件尾，并使用每次请求独立的临时目录；
响应完成后不在 Relay 本地保留照片。该 SDK 能力仅适用于支持新架构用户照片的固件。

可见光下载接口根据工号生成唯一允许的文件名
`verify_biophoto_9_{工号}.jpg`，先使用 `GetUserFacePhotoNames` 核对设备照片清单，
再通过 `GetUserFacePhotoByName` 读取二进制 JPG。Relay 不接受调用方传入任意照片名，
也不会把照片写入磁盘；返回前校验 10 MB 上限以及 JPEG SOI/EOI 标记。设备没有该照片时
返回 `404 visible_light_face_photo_not_found`，SDK 未暴露下载方法时返回
`422 capability_not_supported`。

## 设备能力探测

```http
GET /api/v1/devices/{deviceId}/capabilities
```

```json
{
  "pinWidth": 9,
  "supportsAlphabeticPin": false,
  "accessControlFunction": 14,
  "supportsAdvancedAccess": true,
  "supportsNormallyOpen": true,
  "supportsDoorState": true,
  "supportsUserPhotoDownload": true,
  "supportsVisibleLightFacePhotoDownload": true,
  "supportsAttendanceRangeQuery": true,
  "probeErrors": []
}
```

探测使用 SDK 文档中的 `GetDeviceInfo(76)`（PIN2Width）、
`GetDeviceInfo(77)`（IsSupportABCPin）、`GetACFun`、`GetDoorState` 和
`IsNewFirmwareMachine`。照片探测方法不可用时
`supportsUserPhotoDownload=null`、`supportsVisibleLightFacePhotoDownload=null`，原因写入
`probeErrors`，不会固定报告支持。
单项探测失败会进入 `probeErrors`，调用方不得把未知能力当作已支持。

## 门状态与常开

```http
GET  /api/v1/devices/{deviceId}/access/door-state
POST /api/v1/devices/{deviceId}/access/normally-open/start
POST /api/v1/devices/{deviceId}/access/normally-open/end
```

门状态响应：

```json
{
  "open": false,
  "rawState": 0
}
```

开始常开不再把 `GetACFun=14` 作为硬门槛。Relay 以
`GetDeviceInfo(5)`/`SetDeviceInfo(5,255)` 的实际结果为准：先读取原锁驱动时长，
再将其设为 `255`，并把原值返回给上层保存。`GetACFun` 仍作为设备诊断信息返回，
但部分官方软件可正常控制的固件会错误或不完整地报告该值：

```json
{
  "normallyOpen": true,
  "lockDriveTime": 255,
  "previousLockDriveTime": 5,
  "doorOpen": true
}
```

结束常开必须显式传入上层在开始常开时保存的原值，避免 Relay 重启后猜测设备配置：

```json
{
  "restoreLockDriveTime": 5
}
```

允许恢复范围为 `0-254`。成功响应与开始常开相同，但 `normallyOpen=false`。

厂商 SDK 还把 `SetDeviceInfo(81)` 标为 `~DOTZ`，官方软件称其为
Normal Open/NO Time Zone。当前 SDK 手册没有给出不同机型的取值范围和时区绑定语义，
因此 Relay 暂不暴露未经真机验证的“按时间段常开”写接口；应在目标机型验证
`SetTZInfo` + `SetDeviceInfo(81)` 的组合及取消方式后再形成稳定契约。

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

设备未连接返回 `409 device_unavailable`：

```json
{
  "code": "device_unavailable",
  "message": "Device 'office-gate-01' is not connected.",
  "vendorErrorCode": null
}
```

能力不支持返回 `422 capability_not_supported`。厂商 SDK 操作失败返回
`502 device_operation_failed`：

```json
{
  "code": "device_operation_failed",
  "vendorErrorCode": -1,
  "message": "SSR_SetUserInfo failed."
}
```

不同型号、固件架构和算法版本并不支持所有接口。调用方应把厂商错误码、设备型号和固件版本一起记录，不能仅依据 HTTP 状态判断设备能力。

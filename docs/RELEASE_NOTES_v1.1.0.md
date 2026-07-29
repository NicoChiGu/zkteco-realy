# v1.1.0 发布说明：HUNS 门禁协议兼容

## 新增

- 正式提供 HUNS 已依赖的照片下载、设备能力、门状态、开始常开和结束常开接口。
- 新增需 `X-API-Key` 的 `/api/v1/version` 与 `/api/v1/capabilities`。
- 照片能力改由 `IsNewFirmwareMachine` 探测；无法探测时返回 `null` 和
  `probeErrors`。
- 人员编号统一为 `[A-Za-z0-9_-]+`。
- 错误协议明确为：离线 `409 device_unavailable`、能力不支持
  `422 capability_not_supported`、厂商失败 `502 device_operation_failed`。

## 兼容性

- 设备配置表和 DPAPI 密文不变。
- 已有不合规人员编号不会被改写，但后续设备写入和照片读取会被拒绝，需人工映射。
- HUNS 应先完成版本及能力协商，再读取设备列表。

## 验收

- `dotnet test .\tests\ZktecoRelay.Tests\ZktecoRelay.Tests.csproj -c Release`
- 使用支持与不支持照片的新旧固件各验证一次能力探测和下载。
- 验证重复开始常开、结束后恢复第一次返回的锁驱动时长。

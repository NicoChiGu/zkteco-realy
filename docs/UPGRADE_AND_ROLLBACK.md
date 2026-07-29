# v1.1.0 / v1.2.0 升级与回滚

## 升级前

1. 停止 Relay，备份 `.env` 和 `data/zkteco-relay.db`。
2. 确认 HUNS 已应用 `AccessControlConnection` 与
   `AccessControlRealtimeEvent` 的 Relay 协议迁移。
3. 记录当前 x64/x86 架构和已注册的 ZKTeco SDK 位数。

## 分阶段升级

1. 先部署 v1.1.0，调用 `/health`、`/api/v1/version`、
   `/api/v1/capabilities` 和五个 HUNS 必需接口。
2. 完成照片与常开真机验收后再部署 v1.2.0。
3. v1.2.0 首次启动会原地创建事件表；不会重写设备配置和 DPAPI 密文。
4. 观察 `/api/v1/diagnostics/health`、设备重连状态及 HUNS 事件检查点。

## 回滚

- 回滚 v1.2.0 到 v1.1.0 前先停止写入并备份数据库。v1.1.0 会忽略新增事件表；
  不要删除该表，以便再次升级时保留序号。
- 回滚到 v1.0.x 会失去五个 HUNS 必需接口和协议协商，HUNS 会返回 `76006`；
  只有同时停用对应 HUNS 门禁连接时才可执行。
- HUNS 数据库迁移中的新增列均可被旧代码忽略。若必须物理回退数据库，使用升级前
  备份；不要手工重建包含 DPAPI 密文的设备配置表。
- 回滚后重新运行 readiness、版本/能力协商和一台测试设备连接，确认无误再恢复流量。

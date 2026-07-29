# v1.2.0 发布说明：连接、事件与健康可靠性

## 新增

- 设备 STA 线程每 15 秒执行 `GetConnectStatus`，命令失败时立即补探测。
- 自动连接升级为持续协调器，采用 `1/3/10/30/60` 秒和 ±20% 抖动退避。
- `DeviceStatus` 增加最近通信、掉线、重连次数和下次重连时间。
- SQLite 增加版本化事件表；事件使用十进制字符串 `eventSequence`。
- WebSocket 支持 `afterSequence`、顺序补发、慢消费者追赶和
  `event_replay_gap`。
- 新增 liveness、readiness 和经鉴权的分项诊断。

## 数据保留

- 事件保留 30 天且最多 100,000 条，任一上限命中即删除最旧记录。
- 迁移使用同一数据库并保留 `device_configurations` 与 DPAPI 密文。

## 验收

- 运行中断网后 30 秒内显示离线，恢复后自动重连。
- 断开 HUNS WebSocket 后制造事件，重连后按 sequence 补发且业务记录无重复。
- Relay 与 HUNS 分别重启后检查配置、事件检查点和自动重连状态。
- 分别在 Windows x64/x86 CI 执行测试、发布和安装包验证。

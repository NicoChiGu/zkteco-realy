# ZKTeco Relay

## 开发文档

- 完整 API 端点索引：[`docs/API_REFERENCE.md`](docs/API_REFERENCE.md)
- 详细 API 请求、响应与接入示例：[`docs/API.md`](docs/API.md)
- 考勤查询、分页与清理：[`docs/ATTENDANCE_API.md`](docs/ATTENDANCE_API.md)
- OpenAPI 3.0 定义：[`docs/openapi.yaml`](docs/openapi.yaml)
- 实时事件 WebSocket：[`docs/REALTIME_EVENTS.md`](docs/REALTIME_EVENTS.md)
- 人员、生物特征与门禁扩展接口：[`docs/EXTENDED_API.md`](docs/EXTENDED_API.md)


基于 C#、ASP.NET Core 和 Windows COM 的 ZKTeco 脱机通讯 REST 中继。

项目提供两种启动方式：

- `ZktecoRelay.exe`：无界面 API 服务，适合服务器或计划任务。
- `ZktecoRelay.Manager.exe`：WinForms 图形管理器，可设置端口、内网监听和 API Key，并启动或停止 API。

## 安全设计

- 除 `/health`、`/health/live`、`/health/ready` 和文档外，所有 API 都必须提供 `X-API-Key`。
- API Key 从系统环境变量或 `.env` 文件读取，不写入源代码。
- 图形管理器默认只监听 `127.0.0.1`。
- 只有明确勾选“允许内网访问”后才监听 `0.0.0.0`。
- API Key 使用固定时间比较。
- `.env` 已加入 `.gitignore`。

## GitHub Actions 自动构建

工作流文件：

```text
.github/workflows/build-windows.yml
```

触发方式：

- 推送到 `main` 或 `master`。
- 创建 `v*` 标签。
- Pull Request。
- GitHub Actions 页面手动运行。

每次构建会生成两个 Artifact：

```text
zkteco-relay-win-x64
zkteco-relay-win-x86
```

每个包包含：

```text
api/ZktecoRelay.exe
manager/ZktecoRelay.Manager.exe
dll/ (包含 x64 与 x86 厂商 DLL 及其注册脚本)
.env.example
README.md
scripts/install-sdk-x64.ps1 与 scripts/install-sdk-x86.ps1
```

两个 EXE 都按 self-contained、single-file 方式发布，目标机器不需要预先安装 .NET Runtime。ZKTeco 厂商 DLL 已完整内嵌至安装包（`setup.exe`）与压缩包（`.zip`）中。使用安装包安装时，安装程序会自动将 DLL 复制到 Windows 系统目录（`System32` 或 `SysWOW64`）并自动完成 COM 注册；卸载时会询问是否注销并删除系统目录中的 SDK DLL 组件（默认选择“否”，避免影响同机器上其他软件）。

推送 `v*` 标签时，工作流还会自动创建 GitHub Release，并上传：

```text
zkteco-relay-win-x64-setup.exe
zkteco-relay-win-x64-setup.exe.sha256
zkteco-relay-win-x86-setup.exe
zkteco-relay-win-x86-setup.exe.sha256
zkteco-relay-win-x64.zip
zkteco-relay-win-x64.zip.sha256
zkteco-relay-win-x86.zip
zkteco-relay-win-x86.zip.sha256
```

示例发布命令：

```powershell
git tag v1.2.0
git push origin v1.2.0
```

## 图形管理器

运行：

```powershell
.\manager\ZktecoRelay.Manager.exe
```

界面支持：

- 设置 API 端口。
- 默认仅监听本机。
- 可选择允许内网访问。
- 生成随机 API Key。
- 显示或隐藏 API Key。
- 保存 `.env`。
- 启动和停止 API。
- 打开 `/health` 健康检查。
- 显示启动和停止日志，但不显示密钥。
- 启动时自动检查 ZKTeco COM 注册、DLL 位数、关键依赖和 COM 实例化。
- 提供“检查 SDK / DLL”与“修复/重新注册 DLL”按钮，支持一键查看 DLL 路径/版本，或直接以管理员权限重新注册 `zkemkeeper.dll` 完成组件修复。
- 最小化或关闭窗口时可隐藏到系统托盘；双击托盘图标恢复窗口。
- 托盘菜单支持启动/停止 API、打开健康检查和退出程序。
- 设备连接配置写入 SQLite，API 或 Windows 重启后可自动恢复连接。
- 提供 GitHub Release 更新检查和下载，自动选择与当前进程匹配的 x64/x86 包并校验 SHA-256。
- 支持配置 Release 下载镜像前缀，例如 `https://gh-proxy.org/`；GitHub API 查询始终直连。
- SDK 健康检查失败时阻止启动 API，避免服务启动后才在设备连接时失败。

配置优先保存在程序目录下的 `.env`。若安装在 `C:\Program Files` 等普通用户无写权限的系统目录，程序会自动检测并自动回退至用户目录（`%LOCALAPPDATA%\ZktecoRelay\.env`），避免未以管理员身份运行时出现写权限错误。SQLite 数据库亦同理自动回退至 `%LOCALAPPDATA%\ZktecoRelay\data\zkteco-relay.db`。

更新设置：

```dotenv
ZKTECO_UPDATE_REPOSITORY=NicoChiGu/zkteco-realy
ZKTECO_GITHUB_PROXY=https://gh-proxy.org/
```

版本检查始终直接访问 GitHub API：

```text
https://api.github.com/repos/NicoChiGu/zkteco-realy/releases/latest
```

`ZKTECO_GITHUB_PROXY` 只用于 Release 资产下载。例如 GitHub API 返回：

```text
https://github.com/NicoChiGu/zkteco-realy/releases/download/v1.0.4/zkteco-relay-win-x64-setup.exe
```

配置下载镜像后，实际下载地址为：

```text
https://gh-proxy.org/https://github.com/NicoChiGu/zkteco-realy/releases/download/v1.0.4/zkteco-relay-win-x64-setup.exe
```

同名 `.sha256` 校验文件也使用下载镜像。留空 `ZKTECO_GITHUB_PROXY` 时直接从 GitHub 下载。管理器会优先下载与当前架构匹配的 `setup.exe` 并实时显示可视化进度条，更新包会自动保存到系统 Temp 临时目录中（随系统自动清理），无需手动选择路径。在通过 SHA-256 校验后，可直接启动安装程序完成覆盖升级，同时保留已有 `.env` 与 SQLite 数据库。

更新下载开始后，GUI 会启用“取消下载”按钮。取消后会中止 HTTP 请求并删除 Temp 目录中的 `.download` 临时文件。

## 请求日志与 IP 白名单

GUI 日志区域会显示每个 HTTP/WebSocket 请求的方法、路径、实际来源 IP、响应状态码和耗时。日志不会记录 `X-API-Key`，也不会输出查询字符串中的 WebSocket `apiKey`。

允许访问的 IP 地址或 CIDR 网段可在 GUI 的“允许访问 IP/网段”中配置，多个值支持逗号、分号或换行分隔：

```text
127.0.0.1/32
::1/128
192.168.1.0/24
10.20.30.45
```

对应环境变量：

```dotenv
ZKTECO_ALLOWED_NETWORKS=127.0.0.1/32,::1/128,192.168.1.0/24
```

允许所有 IPv4 地址：

```dotenv
ZKTECO_ALLOWED_NETWORKS=0.0.0.0/0
```

白名单依据 TCP 连接的实际远端地址判断，不信任 `X-Forwarded-For`。未配置时默认只允许 IPv4/IPv6 本机回环地址。白名单与监听地址是两层限制：需要局域网访问时，既要启用“允许内网访问”，也要把对应 IP 或网段加入白名单。

## 设备配置持久化与自动重连

设备通过 `POST /api/v1/devices/{deviceId}/connect` 连接时，会自动写入 SQLite。默认数据库位置：

```text
data/zkteco-relay.db
```

也可通过环境变量指定：

```dotenv
ZKTECO_DATABASE_PATH=D:\ZktecoRelayData\zkteco-relay.db
```

设备通信密码使用 Windows DPAPI 的本机范围加密后保存，不以明文写入数据库。数据库文件复制到另一台 Windows 主机后不能直接解密原密码。

服务持续协调 `autoConnect=true` 的设备配置。运行中掉线后按
`1s → 3s → 10s → 30s → 60s`（±20% 抖动）重试，成功后清零退避。
每个设备 STA 线程每 15 秒调用 `GetConnectStatus`，命令失败后也立即补做探测。
某台设备离线不会使 Relay readiness 失败。

显式调用断开接口会将该设备的自动连接关闭：

```http
POST /api/v1/devices/{deviceId}/disconnect
```

再次调用连接接口会重新启用自动连接。

设备配置接口：

```text
GET    /api/v1/device-configurations
GET    /api/v1/device-configurations/{deviceId}
PUT    /api/v1/device-configurations/{deviceId}
DELETE /api/v1/device-configurations/{deviceId}
```

配置查询不会返回设备通信密码，只返回 `hasCommunicationPassword`。

## 命令行服务配置

复制模板：

```powershell
Copy-Item .env.example .env
```

编辑：

```dotenv
ZKTECO_API_KEY=replace-with-at-least-32-random-characters
ZKTECO_BIND_URL=http://127.0.0.1:5080
ZKTECO_ALLOWED_NETWORKS=127.0.0.1/32,::1/128
```

生产环境也可使用机器级环境变量：

```powershell
[Environment]::SetEnvironmentVariable('ZKTECO_API_KEY', 'your-long-random-key', 'Machine')
[Environment]::SetEnvironmentVariable('ZKTECO_BIND_URL', 'http://192.168.1.20:5080', 'Machine')
```

启动：

```powershell
.\api\ZktecoRelay.exe
```

## SDK 安装与 COM 注册

EXE 位数必须与已注册的 ZKTeco COM SDK 位数一致。GUI 管理器会从对应位数的 Windows 注册表视图读取 `zkemkeeper` COM 注册信息，并实际创建一次 COM 对象验证安装状态。

- **使用 Installer (`setup.exe`) 安装**：安装程序在安装过程中会自动将 DLL 复制到 Windows 系统目录（`System32` 或 `SysWOW64`），并从系统目录中注册 `zkemkeeper.dll`；卸载时会询问用户是否注销并删除系统目录中的 SDK DLL 组件（默认选择“否”）。
- **使用 Portable ZIP 压缩包**：压缩包内已内嵌 `dll/x64` 与 `dll/x86` 目录。解压后可按需运行注册脚本：

### x64 手动注册脚本

以管理员身份运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-sdk-x64.ps1
```

### x86 手动注册脚本

以管理员身份运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-sdk-x86.ps1
```

脚本会优先从内嵌的 `dll\x64` / `dll\x86` 目录自动读取并注册 `zkemkeeper.dll`。

## 本地开发

需要安装 .NET 8 SDK。

```powershell
dotnet restore .\ZktecoRelay.csproj
dotnet restore .\Manager\ZktecoRelay.Manager.csproj
dotnet restore .\tests\ZktecoRelay.Tests\ZktecoRelay.Tests.csproj
dotnet build .\ZktecoRelay.csproj -c Release
dotnet build .\Manager\ZktecoRelay.Manager.csproj -c Release
dotnet test .\tests\ZktecoRelay.Tests\ZktecoRelay.Tests.csproj -c Release
```

发布 x64：

```powershell
dotnet publish .\ZktecoRelay.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish\x64\api
dotnet publish .\Manager\ZktecoRelay.Manager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish\x64\manager
```

## API

健康检查：

```http
GET /health/live
GET /health
GET /health/ready
```

`/health` 与 `/health/ready` 会检查 COM 注册/位数/实例化、SQLite 读写、
事件库和 STA 工作线程；核心失败返回 `503`。经 API Key 鉴权的
`/api/v1/diagnostics/health` 返回相同分项诊断。

其他请求需要：

```http
X-API-Key: your-long-random-key
```

接口：

```text
GET  /api/v1/version
GET  /api/v1/capabilities
GET  /api/v1/devices
GET  /api/v1/devices/{deviceId}
POST /api/v1/devices/{deviceId}/connect
POST /api/v1/devices/{deviceId}/disconnect
GET  /api/v1/devices/{deviceId}/attendance
GET  /api/v1/devices/{deviceId}/attendance/query
POST /api/v1/devices/{deviceId}/attendance/clear
POST /api/v1/devices/{deviceId}/restart
GET  /api/v1/events/ws                 WebSocket 实时事件
GET/PUT/DELETE /api/v1/devices/{deviceId}/users/...
GET/PUT/DELETE /api/v1/devices/{deviceId}/users/{enrollNumber}/fingerprints/...
GET/PUT/DELETE /api/v1/devices/{deviceId}/users/{enrollNumber}/face
PUT  /api/v1/devices/{deviceId}/users/{enrollNumber}/photo
GET  /api/v1/devices/{deviceId}/users/{enrollNumber}/photo
GET  /api/v1/devices/{deviceId}/capabilities
GET  /api/v1/devices/{deviceId}/access/door-state
POST /api/v1/devices/{deviceId}/access/unlock
POST /api/v1/devices/{deviceId}/access/normally-open/start
POST /api/v1/devices/{deviceId}/access/normally-open/end
GET/PUT /api/v1/devices/{deviceId}/access/time-zones/...
GET/PUT /api/v1/devices/{deviceId}/access/groups/...
GET/PUT /api/v1/devices/{deviceId}/access/users/...
GET/PUT /api/v1/devices/{deviceId}/access/unlock-combinations/...
```

WebSocket 持久事件带十进制字符串 `eventSequence`。重连时使用
`afterSequence` 断点续传；默认保留 30 天且最多 100,000 条。

服务启动后还可访问：

```text
GET /docs/          Swagger UI
GET /openapi.yaml   完整 OpenAPI 3.0 定义
```

连接设备示例：

```http
POST /api/v1/devices/device-001/connect
Content-Type: application/json
X-API-Key: your-long-random-key

{
  "ipAddress": "192.168.1.100",
  "port": 4370,
  "communicationPassword": ""
}
```

## Node.js 调用示例

```ts
const response = await fetch(
  `${process.env.ZKTECO_RELAY_URL}/api/v1/devices/device-001/connect`,
  {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
      'x-api-key': process.env.ZKTECO_RELAY_KEY!,
    },
    body: JSON.stringify({
      ipAddress: '192.168.1.100',
      port: 4370,
      communicationPassword: '',
    }),
  },
);

if (!response.ok) {
  throw new Error(`ZKTeco relay returned ${response.status}: ${await response.text()}`);
}
```

## 内网部署建议

- 优先绑定中继服务器的固定内网 IP，而不是 `0.0.0.0`。
- 使用 Windows 防火墙只允许 Web 应用服务器访问 API 端口。
- 跨网段或不可信网络访问时，在前方增加 HTTPS 反向代理、mTLS 或 VPN。
- 不要把设备通信密码返回前端或写入日志。

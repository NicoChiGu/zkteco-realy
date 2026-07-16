# ZKTeco Relay

## 开发文档

- 详细 API 请求、响应与接入示例：[`docs/API.md`](docs/API.md)
- OpenAPI 3.0 定义：[`docs/openapi.yaml`](docs/openapi.yaml)


基于 C#、ASP.NET Core 和 Windows COM 的 ZKTeco 脱机通讯 REST 中继。

项目提供两种启动方式：

- `ZktecoRelay.exe`：无界面 API 服务，适合服务器或计划任务。
- `ZktecoRelay.Manager.exe`：WinForms 图形管理器，可设置端口、内网监听和 API Key，并启动或停止 API。

## 安全设计

- 除 `/health` 外，所有 API 都必须提供 `X-API-Key`。
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
.env.example
README.md
scripts/install-sdk-x64.ps1 或 scripts/install-sdk-x86.ps1
```

两个 EXE 都按 self-contained、single-file 方式发布，目标机器不需要预先安装 .NET Runtime。ZKTeco 厂商 DLL 不进入 GitHub 构建包，需要在目标 Windows 主机上从授权开发包中安装并注册。

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

配置保存在管理器 EXE 所在目录的 `.env`。请把程序放到当前用户拥有写权限的目录，不要直接放入需要管理员写权限的系统目录。

## 命令行服务配置

复制模板：

```powershell
Copy-Item .env.example .env
```

编辑：

```dotenv
ZKTECO_API_KEY=replace-with-at-least-32-random-characters
ZKTECO_BIND_URL=http://127.0.0.1:5080
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

## SDK 安装

EXE 位数必须与已注册的 ZKTeco COM SDK 位数一致。

### x64 包

以管理员身份运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-sdk-x64.ps1
```

### x86 包

以管理员身份运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-sdk-x86.ps1
```

仓库内脚本默认从以下相邻开发包目录复制 DLL：

```text
..\docs\脱机通讯开发包-6.3.1.55\SDK\x64
..\docs\脱机通讯开发包-6.3.1.55\SDK\x86
```

若使用 GitHub 下载的 Artifact，请先将对应架构的厂商 DLL 放到 `sdk\x64` 或 `sdk\x86`，或根据实际开发包路径调整安装脚本。

## 本地开发

需要安装 .NET 8 SDK。

```powershell
dotnet restore .\ZktecoRelay.csproj
dotnet restore .\Manager\ZktecoRelay.Manager.csproj
dotnet build .\ZktecoRelay.csproj -c Release
dotnet build .\Manager\ZktecoRelay.Manager.csproj -c Release
```

发布 x64：

```powershell
dotnet publish .\ZktecoRelay.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish\x64\api
dotnet publish .\Manager\ZktecoRelay.Manager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish\x64\manager
```

## API

健康检查无需密钥：

```http
GET /health
```

其他请求需要：

```http
X-API-Key: your-long-random-key
```

接口：

```text
GET  /api/v1/devices
GET  /api/v1/devices/{deviceId}
POST /api/v1/devices/{deviceId}/connect
POST /api/v1/devices/{deviceId}/disconnect
GET  /api/v1/devices/{deviceId}/attendance
POST /api/v1/devices/{deviceId}/restart
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

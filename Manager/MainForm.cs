using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using ZktecoRelay.Hosting;

namespace ZktecoRelay.Manager;

public sealed class MainForm : Form
{
    private readonly NumericUpDown _port = new() { Minimum = 1, Maximum = 65535, Value = 5080, Width = 140 };
    private readonly CheckBox _allowLan = new() { Text = "允许内网访问（绑定 0.0.0.0）", AutoSize = true };
    private readonly TextBox _apiKey = new() { Width = 430, UseSystemPasswordChar = true };
    private readonly CheckBox _showKey = new() { Text = "显示密钥", AutoSize = true };
    private readonly Button _generateKey = new() { Text = "生成密钥", AutoSize = true };
    private readonly Button _save = new() { Text = "保存配置", AutoSize = true };
    private readonly Button _start = new() { Text = "启动 API", AutoSize = true };
    private readonly Button _stop = new() { Text = "停止 API", AutoSize = true, Enabled = false };
    private readonly Button _openHealth = new() { Text = "打开健康检查", AutoSize = true, Enabled = false };
    private readonly Button _checkSdk = new() { Text = "检查 SDK / DLL", AutoSize = true };
    private readonly TextBox _updateRepository = new() { Width = 300, Text = "NicoChiGu/zkteco-realy" };
    private readonly TextBox _githubProxy = new() { Width = 430, PlaceholderText = "例如：https://v4.gh-proxy.org/" };
    private readonly Button _checkUpdate = new() { Text = "检查更新", AutoSize = true };
    private readonly TextBox _databasePath = new() { Width = 430, PlaceholderText = "留空使用 data\\zkteco-relay.db" };
    private readonly CheckBox _minimizeToTray = new() { Text = "最小化或关闭时隐藏到系统托盘", AutoSize = true, Checked = true };
    private readonly NotifyIcon _trayIcon = new() { Text = "ZKTeco Relay", Visible = true, Icon = SystemIcons.Application };
    private readonly ContextMenuStrip _trayMenu = new();
    private readonly Label _versionStatus = new() { Text = $"当前版本：{GitHubUpdateService.CurrentVersion}", AutoSize = true };
    private readonly Label _sdkStatus = new() { Text = "SDK 状态：尚未检查", AutoSize = true };
    private readonly Label _status = new() { Text = "状态：已停止", AutoSize = true };
    private readonly TextBox _log = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill
    };

    private WebApplication? _application;
    private bool _exitRequested;

    public MainForm()
    {
        Text = "ZKTeco Relay 管理器";
        Width = 760;
        Height = 650;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(680, 440);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 4
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var settings = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = 3,
            Padding = new Padding(0, 0, 0, 10)
        };
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        settings.Controls.Add(new Label { Text = "API 端口", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        settings.Controls.Add(_port, 1, 0);
        settings.Controls.Add(_allowLan, 1, 1);
        settings.Controls.Add(new Label { Text = "API Key", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        settings.Controls.Add(_apiKey, 1, 2);
        settings.Controls.Add(_generateKey, 2, 2);
        settings.Controls.Add(_showKey, 1, 3);
        settings.Controls.Add(new Label { Text = "更新仓库", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
        settings.Controls.Add(_updateRepository, 1, 4);
        settings.Controls.Add(new Label { Text = "GitHub 镜像", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
        settings.Controls.Add(_githubProxy, 1, 5);
        settings.Controls.Add(new Label { Text = "SQLite 路径", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 6);
        settings.Controls.Add(_databasePath, 1, 6);
        settings.Controls.Add(_minimizeToTray, 1, 7);

        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top };
        actions.Controls.AddRange([_save, _checkSdk, _checkUpdate, _start, _stop, _openHealth]);

        var statuses = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        statuses.Controls.Add(_versionStatus);
        statuses.Controls.Add(_sdkStatus);
        statuses.Controls.Add(_status);

        root.Controls.Add(settings, 0, 0);
        root.Controls.Add(actions, 0, 1);
        root.Controls.Add(statuses, 0, 2);
        root.Controls.Add(_log, 0, 3);
        Controls.Add(root);

        _generateKey.Click += (_, _) => _apiKey.Text = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _showKey.CheckedChanged += (_, _) => _apiKey.UseSystemPasswordChar = !_showKey.Checked;
        _save.Click += (_, _) => SaveConfiguration(showMessage: true);
        _start.Click += async (_, _) => await StartApiAsync();
        _stop.Click += async (_, _) => await StopApiAsync();
        _openHealth.Click += (_, _) => OpenHealthPage();
        _checkSdk.Click += (_, _) => CheckSdkHealth(showDialog: true);
        _checkUpdate.Click += async (_, _) => await CheckForUpdatesAsync();
        Resize += OnResize;
        FormClosing += OnFormClosing;

        ConfigureTrayMenu();

        LoadConfiguration();
        CheckSdkHealth(showDialog: false);
    }

    private string BindUrl => $"http://{(_allowLan.Checked ? "0.0.0.0" : "127.0.0.1")}:{(int)_port.Value}";
    private string BrowserUrl => $"http://127.0.0.1:{(int)_port.Value}/health";
    private string EnvPath => Path.Combine(AppContext.BaseDirectory, ".env");

    private void LoadConfiguration()
    {
        var values = EnvSettings.Read(EnvPath);

        if (values.TryGetValue("ZKTECO_API_KEY", out var key))
        {
            _apiKey.Text = key;
        }

        if (values.TryGetValue("ZKTECO_UPDATE_REPOSITORY", out var repository) && !string.IsNullOrWhiteSpace(repository))
        {
            _updateRepository.Text = repository;
        }

        if (values.TryGetValue("ZKTECO_GITHUB_PROXY", out var proxy))
        {
            _githubProxy.Text = proxy;
        }

        if (values.TryGetValue("ZKTECO_DATABASE_PATH", out var databasePath))
        {
            _databasePath.Text = databasePath;
        }

        if (values.TryGetValue("ZKTECO_MINIMIZE_TO_TRAY", out var minimizeToTray) &&
            bool.TryParse(minimizeToTray, out var minimizeEnabled))
        {
            _minimizeToTray.Checked = minimizeEnabled;
        }

        if (values.TryGetValue("ZKTECO_BIND_URL", out var bindUrl) && Uri.TryCreate(bindUrl, UriKind.Absolute, out var uri))
        {
            if (uri.Port is >= 1 and <= 65535)
            {
                _port.Value = uri.Port;
            }

            _allowLan.Checked = uri.Host is "0.0.0.0" or "*" or "+";
        }

        AppendLog(File.Exists(EnvPath) ? "已读取 .env 配置。" : "尚未创建 .env，请生成或填写 API Key。" );
    }

    private bool SaveConfiguration(bool showMessage)
    {
        var key = _apiKey.Text.Trim();
        if (key.Length < 16)
        {
            MessageBox.Show(this, "API Key 至少需要 16 个字符。", "配置无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        EnvSettings.Write(EnvPath, new Dictionary<string, string>
        {
            ["ZKTECO_API_KEY"] = key,
            ["ZKTECO_BIND_URL"] = BindUrl,
            ["ZKTECO_UPDATE_REPOSITORY"] = _updateRepository.Text.Trim(),
            ["ZKTECO_GITHUB_PROXY"] = _githubProxy.Text.Trim(),
            ["ZKTECO_DATABASE_PATH"] = _databasePath.Text.Trim(),
            ["ZKTECO_MINIMIZE_TO_TRAY"] = _minimizeToTray.Checked.ToString().ToLowerInvariant()
        });

        Environment.SetEnvironmentVariable("ZKTECO_DATABASE_PATH", _databasePath.Text.Trim());
        Environment.SetEnvironmentVariable("ZKTECO_MINIMIZE_TO_TRAY", _minimizeToTray.Checked.ToString().ToLowerInvariant());

        AppendLog($"配置已保存，监听地址：{BindUrl}");
        if (showMessage)
        {
            MessageBox.Show(this, "配置已保存到程序目录的 .env。", "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        return true;
    }

    private async Task StartApiAsync()
    {
        if (_application is not null || !SaveConfiguration(showMessage: false))
        {
            return;
        }

        var sdkHealth = CheckSdkHealth(showDialog: false);
        if (!sdkHealth.IsHealthy)
        {
            MessageBox.Show(
                this,
                "ZKTeco SDK/DLL 健康检查未通过，API 未启动。请查看日志并运行对应位数的 SDK 安装脚本。",
                "SDK 未就绪",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        try
        {
            SetBusy(true);
            _application = RelayApplication.Build(
                Array.Empty<string>(),
                new RelayOverrides(BindUrl, _apiKey.Text.Trim()));

            await _application.StartAsync();
            _status.Text = $"状态：运行中（{BindUrl}）";
            _start.Enabled = false;
            _stop.Enabled = true;
            _openHealth.Enabled = true;
            _port.Enabled = false;
            _allowLan.Enabled = false;
            _apiKey.Enabled = false;
            _generateKey.Enabled = false;
            _updateRepository.Enabled = false;
            _githubProxy.Enabled = false;
            _databasePath.Enabled = false;
            AppendLog("API 已启动。除 /health 外，所有请求必须携带 X-API-Key。" );
        }
        catch (Exception ex)
        {
            if (_application is not null)
            {
                await _application.DisposeAsync();
                _application = null;
            }

            MessageBox.Show(this, ex.Message, "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            AppendLog($"启动失败：{ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task StopApiAsync()
    {
        if (_application is null)
        {
            return;
        }

        try
        {
            SetBusy(true);
            using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _application.StopAsync(stopCts.Token);
            await _application.DisposeAsync();
            AppendLog("API 已停止。" );
        }
        catch (Exception ex)
        {
            AppendLog($"停止 API 时发生错误：{ex.Message}");
        }
        finally
        {
            _application = null;
            _status.Text = "状态：已停止";
            _start.Enabled = true;
            _stop.Enabled = false;
            _openHealth.Enabled = false;
            _port.Enabled = true;
            _allowLan.Enabled = true;
            _apiKey.Enabled = true;
            _generateKey.Enabled = true;
            _updateRepository.Enabled = true;
            _githubProxy.Enabled = true;
            _databasePath.Enabled = true;
            SetBusy(false);
        }
    }

    private void OpenHealthPage()
    {
        Process.Start(new ProcessStartInfo(BrowserUrl) { UseShellExecute = true });
    }

    private SdkHealthResult CheckSdkHealth(bool showDialog)
    {
        var result = SdkHealthChecker.Check();
        _sdkStatus.Text = result.IsHealthy
            ? $"SDK 状态：正常（{result.Architecture}）"
            : $"SDK 状态：异常（{result.Architecture}）";
        _sdkStatus.ForeColor = result.IsHealthy ? Color.DarkGreen : Color.DarkRed;

        AppendLog("开始检查 ZKTeco SDK/DLL。" );
        foreach (var detail in result.Details)
        {
            AppendLog($"SDK：{detail}");
        }

        foreach (var warning in result.Warnings)
        {
            AppendLog($"SDK 警告：{warning}");
        }

        foreach (var error in result.Errors)
        {
            AppendLog($"SDK 错误：{error}");
        }

        AppendLog(result.IsHealthy ? "SDK/DLL 健康检查通过。" : "SDK/DLL 健康检查失败。" );

        if (showDialog)
        {
            var message = result.IsHealthy
                ? $"SDK/DLL 健康检查通过。\n架构：{result.Architecture}\n版本：{result.FileVersion ?? "未知"}\n路径：{result.ComServerPath ?? "未知"}"
                : $"SDK/DLL 健康检查失败：\n\n{string.Join(Environment.NewLine, result.Errors)}\n\n请运行与 {result.Architecture} 程序匹配的 SDK 安装脚本。";

            MessageBox.Show(
                this,
                message,
                result.IsHealthy ? "SDK 正常" : "SDK 异常",
                MessageBoxButtons.OK,
                result.IsHealthy ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }

        return result;
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            SetBusy(true);
            AppendLog("正在检查 GitHub Release 更新。" );
            var settings = GitHubUpdateService.NormalizeSettings(_updateRepository.Text, _githubProxy.Text);
            var update = await GitHubUpdateService.CheckAsync(settings, CancellationToken.None);
            _versionStatus.Text = $"当前版本：{update.CurrentVersion}；最新版本：{update.LatestVersion}";

            if (!update.IsUpdateAvailable)
            {
                AppendLog($"当前已是最新版本：{update.LatestVersion}。" );
                MessageBox.Show(this, $"当前已是最新版本 {update.LatestVersion}。", "无需更新", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            AppendLog($"发现新版本 {update.TagName}，发布时间：{update.PublishedAt:yyyy-MM-dd HH:mm:ss zzz}。" );
            var message = $"发现新版本 {update.TagName}\n当前版本：{update.CurrentVersion}\n发布名称：{update.ReleaseName}\n\n是否下载 {update.Package.Name}？";
            var choice = MessageBox.Show(this, message, "发现更新", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
            if (choice == DialogResult.Cancel)
            {
                GitHubUpdateService.OpenReleasePage(update);
                return;
            }

            if (choice != DialogResult.Yes)
            {
                return;
            }

            using var folderDialog = new FolderBrowserDialog
            {
                Description = "选择更新包保存目录",
                SelectedPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                ShowNewFolderButton = true
            };
            if (folderDialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            var progress = new Progress<int>(percent => _versionStatus.Text = $"正在下载 {update.TagName}：{percent}%");
            var savedPath = await GitHubUpdateService.DownloadAsync(settings, update, folderDialog.SelectedPath, progress, CancellationToken.None);
            _versionStatus.Text = $"更新包已下载：{update.TagName}";
            AppendLog($"更新包已下载并通过校验：{savedPath}" );

            var openFolder = MessageBox.Show(
                this,
                $"更新包已下载并通过 SHA-256 校验：\n{savedPath}\n\n请停止 API、退出管理器后解压覆盖。是否打开所在目录？",
                "下载完成",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);
            if (openFolder == DialogResult.Yes)
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{savedPath}\"") { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            AppendLog($"检查或下载更新失败：{ex.Message}" );
            MessageBox.Show(this, ex.Message, "更新失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        UseWaitCursor = busy;
        _save.Enabled = !busy && _application is null;
        _checkSdk.Enabled = !busy && _application is null;
        _checkUpdate.Enabled = !busy;
    }

    private void ConfigureTrayMenu()
    {
        var showItem = new ToolStripMenuItem("显示管理器", null, (_, _) => RestoreFromTray());
        var startItem = new ToolStripMenuItem("启动 API", null, async (_, _) => await StartApiAsync());
        var stopItem = new ToolStripMenuItem("停止 API", null, async (_, _) => await StopApiAsync());
        var healthItem = new ToolStripMenuItem("打开健康检查", null, (_, _) => OpenHealthPage());
        var exitItem = new ToolStripMenuItem("退出程序", null, async (_, _) => await ExitApplicationAsync());

        _trayMenu.Items.AddRange([
            showItem,
            new ToolStripSeparator(),
            startItem,
            stopItem,
            healthItem,
            new ToolStripSeparator(),
            exitItem
        ]);
        _trayMenu.Opening += (_, _) =>
        {
            startItem.Enabled = _application is null;
            stopItem.Enabled = _application is not null;
            healthItem.Enabled = _application is not null;
        };

        _trayIcon.ContextMenuStrip = _trayMenu;
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
    }

    private void OnResize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized && _minimizeToTray.Checked)
        {
            HideToTray();
        }
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
        _trayIcon.Visible = true;
        _trayIcon.ShowBalloonTip(1500, "ZKTeco Relay", "管理器仍在系统托盘中运行。", ToolTipIcon.Info);
    }

    private void RestoreFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private async void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_exitRequested && _minimizeToTray.Checked)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        if (_application is not null)
        {
            e.Cancel = true;
            Enabled = false;
            await StopApiAsync();
            FormClosing -= OnFormClosing;
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayMenu.Dispose();
            Close();
        }
    }

    private async Task ExitApplicationAsync()
    {
        _exitRequested = true;
        if (_application is not null)
        {
            await StopApiAsync();
        }

        _trayIcon.Visible = false;
        Close();
    }

    private void AppendLog(string message)
    {
        _log.AppendText($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
    }
}

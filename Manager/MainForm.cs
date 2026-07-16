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
    private readonly Label _status = new() { Text = "状态：已停止", AutoSize = true };
    private readonly TextBox _log = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill
    };

    private WebApplication? _application;

    public MainForm()
    {
        Text = "ZKTeco Relay 管理器";
        Width = 760;
        Height = 520;
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

        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top };
        actions.Controls.AddRange([_save, _start, _stop, _openHealth]);

        root.Controls.Add(settings, 0, 0);
        root.Controls.Add(actions, 0, 1);
        root.Controls.Add(_status, 0, 2);
        root.Controls.Add(_log, 0, 3);
        Controls.Add(root);

        _generateKey.Click += (_, _) => _apiKey.Text = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _showKey.CheckedChanged += (_, _) => _apiKey.UseSystemPasswordChar = !_showKey.Checked;
        _save.Click += (_, _) => SaveConfiguration(showMessage: true);
        _start.Click += async (_, _) => await StartApiAsync();
        _stop.Click += async (_, _) => await StopApiAsync();
        _openHealth.Click += (_, _) => OpenHealthPage();
        FormClosing += OnFormClosing;

        LoadConfiguration();
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
            ["ZKTECO_BIND_URL"] = BindUrl
        });

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
            SetBusy(false);
        }
    }

    private void OpenHealthPage()
    {
        Process.Start(new ProcessStartInfo(BrowserUrl) { UseShellExecute = true });
    }

    private void SetBusy(bool busy)
    {
        UseWaitCursor = busy;
        _save.Enabled = !busy && _application is null;
    }

    private async void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_application is null)
        {
            return;
        }

        e.Cancel = true;
        Enabled = false;
        await StopApiAsync();
        FormClosing -= OnFormClosing;
        Close();
    }

    private void AppendLog(string message)
    {
        _log.AppendText($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
    }
}

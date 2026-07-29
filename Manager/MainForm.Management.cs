namespace ZktecoRelay.Manager;

public sealed partial class MainForm
{
    private static readonly Color CanvasColor =
        Color.FromArgb(246, 247, 249);
    private static readonly Color PanelColor =
        Color.FromArgb(252, 252, 253);
    private static readonly Color BorderColor =
        Color.FromArgb(218, 222, 227);
    private static readonly Color PrimaryTextColor =
        Color.FromArgb(31, 36, 43);
    private static readonly Color SecondaryTextColor =
        Color.FromArgb(91, 99, 110);
    private static readonly Color OnlineColor =
        Color.FromArgb(27, 122, 78);
    private static readonly Color WarningColor =
        Color.FromArgb(166, 99, 0);
    private static readonly Color OfflineColor =
        Color.FromArgb(174, 55, 55);
    private static readonly Font MonospaceFont =
        new("Consolas", 9F);
    private static readonly Font LogFont =
        new("Consolas", 9.5F);
    private static readonly Font StatusFont =
        new("Segoe UI Semibold", 9F, FontStyle.Bold);

    private readonly TabControl _mainTabs = new()
    {
        Dock = DockStyle.Fill,
        Padding = new Point(18, 6)
    };
    private readonly TabPage _devicesTab = new()
    {
        Text = "设备管理",
        BackColor = CanvasColor,
        Padding = new Padding(12)
    };
    private readonly DataGridView _deviceGrid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        AutoGenerateColumns = false,
        BackgroundColor = PanelColor,
        BorderStyle = BorderStyle.FixedSingle,
        CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
        ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
        ColumnHeadersHeight = 36,
        EnableHeadersVisualStyles = false,
        MultiSelect = false,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect
    };
    private readonly Button _refreshDevices = new()
    {
        Text = "刷新",
        AutoSize = true
    };
    private readonly Button _connectDevice = new()
    {
        Text = "立即连接",
        AutoSize = true,
        Enabled = false
    };
    private readonly Button _disconnectDevice = new()
    {
        Text = "断开连接",
        AutoSize = true,
        Enabled = false
    };
    private readonly Button _toggleAutoConnect = new()
    {
        Text = "切换自动连接",
        AutoSize = true,
        Enabled = false
    };
    private readonly Button _deleteDevice = new()
    {
        Text = "删除配置",
        AutoSize = true,
        Enabled = false,
        ForeColor = OfflineColor
    };
    private readonly Label _deviceSummary = new()
    {
        AutoSize = true,
        ForeColor = PrimaryTextColor,
        Font = StatusFont
    };
    private readonly Label _databaseStatus = new()
    {
        AutoEllipsis = true,
        Dock = DockStyle.Fill,
        ForeColor = SecondaryTextColor,
        TextAlign = ContentAlignment.MiddleLeft
    };
    private readonly Label _deviceDetails = new()
    {
        AutoEllipsis = true,
        BorderStyle = BorderStyle.FixedSingle,
        Dock = DockStyle.Fill,
        ForeColor = SecondaryTextColor,
        Padding = new Padding(10, 8, 10, 8),
        Text = "选择一台设备查看连接轨迹。",
        TextAlign = ContentAlignment.MiddleLeft
    };
    private readonly Label _logStatus = new()
    {
        AutoSize = true,
        ForeColor = SecondaryTextColor,
        Text = "0 条 · 当前会话"
    };
    private readonly Button _copyLog = new()
    {
        Text = "复制全部",
        AutoSize = true
    };
    private readonly Button _clearLog = new()
    {
        Text = "清空",
        AutoSize = true
    };
    private readonly System.Windows.Forms.Timer _deviceRefreshTimer = new()
    {
        Interval = 5000
    };

    private DeviceManagementController? _deviceManagement;
    private bool _deviceActionBusy;
    private int _logEntryCount;

    private Control BuildMainTabs()
    {
        _mainTabs.TabPages.Add(BuildConfigurationTab());
        _mainTabs.TabPages.Add(BuildDevicesTab());
        _mainTabs.TabPages.Add(BuildLogsTab());
        return _mainTabs;
    }

    private TabPage BuildConfigurationTab()
    {
        var tab = new TabPage
        {
            Text = "运行与配置",
            BackColor = CanvasColor,
            Padding = new Padding(16)
        };
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var settings = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = 3,
            Padding = new Padding(0, 0, 0, 12)
        };
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        AddSettingRow(settings, "API 端口", _port, 0);
        settings.Controls.Add(_allowLan, 1, 1);
        AddSettingRow(settings, "API Key", _apiKey, 2);
        settings.Controls.Add(_generateKey, 2, 2);
        settings.Controls.Add(_showKey, 1, 3);
        AddSettingRow(settings, "更新仓库", _updateRepository, 4);
        AddSettingRow(settings, "下载镜像", _githubProxy, 5);
        AddSettingRow(settings, "SQLite 路径", _databasePath, 6);
        AddSettingRow(settings, "允许访问 IP/网段", _allowedNetworks, 7);
        settings.Controls.Add(_minimizeToTray, 1, 8);

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 0, 0, 12)
        };
        actions.Controls.AddRange(
        [
            _save,
            _checkSdk,
            _repairSdk,
            _checkUpdate,
            _cancelUpdate,
            _start,
            _stop,
            _openHealth
        ]);

        var statuses = new FlowLayoutPanel
        {
            AutoSize = true,
            BackColor = PanelColor,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(12),
            WrapContents = false
        };
        statuses.Controls.Add(_versionStatus);
        statuses.Controls.Add(_updateProgressBar);
        statuses.Controls.Add(_sdkStatus);
        statuses.Controls.Add(_status);

        root.Controls.Add(settings, 0, 0);
        root.Controls.Add(actions, 0, 1);
        root.Controls.Add(statuses, 0, 2);
        tab.Controls.Add(root);
        return tab;
    }

    private TabPage BuildDevicesTab()
    {
        ConfigureDeviceGrid();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));

        var commandBar = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 0, 8)
        };

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Left,
            Margin = Padding.Empty,
            WrapContents = false
        };
        actions.Controls.AddRange(
        [
            _refreshDevices,
            _connectDevice,
            _disconnectDevice,
            _toggleAutoConnect,
            _deleteDevice
        ]);
        _deviceSummary.AutoSize = false;
        _deviceSummary.Dock = DockStyle.Right;
        _deviceSummary.TextAlign = ContentAlignment.MiddleRight;
        _deviceSummary.Width = 260;
        commandBar.Controls.Add(_deviceSummary);
        commandBar.Controls.Add(actions);

        var databaseBar = new Panel
        {
            BackColor = PanelColor,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Fill,
            Height = 36,
            Padding = new Padding(10, 0, 10, 0)
        };
        databaseBar.Controls.Add(_databaseStatus);

        root.Controls.Add(commandBar, 0, 0);
        root.Controls.Add(databaseBar, 0, 1);
        root.Controls.Add(_deviceGrid, 0, 2);
        root.Controls.Add(_deviceDetails, 0, 3);
        _devicesTab.Controls.Add(root);
        return _devicesTab;
    }

    private TabPage BuildLogsTab()
    {
        var tab = new TabPage
        {
            Text = "日志",
            BackColor = CanvasColor,
            Padding = new Padding(12)
        };
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var commandBar = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = 2,
            Padding = new Padding(0, 0, 0, 8)
        };
        commandBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        commandBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            WrapContents = false
        };
        actions.Controls.Add(_copyLog);
        actions.Controls.Add(_clearLog);
        commandBar.Controls.Add(_logStatus, 0, 0);
        commandBar.Controls.Add(actions, 1, 0);
        _logStatus.Anchor = AnchorStyles.Left;

        _log.BackColor = Color.FromArgb(27, 31, 36);
        _log.BorderStyle = BorderStyle.FixedSingle;
        _log.Font = LogFont;
        _log.ForeColor = Color.FromArgb(226, 230, 235);
        _log.WordWrap = false;

        root.Controls.Add(commandBar, 0, 0);
        root.Controls.Add(_log, 0, 1);
        tab.Controls.Add(root);
        return tab;
    }

    private static void AddSettingRow(
        TableLayoutPanel settings,
        string label,
        Control control,
        int row)
    {
        settings.Controls.Add(
            new Label
            {
                Anchor = AnchorStyles.Left,
                AutoSize = true,
                ForeColor = SecondaryTextColor,
                Margin = new Padding(0, 7, 14, 7),
                Text = label
            },
            0,
            row);
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        control.Margin = new Padding(0, 4, 8, 4);
        settings.Controls.Add(control, 1, row);
    }

    private void ConfigureDeviceGrid()
    {
        _deviceGrid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            BackColor = Color.FromArgb(237, 239, 242),
            ForeColor = PrimaryTextColor,
            Font = StatusFont,
            Padding = new Padding(4, 0, 4, 0),
            SelectionBackColor = Color.FromArgb(237, 239, 242),
            SelectionForeColor = PrimaryTextColor
        };
        _deviceGrid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = PanelColor,
            ForeColor = PrimaryTextColor,
            Padding = new Padding(4, 0, 4, 0),
            SelectionBackColor = Color.FromArgb(220, 230, 240),
            SelectionForeColor = PrimaryTextColor
        };
        _deviceGrid.GridColor = BorderColor;
        _deviceGrid.RowTemplate.Height = 32;

        AddDeviceColumn("ConnectionState", "状态", 116);
        AddDeviceColumn("DeviceId", "设备编号", 130, monospace: true);
        AddDeviceColumn("Endpoint", "IP / 端口", 165, monospace: true);
        AddDeviceColumn("AutoConnectText", "自动连接", 90);
        AddDeviceColumn(
            "LastCommunicationText",
            "最近通信",
            152,
            monospace: true);
        AddDeviceColumn(
            "NextReconnectText",
            "下次重连",
            152,
            monospace: true);
        AddDeviceColumn(
            "LastError",
            "最后错误",
            180,
            fill: true);
        AddDeviceColumn(
            "UpdatedAtText",
            "配置更新",
            152,
            monospace: true);

        _deviceGrid.CellFormatting += (_, args) =>
        {
            if (_deviceGrid.Columns[args.ColumnIndex].DataPropertyName
                != nameof(ManagedDeviceRow.ConnectionState) ||
                _deviceGrid.Rows[args.RowIndex].DataBoundItem
                is not ManagedDeviceRow device)
            {
                return;
            }

            args.CellStyle!.ForeColor = device.Connected == true
                ? OnlineColor
                : device.RelayRunning &&
                    (device.AutoConnect || device.ReconnectAttempt is > 0)
                    ? WarningColor
                    : OfflineColor;
            args.CellStyle.Font = StatusFont;
        };
        _deviceGrid.Paint += (_, args) =>
        {
            if (_deviceGrid.Rows.Count != 0)
            {
                return;
            }

            using var brush = new SolidBrush(SecondaryTextColor);
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            var contentArea = new RectangleF(
                0,
                _deviceGrid.ColumnHeadersHeight,
                _deviceGrid.ClientSize.Width,
                Math.Max(
                    0,
                    _deviceGrid.ClientSize.Height -
                    _deviceGrid.ColumnHeadersHeight));
            args.Graphics.DrawString(
                "SQLite 中尚无设备连接配置。\n通过 HUNS 或 Relay API 连接设备后将在此显示。",
                Font,
                brush,
                contentArea,
                format);
        };
    }

    private void AddDeviceColumn(
        string property,
        string title,
        int width,
        bool monospace = false,
        bool fill = false)
    {
        var column = new DataGridViewTextBoxColumn
        {
            DataPropertyName = property,
            HeaderText = title,
            MinimumWidth = Math.Min(width, 100),
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.Automatic,
            Width = width
        };
        if (fill)
        {
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column.FillWeight = 100;
        }

        if (monospace)
        {
            column.DefaultCellStyle.Font = MonospaceFont;
        }

        _deviceGrid.Columns.Add(column);
    }

    private void InitializeDeviceManagement()
    {
        _deviceManagement = new DeviceManagementController(
            () => _application);
        _refreshDevices.Click += (_, _) =>
            RefreshDeviceGrid(showErrors: true);
        _connectDevice.Click += async (_, _) =>
            await ConnectSelectedDeviceAsync();
        _disconnectDevice.Click += async (_, _) =>
            await DisconnectSelectedDeviceAsync();
        _toggleAutoConnect.Click += (_, _) =>
            ToggleSelectedDeviceAutoConnect();
        _deleteDevice.Click += async (_, _) =>
            await DeleteSelectedDeviceAsync();
        _deviceGrid.SelectionChanged += (_, _) =>
            UpdateSelectedDevice();
        _mainTabs.SelectedIndexChanged += (_, _) =>
        {
            if (_mainTabs.SelectedTab == _devicesTab)
            {
                RefreshDeviceGrid(showErrors: false);
            }
        };
        _copyLog.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(_log.Text))
            {
                Clipboard.SetText(_log.Text);
            }
        };
        _clearLog.Click += (_, _) =>
        {
            _log.Clear();
            _logEntryCount = 0;
            _logStatus.Text = "0 条 · 当前会话";
        };
        _deviceRefreshTimer.Tick += (_, _) =>
            RefreshDeviceGrid(showErrors: false);
        _deviceRefreshTimer.Start();
        RefreshDeviceGrid(showErrors: true);
    }

    private ManagedDeviceRow? SelectedDevice =>
        _deviceGrid.CurrentRow?.DataBoundItem as ManagedDeviceRow;

    private void RefreshDeviceGrid(bool showErrors)
    {
        if (_deviceManagement is null ||
            IsDisposed ||
            Disposing)
        {
            return;
        }

        var selectedId = SelectedDevice?.DeviceId;
        try
        {
            var snapshot = _deviceManagement.LoadSnapshot();
            _deviceGrid.DataSource = snapshot.Devices.ToArray();
            _databaseStatus.Text =
                $"SQLite · {snapshot.DatabasePath}";
            _databaseStatus.Font = MonospaceFont;
            _deviceSummary.Text =
                $"总计 {snapshot.Devices.Count} · 在线 {snapshot.OnlineCount} · " +
                $"重连 {snapshot.ReconnectingCount}";

            if (selectedId is not null)
            {
                foreach (DataGridViewRow row in _deviceGrid.Rows)
                {
                    if (row.DataBoundItem is ManagedDeviceRow device &&
                        string.Equals(
                            device.DeviceId,
                            selectedId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        row.Selected = true;
                        _deviceGrid.CurrentCell = row.Cells[0];
                        break;
                    }
                }
            }

            UpdateSelectedDevice();
        }
        catch (Exception ex)
        {
            _deviceSummary.Text = "设备数据读取失败";
            _databaseStatus.Text = ex.Message;
            if (showErrors)
            {
                AppendLog($"读取设备管理数据失败：{ex.Message}");
            }
        }
    }

    private void UpdateSelectedDevice()
    {
        var device = SelectedDevice;
        if (device is null)
        {
            _deviceDetails.Text = "选择一台设备查看连接轨迹。";
            UpdateDeviceActionState();
            return;
        }

        var connectedAt = device.ConnectedAt?
            .ToLocalTime()
            .ToString("yyyy-MM-dd HH:mm:ss") ?? "—";
        var disconnectedAt = device.DisconnectedAt?
            .ToLocalTime()
            .ToString("yyyy-MM-dd HH:mm:ss") ?? "—";
        _deviceDetails.Text =
            $"{device.DeviceId}  ·  {device.Endpoint}  ·  {device.ConnectionState}" +
            $"    连接于 {connectedAt}  ·  断开于 {disconnectedAt}" +
            $"    {device.LastError ?? "无错误"}";
        UpdateDeviceActionState();
    }

    private void UpdateDeviceActionState()
    {
        var device = SelectedDevice;
        var relayRunning = _application is not null;
        var available = !_deviceActionBusy && device is not null;
        _refreshDevices.Enabled = !_deviceActionBusy;
        _connectDevice.Enabled =
            available && relayRunning && device!.Connected != true;
        _disconnectDevice.Enabled =
            available && relayRunning && device!.Connected is not null;
        _toggleAutoConnect.Enabled = available;
        _toggleAutoConnect.Text = device?.AutoConnect == true
            ? "停用自动连接"
            : "启用自动连接";
        _deleteDevice.Enabled = available;
    }

    private async Task ConnectSelectedDeviceAsync()
    {
        var device = SelectedDevice;
        if (device is null || _deviceManagement is null)
        {
            return;
        }

        await RunDeviceActionAsync(async cancellationToken =>
        {
            AppendLog($"正在连接设备 {device.DeviceId}（{device.Endpoint}）。");
            var result = await _deviceManagement.ConnectAsync(
                device.DeviceId,
                cancellationToken);
            AppendLog(result.Connected
                ? $"设备 {device.DeviceId} 已连接。"
                : $"设备 {device.DeviceId} 连接失败：{result.Error ?? "未知错误"}");
            if (!result.Connected)
            {
                MessageBox.Show(
                    this,
                    result.Error ?? "设备连接失败。",
                    "连接失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        });
    }

    private async Task DisconnectSelectedDeviceAsync()
    {
        var device = SelectedDevice;
        if (device is null || _deviceManagement is null)
        {
            return;
        }

        await RunDeviceActionAsync(async cancellationToken =>
        {
            var disconnected = await _deviceManagement.DisconnectAsync(
                device.DeviceId,
                cancellationToken);
            AppendLog(disconnected
                ? $"设备 {device.DeviceId} 已断开，自动连接已停用。"
                : $"设备 {device.DeviceId} 当前没有活动会话。");
        });
    }

    private void ToggleSelectedDeviceAutoConnect()
    {
        var device = SelectedDevice;
        if (device is null || _deviceManagement is null)
        {
            return;
        }

        try
        {
            var enabled = !device.AutoConnect;
            if (!_deviceManagement.SetAutoConnect(
                    device.DeviceId,
                    enabled))
            {
                throw new InvalidOperationException("设备配置已不存在。");
            }

            AppendLog(
                $"设备 {device.DeviceId} 自动连接已{(enabled ? "启用" : "停用")}。");
            RefreshDeviceGrid(showErrors: true);
        }
        catch (Exception ex)
        {
            ShowDeviceActionError(ex);
        }
    }

    private async Task DeleteSelectedDeviceAsync()
    {
        var device = SelectedDevice;
        if (device is null || _deviceManagement is null)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            $"确认删除设备“{device.DeviceId}”的数据库连接配置？\n\n" +
            "如设备当前在线，将先断开连接。此操作不会删除设备内的人员或考勤数据。",
            "删除设备配置",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        await RunDeviceActionAsync(async cancellationToken =>
        {
            var deleted = await _deviceManagement.DeleteAsync(
                device.DeviceId,
                cancellationToken);
            AppendLog(deleted
                ? $"设备 {device.DeviceId} 的数据库连接配置已删除。"
                : $"设备 {device.DeviceId} 的配置已不存在。");
        });
    }

    private async Task RunDeviceActionAsync(
        Func<CancellationToken, Task> action)
    {
        if (_deviceActionBusy)
        {
            return;
        }

        _deviceActionBusy = true;
        UpdateDeviceActionState();
        try
        {
            using var timeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(30));
            await action(timeout.Token);
        }
        catch (Exception ex)
        {
            ShowDeviceActionError(ex);
        }
        finally
        {
            _deviceActionBusy = false;
            RefreshDeviceGrid(showErrors: false);
            UpdateDeviceActionState();
        }
    }

    private void ShowDeviceActionError(Exception exception)
    {
        AppendLog($"设备管理操作失败：{exception.Message}");
        MessageBox.Show(
            this,
            exception.Message,
            "设备管理操作失败",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}

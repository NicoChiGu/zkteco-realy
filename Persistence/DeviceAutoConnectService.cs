using ZktecoRelay.Devices;

namespace ZktecoRelay.Persistence;

public sealed class DeviceAutoConnectService : BackgroundService
{
    private readonly DeviceConfigurationStore _store;
    private readonly DeviceManager _deviceManager;
    private readonly ILogger<DeviceAutoConnectService> _logger;

    public DeviceAutoConnectService(
        DeviceConfigurationStore store,
        DeviceManager deviceManager,
        ILogger<DeviceAutoConnectService> logger)
    {
        _store = store;
        _deviceManager = deviceManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        IReadOnlyList<DeviceConfiguration> configurations;
        try
        {
            configurations = _store.GetAutoConnectConfigurations();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load persisted ZKTeco device configurations.");
            return;
        }

        foreach (var configuration in configurations)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var result = await _deviceManager.RestoreAsync(configuration, stoppingToken);
                if (result.Connected)
                {
                    _logger.LogInformation(
                        "Automatically connected persisted device {DeviceId} at {IpAddress}:{Port}.",
                        configuration.DeviceId,
                        configuration.IpAddress,
                        configuration.Port);
                }
                else
                {
                    _logger.LogWarning(
                        "Automatic connection failed for device {DeviceId}. Vendor error: {VendorErrorCode}.",
                        configuration.DeviceId,
                        result.VendorErrorCode);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Automatic connection failed for device {DeviceId}.", configuration.DeviceId);
            }
        }
    }
}

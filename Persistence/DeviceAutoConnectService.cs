using ZktecoRelay.Devices;

namespace ZktecoRelay.Persistence;

public sealed class DeviceAutoConnectService : BackgroundService
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(3),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60)
    ];

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
        var retryStates = new Dictionary<string, RetryState>(
            StringComparer.OrdinalIgnoreCase);

        while (!stoppingToken.IsCancellationRequested)
        {
            IReadOnlyList<DeviceConfiguration> configurations;
            try
            {
                configurations = _store.GetAutoConnectConfigurations();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to load persisted ZKTeco device configurations.");
                await DelayUntilNextCoordination(stoppingToken);
                continue;
            }

            var configuredIds = configurations
                .Select(configuration => configuration.DeviceId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var staleDeviceId in retryStates.Keys
                         .Where(deviceId => !configuredIds.Contains(deviceId))
                         .ToArray())
            {
                retryStates.Remove(staleDeviceId);
            }

            foreach (var configuration in configurations)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                var status = _deviceManager.GetStatus(configuration.DeviceId);
                if (status?.Connected == true)
                {
                    retryStates.Remove(configuration.DeviceId);
                    _deviceManager.UpdateReconnectState(
                        configuration.DeviceId,
                        null,
                        null);
                    continue;
                }

                var now = DateTimeOffset.UtcNow;
                if (retryStates.TryGetValue(
                        configuration.DeviceId,
                        out var retryState) &&
                    retryState.NextAttemptAt > now)
                {
                    _deviceManager.UpdateReconnectState(
                        configuration.DeviceId,
                        retryState.Attempt,
                        retryState.NextAttemptAt);
                    continue;
                }

                var attempt = retryState?.Attempt + 1 ?? 1;
                _deviceManager.UpdateReconnectState(
                    configuration.DeviceId,
                    attempt,
                    null);
                try
                {
                    var result = await _deviceManager.RestoreAsync(
                        configuration,
                        stoppingToken);
                    if (result.Connected)
                    {
                        retryStates.Remove(configuration.DeviceId);
                        _deviceManager.UpdateReconnectState(
                            configuration.DeviceId,
                            null,
                            null);
                        _logger.LogInformation(
                            "Automatically connected persisted device {DeviceId} at {IpAddress}:{Port}.",
                            configuration.DeviceId,
                            configuration.IpAddress,
                            configuration.Port);
                        continue;
                    }

                    _logger.LogWarning(
                        "Automatic connection failed for device {DeviceId}. Vendor error: {VendorErrorCode}.",
                        configuration.DeviceId,
                        result.VendorErrorCode);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Automatic connection failed for device {DeviceId}.",
                        configuration.DeviceId);
                }

                var nextAttemptAt = now.Add(GetDelay(attempt));
                retryStates[configuration.DeviceId] =
                    new RetryState(attempt, nextAttemptAt);
                _deviceManager.UpdateReconnectState(
                    configuration.DeviceId,
                    attempt,
                    nextAttemptAt);
            }

            await DelayUntilNextCoordination(stoppingToken);
        }
    }

    internal static TimeSpan GetDelay(
        int attempt,
        double? jitterFactor = null)
    {
        if (attempt < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(attempt));
        }

        var baseDelay =
            RetryDelays[Math.Min(attempt - 1, RetryDelays.Length - 1)];
        var jitter =
            jitterFactor ?? 0.8 + Random.Shared.NextDouble() * 0.4;
        if (jitter is < 0.8 or > 1.2)
        {
            throw new ArgumentOutOfRangeException(nameof(jitterFactor));
        }

        return TimeSpan.FromMilliseconds(
            baseDelay.TotalMilliseconds * jitter);
    }

    private static async Task DelayUntilNextCoordination(
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private sealed record RetryState(
        int Attempt,
        DateTimeOffset NextAttemptAt);
}

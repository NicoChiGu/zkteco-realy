using System.Runtime.InteropServices;
using ZktecoRelay.Devices;
using ZktecoRelay.Persistence;
using ZktecoRelay.Realtime;

namespace ZktecoRelay.Diagnostics;

public sealed record HealthComponent(
    bool Healthy,
    string Message,
    DateTimeOffset CheckedAt,
    IReadOnlyDictionary<string, object?>? Details = null);

public sealed record RelayHealthReport(
    bool Healthy,
    DateTimeOffset CheckedAt,
    IReadOnlyDictionary<string, HealthComponent> Components);

public sealed class RelayHealthService
{
    private static readonly TimeSpan ComCacheDuration =
        TimeSpan.FromSeconds(30);

    private readonly RelayDatabase _database;
    private readonly RealtimeEventStore _eventStore;
    private readonly DeviceManager _deviceManager;
    private readonly SemaphoreSlim _comProbeLock = new(1, 1);
    private HealthComponent? _cachedComProbe;

    public RelayHealthService(
        RelayDatabase database,
        RealtimeEventStore eventStore,
        DeviceManager deviceManager)
    {
        _database = database;
        _eventStore = eventStore;
        _deviceManager = deviceManager;
    }

    public async Task<RelayHealthReport> CheckReadinessAsync(
        CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var comTask = GetComHealthAsync(cancellationToken);
        var databaseTask = Task.Run(
            () => CheckComponent(
                "SQLite read/write probe succeeded.",
                _database.VerifyReadWrite),
            cancellationToken);
        var eventStoreTask = Task.Run(
            () => CheckComponent(
                "Event store is readable.",
                _eventStore.VerifyRead),
            cancellationToken);
        var workerTask = CheckWorkersAsync(cancellationToken);

        await Task.WhenAll(
            comTask,
            databaseTask,
            eventStoreTask,
            workerTask);

        var components = new Dictionary<string, HealthComponent>
        {
            ["com"] = await comTask,
            ["sqlite"] = await databaseTask,
            ["eventStore"] = await eventStoreTask,
            ["staWorkers"] = await workerTask
        };

        return new RelayHealthReport(
            components.Values.All(component => component.Healthy),
            checkedAt,
            components);
    }

    private async Task<HealthComponent> GetComHealthAsync(
        CancellationToken cancellationToken)
    {
        var cached = _cachedComProbe;
        if (cached is not null &&
            DateTimeOffset.UtcNow - cached.CheckedAt < ComCacheDuration)
        {
            return cached;
        }

        await _comProbeLock.WaitAsync(cancellationToken);
        try
        {
            cached = _cachedComProbe;
            if (cached is not null &&
                DateTimeOffset.UtcNow - cached.CheckedAt < ComCacheDuration)
            {
                return cached;
            }

            var completion =
                new TaskCompletionSource<HealthComponent>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                object? instance = null;
                try
                {
                    var comType =
                        Type.GetTypeFromProgID(
                            "zkemkeeper.ZKEM.1",
                            throwOnError: false)
                        ?? Type.GetTypeFromProgID(
                            "zkemkeeper.ZKEM",
                            throwOnError: false);
                    if (comType is null)
                    {
                        completion.TrySetResult(new HealthComponent(
                            false,
                            "ZKTeco COM is not registered for the Relay process architecture.",
                            DateTimeOffset.UtcNow,
                            new Dictionary<string, object?>
                            {
                                ["processArchitecture"] =
                                    Environment.Is64BitProcess ? "x64" : "x86",
                                ["operatingSystemArchitecture"] =
                                    Environment.Is64BitOperatingSystem
                                        ? "x64"
                                        : "x86"
                            }));
                        return;
                    }

                    instance = Activator.CreateInstance(comType);
                    if (instance is null)
                    {
                        throw new InvalidOperationException(
                            "The COM activator returned null.");
                    }

                    completion.TrySetResult(new HealthComponent(
                        true,
                        "ZKTeco COM registration, architecture and activation probe succeeded.",
                        DateTimeOffset.UtcNow,
                        new Dictionary<string, object?>
                        {
                            ["processArchitecture"] =
                                Environment.Is64BitProcess ? "x64" : "x86",
                            ["clsid"] = comType.GUID.ToString("B")
                        }));
                }
                catch (BadImageFormatException ex)
                {
                    completion.TrySetResult(new HealthComponent(
                        false,
                        $"ZKTeco COM architecture mismatch: {ex.Message}",
                        DateTimeOffset.UtcNow));
                }
                catch (Exception ex)
                {
                    completion.TrySetResult(new HealthComponent(
                        false,
                        $"ZKTeco COM activation failed: {ex.Message}",
                        DateTimeOffset.UtcNow));
                }
                finally
                {
                    if (instance is not null &&
                        Marshal.IsComObject(instance))
                    {
                        try
                        {
                            Marshal.FinalReleaseComObject(instance);
                        }
                        catch
                        {
                        }
                    }
                }
            })
            {
                IsBackground = true,
                Name = "zkteco-health-probe"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            _cachedComProbe = await completion.Task
                .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            return _cachedComProbe;
        }
        catch (TimeoutException)
        {
            _cachedComProbe = new HealthComponent(
                false,
                "ZKTeco COM activation probe timed out.",
                DateTimeOffset.UtcNow);
            return _cachedComProbe;
        }
        finally
        {
            _comProbeLock.Release();
        }
    }

    private async Task<HealthComponent> CheckWorkersAsync(
        CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var responsive =
            await _deviceManager.AreSessionWorkersResponsiveAsync(
                TimeSpan.FromSeconds(2),
                cancellationToken);
        return new HealthComponent(
            responsive,
            responsive
                ? "All active device STA workers are responsive."
                : "At least one device STA worker is unresponsive.",
            checkedAt,
            new Dictionary<string, object?>
            {
                ["activeSessions"] =
                    _deviceManager.GetStatuses().Count
            });
    }

    private static HealthComponent CheckComponent(
        string successMessage,
        Action probe)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        try
        {
            probe();
            return new HealthComponent(
                true,
                successMessage,
                checkedAt);
        }
        catch (Exception ex)
        {
            return new HealthComponent(
                false,
                ex.Message,
                checkedAt);
        }
    }
}

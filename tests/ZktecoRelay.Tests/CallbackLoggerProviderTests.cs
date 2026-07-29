using Microsoft.Extensions.Logging;
using ZktecoRelay.Hosting;

namespace ZktecoRelay.Tests;

public sealed class CallbackLoggerProviderTests
{
    [Fact]
    public void ProviderForwardsOperationalLogsAndFiltersDebugMessages()
    {
        var messages = new List<string>();
        using var provider = new CallbackLoggerProvider(messages.Add);
        var logger = provider.CreateLogger(
            "ZktecoRelay.Persistence.DeviceAutoConnectService");
        var frameworkRequestLogger = provider.CreateLogger(
            "Microsoft.AspNetCore.Hosting.Diagnostics");

        logger.LogDebug("debug detail");
        frameworkRequestLogger.LogInformation(
            "Request starting /api/v1/events/ws?apiKey=must-not-leak");
        logger.LogInformation(
            "Connected device {DeviceId}.",
            "front-door");
        logger.LogError(
            new InvalidOperationException("socket closed"),
            "Reconnect failed.");

        Assert.Equal(2, messages.Count);
        Assert.DoesNotContain(
            messages,
            message => message.Contains(
                "must-not-leak",
                StringComparison.Ordinal));
        Assert.Contains(
            "INFORMATION [DeviceAutoConnectService] Connected device front-door.",
            messages[0]);
        Assert.Contains(
            "ERROR       [DeviceAutoConnectService] Reconnect failed.",
            messages[1]);
        Assert.Contains(
            "InvalidOperationException: socket closed",
            messages[1]);
    }
}

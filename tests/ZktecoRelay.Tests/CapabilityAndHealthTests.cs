using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ZktecoRelay.Devices;
using ZktecoRelay.Models;

namespace ZktecoRelay.Tests;

public sealed class CapabilityAndHealthTests
{
    [Fact]
    public async Task UnknownPhotoProbeIsNullableAndErrorsUseProtocolStatuses()
    {
        using var factory = new RelayApiFactory(
            new FakeComClientFactory(() => new UnknownCapabilityComClient()),
            maximumRetainedEvents: 100);
        using var client = factory.CreateClient();
        await Connect(client);

        using var capabilitiesRequest = Authenticated(
            HttpMethod.Get,
            "/api/v1/devices/front-door/capabilities");
        var capabilitiesResponse =
            await client.SendAsync(capabilitiesRequest);
        capabilitiesResponse.EnsureSuccessStatusCode();
        var capabilities =
            await capabilitiesResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            JsonValueKind.Null,
            capabilities
                .GetProperty("supportsUserPhotoDownload")
                .ValueKind);
        Assert.Contains(
            "IsNewFirmwareMachine",
            capabilities.GetProperty("probeErrors")[0].GetString());

        using var normallyOpenRequest = Authenticated(
            HttpMethod.Post,
            "/api/v1/devices/front-door/access/normally-open/start");
        var normallyOpenResponse =
            await client.SendAsync(normallyOpenRequest);
        Assert.Equal(
            HttpStatusCode.UnprocessableEntity,
            normallyOpenResponse.StatusCode);
        Assert.Equal(
            "capability_not_supported",
            (await normallyOpenResponse.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code")
                .GetString());

        using var doorRequest = Authenticated(
            HttpMethod.Get,
            "/api/v1/devices/front-door/access/door-state");
        var doorResponse = await client.SendAsync(doorRequest);
        Assert.Equal(
            HttpStatusCode.BadGateway,
            doorResponse.StatusCode);
        Assert.Equal(
            "device_operation_failed",
            (await doorResponse.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code")
                .GetString());
    }

    [Fact]
    public async Task HealthSeparatesLivenessReadinessAndAuthenticatedDiagnostics()
    {
        using var factory = new RelayApiFactory();
        using var client = factory.CreateClient();

        var liveness = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, liveness.StatusCode);

        var readiness = await client.GetAsync("/health/ready");
        Assert.Contains(
            readiness.StatusCode,
            new[] { HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable });
        var readinessBody =
            await readiness.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(readinessBody.GetProperty("components").TryGetProperty(
            "com",
            out _));
        Assert.True(readinessBody.GetProperty("components").TryGetProperty(
            "sqlite",
            out _));
        Assert.True(readinessBody.GetProperty("components").TryGetProperty(
            "eventStore",
            out _));
        Assert.True(readinessBody.GetProperty("components").TryGetProperty(
            "staWorkers",
            out _));

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/diagnostics/health")).StatusCode);
        using var diagnosticsRequest = Authenticated(
            HttpMethod.Get,
            "/api/v1/diagnostics/health");
        var diagnostics = await client.SendAsync(diagnosticsRequest);
        Assert.Equal(readiness.StatusCode, diagnostics.StatusCode);
    }

    private static async Task Connect(HttpClient client)
    {
        using var request = Authenticated(
            HttpMethod.Post,
            "/api/v1/devices/front-door/connect");
        request.Content = JsonContent.Create(
            new ConnectDeviceRequest("192.168.1.10"));
        (await client.SendAsync(request)).EnsureSuccessStatusCode();
    }

    private static HttpRequestMessage Authenticated(
        HttpMethod method,
        string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-API-Key", RelayApiFactory.ApiKey);
        return request;
    }

    private sealed class UnknownCapabilityComClient : FakeComClient
    {
        public override DeviceCapabilities GetCapabilities() =>
            new(
                16,
                true,
                14,
                true,
                true,
                true,
                null,
                true,
                ["IsNewFirmwareMachine is unavailable."]);

        public override DoorModeResult StartNormallyOpen() =>
            throw new CapabilityNotSupportedException(
                "Normally-open is unavailable.");

        public override DoorStateResult GetDoorState() =>
            throw new DeviceOperationException(
                "GetDoorState failed.",
                -1);
    }
}

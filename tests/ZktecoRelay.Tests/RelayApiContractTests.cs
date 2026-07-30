using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ZktecoRelay.Models;

namespace ZktecoRelay.Tests;

public sealed class RelayApiContractTests : IClassFixture<RelayApiFactory>
{
    private readonly HttpClient _client;

    public RelayApiContractTests(RelayApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ProtocolEndpointsRequireApiKeyAndDescribeStableFeatures()
    {
        var unauthorized = await _client.GetAsync("/api/v1/version");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var request = Authenticated(HttpMethod.Get, "/api/v1/version");
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var version =
            await response.Content.ReadFromJsonAsync<RelayVersion>();
        Assert.NotNull(version);
        Assert.Equal("zkteco-relay", version.Product);
        Assert.Equal("v1", version.ApiVersion);
        Assert.Equal(1, version.ProtocolVersion);

        using var capabilitiesRequest =
            Authenticated(HttpMethod.Get, "/api/v1/capabilities");
        var capabilitiesResponse =
            await _client.SendAsync(capabilitiesRequest);
        capabilitiesResponse.EnsureSuccessStatusCode();
        var capabilities =
            await capabilitiesResponse.Content
                .ReadFromJsonAsync<RelayCapabilities>();
        Assert.NotNull(capabilities);
        Assert.Contains("user-photo-download", capabilities.Features);
        Assert.Contains(
            "visible-light-face-photo-download",
            capabilities.Features);
        Assert.Contains("device-capability-probe", capabilities.Features);
        Assert.Contains("door-state", capabilities.Features);
        Assert.Contains("normally-open", capabilities.Features);
        Assert.Contains("event-replay", capabilities.Features);
    }

    [Fact]
    public async Task HunsRequiredDeviceRoutesReturnContractResponses()
    {
        await ConnectDevice();

        var photo = await Get<JsonElement>(
            "/api/v1/devices/front-door/users/EMP_1/photo");
        Assert.Equal("EMP_1.jpg", photo.GetProperty("fileName").GetString());

        var visibleLightPhoto = await Get<JsonElement>(
            "/api/v1/devices/front-door/users/EMP_1/visible-light-face-photo");
        Assert.Equal(
            "verify_biophoto_9_EMP_1.jpg",
            visibleLightPhoto.GetProperty("fileName").GetString());

        var capabilities = await Get<JsonElement>(
            "/api/v1/devices/front-door/capabilities");
        Assert.True(
            capabilities
                .GetProperty("supportsUserPhotoDownload")
                .GetBoolean());
        Assert.True(
            capabilities
                .GetProperty("supportsVisibleLightFacePhotoDownload")
                .GetBoolean());

        var doorState = await Get<JsonElement>(
            "/api/v1/devices/front-door/access/door-state");
        Assert.True(doorState.GetProperty("open").GetBoolean());

        var started = await Post<JsonElement>(
            "/api/v1/devices/front-door/access/normally-open/start",
            content: null);
        Assert.Equal(
            5,
            started.GetProperty("previousLockDriveTime").GetInt32());

        var ended = await Post<JsonElement>(
            "/api/v1/devices/front-door/access/normally-open/end",
            JsonContent.Create(new EndNormallyOpenRequest(5)));
        Assert.Equal(5, ended.GetProperty("lockDriveTime").GetInt32());
    }

    [Fact]
    public async Task OpenApiContainsProtocolAndRequiredDeviceRoutes()
    {
        var openApi = await _client.GetStringAsync("/openapi.yaml");
        Assert.Contains("/api/v1/version:", openApi);
        Assert.Contains("/api/v1/capabilities:", openApi);
        Assert.Contains(
            "/api/v1/devices/{deviceId}/users/{enrollNumber}/photo:",
            openApi);
        Assert.Contains(
            "/api/v1/devices/{deviceId}/users/{enrollNumber}/visible-light-face-photo:",
            openApi);
        Assert.Contains(
            "/api/v1/devices/{deviceId}/access/door-state:",
            openApi);
        Assert.Contains(
            "/api/v1/devices/{deviceId}/access/normally-open/start:",
            openApi);
        Assert.Contains(
            "/api/v1/devices/{deviceId}/access/normally-open/end:",
            openApi);
    }

    private async Task ConnectDevice()
    {
        using var request =
            Authenticated(
                HttpMethod.Post,
                "/api/v1/devices/front-door/connect");
        request.Content = JsonContent.Create(
            new ConnectDeviceRequest("192.168.1.10", 4370, string.Empty));
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private async Task<T> Get<T>(string path)
    {
        using var request = Authenticated(HttpMethod.Get, path);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<T> Post<T>(string path, HttpContent? content)
    {
        using var request = Authenticated(HttpMethod.Post, path);
        request.Content = content;
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static HttpRequestMessage Authenticated(
        HttpMethod method,
        string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-API-Key", RelayApiFactory.ApiKey);
        return request;
    }
}

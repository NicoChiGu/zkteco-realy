using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZktecoRelay.Devices;
using ZktecoRelay.Models;
using ZktecoRelay.Realtime;

namespace ZktecoRelay.Tests;

public sealed class RelayApiFactory : WebApplicationFactory<Program>
{
    public const string ApiKey = "test-api-key-123456789";
    private readonly string _temporaryDirectory =
        Path.Combine(
            Path.GetTempPath(),
            "zkteco-relay-tests",
            Guid.NewGuid().ToString("N"));
    private readonly IZktecoComClientFactory _clientFactory;
    private readonly int _maximumRetainedEvents;

    public RelayApiFactory()
        : this(new FakeComClientFactory(), 100_000)
    {
    }

    internal RelayApiFactory(
        IZktecoComClientFactory clientFactory,
        int maximumRetainedEvents)
    {
        _clientFactory = clientFactory;
        _maximumRetainedEvents = maximumRetainedEvents;
        Directory.CreateDirectory(_temporaryDirectory);
        Environment.SetEnvironmentVariable("ZKTECO_API_KEY", ApiKey);
        Environment.SetEnvironmentVariable(
            "ZKTECO_DATABASE_PATH",
            Path.Combine(_temporaryDirectory, "relay.db"));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IZktecoComClientFactory>();
            services.AddSingleton(_clientFactory);
            services.RemoveAll<RealtimeEventStore>();
            services.AddSingleton(provider => new RealtimeEventStore(
                provider.GetRequiredService<ZktecoRelay.Persistence.RelayDatabase>(),
                _maximumRetainedEvents));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Environment.SetEnvironmentVariable("ZKTECO_API_KEY", null);
        Environment.SetEnvironmentVariable("ZKTECO_DATABASE_PATH", null);
        try
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
        catch
        {
        }
    }
}

internal sealed class FakeComClientFactory : IZktecoComClientFactory
{
    private readonly Func<IZktecoComClient> _create;

    public FakeComClientFactory(Func<IZktecoComClient>? create = null)
    {
        _create = create ?? (() => new FakeComClient());
    }

    public IZktecoComClient Create(
        string deviceId,
        Action<RealtimeEvent>? eventSink = null) =>
        _create();
}

internal class FakeComClient : IZktecoComClient
{
    public virtual bool Connect(string ipAddress, int port, string communicationPassword) =>
        true;
    public virtual void Disconnect() { }
    public int GetLastError() => 0;
    public virtual ConnectionProbeResult ProbeConnection() => new(true, 0);
    public void RegisterRealtimeEvents() { }
    public IReadOnlyList<AttendanceRecord> ReadAttendance(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null) => [];
    public OperationResult ClearAttendance(AttendanceClearRequest request) =>
        new(true);
    public void Restart() { }
    public virtual IReadOnlyList<UserInfo> GetUsers() => [];
    public UserInfo GetUser(string enrollNumber) =>
        new(enrollNumber, "Test", 0, true, string.Empty, false);
    public OperationResult UpsertUser(
        string enrollNumber,
        UpsertUserRequest request) => new(true);
    public OperationResult DeleteUser(string enrollNumber) => new(true);
    public FingerprintTemplateResult GetFingerprint(
        string enrollNumber,
        int fingerIndex) => new(enrollNumber, fingerIndex, "template", 8);
    public OperationResult SetFingerprint(
        string enrollNumber,
        FingerprintTemplateRequest request) => new(true);
    public OperationResult DeleteFingerprint(
        string enrollNumber,
        int fingerIndex) => new(true);
    public FaceTemplateResult GetFace(string enrollNumber, int faceIndex) =>
        new(enrollNumber, faceIndex, "template", 8);
    public OperationResult SetFace(
        string enrollNumber,
        FaceTemplateRequest request) => new(true);
    public OperationResult DeleteFace(string enrollNumber, int faceIndex) =>
        new(true);
    public OperationResult UploadUserPhoto(
        string enrollNumber,
        UserPhotoRequest request) => new(true);
    public UserPhotoResult DownloadUserPhoto(string enrollNumber) =>
        new(enrollNumber, $"{enrollNumber}.jpg", "/9j/2Q==", 4);
    public OperationResult UnlockDoor(DoorUnlockRequest request) => new(true);
    public virtual DeviceCapabilities GetCapabilities() =>
        new(16, true, 14, true, true, true, true, true, []);
    public virtual DoorStateResult GetDoorState() => new(true, 1);
    public virtual DoorModeResult StartNormallyOpen() =>
        new(true, 255, 5, true);
    public DoorModeResult EndNormallyOpen(EndNormallyOpenRequest request) =>
        new(false, request.RestoreLockDriveTime, 255, false);
    public TimeZoneInfoResult GetTimeZone(int timeZoneIndex) =>
        new(timeZoneIndex, string.Empty);
    public OperationResult SetTimeZone(TimeZoneRequest request) => new(true);
    public AccessGroupInfo GetAccessGroup(int groupNumber) =>
        new(groupNumber, 1, 2, 3, false, 0);
    public OperationResult SetAccessGroup(AccessGroupRequest request) =>
        new(true);
    public UserAccessInfo GetUserAccess(string enrollNumber) =>
        new(enrollNumber, 1, "0:0:0:0", true);
    public OperationResult SetUserAccess(
        string enrollNumber,
        UserAccessRequest request) => new(true);
    public UnlockCombinationInfo GetUnlockCombination(int combinationNumber) =>
        new(combinationNumber, 1, 0, 0, 0, 0);
    public OperationResult SetUnlockCombination(
        UnlockCombinationRequest request) => new(true);
    public void Dispose() { }
}

using ZktecoRelay.Models;
using ZktecoRelay.Realtime;

namespace ZktecoRelay.Devices;

public sealed record ConnectionProbeResult(
    bool? Connected,
    int VendorErrorCode,
    string? Error = null);

public interface IZktecoComClient : IDisposable
{
    bool Connect(string ipAddress, int port, string communicationPassword);
    void Disconnect();
    int GetLastError();
    ConnectionProbeResult ProbeConnection();
    void RegisterRealtimeEvents();
    IReadOnlyList<AttendanceRecord> ReadAttendance(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null);
    OperationResult ClearAttendance(AttendanceClearRequest request);
    void Restart();
    IReadOnlyList<UserInfo> GetUsers();
    UserInfo GetUser(string enrollNumber);
    OperationResult UpsertUser(
        string enrollNumber,
        UpsertUserRequest request);
    OperationResult DeleteUser(string enrollNumber);
    FingerprintTemplateResult GetFingerprint(
        string enrollNumber,
        int fingerIndex);
    OperationResult SetFingerprint(
        string enrollNumber,
        FingerprintTemplateRequest request);
    OperationResult DeleteFingerprint(
        string enrollNumber,
        int fingerIndex);
    FaceTemplateResult GetFace(string enrollNumber, int faceIndex);
    OperationResult SetFace(
        string enrollNumber,
        FaceTemplateRequest request);
    OperationResult DeleteFace(string enrollNumber, int faceIndex);
    OperationResult UploadUserPhoto(
        string enrollNumber,
        UserPhotoRequest request);
    UserPhotoResult DownloadUserPhoto(string enrollNumber);
    OperationResult UnlockDoor(DoorUnlockRequest request);
    DeviceCapabilities GetCapabilities();
    DoorStateResult GetDoorState();
    DoorModeResult StartNormallyOpen();
    DoorModeResult EndNormallyOpen(EndNormallyOpenRequest request);
    TimeZoneInfoResult GetTimeZone(int timeZoneIndex);
    OperationResult SetTimeZone(TimeZoneRequest request);
    AccessGroupInfo GetAccessGroup(int groupNumber);
    OperationResult SetAccessGroup(AccessGroupRequest request);
    UserAccessInfo GetUserAccess(string enrollNumber);
    OperationResult SetUserAccess(
        string enrollNumber,
        UserAccessRequest request);
    UnlockCombinationInfo GetUnlockCombination(int combinationNumber);
    OperationResult SetUnlockCombination(
        UnlockCombinationRequest request);
}

public interface IZktecoComClientFactory
{
    IZktecoComClient Create(
        string deviceId,
        Action<RealtimeEvent>? eventSink = null);
}

public sealed class ZktecoComClientFactory : IZktecoComClientFactory
{
    public IZktecoComClient Create(
        string deviceId,
        Action<RealtimeEvent>? eventSink = null) =>
        new ZktecoComClient(deviceId, eventSink);
}

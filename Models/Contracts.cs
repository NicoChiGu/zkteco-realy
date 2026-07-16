namespace ZktecoRelay.Models;

public sealed record ApiError(string Code, string Message);

public sealed record ConnectDeviceRequest(
    string IpAddress,
    int Port = 4370,
    string? CommunicationPassword = null);

public sealed record DeviceConnectionResult(
    string DeviceId,
    bool Connected,
    string? Error = null,
    int? VendorErrorCode = null);

public sealed record DeviceStatus(
    string DeviceId,
    string IpAddress,
    int Port,
    bool Connected,
    DateTimeOffset? ConnectedAt,
    string? LastError);

public sealed record AttendanceRecord(
    string EnrollNumber,
    int VerifyMode,
    int InOutMode,
    DateTimeOffset Timestamp,
    int WorkCode);

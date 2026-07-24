namespace ZktecoRelay.Models;

public sealed record ApiError(string Code, string Message, int? VendorErrorCode = null);

public sealed record ConnectDeviceRequest(
    string IpAddress,
    int Port = 4370,
    string? CommunicationPassword = null);

public sealed record UpdateDeviceConfigurationRequest(
    string IpAddress,
    int Port = 4370,
    string? CommunicationPassword = null,
    bool AutoConnect = true);

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

public sealed record AttendancePage(
    IReadOnlyList<AttendanceRecord> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    DateTimeOffset? From,
    DateTimeOffset? To);

public sealed record AttendanceClearRequest(
    bool Confirm = false,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    DateTimeOffset? Before = null);

public sealed record UserInfo(
    string EnrollNumber,
    string Name,
    int Privilege,
    bool Enabled,
    string CardNumber,
    bool HasPassword);

public sealed record UpsertUserRequest(
    string Name,
    string? Password = null,
    int Privilege = 0,
    bool Enabled = true,
    string? CardNumber = null);

public sealed record FingerprintTemplateRequest(int FingerIndex, string TemplateData);
public sealed record FingerprintTemplateResult(string EnrollNumber, int FingerIndex, string TemplateData, int TemplateLength);

public sealed record FaceTemplateRequest(string TemplateData, int FaceIndex = 50);
public sealed record FaceTemplateResult(string EnrollNumber, int FaceIndex, string TemplateData, int TemplateLength);

public sealed record UserPhotoRequest(string Base64Jpeg, bool VisibleLightFacePhoto = false);

public sealed record DoorUnlockRequest(int DelayTenthsOfSecond = 30);

public sealed record TimeZoneRequest(int TimeZoneIndex, string Schedule);
public sealed record TimeZoneInfoResult(int TimeZoneIndex, string Schedule);

public sealed record AccessGroupRequest(
    int GroupNumber,
    int TimeZone1,
    int TimeZone2,
    int TimeZone3,
    bool HolidayValid = false,
    int VerifyStyle = 0);

public sealed record AccessGroupInfo(
    int GroupNumber,
    int TimeZone1,
    int TimeZone2,
    int TimeZone3,
    bool HolidayValid,
    int VerifyStyle);

public sealed record UserAccessRequest(int GroupNumber, int TimeZone1 = 0, int TimeZone2 = 0, int TimeZone3 = 0, bool UseGroupTimeZone = true);
public sealed record UserAccessInfo(string EnrollNumber, int GroupNumber, string TimeZones, bool UsesGroupTimeZone);

public sealed record UnlockCombinationRequest(int CombinationNumber, int Group1, int Group2 = 0, int Group3 = 0, int Group4 = 0, int Group5 = 0);
public sealed record UnlockCombinationInfo(int CombinationNumber, int Group1, int Group2, int Group3, int Group4, int Group5);

public sealed record OperationResult(bool Success, int? VendorErrorCode = null, string? Message = null);

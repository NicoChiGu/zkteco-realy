using ZktecoRelay.Models;

namespace ZktecoRelay.Devices;

internal sealed partial class ZktecoComClient
{
    private const int LockDriveTimeDeviceInfo = 5;
    private const int NormallyOpenLockDriveTime = 255;

    public OperationResult UnlockDoor(DoorUnlockRequest request)
    {
        ThrowIfDisposed();
        var ok = _sdk.ACUnlock(MachineNumber, request.DelayTenthsOfSecond);
        return ok ? new OperationResult(true) : Failure("ACUnlock");
    }

    public TimeZoneInfoResult GetTimeZone(int timeZoneIndex)
    {
        ThrowIfDisposed();
        string schedule = string.Empty;
        if (!_sdk.GetTZInfo(MachineNumber, timeZoneIndex, out schedule))
        {
            throw VendorFailure("GetTZInfo");
        }

        return new TimeZoneInfoResult(timeZoneIndex, schedule);
    }

    public OperationResult SetTimeZone(TimeZoneRequest request)
    {
        ThrowIfDisposed();
        var ok = _sdk.SetTZInfo(MachineNumber, request.TimeZoneIndex, request.Schedule);
        return ok ? new OperationResult(true) : Failure("SetTZInfo");
    }

    public AccessGroupInfo GetAccessGroup(int groupNumber)
    {
        ThrowIfDisposed();
        var tz1 = 0;
        var tz2 = 0;
        var tz3 = 0;
        var validHoliday = 0;
        var verifyStyle = 0;
        if (!_sdk.SSR_GetGroupTZ(MachineNumber, groupNumber, out tz1, out tz2, out tz3, out validHoliday, out verifyStyle))
        {
            throw VendorFailure("SSR_GetGroupTZ");
        }

        return new AccessGroupInfo(groupNumber, tz1, tz2, tz3, validHoliday != 0, verifyStyle);
    }

    public OperationResult SetAccessGroup(AccessGroupRequest request)
    {
        ThrowIfDisposed();
        var ok = _sdk.SSR_SetGroupTZ(
            MachineNumber,
            request.GroupNumber,
            request.TimeZone1,
            request.TimeZone2,
            request.TimeZone3,
            request.HolidayValid ? 1 : 0,
            request.VerifyStyle);
        return ok ? new OperationResult(true) : Failure("SSR_SetGroupTZ");
    }

    public UserAccessInfo GetUserAccess(string enrollNumber)
    {
        ThrowIfDisposed();
        if (!int.TryParse(enrollNumber, out var userId))
        {
            throw new ArgumentException("Access-control group APIs require a numeric enrollNumber.");
        }

        var groupNumber = 0;
        if (!_sdk.GetUserGroup(MachineNumber, userId, out groupNumber))
        {
            throw VendorFailure("GetUserGroup");
        }

        string timeZones = string.Empty;
        if (!_sdk.GetUserTZStr(MachineNumber, userId, out timeZones))
        {
            throw VendorFailure("GetUserTZStr");
        }

        var usesGroup = false;
        try { usesGroup = _sdk.UseGroupTimeZone(); } catch { usesGroup = timeZones.StartsWith("0:", StringComparison.Ordinal); }
        return new UserAccessInfo(enrollNumber, groupNumber, timeZones, usesGroup);
    }

    public OperationResult SetUserAccess(string enrollNumber, UserAccessRequest request)
    {
        ThrowIfDisposed();
        ValidateWritableEnrollNumber(enrollNumber);
        if (!int.TryParse(enrollNumber, out var userId))
        {
            throw new ArgumentException("Access-control group APIs require a numeric enrollNumber.");
        }

        if (!_sdk.SetUserGroup(MachineNumber, userId, request.GroupNumber))
        {
            return Failure("SetUserGroup");
        }

        var timeZones = request.UseGroupTimeZone
            ? "0:0:0:0"
            : $"{request.TimeZone1}:{request.TimeZone2}:{request.TimeZone3}:1";

        if (!_sdk.SetUserTZStr(MachineNumber, userId, timeZones))
        {
            return Failure("SetUserTZStr");
        }

        _sdk.RefreshData(MachineNumber);
        return new OperationResult(true);
    }

    public UnlockCombinationInfo GetUnlockCombination(int combinationNumber)
    {
        ThrowIfDisposed();
        var g1 = 0;
        var g2 = 0;
        var g3 = 0;
        var g4 = 0;
        var g5 = 0;
        if (!_sdk.SSR_GetUnLockGroup(MachineNumber, combinationNumber, out g1, out g2, out g3, out g4, out g5))
        {
            throw VendorFailure("SSR_GetUnLockGroup");
        }

        return new UnlockCombinationInfo(combinationNumber, g1, g2, g3, g4, g5);
    }

    public OperationResult SetUnlockCombination(UnlockCombinationRequest request)
    {
        ThrowIfDisposed();
        var ok = _sdk.SSR_SetUnLockGroup(
            MachineNumber,
            request.CombinationNumber,
            request.Group1,
            request.Group2,
            request.Group3,
            request.Group4,
            request.Group5);
        return ok ? new OperationResult(true) : Failure("SSR_SetUnLockGroup");
    }

    public DeviceCapabilities GetCapabilities()
    {
        ThrowIfDisposed();
        var errors = new List<string>();
        var pinWidth = TryGetDeviceInfo(76, "PIN2Width", errors);
        var supportsAlphabeticPinValue = TryGetDeviceInfo(77, "IsSupportABCPin", errors);
        var lockDriveTime = TryGetDeviceInfo(
            LockDriveTimeDeviceInfo,
            "lock drive time",
            errors);
        int? accessControlFunction = null;
        var supportsDoorState = false;
        bool? supportsUserPhotoDownload = null;

        try
        {
            var value = 0;
            if (_sdk.GetACFun(out value))
            {
                accessControlFunction = value;
            }
            else
            {
                errors.Add($"GetACFun failed with vendor error {GetLastError()}.");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"GetACFun is unavailable: {ex.Message}");
        }

        try
        {
            var state = 0;
            supportsDoorState = _sdk.GetDoorState(MachineNumber, out state);
            if (!supportsDoorState)
            {
                errors.Add($"GetDoorState failed with vendor error {GetLastError()}.");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"GetDoorState is unavailable: {ex.Message}");
        }

        try
        {
            supportsUserPhotoDownload = _sdk.IsNewFirmwareMachine(MachineNumber);
        }
        catch (Exception ex)
        {
            errors.Add($"IsNewFirmwareMachine is unavailable: {ex.Message}");
        }

        return new DeviceCapabilities(
            pinWidth,
            supportsAlphabeticPinValue.HasValue
                ? supportsAlphabeticPinValue.Value != 0
                : null,
            accessControlFunction,
            accessControlFunction is 6 or 14 or 15,
            accessControlFunction == 14 ||
                lockDriveTime is >= 0 and <= NormallyOpenLockDriveTime,
            supportsDoorState,
            supportsUserPhotoDownload,
            supportsUserPhotoDownload,
            SupportsAttendanceRangeQuery: true,
            errors);
    }

    public DoorStateResult GetDoorState()
    {
        ThrowIfDisposed();
        var state = 0;
        if (!_sdk.GetDoorState(MachineNumber, out state))
        {
            throw VendorFailure("GetDoorState");
        }

        return new DoorStateResult(state == 1, state);
    }

    public DoorModeResult StartNormallyOpen()
    {
        ThrowIfDisposed();
        var previousLockDriveTime = GetRequiredDeviceInfo(
            LockDriveTimeDeviceInfo,
            "lock drive time");
        if (previousLockDriveTime != NormallyOpenLockDriveTime &&
            !_sdk.SetDeviceInfo(
                MachineNumber,
                LockDriveTimeDeviceInfo,
                NormallyOpenLockDriveTime))
        {
            throw VendorFailure("SetDeviceInfo(5,255)");
        }

        _sdk.RefreshData(MachineNumber);
        return new DoorModeResult(
            NormallyOpen: true,
            LockDriveTime: NormallyOpenLockDriveTime,
            PreviousLockDriveTime: previousLockDriveTime,
            DoorOpen: TryReadDoorOpen());
    }

    public DoorModeResult EndNormallyOpen(EndNormallyOpenRequest request)
    {
        ThrowIfDisposed();
        if (request.RestoreLockDriveTime is < 0 or >= NormallyOpenLockDriveTime)
        {
            throw new ArgumentException(
                "restoreLockDriveTime must be between 0 and 254.");
        }

        var previousLockDriveTime = GetRequiredDeviceInfo(
            LockDriveTimeDeviceInfo,
            "lock drive time");
        if (!_sdk.SetDeviceInfo(
                MachineNumber,
                LockDriveTimeDeviceInfo,
                request.RestoreLockDriveTime))
        {
            throw VendorFailure("SetDeviceInfo(5,restoreLockDriveTime)");
        }

        _sdk.RefreshData(MachineNumber);
        return new DoorModeResult(
            NormallyOpen: false,
            LockDriveTime: request.RestoreLockDriveTime,
            PreviousLockDriveTime: previousLockDriveTime,
            DoorOpen: TryReadDoorOpen());
    }

    private int? TryGetDeviceInfo(
        int info,
        string label,
        ICollection<string> errors)
    {
        try
        {
            var value = 0;
            if (_sdk.GetDeviceInfo(MachineNumber, info, out value))
            {
                return value;
            }

            errors.Add(
                $"GetDeviceInfo({info}:{label}) failed with vendor error {GetLastError()}.");
        }
        catch (Exception ex)
        {
            errors.Add($"GetDeviceInfo({info}:{label}) is unavailable: {ex.Message}");
        }

        return null;
    }

    private int GetRequiredDeviceInfo(int info, string label)
    {
        var errors = new List<string>();
        var value = TryGetDeviceInfo(info, label, errors);
        if (value.HasValue)
        {
            return value.Value;
        }

        throw new DeviceOperationException(string.Join(" ", errors));
    }

    private bool? TryReadDoorOpen()
    {
        try
        {
            var state = 0;
            return _sdk.GetDoorState(MachineNumber, out state)
                ? state == 1
                : null;
        }
        catch
        {
            return null;
        }
    }
}

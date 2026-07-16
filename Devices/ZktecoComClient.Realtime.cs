using System.Runtime.InteropServices;
using ZktecoRelay.Realtime;

namespace ZktecoRelay.Devices;

internal sealed partial class ZktecoComClient
{
    private static readonly Guid EventsInterfaceId = new("CF83B580-5D32-4C65-B44D-BEDC750CDFA8");
    private readonly List<(int DispId, Delegate Handler)> _eventHandlers = new();
    private bool _eventsRegistered;

    private delegate void EmptyEventHandler();
    private delegate void IntEventHandler(int value);
    private delegate void ThreeIntEventHandler(int value1, int value2, int value3);
    private delegate void TwoIntEventHandler(int value1, int value2);
    private delegate void AttTransactionExEventHandler(
        string enrollNumber,
        int isInvalid,
        int attendanceState,
        int verifyMethod,
        int year,
        int month,
        int day,
        int hour,
        int minute,
        int second,
        int workCode);
    private delegate void EnrollFingerExEventHandler(string enrollNumber, int fingerIndex, int actionResult, int templateLength);

    public void RegisterRealtimeEvents()
    {
        ThrowIfDisposed();
        if (_eventsRegistered)
        {
            return;
        }

        if (!_sdk.RegEvent(MachineNumber, 65535))
        {
            throw new InvalidOperationException($"RegEvent failed. Vendor error: {GetLastError()}.");
        }

        AddComEvent(6, (EmptyEventHandler)(() => Emit("connected")));
        AddComEvent(8, (EmptyEventHandler)(() => Emit("finger_detected")));
        AddComEvent(9, (IntEventHandler)(userId => Emit("verification", ("userId", userId), ("success", userId != -1))));
        AddComEvent(10, (IntEventHandler)(score => Emit("finger_feature", ("score", score))));
        AddComEvent(11, (IntEventHandler)(cardNumber => Emit("card_swiped", ("cardNumber", cardNumber.ToString()))));
        AddComEvent(12, (IntEventHandler)(eventType => Emit("door", ("eventType", eventType))));
        AddComEvent(13, (ThreeIntEventHandler)((alarmType, enrollNumber, verified) =>
            Emit("alarm", ("alarmType", alarmType), ("enrollNumber", enrollNumber.ToString()), ("verified", verified != 0))));
        AddComEvent(16, (TwoIntEventHandler)((enrollNumber, fingerIndex) =>
            Emit("template_deleted", ("enrollNumber", enrollNumber.ToString()), ("fingerIndex", fingerIndex))));
        AddComEvent(17, (AttTransactionExEventHandler)((enrollNumber, isInvalid, attendanceState, verifyMethod, year, month, day, hour, minute, second, workCode) =>
        {
            DateTimeOffset? timestamp = null;
            try
            {
                var local = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
                timestamp = new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
            }
            catch (ArgumentOutOfRangeException)
            {
                // Keep the raw fields when a device reports an invalid date.
            }

            Emit(
                "attendance",
                ("enrollNumber", enrollNumber),
                ("isInvalid", isInvalid != 0),
                ("attendanceState", attendanceState),
                ("verifyMethod", verifyMethod),
                ("timestamp", timestamp),
                ("year", year),
                ("month", month),
                ("day", day),
                ("hour", hour),
                ("minute", minute),
                ("second", second),
                ("workCode", workCode));
        }));
        AddComEvent(18, (EnrollFingerExEventHandler)((enrollNumber, fingerIndex, actionResult, templateLength) =>
            Emit(
                "finger_enrolled",
                ("enrollNumber", enrollNumber),
                ("fingerIndex", fingerIndex),
                ("actionResult", actionResult),
                ("templateLength", templateLength))));

        _eventsRegistered = true;
        Emit("events_registered", ("eventMask", 65535));
    }

    private void AddComEvent(int dispId, Delegate handler)
    {
        ComEventsHelper.Combine(_sdk, EventsInterfaceId, dispId, handler);
        _eventHandlers.Add((dispId, handler));
    }

    private void UnregisterRealtimeEvents()
    {
        foreach (var (dispId, handler) in _eventHandlers)
        {
            try
            {
                ComEventsHelper.Remove(_sdk, EventsInterfaceId, dispId, handler);
            }
            catch
            {
                // Ignore COM teardown failures.
            }
        }

        _eventHandlers.Clear();
        _eventsRegistered = false;
    }

    private void Emit(string eventType, params (string Key, object? Value)[] values)
    {
        var data = values.ToDictionary(value => value.Key, value => value.Value, StringComparer.OrdinalIgnoreCase);
        _eventSink?.Invoke(new RealtimeEvent(
            Guid.NewGuid().ToString("N"),
            _deviceId,
            eventType,
            DateTimeOffset.UtcNow,
            data));
    }
}

namespace ZktecoRelay.Persistence;

public sealed record DeviceConfiguration(
    string DeviceId,
    string IpAddress,
    int Port,
    string CommunicationPassword,
    bool AutoConnect,
    DateTimeOffset UpdatedAt);

public sealed record DeviceConfigurationView(
    string DeviceId,
    string IpAddress,
    int Port,
    bool HasCommunicationPassword,
    bool AutoConnect,
    DateTimeOffset UpdatedAt);

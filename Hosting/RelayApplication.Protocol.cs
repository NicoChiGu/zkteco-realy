using System.Reflection;
using ZktecoRelay.Models;

namespace ZktecoRelay.Hosting;

public static partial class RelayApplication
{
    public const int ProtocolVersion = 1;

    public static readonly IReadOnlyList<string> ProtocolFeatures =
    [
        "user-photo-download",
        "device-capability-probe",
        "door-state",
        "normally-open",
        "event-replay",
        "device-connection-liveness",
        "diagnostics-health"
    ];

    private static void MapProtocolEndpoints(WebApplication app)
    {
        app.MapGet("/api/v1/version", () => Results.Ok(new RelayVersion(
            Product: "zkteco-relay",
            Version: GetAssemblyVersion(),
            ApiVersion: "v1",
            ProtocolVersion)));

        app.MapGet("/api/v1/capabilities", () => Results.Ok(new RelayCapabilities(
            ProtocolVersion,
            ProtocolFeatures)));
    }

    private static string GetAssemblyVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            return informational.Split('+', 2)[0];
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}

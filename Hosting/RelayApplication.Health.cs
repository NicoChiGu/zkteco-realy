using ZktecoRelay.Diagnostics;

namespace ZktecoRelay.Hosting;

public static partial class RelayApplication
{
    private static void MapHealthEndpoints(WebApplication app)
    {
        app.MapGet("/health/live", () => Results.Ok(new
        {
            status = "healthy",
            checkedAt = DateTimeOffset.UtcNow
        }));

        static async Task<IResult> Readiness(
            RelayHealthService healthService,
            CancellationToken cancellationToken)
        {
            var report =
                await healthService.CheckReadinessAsync(cancellationToken);
            return Results.Json(
                report,
                statusCode: report.Healthy
                    ? StatusCodes.Status200OK
                    : StatusCodes.Status503ServiceUnavailable);
        }

        app.MapGet("/health", Readiness);
        app.MapGet("/health/ready", Readiness);
        app.MapGet("/api/v1/diagnostics/health", Readiness);
    }
}

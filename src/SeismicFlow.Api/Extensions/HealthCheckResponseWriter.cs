using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SeismicFlow.Api.Extensions;

/// <summary>
/// Writes health check results as structured JSON matching the format
/// expected by the UI:
/// {
///   "status": "Healthy",
///   "totalDuration": "00:00:00.045",
///   "entries": {
///     "postgres": { "status": "Healthy", "duration": "00:00:00.012" },
///     "keycloak": { "status": "Healthy", "duration": "00:00:00.034" }
///   }
/// }
///
/// ASP.NET Core's default ResponseWriter only returns a plain text string
/// like "Healthy" — this replaces it with proper JSON.
/// </summary>
public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var entries = new Dictionary<string, object>();
        foreach (var (name, entry) in report.Entries)
        {
            entries[name] = new
            {
                status = entry.Status.ToString(),
                duration = entry.Duration.ToString(),
                description = entry.Description,
                exception = entry.Exception?.Message,
            };
        }

        var result = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.ToString(),
            entries,
        };

        return context.Response.WriteAsync(
            JsonSerializer.Serialize(result, JsonOpts));
    }
}
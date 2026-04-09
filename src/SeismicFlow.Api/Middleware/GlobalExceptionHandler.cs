using System.Net;
using System.Text.Json;

namespace SeismicFlow.Api.Middleware;

/// <summary>
/// Catches all unhandled exceptions and returns a consistent ProblemDetails response.
/// Register BEFORE other middleware so it wraps the entire pipeline.
/// </summary>
public sealed class GlobalExceptionHandler(
    RequestDelegate next,
    ILogger<GlobalExceptionHandler> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                ctx.Request.Method, ctx.Request.Path);

            await WriteProblemAsync(ctx, ex);
        }
    }

    private static Task WriteProblemAsync(HttpContext ctx, Exception ex)
    {
        var (status, title) = ex switch
        {
            ArgumentException or ArgumentNullException
                => (HttpStatusCode.BadRequest, "Bad Request"),
            InvalidOperationException
                => (HttpStatusCode.Conflict, "Conflict"),
            KeyNotFoundException
                => (HttpStatusCode.NotFound, "Not Found"),
            UnauthorizedAccessException
                => (HttpStatusCode.Forbidden, "Forbidden"),
            _ => (HttpStatusCode.InternalServerError, "Internal Server Error")
        };

        ctx.Response.StatusCode = (int)status;
        ctx.Response.ContentType = "application/problem+json";

        var problem = new
        {
            type = $"https://httpstatuses.com/{(int)status}",
            title,
            status = (int)status,
            detail = ex.Message,
            traceId = ctx.TraceIdentifier
        };

        return ctx.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOpts));
    }
}
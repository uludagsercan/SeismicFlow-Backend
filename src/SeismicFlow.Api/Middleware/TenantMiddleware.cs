namespace SeismicFlow.Api.Middleware;

public sealed class TenantMiddleware(RequestDelegate next)
{
    private static readonly string[] BypassPaths =
    [
        "/api/v1/tenants",
        "/auth",
        "/swagger",
        "/mqtt",
        "/openapi"
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var path = context.Request.Path.Value ?? "";
            var isBypass = BypassPaths.Any(p =>
                path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

            var isSuperAdmin = context.User.IsInRole("super-admin");

            if (!isBypass)
            {
                Guid? tenantId = null;

                if (isSuperAdmin)
                {
                    // Super-admin: X-Tenant-Id header takes priority.
                    // JWT may contain a dummy tenant_id (e.g. 00000000-...-0001)
                    // which doesn't exist in the database.
                    var header = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(header) && Guid.TryParse(header, out var headerId))
                        tenantId = headerId;
                }
                else
                {
                    // Regular users: JWT claim first, then header fallback
                    var claim = context.User.FindFirst("tenant_id")?.Value;
                    if (!string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out var claimId))
                        tenantId = claimId;

                    if (tenantId is null)
                    {
                        var header = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
                        if (!string.IsNullOrEmpty(header) && Guid.TryParse(header, out var headerId))
                            tenantId = headerId;
                    }
                }

                if (tenantId is not null)
                {
                    context.Items["TenantId"] = tenantId.Value;
                }
                else if (!isSuperAdmin)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Missing tenant_id. Provide via JWT claim or X-Tenant-Id header."
                    });
                    return;
                }
            }
        }

        await next(context);
    }
}

public static class HttpContextExtensions
{
    public static Guid GetTenantId(this HttpContext context)
    {
        if (context.Items.TryGetValue("TenantId", out var val) && val is Guid id)
            return id;
        throw new InvalidOperationException("TenantId not found in HttpContext.");
    }

    public static Guid? GetTenantIdOrNull(this HttpContext context)
    {
        if (context.Items.TryGetValue("TenantId", out var val) && val is Guid id)
            return id;

        return null;
    }
}
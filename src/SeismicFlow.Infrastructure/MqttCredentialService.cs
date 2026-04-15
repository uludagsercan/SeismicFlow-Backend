using Microsoft.EntityFrameworkCore;
using SeismicFlow.Domain.Enums;
using SeismicFlow.Domain.Aggregates.Devices;
using SeismicFlow.Application.Common.Interfaces;
using SeismicFlow.Infrastructure.Persistence.Contexts;

namespace SeismicFlow.Infrastructure.Persistence;

/// <summary>
/// Validates MQTT device credentials and ACL checks.
/// Username = device ID (Guid), password = device.MqttPassword.
/// Topic format: tenant/{tenantId}/devices/{deviceId}/data
/// </summary>
public sealed class MqttCredentialService(MasterDbContext masterDb) : IMqttCredentialService
{
    public async Task<bool> ValidateCredentialsAsync(
        string username, string password, CancellationToken ct = default)
    {
        if (!Guid.TryParse(username, out var deviceId)) return false;

        var tenants = await masterDb.Tenants
            .AsNoTracking()
            .Where(t => t.Status == TenantStatus.Active)
            .ToListAsync(ct);

        foreach (var tenant in tenants)
        {
            if (await ValidateInTenantDbAsync(tenant.Id, deviceId, password, ct))
                return true;
        }

        return false;
    }

    public async Task<bool> CheckAclAsync(
        string username, string topic, string access, CancellationToken ct = default)
    {
        // Topic format: tenant/{tenantId}/devices/{deviceId}/data
        // Device can only publish (acc=2) to its own topic
        if (!Guid.TryParse(username, out var deviceId)) return false;

        var parts = topic.Split('/');
        if (parts.Length < 4) return false;
        if (parts[0] != "tenant") return false;
        if (!Guid.TryParse(parts[1], out var topicTenantId)) return false;
        if (parts[2] != "devices") return false;
        if (!Guid.TryParse(parts[3], out var topicDeviceId)) return false;

        // Device can only access its own topic
        if (topicDeviceId != deviceId) return false;

        // Verify device belongs to the tenant in the topic
        var tenant = await masterDb.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == topicTenantId && t.Status == TenantStatus.Active, ct);

        if (tenant is null) return false;

        return await DeviceExistsInTenantAsync(topicTenantId, deviceId, ct);
    }

    private async Task<bool> ValidateInTenantDbAsync(
        Guid tenantId, Guid deviceId, string password, CancellationToken ct)
    {
        try
        {
            var tenant = await masterDb.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId && t.Status == TenantStatus.Active, ct);

            if (tenant is null) return false;

            var dbPassword = Environment.GetEnvironmentVariable(
                $"TENANT_DB_PASSWORD_{tenant.Slug.Value.Replace("-", "_").ToUpperInvariant()}")
                ?? "postgres";

            var connStr =
                $"Host={tenant.Database.Host};" +
                $"Port={tenant.Database.Port};" +
                $"Database={tenant.Database.DbName};" +
                $"Username={tenant.Database.DbUser};" +
                $"Password={dbPassword};";

            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseNpgsql(connStr)
                .Options;

            await using var ctx = new TenantDbContext(options);
            var device = await ctx.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == deviceId && d.Status == DeviceStatus.Active, ct);

            return device is not null && device.MqttPassword == password;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> DeviceExistsInTenantAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct)
    {
        try
        {
            var tenant = await masterDb.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId && t.Status == TenantStatus.Active, ct);

            if (tenant is null) return false;

            var dbPassword = Environment.GetEnvironmentVariable(
                $"TENANT_DB_PASSWORD_{tenant.Slug.Value.Replace("-", "_").ToUpperInvariant()}")
                ?? "postgres";

            var connStr =
                $"Host={tenant.Database.Host};" +
                $"Port={tenant.Database.Port};" +
                $"Database={tenant.Database.DbName};" +
                $"Username={tenant.Database.DbUser};" +
                $"Password={dbPassword};";

            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseNpgsql(connStr)
                .Options;

            await using var ctx = new TenantDbContext(options);
            return await ctx.Devices
                .AsNoTracking()
                .AnyAsync(d => d.Id == deviceId && d.Status == DeviceStatus.Active, ct);
        }
        catch
        {
            return false;
        }
    }
}
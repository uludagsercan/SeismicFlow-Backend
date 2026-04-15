using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using SeismicFlow.Application.Common.Interfaces;
using SeismicFlow.Domain.Repositories;
using SeismicFlow.Infrastructure.External.Keycloak;
using SeismicFlow.Infrastructure.Persistence;
using SeismicFlow.Infrastructure.Persistence.Contexts;
using SeismicFlow.Infrastructure.Persistence.Repositories;

namespace SeismicFlow.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<MasterDbContext>(opts =>
            opts.UseNpgsql(configuration.GetConnectionString("MasterDb")));

        // Factory — creates a NEW TenantDbContext per call (used by background services)
        services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>();

        // Scoped accessor — caches a SINGLE TenantDbContext per HTTP request scope
        // Both DeviceRepository and TenantUnitOfWork share this instance
        services.AddScoped<IScopedTenantDbContext, ScopedTenantDbContext>();

        services.AddHttpContextAccessor();

        // Repositories
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();

        // Units of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();              // master DB
        services.AddScoped<ITenantUnitOfWork, TenantUnitOfWork>();  // tenant DB

        // Infrastructure services
        services.AddScoped<ITenantDatabaseProvisioner, TenantDatabaseProvisioner>();
        services.AddScoped<IMqttCredentialService, MqttCredentialService>();

        return services;
    }

    public static IServiceCollection AddExternalServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<KeycloakOptions>(
            configuration.GetSection(KeycloakOptions.Section));

        services.Configure<SeedOptions>(
            configuration.GetSection(SeedOptions.Section));

        services.AddHttpClient<KeycloakTokenService>();
        services.AddHttpClient<KeycloakTenantService>();
        services.AddHttpClient<KeycloakUserService>();
        services.AddHttpClient<KeycloakRealmBootstrapper>();
        services.AddHttpClient<KeycloakSeedService>();

        services.AddScoped<IKeycloakTenantService, KeycloakTenantService>();
        services.AddScoped<IKeycloakUserService, KeycloakUserService>();

        return services;
    }

    public static IServiceCollection AddAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts =>
            {
                opts.Authority = configuration["Keycloak:Authority"];
                opts.RequireHttpsMetadata = false;
                opts.TokenValidationParameters = new()
                {
                    ValidateAudience = false,
                    ValidIssuer = configuration["Keycloak:Authority"]
                };
            });

        services.AddAuthorization(opts =>
        {
            opts.AddPolicy("AnyRole", policy =>
                policy.RequireAuthenticatedUser());

            opts.AddPolicy("DeviceRead", policy =>
                policy.RequireRole("super-admin", "tenant-admin", "device-manager", "tenant-user"));

            opts.AddPolicy("DeviceWrite", policy =>
                policy.RequireRole("super-admin", "tenant-admin", "device-manager"));

            opts.AddPolicy("SuperAdmin", policy =>
                policy.RequireRole("super-admin"));

            opts.AddPolicy("TenantAdmin", policy =>
                policy.RequireRole("super-admin", "tenant-admin"));
        });

        return services;
    }
}
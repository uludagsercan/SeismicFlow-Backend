using Microsoft.EntityFrameworkCore;
using SeismicFlow.Api.Endpoints;
using SeismicFlow.Api.Extensions;
using SeismicFlow.Api.Middleware;
using SeismicFlow.Application.Common.Interfaces;
using SeismicFlow.Infrastructure.External.Keycloak;
using SeismicFlow.Infrastructure.External.Mqtt;
using SeismicFlow.Infrastructure.Persistence;
using SeismicFlow.Infrastructure.Persistence.Contexts;
using SeismicFlow.Infrastructure.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddExternalServices(builder.Configuration);
builder.Services.AddAuth(builder.Configuration);
builder.Services.AddHttpClient();

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(
        typeof(SeismicFlow.Application.Devices.Commands.RegisterDeviceCommand).Assembly));

// Schema sync
builder.Services.AddScoped<TenantSchemaSyncService>();

// Reading repository & persister (used by MQTT background service — separate from HTTP path)
builder.Services.AddScoped<SeismicReadingRepository>();
builder.Services.AddScoped<ISeismicReadingPersister, SeismicReadingPersister>();

// In-process event bus (singleton — shared across all requests)
builder.Services.AddSingleton<IReadingEventBus, ReadingEventBus>();

// MQTT consumer background service
builder.Services.Configure<MqttConsumerOptions>(
    builder.Configuration.GetSection("Mqtt"));
builder.Services.AddHostedService<MqttConsumerService>();

// Health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("MasterDb")!,
        name: "postgres",
        tags: ["db", "ready"])
    .AddUrlGroup(
        new Uri($"{builder.Configuration["Keycloak:BaseUrl"]}/health/ready"),
        name: "keycloak",
        tags: ["keycloak", "ready"]);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ── Middleware ────────────────────────────────────────────────────────────────
app.UseMiddleware<GlobalExceptionHandler>();   // outermost

// ── 1. Database Migration ─────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
    await db.Database.MigrateAsync();
}

// ── 2. Tenant Schema Sync ─────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var sync = scope.ServiceProvider.GetRequiredService<TenantSchemaSyncService>();
    await sync.SyncAllAsync();
}

// ── 3. Keycloak Bootstrap ─────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var bootstrapper = scope.ServiceProvider.GetRequiredService<KeycloakRealmBootstrapper>();
    await bootstrapper.BootstrapAsync();
}

// ── 4. Seed ───────────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<KeycloakSeedService>();
    await seeder.SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantMiddleware>();

// ── Endpoints ─────────────────────────────────────────────────────────────────
app.MapAuthEndpoints();
app.MapTenantEndpoints();
app.MapDeviceEndpoints();
app.MapUserManagementEndpoints();
app.MapMqttAuthEndpoints();
app.MapReadingEndpoints();
app.MapChannelEndpoints();

// Health

// Health — anonymous + JSON response
app.MapHealthChecks("/health", new()
{
    ResponseWriter = HealthCheckResponseWriter.WriteAsync,
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new()
{
    Predicate = c => c.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync,
}).AllowAnonymous();

app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false,
    ResponseWriter = HealthCheckResponseWriter.WriteAsync,
}).AllowAnonymous();

app.Run();
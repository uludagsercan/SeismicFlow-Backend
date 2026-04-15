using MediatR;
using Microsoft.Extensions.Logging;
using SeismicFlow.Application.Devices.DTOs;
using SeismicFlow.Domain.Aggregates.Devices;
using SeismicFlow.Domain.Repositories;
using SeismicFlow.Domain.ValueObjects;
using SeismicFlow.Shared.Results;
using Error = SeismicFlow.Shared.Results.Error;

namespace SeismicFlow.Application.Devices.Commands;

// ── Command ───────────────────────────────────────────────────────────────────

public sealed record RegisterDeviceCommand(
    string SerialNumber,
    Guid TenantId,
    string Name,
    double Latitude,
    double Longitude,
    double Altitude,
    string NetworkCode,
    string StationCode
) : IRequest<Result<DeviceDto>>;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class RegisterDeviceCommandHandler(
    IDeviceRepository deviceRepository,
    ITenantRepository tenantRepository,
    ITenantUnitOfWork tenantUnitOfWork,
    ILogger<RegisterDeviceCommandHandler> logger)
    : IRequestHandler<RegisterDeviceCommand, Result<DeviceDto>>
{
    public async Task<Result<DeviceDto>> Handle(
        RegisterDeviceCommand cmd, CancellationToken ct)
    {
        // Step 1: Tenant must exist
        var tenant = await tenantRepository.GetByIdAsync(cmd.TenantId, ct);
        if (tenant is null)
            return Error.NotFound("Tenant", cmd.TenantId);

        if (!tenant.IsActive)
            return Error.InvalidOperation(
                $"Tenant '{tenant.Slug}' is not active. Cannot register device.");

        // Step 2: Serial number must be globally unique
        var serialNumber = DeviceId.Create(cmd.SerialNumber);
        if (await deviceRepository.ExistsBySerialNumberAsync(serialNumber, ct))
            return Error.Conflict("Device",
                $"A device with serial number '{cmd.SerialNumber}' already exists.");

        // Step 3: Create Device aggregate
        var device = Device.Create(
            serialNumber,
            cmd.TenantId,
            cmd.Name,
            new DeviceLocation(cmd.Latitude, cmd.Longitude, cmd.Altitude),
            IrisMapping.Create(cmd.NetworkCode, cmd.StationCode));

        deviceRepository.Add(device);
        await tenantUnitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Device registered. SerialNumber: {SerialNumber}, Tenant: {TenantId}",
            cmd.SerialNumber, cmd.TenantId);

        return DeviceDto.FromDomain(device);
    }
}
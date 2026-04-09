using MediatR;
using Microsoft.Extensions.Logging;
using SeismicFlow.Application.Devices.DTOs;
using SeismicFlow.Domain.Repositories;
using SeismicFlow.Shared.Results;
using Error = SeismicFlow.Shared.Results.Error;

namespace SeismicFlow.Application.Devices.Commands;

// ── Command ───────────────────────────────────────────────────────────────────

public sealed record DeactivateDeviceCommand(
    Guid DeviceId,
    Guid TenantId
) : IRequest<Result<DeviceDto>>;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class DeactivateDeviceCommandHandler(
    IDeviceRepository deviceRepository,
    ITenantUnitOfWork tenantUnitOfWork,
    ILogger<DeactivateDeviceCommandHandler> logger)
    : IRequestHandler<DeactivateDeviceCommand, Result<DeviceDto>>
{
    public async Task<Result<DeviceDto>> Handle(
        DeactivateDeviceCommand cmd, CancellationToken ct)
    {
        var device = await deviceRepository.GetByIdAsync(cmd.DeviceId, ct);
        if (device is null)
            return Error.NotFound("Device", cmd.DeviceId);

        // Tenant isolation: a tenant can only deactivate its own devices
        if (device.TenantId != cmd.TenantId)
            return Error.NotFound("Device", cmd.DeviceId);

        // Business rule enforced inside the aggregate
        try
        {
            device.Deactivate();
        }
        catch (InvalidOperationException ex)
        {
            return Error.InvalidOperation(ex.Message);
        }

        // No explicit Update() needed — EF change tracking detects property changes.
        await tenantUnitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Device deactivated. DeviceId: {DeviceId}", cmd.DeviceId);

        return DeviceDto.FromDomain(device);
    }
}
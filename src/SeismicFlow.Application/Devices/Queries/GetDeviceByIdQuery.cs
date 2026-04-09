using MediatR;
using SeismicFlow.Application.Devices.DTOs;
using SeismicFlow.Domain.Repositories;
using SeismicFlow.Shared.Results;

namespace SeismicFlow.Application.Devices.Queries
{

    public sealed record GetDeviceByIdQuery(
        Guid DeviceId,
        Guid TenantId    // tenant isolation check
    ) : IRequest<Result<DeviceDto>>;

    public sealed class GetDeviceByIdQueryHandler(IDeviceRepository deviceRepository)
        : IRequestHandler<GetDeviceByIdQuery, Result<DeviceDto>>
    {
        public async Task<Result<DeviceDto>> Handle(
            GetDeviceByIdQuery query, CancellationToken ct)
        {
            var device = await deviceRepository.GetByIdAsync(query.DeviceId, ct);

            if (device is null || device.TenantId != query.TenantId)
                return Error.NotFound("Device", query.DeviceId);

            return DeviceDto.FromDomain(device);
        }
    }
}

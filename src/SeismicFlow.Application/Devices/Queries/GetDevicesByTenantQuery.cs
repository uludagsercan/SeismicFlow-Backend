using MediatR;
using SeismicFlow.Application.Devices.DTOs;
using SeismicFlow.Domain.Repositories;
using SeismicFlow.Shared.Results;

namespace SeismicFlow.Application.Devices.Queries
{
    public sealed record GetDevicesByTenantQuery(Guid TenantId)
        : IRequest<Result<IReadOnlyList<DeviceDto>>>;

    public sealed class GetDevicesByTenantQueryHandler(IDeviceRepository deviceRepository)
        : IRequestHandler<GetDevicesByTenantQuery, Result<IReadOnlyList<DeviceDto>>>
    {
        public async Task<Result<IReadOnlyList<DeviceDto>>> Handle(
            GetDevicesByTenantQuery query, CancellationToken ct)
        {
            var devices = await deviceRepository.GetByTenantIdAsync(query.TenantId, ct);
            IReadOnlyList<DeviceDto> dtos = devices.Select(DeviceDto.FromDomain).ToList();
            return Result<IReadOnlyList<DeviceDto>>.Success(dtos);
        }
    }
}

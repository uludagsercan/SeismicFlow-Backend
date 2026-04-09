using MediatR;
using SeismicFlow.Application.Tenants.DTOs;
using SeismicFlow.Domain.Repositories;
using SeismicFlow.Shared.Results;

namespace SeismicFlow.Application.Tenants.Queries
{

    public sealed record GetTenantByIdQuery(Guid TenantId) : IRequest<Result<TenantDto>>;

    public sealed class GetTenantByIdQueryHandler(ITenantRepository tenantRepository)
        : IRequestHandler<GetTenantByIdQuery, Result<TenantDto>>
    {
        public async Task<Result<TenantDto>> Handle(
            GetTenantByIdQuery query, CancellationToken ct)
        {
            var tenant = await tenantRepository.GetByIdAsync(query.TenantId, ct);

            return tenant is null
                ? Error.NotFound("Tenant", query.TenantId)
                : TenantDto.FromDomain(tenant);
        }
    }
}

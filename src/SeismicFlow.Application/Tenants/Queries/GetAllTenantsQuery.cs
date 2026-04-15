using MediatR;
using SeismicFlow.Application.Tenants.DTOs;
using SeismicFlow.Domain.Repositories;
using SeismicFlow.Shared.Results;

namespace SeismicFlow.Application.Tenants.Queries;

public sealed record GetAllTenantsQuery() : IRequest<Result<IReadOnlyList<TenantDto>>>;

public sealed class GetAllTenantsQueryHandler(ITenantRepository tenantRepository)
    : IRequestHandler<GetAllTenantsQuery, Result<IReadOnlyList<TenantDto>>>
{
    public async Task<Result<IReadOnlyList<TenantDto>>> Handle(
        GetAllTenantsQuery query, CancellationToken ct)
    {
        var tenants = await tenantRepository.GetAllAsync(ct);
        IReadOnlyList<TenantDto> dtos = tenants.Select(TenantDto.FromDomain).ToList();
        return Result<IReadOnlyList<TenantDto>>.Success(dtos);
    }
}
using SeismicFlow.Domain.ValueObjects;
using SeismicFlow.Shared.Results;

namespace SeismicFlow.Application.Common.Interfaces
{

    public interface IKeycloakTenantService
    {
        Task<Result<(string GroupId, string GroupPath)>> CreateTenantGroupAsync(
        TenantSlug slug,
        CancellationToken ct = default);
    }
}

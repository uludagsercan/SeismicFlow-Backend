using SeismicFlow.Application.Common.DTOs;
using SeismicFlow.Shared.Results;

namespace SeismicFlow.Application.Common.Interfaces;

public interface IKeycloakUserService
{
    Task<Result<string>> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<Result> AssignRoleAsync(string userId, string roleName, CancellationToken ct = default);
    Task<Result> UpdateUserTenantAsync(string userId, Guid tenantId, CancellationToken ct = default);
    Task<Result> DeleteUserAsync(string userId, CancellationToken ct = default);
    Task<Result<List<KeycloakUserDto>>> GetTenantUsersAsync(Guid tenantId, CancellationToken ct = default);
}
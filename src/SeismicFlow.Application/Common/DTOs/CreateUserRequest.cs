namespace SeismicFlow.Application.Common.DTOs;

public sealed record CreateUserRequest(
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string Password,
    string Role,
    Guid TenantId);

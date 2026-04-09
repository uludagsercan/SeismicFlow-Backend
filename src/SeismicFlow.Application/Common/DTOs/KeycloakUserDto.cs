namespace SeismicFlow.Application.Common.DTOs;

public sealed record KeycloakUserDto(
    string Id,
    string Username,
    string? Email,
    string? FirstName,
    string? LastName);
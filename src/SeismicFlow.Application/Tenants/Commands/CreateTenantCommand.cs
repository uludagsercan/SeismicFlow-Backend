using MediatR;
using Microsoft.Extensions.Logging;
using SeismicFlow.Application.Common.Interfaces;
using SeismicFlow.Application.Tenants.DTOs;
using SeismicFlow.Domain.Aggregates.Tenants;
using SeismicFlow.Domain.Repositories;
using SeismicFlow.Domain.ValueObjects;
using SeismicFlow.Shared.Results;

namespace SeismicFlow.Application.Tenants.Commands
{
    // ── Command ───────────────────────────────────────────────────────────────────

    public sealed record CreateTenantCommand(
     string Slug,
     string DisplayName,
     string DbHost,
     int DbPort = 5432
 ) : IRequest<Result<TenantDto>>;

    public sealed class CreateTenantCommandHandler(
        ITenantRepository tenantRepository,
        IUnitOfWork unitOfWork,
        ITenantDatabaseProvisioner dbProvisioner,
        IKeycloakTenantService keycloakService,
        ILogger<CreateTenantCommandHandler> logger)
        : IRequestHandler<CreateTenantCommand, Result<TenantDto>>
    {
        public async Task<Result<TenantDto>> Handle(
            CreateTenantCommand cmd, CancellationToken ct)
        {
            // Step 1: Validate slug
            TenantSlug slug;
            try
            {
                slug = TenantSlug.Create(cmd.Slug);
            }
            catch (ArgumentException ex)
            {
                return Error.Validation(ex.Message);
            }

            if (await tenantRepository.ExistsBySlugAsync(slug, ct))
                return Error.Conflict("Tenant", $"Slug '{slug}' already exists.");

            // Step 2: Create tenant (status = Provisioning)
            var database = TenantDatabase.FromSlug(slug.Value, cmd.DbHost, cmd.DbPort);
            var tenant = Tenant.Create(slug, cmd.DisplayName, database);

            tenantRepository.Add(tenant);
            await unitOfWork.SaveChangesAsync(ct);

            logger.LogInformation("Tenant created. Id: {TenantId}, Slug: {Slug}", tenant.Id, slug);

            // Step 3: Provision database
            var dbResult = await dbProvisioner.ProvisionAsync(tenant, ct);
            if (dbResult.IsFailure)
            {
                logger.LogError("DB provisioning failed for {Slug}: {Error}", slug, dbResult.Error!.Message);
                return dbResult.Error!;
            }

            // Step 4: Create Keycloak group
            var keycloakResult = await keycloakService.CreateTenantGroupAsync(slug, ct);
            if (keycloakResult.IsFailure)
            {
                logger.LogError("Keycloak failed for {Slug}: {Error}", slug, keycloakResult.Error!.Message);
                return keycloakResult.Error!;
            }

            // Step 5: Activate tenant
            tenant.Activate(keycloakResult.Value.GroupId, keycloakResult.Value.GroupPath);
            tenantRepository.Update(tenant);
            await unitOfWork.SaveChangesAsync(ct);

            logger.LogInformation("Tenant {Slug} is now ACTIVE.", slug);

            return TenantDto.FromDomain(tenant);
        }
    }
}

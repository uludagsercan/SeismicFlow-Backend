namespace SeismicFlow.Domain.Enums
{

    /// <summary>
    /// Lifecycle states of a Tenant.
    /// Valid transitions:
    ///   Provisioning -> Active
    ///   Active -> Suspended
    ///   Suspended -> Active
    ///   Any -> Deleted (soft delete)
    /// </summary>
    public enum TenantStatus
    {
        Provisioning = 0,
        Active = 1,
        Suspended = 2,
        Deleted = 3
    }
}

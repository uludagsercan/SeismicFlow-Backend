namespace SeismicFlow.Shared.Results
{
    /// <summary>
    /// Represents a structured error with a machine-readable code and human-readable message.
    ///
    /// Code convention:
    ///   "Tenant.NotFound"      → 404
    ///   "Tenant.Conflict"      → 409
    ///   "Validation.Failed"    → 400
    ///   "InvalidOperation"     → 400
    ///   "ExternalService.*.Failed" → 502
    /// </summary>
    public sealed record Error(string Code, string Message)
    {
        public static readonly Error None = new(string.Empty, string.Empty);

        /// <summary>Input validation failed.</summary>
        public static Error Validation(string message) =>
            new("Validation.Failed", message);

        /// <summary>A required resource does not exist.</summary>
        public static Error NotFound(string resourceName, object id) =>
            new($"{resourceName}.NotFound",
                $"{resourceName} with id '{id}' was not found.");

        /// <summary>A resource with the same unique key already exists.</summary>
        public static Error Conflict(string resourceName, string detail) =>
            new($"{resourceName}.Conflict", detail);

        /// <summary>An external service call failed (Keycloak, DB provisioner, etc.)</summary>
        public static Error ExternalService(string serviceName, string detail) =>
            new($"ExternalService.{serviceName}.Failed", detail);

        /// <summary>Operation is not allowed in the current state.</summary>
        public static Error InvalidOperation(string message) =>
            new("InvalidOperation", message);
    }







}

namespace SeismicFlow.Domain.ValueObjects
{
    /// <summary>
    /// URL-safe unique identifier for a Tenant.
    /// Rules:
    ///   - 3 to 64 characters
    ///   - Lowercase letters, digits, and hyphens only
    ///   - Cannot start or end with a hyphen
    /// </summary>
    public sealed record TenantSlug
    {
        public string Value { get; }

        private TenantSlug(string value) => Value = value;

        public static TenantSlug Create(string raw)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(raw);

            var slug = raw.Trim().ToLowerInvariant();

            if (slug.Length < 3 || slug.Length > 64)
                throw new ArgumentException($"Tenant slug must be 3-64 characters. Got: '{slug}'.");

            if (!System.Text.RegularExpressions.Regex.IsMatch(slug, @"^[a-z0-9]+(-[a-z0-9]+)*$"))
                throw new ArgumentException(
                    $"Tenant slug '{slug}' is invalid. Use lowercase letters, digits, and hyphens only.");

            return new TenantSlug(slug);
        }

        public override string ToString() => Value;

        public static implicit operator string(TenantSlug slug) => slug.Value;
    }
}

namespace SeismicFlow.Domain.ValueObjects
{
    public sealed record TenantDatabase
    {
        public string Host { get; }
        public int Port { get; }
        public string DbName { get; }
        public string DbUser { get; }

        private TenantDatabase(string host, int port, string dbName, string dbUser)
        {
            Host = host;
            Port = port;
            DbName = dbName;
            DbUser = dbUser;
        }

        public static TenantDatabase FromSlug(string slug, string host, int port)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(slug);
            ArgumentException.ThrowIfNullOrWhiteSpace(host);

            if (port is < 1 or > 65535)
                throw new ArgumentOutOfRangeException(nameof(port));

            var underscored = slug.Replace("-", "_");

            return new TenantDatabase(
                host: host,
                port: port,
                dbName: $"sf_tenant_{underscored}",
                dbUser: $"sf_{underscored}");
        }

        public override string ToString() => $"{Host}:{Port}/{DbName}";
    }
}

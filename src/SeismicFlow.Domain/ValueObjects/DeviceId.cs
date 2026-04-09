namespace SeismicFlow.Domain.ValueObjects
{
    public sealed record DeviceId
    {
        public string Value { get; }

        private DeviceId(string value) => Value = value;

        public static DeviceId Create(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("DeviceId cannot be empty.");

            return new DeviceId(value.Trim());
        }
    }
}

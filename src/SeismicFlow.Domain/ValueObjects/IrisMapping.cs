namespace SeismicFlow.Domain.ValueObjects
{
    /// <summary>
    /// Maps a Device to its corresponding IRIS/FDSN station for simulation.
    /// Example: NetworkCode = "IU", StationCode = "KONO"
    /// </summary>
    public sealed record IrisMapping
    {
        public string NetworkCode { get; }
        public string StationCode { get; }

        private IrisMapping(string networkCode, string stationCode)
        {
            NetworkCode = networkCode;
            StationCode = stationCode;
        }

        public static IrisMapping Create(string networkCode, string stationCode)
        {
            if (string.IsNullOrWhiteSpace(networkCode))
                throw new ArgumentException("NetworkCode cannot be empty.");

            if (string.IsNullOrWhiteSpace(stationCode))
                throw new ArgumentException("StationCode cannot be empty.");

            return new IrisMapping(
                networkCode.Trim().ToUpperInvariant(),
                stationCode.Trim().ToUpperInvariant());
        }

        public override string ToString() => $"{NetworkCode}.{StationCode}";
    }
}

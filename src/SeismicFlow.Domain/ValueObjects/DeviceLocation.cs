namespace SeismicFlow.Domain.ValueObjects
{
    /// <summary>
    /// Physical location where the device is permanently mounted.
    /// If location changes, the entire object is replaced.
    /// </summary>
    public sealed record DeviceLocation(
        double Latitude,
        double Longitude,
        double Altitude);
}

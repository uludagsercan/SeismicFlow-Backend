namespace SeismicFlow.Domain.Enums
{
    /// <summary>
    /// Lifecycle states of a Device.
    /// A device can never be deleted — only deactivated.
    /// </summary>
    public enum DeviceStatus
    {
        Active = 0,
        Inactive = 1
    }
}

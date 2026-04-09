namespace SeismicFlow.Application.Common.Interfaces;

public interface IMqttCredentialService
{
    /// <summary>
    /// Validates device MQTT credentials (username = deviceId, password = mqtt secret).
    /// </summary>
    Task<bool> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default);

    /// <summary>
    /// Checks if the device is allowed to publish/subscribe to the given topic.
    /// Topic format: tenant/{tenantId}/devices/{deviceId}/data
    /// </summary>
    Task<bool> CheckAclAsync(string username, string topic, string access, CancellationToken ct = default);
}
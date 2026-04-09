using SeismicFlow.Domain.Common;
using SeismicFlow.Domain.Entities;
using SeismicFlow.Domain.Enums;
using SeismicFlow.Domain.Events;
using SeismicFlow.Domain.ValueObjects;

namespace SeismicFlow.Domain.Aggregates.Devices;

/// <summary>
/// Represents a physical seismometer device owned by a tenant.
/// Business rules:
///   - Channels are auto-discovered when data arrives; not pre-defined.
///   - A Device cannot be deleted, only deactivated.
/// </summary>
public sealed class Device : AggregateRoot
{
    public DeviceId SerialNumber { get; private set; } = default!;
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public DeviceLocation Location { get; private set; } = default!;
    public IrisMapping IrisMapping { get; private set; } = default!;
    public DeviceStatus Status { get; private set; }
    public string MqttPassword { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<Channel> _channels = [];
    public IReadOnlyList<Channel> Channels => _channels.AsReadOnly();

    private Device() { }

    public static Device Create(
        DeviceId serialNumber,
        Guid tenantId,
        string name,
        DeviceLocation location,
        IrisMapping irisMapping)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var device = new Device
        {
            Id = Guid.NewGuid(),
            SerialNumber = serialNumber,
            TenantId = tenantId,
            Name = name,
            Location = location,
            IrisMapping = irisMapping,
            Status = DeviceStatus.Active,
            MqttPassword = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        // No channels pre-created — they are auto-discovered on first data arrival.

        device.RaiseDomainEvent(
            new DeviceCreatedEvent(device.Id, serialNumber.Value, tenantId));

        return device;
    }

    /// <summary>
    /// Called by the MQTT consumer when a reading arrives.
    /// Creates the channel on first sight; updates lastSeenAt on subsequent calls.
    /// </summary>
    public void DiscoverChannel(string code, double sampleRate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var existing = _channels.FirstOrDefault(c =>
            string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
            _channels.Add(Channel.Create(code, sampleRate));
        else
            existing.UpdateLastSeen(sampleRate);

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        if (Status == DeviceStatus.Inactive)
            throw new InvalidOperationException(
                $"Device '{SerialNumber}' is already inactive.");

        Status = DeviceStatus.Inactive;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new DeviceDeactivatedEvent(Id, SerialNumber.Value));
    }

    public bool IsActive => Status == DeviceStatus.Active;
}
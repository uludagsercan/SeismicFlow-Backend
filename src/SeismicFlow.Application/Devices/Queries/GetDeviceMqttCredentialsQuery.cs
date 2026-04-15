using MediatR;
using SeismicFlow.Domain.Repositories;
using SeismicFlow.Shared.Results;

namespace SeismicFlow.Application.Devices.Queries;

public sealed record GetDeviceMqttCredentialsQuery(
    Guid DeviceId,
    Guid TenantId
) : IRequest<Result<DeviceMqttCredentialsDto>>;

public sealed record DeviceMqttCredentialsDto(
    string MQTT_DEVICE_ID,
    string MQTT_DEVICE_PASSWORD,
    string MQTT_TENANT_ID);

public sealed class GetDeviceMqttCredentialsQueryHandler(IDeviceRepository deviceRepository)
    : IRequestHandler<GetDeviceMqttCredentialsQuery, Result<DeviceMqttCredentialsDto>>
{
    public async Task<Result<DeviceMqttCredentialsDto>> Handle(
        GetDeviceMqttCredentialsQuery query, CancellationToken ct)
    {
        var device = await deviceRepository.GetByIdAsync(query.DeviceId, ct);

        if (device is null || device.TenantId != query.TenantId)
            return Error.NotFound("Device", query.DeviceId);

        return new DeviceMqttCredentialsDto(
            MQTT_DEVICE_ID:       device.Id.ToString(),
            MQTT_DEVICE_PASSWORD: device.MqttPassword,
            MQTT_TENANT_ID:       device.TenantId.ToString());
    }
}

using SeismicFlow.Domain.Aggregates.Devices;
using SeismicFlow.Domain.Enums;

namespace SeismicFlow.Application.Devices.DTOs
{
    public sealed record DeviceDto(
     Guid Id,
     string SerialNumber,
     Guid TenantId,
     string Name,
     double Latitude,
     double Longitude,
     double Altitude,
     string NetworkCode,
     string StationCode,
     DeviceStatus Status,
     IReadOnlyList<ChannelDto> Channels,
     DateTimeOffset CreatedAt)
    {
        public static DeviceDto FromDomain(Device device) => new(
            Id: device.Id,
            SerialNumber: device.SerialNumber.Value,
            TenantId: device.TenantId,
            Name: device.Name,
            Latitude: device.Location.Latitude,
            Longitude: device.Location.Longitude,
            Altitude: device.Location.Altitude,
            NetworkCode: device.IrisMapping.NetworkCode,
            StationCode: device.IrisMapping.StationCode,
            Status: device.Status,
            Channels: device.Channels
                              .Select(c => new ChannelDto(
                                  c.Id,
                                  c.Code,
                                  c.SampleRate,
                                  c.FirstSeenAt,
                                  c.LastSeenAt))
                              .ToList()
                              .AsReadOnly(),
            CreatedAt: device.CreatedAt);
    }
}

using SeismicFlow.Domain.Enums;

namespace SeismicFlow.Application.Devices.DTOs
{

    public sealed record ChannelDto(
        Guid Id,
        string Code,        
        double SampleRate,
        DateTimeOffset FirstSeenAt,
        DateTimeOffset LastSeenAt);
}

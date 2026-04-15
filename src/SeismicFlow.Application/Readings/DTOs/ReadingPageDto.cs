namespace SeismicFlow.Application.Readings.DTOs;

public sealed record ReadingPageDto(
    IReadOnlyList<SeismicReadingDto> Items,
    int Total
);
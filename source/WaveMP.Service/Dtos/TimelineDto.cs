namespace WaveMP.Service.Dtos;

/// <summary>
/// The plays over a period, without gaps: per day for up to 45 days, per week for up to 190 days, per month for up to
/// 2200 days, otherwise per year.
/// </summary>
public sealed class TimelineDto
{
    public required TimelineGranularityDto Granularity { get; init; }

    /// <summary>
    /// The steps in their order.
    /// </summary>
    public required IReadOnlyList<TimelineBucketDto> Buckets { get; init; }
}

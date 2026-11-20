namespace WaveMP.Service.Dtos;

/// <summary>
/// A recap a profile shared with the other profiles of the instance.
/// </summary>
public sealed class RecapShareDto
{
    public required Guid Id { get; init; }

    /// <summary>
    /// The profile that shared the recap; its plays are counted.
    /// </summary>
    public required ProfileReferenceDto Owner { get; init; }

    /// <summary>
    /// Whether the current profile shared the recap.
    /// </summary>
    public required bool IsOwn { get; init; }

    public required RecapPeriodDto Period { get; init; }

    /// <summary>
    /// First day of the period; null from the first play on.
    /// </summary>
    public DateOnly? From { get; init; }

    /// <summary>
    /// Last day of the period.
    /// </summary>
    public required DateOnly To { get; init; }

    /// <summary>
    /// IANA time zone of the days.
    /// </summary>
    public required string TimeZone { get; init; }

    /// <summary>
    /// Id of the design of the clients the recap is shown in, e.g. aurora.
    /// </summary>
    public required string Design { get; init; }

    /// <summary>
    /// When the recap was shared first.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// When it was shared last (with its current design).
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}

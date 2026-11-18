namespace WaveMP.Service.Dtos;

/// <summary>
/// The scrobbling of the current profile: every supported service with its state, Last.fm first.
/// </summary>
public sealed class ScrobblingDto
{
    public required IReadOnlyList<ScrobblingServiceDto> Services { get; init; }
}

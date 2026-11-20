namespace WaveMP.Service.Dtos;

/// <summary>
/// An artist the profile played for the first time in a period.
/// </summary>
public sealed class NewArtistDto
{
    public required ArtistReferenceDto Artist { get; init; }

    /// <summary>
    /// The first play of a track of the artist.
    /// </summary>
    public required DateTimeOffset FirstPlayedAt { get; init; }

    /// <summary>
    /// Number of plays in the period.
    /// </summary>
    public required int Plays { get; init; }

    /// <summary>
    /// Listening time in the period in milliseconds.
    /// </summary>
    public required long ListeningMilliseconds { get; init; }

    /// <summary>
    /// The most played track of the artist with a cover (GetTrackCover); null if none has one.
    /// </summary>
    public Guid? CoverTrackId { get; init; }
}

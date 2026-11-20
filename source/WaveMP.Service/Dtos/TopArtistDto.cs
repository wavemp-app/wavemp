namespace WaveMP.Service.Dtos;

/// <summary>
/// An artist among the most played of a period.
/// </summary>
public sealed class TopArtistDto
{
    public required ArtistReferenceDto Artist { get; init; }

    /// <summary>
    /// Number of plays in the period.
    /// </summary>
    public required int Plays { get; init; }

    /// <summary>
    /// Listening time in the period in milliseconds.
    /// </summary>
    public required long ListeningMilliseconds { get; init; }

    /// <summary>
    /// Number of different tracks played.
    /// </summary>
    public required int Tracks { get; init; }

    /// <summary>
    /// The most played track of the artist with a cover (GetTrackCover); null if none has one.
    /// </summary>
    public Guid? CoverTrackId { get; init; }
}

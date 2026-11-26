namespace WaveMP.Core.Metadata.Tags;

/// <summary>
/// The tags of a media file. Texts are normalized; missing values and zero numbers are <c>null</c>.
/// </summary>
public sealed record MediaTags
{
    public static readonly MediaTags Empty = new();

    public string? Title { get; init; }

    public string? TitleSort { get; init; }

    public string? Artist { get; init; }

    public string? Artists { get; init; }

    public string? ArtistSort { get; init; }

    public string? AlbumArtist { get; init; }

    public string? AlbumArtistSort { get; init; }

    public string? Album { get; init; }

    public string? AlbumSort { get; init; }

    public int? TrackNumber { get; init; }

    public int? TrackCount { get; init; }

    public int? DiscNumber { get; init; }

    public int? DiscCount { get; init; }

    public int? Year { get; init; }

    public string? Genre { get; init; }

    public string? Genres { get; init; }

    public string? Composer { get; init; }

    public double? ReplayGainTrackGain { get; init; }

    public double? ReplayGainTrackPeak { get; init; }

    public double? ReplayGainAlbumGain { get; init; }

    public double? ReplayGainAlbumPeak { get; init; }

    public string? MusicBrainzTrackId { get; init; }

    public string? MusicBrainzReleaseId { get; init; }

    /// <summary>
    /// The MusicBrainz artist of the artist tag, only if the tag names exactly one (see
    /// <see cref="Texts.MusicBrainzIds"/>).
    /// </summary>
    public string? MusicBrainzArtistId { get; init; }

    /// <summary>
    /// The MusicBrainz artist of the album artist tag, only if the tag names exactly one.
    /// </summary>
    public string? MusicBrainzAlbumArtistId { get; init; }

    /// <summary>
    /// Whether the file is marked as part of a compilation: ID3 <c>TCMP</c>, MP4 <c>cpil</c> or the Vorbis comment
    /// <c>COMPILATION</c>.
    /// </summary>
    public bool IsCompilation { get; init; }
}

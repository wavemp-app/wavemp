namespace WaveMP.Core.Metadata.Tags;

/// <summary>
/// The tags of a media file. Texts are normalized; missing values and zero numbers are <c>null</c>.
/// </summary>
public sealed record MediaTags
{
    public static readonly MediaTags Empty = new();

    public string? Title { get; init; }

    public string? TitleSort { get; init; }

    /// <summary>
    /// The text of the main artist field: its value, or its values joined by <c>"; "</c> where it holds several (see
    /// <see cref="ArtistValues"/>).
    /// </summary>
    public string? Artist { get; init; }

    public string? Artists { get; init; }

    /// <summary>
    /// The artists as separate values: the field ARTISTS if the file has one (also with a single value, which confirms
    /// that the text is one artist), otherwise the values of the main field if it holds several; empty if the tags
    /// hold the artist as one text only.
    /// </summary>
    public IReadOnlyList<string> ArtistValues { get; init; } = [];

    public string? ArtistSort { get; init; }

    /// <summary>
    /// The text of the main album artist field, like <see cref="Artist"/>.
    /// </summary>
    public string? AlbumArtist { get; init; }

    /// <summary>
    /// The album artists as separate values, like <see cref="ArtistValues"/> with the field ALBUMARTISTS.
    /// </summary>
    public IReadOnlyList<string> AlbumArtistValues { get; init; } = [];

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
    /// All MusicBrainz artists of the artist tag in their order (the artist credit of a song with several artists).
    /// </summary>
    public IReadOnlyList<string> MusicBrainzArtistIds { get; init; } = [];

    /// <summary>
    /// All MusicBrainz artists of the album artist tag in their order.
    /// </summary>
    public IReadOnlyList<string> MusicBrainzAlbumArtistIds { get; init; } = [];

    /// <summary>
    /// Whether the file is marked as part of a compilation: ID3 <c>TCMP</c>, MP4 <c>cpil</c> or the Vorbis comment
    /// <c>COMPILATION</c>.
    /// </summary>
    public bool IsCompilation { get; init; }
}

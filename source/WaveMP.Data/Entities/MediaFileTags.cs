namespace WaveMP.Data.Entities;

/// <summary>
/// The tags of a media file as read, stored with the file (complex type, columns <c>tags_*</c>). Kept for later
/// steps such as sorting, ReplayGain and the metadata editor, so unchanged files never have to be read again.
/// </summary>
public class MediaFileTags
{
    public string? Title { get; set; }

    public string? TitleSort { get; set; }

    /// <summary>
    /// First performer.
    /// </summary>
    public string? Artist { get; set; }

    /// <summary>
    /// All performers, joined.
    /// </summary>
    public string? Artists { get; set; }

    /// <summary>
    /// The artists of the file as separate values, where the tags hold several (e.g. several values of the ID3v2.4
    /// frame TPE1 or the field ARTISTS); empty if the tags hold the artist as one text only.
    /// </summary>
    public List<string> ArtistValues { get; set; } = [];

    public string? ArtistSort { get; set; }

    public string? AlbumArtist { get; set; }

    /// <summary>
    /// The album artists of the file as separate values, where the tags hold several (e.g. several values of the
    /// ID3v2.4 frame TPE2 or the field ALBUMARTISTS); empty if the tags hold the album artist as one text only.
    /// </summary>
    public List<string> AlbumArtistValues { get; set; } = [];

    public string? AlbumArtistSort { get; set; }

    public string? Album { get; set; }

    public string? AlbumSort { get; set; }

    public int? TrackNumber { get; set; }

    public int? TrackCount { get; set; }

    public int? DiscNumber { get; set; }

    public int? DiscCount { get; set; }

    public int? Year { get; set; }

    /// <summary>
    /// First genre.
    /// </summary>
    public string? Genre { get; set; }

    /// <summary>
    /// All genres, joined.
    /// </summary>
    public string? Genres { get; set; }

    public string? Composer { get; set; }

    public double? ReplayGainTrackGain { get; set; }

    public double? ReplayGainTrackPeak { get; set; }

    public double? ReplayGainAlbumGain { get; set; }

    public double? ReplayGainAlbumPeak { get; set; }

    public string? MusicBrainzTrackId { get; set; }

    public string? MusicBrainzReleaseId { get; set; }

    /// <summary>
    /// The MusicBrainz artist of the artist tag, only if the tag names exactly one.
    /// </summary>
    public string? MusicBrainzArtistId { get; set; }

    /// <summary>
    /// The MusicBrainz artist of the album artist tag, only if the tag names exactly one.
    /// </summary>
    public string? MusicBrainzAlbumArtistId { get; set; }

    /// <summary>
    /// The MusicBrainz artists of the artist tag in their order, where it names several (the artist credit).
    /// </summary>
    public List<string> MusicBrainzArtistIds { get; set; } = [];

    /// <summary>
    /// The MusicBrainz artists of the album artist tag in their order, where it names several.
    /// </summary>
    public List<string> MusicBrainzAlbumArtistIds { get; set; } = [];

    /// <summary>
    /// Whether the file is marked as part of a compilation (ID3 <c>TCMP</c>, MP4 <c>cpil</c>, Vorbis comment
    /// <c>COMPILATION</c>).
    /// </summary>
    public bool IsCompilation { get; set; }
}

using WaveMP.Core.Metadata.Artists;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Metadata.Resolutions;

/// <summary>
/// The metadata of a track, field by field from the tags, the folder and file name or the file name itself.
/// </summary>
/// <param name="Title">Title of the track.</param>
/// <param name="TitleSource">Where the title comes from.</param>
/// <param name="Artist">Artist of the track as one text (the main field of the tags).</param>
/// <param name="AlbumArtist">Album artist as one text; only from the tags if <paramref name="IsCompilation"/> is
/// set.</param>
/// <param name="Album">Title of the album.</param>
/// <param name="TrackNumber">Number of the track.</param>
/// <param name="DiscNumber">Number of the disc.</param>
/// <param name="Year">Year of the track.</param>
/// <param name="Genre">Genre of the track.</param>
/// <param name="IsCompilation">Whether the tags mark the track as part of a compilation, which changes how its album
/// is formed (see <see cref="AlbumResolver"/>).</param>
/// <param name="MusicBrainzArtistId">The MusicBrainz artist of <paramref name="Artist"/>, if its tag names exactly
/// one.</param>
/// <param name="MusicBrainzAlbumArtistId">The MusicBrainz artist of <paramref name="AlbumArtist"/>, if its tag names
/// exactly one.</param>
/// <param name="MusicBrainzReleaseId">The MusicBrainz release of <paramref name="Album"/>.</param>
/// <param name="Artists">The artists of the track in their order, with their roles (see <see cref="ArtistNames"/>);
/// <c>null</c> takes them from <paramref name="Artist"/>.</param>
/// <param name="AlbumArtists">The album artists in their order, all main artists; <c>null</c> takes them from
/// <paramref name="AlbumArtist"/>.</param>
public sealed record ResolvedTrackMetadata(
    string Title,
    TrackMetadataSource TitleSource,
    string? Artist,
    string? AlbumArtist,
    string? Album,
    int? TrackNumber,
    int? DiscNumber,
    int? Year,
    string? Genre,
    bool IsCompilation = false,
    Guid? MusicBrainzArtistId = null,
    Guid? MusicBrainzAlbumArtistId = null,
    Guid? MusicBrainzReleaseId = null,
    IReadOnlyList<ArtistName>? Artists = null,
    IReadOnlyList<ArtistName>? AlbumArtists = null)
{
    /// <summary>
    /// The artists of the track in their order, with their roles.
    /// </summary>
    public IReadOnlyList<ArtistName> Artists { get; init; } = Artists
        ?? WithId(ArtistNames.Split(Artist), MusicBrainzArtistId);

    /// <summary>
    /// The album artists in their order, all main artists.
    /// </summary>
    public IReadOnlyList<ArtistName> AlbumArtists { get; init; } = AlbumArtists
        ?? WithId(
            [.. ArtistNames.Split(AlbumArtist).Select(artist => artist with { Role = ArtistRole.Main })],
            MusicBrainzAlbumArtistId);

    /// <summary>
    /// Equal when all values are equal, the lists of artists by their content.
    /// </summary>
    public bool Equals(ResolvedTrackMetadata? other)
    {
        return other is not null
            && Title == other.Title
            && TitleSource == other.TitleSource
            && Artist == other.Artist
            && AlbumArtist == other.AlbumArtist
            && Album == other.Album
            && TrackNumber == other.TrackNumber
            && DiscNumber == other.DiscNumber
            && Year == other.Year
            && Genre == other.Genre
            && IsCompilation == other.IsCompilation
            && MusicBrainzArtistId == other.MusicBrainzArtistId
            && MusicBrainzAlbumArtistId == other.MusicBrainzAlbumArtistId
            && MusicBrainzReleaseId == other.MusicBrainzReleaseId
            && Artists.SequenceEqual(other.Artists)
            && AlbumArtists.SequenceEqual(other.AlbumArtists);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Title, Artist, AlbumArtist, Album, TrackNumber, DiscNumber, Year, Artists.Count);
    }

    private static IReadOnlyList<ArtistName> WithId(IReadOnlyList<ArtistName> artists, Guid? musicBrainzArtistId)
    {
        return artists is [ArtistName single] && musicBrainzArtistId is { } id
            ? [single with { MusicBrainzArtistId = id }]
            : artists;
    }
}

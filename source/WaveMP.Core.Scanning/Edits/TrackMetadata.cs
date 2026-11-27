using WaveMP.Core.Metadata.Artists;

namespace WaveMP.Core.Scanning.Edits;

/// <summary>
/// The metadata of a track as the metadata editor shows it: the values of the tags of its audio file; values the tags
/// do not contain come from the library (e.g. derived from the folder and file name).
/// </summary>
/// <param name="TrackId">Id of the track.</param>
/// <param name="Status">Whether the editor can write the tags.</param>
/// <param name="AudioFilePath">The audio file the editor writes, relative to the media folder; <c>null</c> without
/// audio file.</param>
/// <param name="Title">Title.</param>
/// <param name="Artist">Artist.</param>
/// <param name="Album">Album.</param>
/// <param name="AlbumArtist">Album artist.</param>
/// <param name="Year">Year.</param>
/// <param name="TrackNumber">Number of the track on its disc.</param>
/// <param name="DiscNumber">Number of the disc.</param>
/// <param name="Genre">Genre.</param>
/// <param name="Artists">All artists in their order with their roles: the separate values of the tags, otherwise the
/// artist field split at the unambiguous separators (see <see cref="ArtistNames.Resolve"/>).</param>
/// <param name="AlbumArtists">All album artists in their order, read like the artists.</param>
/// <param name="HasArtistValues">Whether the tags hold the artists as separate values, so the list is confirmed.
/// </param>
public sealed record TrackMetadata(
    Guid TrackId,
    TrackMetadataStatus Status,
    string? AudioFilePath,
    string Title,
    string? Artist,
    string? Album,
    string? AlbumArtist,
    int? Year,
    int? TrackNumber,
    int? DiscNumber,
    string? Genre,
    IReadOnlyList<ArtistName> Artists,
    IReadOnlyList<string> AlbumArtists,
    bool HasArtistValues = false)
{
    public bool Equals(TrackMetadata? other)
    {
        return other is not null
            && TrackId == other.TrackId
            && Status == other.Status
            && AudioFilePath == other.AudioFilePath
            && Title == other.Title
            && Artist == other.Artist
            && Album == other.Album
            && AlbumArtist == other.AlbumArtist
            && Year == other.Year
            && TrackNumber == other.TrackNumber
            && DiscNumber == other.DiscNumber
            && Genre == other.Genre
            && Artists.SequenceEqual(other.Artists)
            && AlbumArtists.SequenceEqual(other.AlbumArtists)
            && HasArtistValues == other.HasArtistValues;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(TrackId, Title, Artist, Album, AlbumArtist, Year, Artists.Count, AlbumArtists.Count);
    }
}

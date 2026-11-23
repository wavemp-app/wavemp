using WaveMP.Core.Metadata.Readers;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Scanning.Moves;

/// <summary>
/// Confirms a move of <see cref="TrackMovePlanner"/> once the new file was read: it must be the same recording with
/// the same tags as the file the vanished track played. Kind, size and extension are equal already.
/// </summary>
public static class TrackMoveEvidence
{
    /// <summary>
    /// Whether the new file and the stored file of the vanished track were both read without a problem, have a known,
    /// equal duration (at the precision of the database, a microsecond) and equal core tags: title, artist, album
    /// artist, album, track number, disc number and year (texts compared exactly).
    /// </summary>
    /// <param name="stored">The stored file the vanished track played.</param>
    /// <param name="read">What the scan read from the new file.</param>
    public static bool Matches(MediaFile stored, MediaFileMetadata read)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(read);
        MediaFileTags old = stored.Tags;
        return stored.Issue is null
            && read.Issue is null
            && stored.Duration is { } storedDuration
            && read.Duration is { } readDuration
            && Math.Abs((storedDuration - readDuration).Ticks) < TimeSpan.TicksPerMicrosecond
            && string.Equals(old.Title, read.Tags.Title, StringComparison.Ordinal)
            && string.Equals(old.Artist, read.Tags.Artist, StringComparison.Ordinal)
            && string.Equals(old.AlbumArtist, read.Tags.AlbumArtist, StringComparison.Ordinal)
            && string.Equals(old.Album, read.Tags.Album, StringComparison.Ordinal)
            && old.TrackNumber == read.Tags.TrackNumber
            && old.DiscNumber == read.Tags.DiscNumber
            && old.Year == read.Tags.Year;
    }
}

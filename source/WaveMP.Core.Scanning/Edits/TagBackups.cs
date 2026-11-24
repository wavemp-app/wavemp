using WaveMP.Core.Metadata.Tags;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Scanning.Edits;

/// <summary>
/// Creates the backups of the tags of audio files that WaveMP writes before it changes them (the metadata editor and
/// the organization of the media files).
/// </summary>
public static class TagBackups
{
    /// <summary>
    /// The backup of the tags of a file. PostgreSQL does not allow NUL characters in texts, so they are removed.
    /// </summary>
    /// <param name="snapshot">The tags as they are in the file.</param>
    /// <param name="mediaFileId">The file of the library.</param>
    /// <param name="relativePath">Its path relative to the folder of its source.</param>
    /// <param name="now">The time of the backup.</param>
    public static TagBackup Create(TagSnapshot snapshot, Guid? mediaFileId, string relativePath, DateTimeOffset now)
    {
        return new TagBackup
        {
            Id = Guid.CreateVersion7(now),
            MediaFileId = mediaFileId,
            RelativePath = relativePath,
            Title = WithoutNul(snapshot.Title),
            Performers = [.. snapshot.Performers.Select(value => WithoutNul(value)!)],
            AlbumArtists = [.. snapshot.AlbumArtists.Select(value => WithoutNul(value)!)],
            Album = WithoutNul(snapshot.Album),
            Year = snapshot.Year,
            TrackNumber = snapshot.TrackNumber,
            TrackCount = snapshot.TrackCount,
            DiscNumber = snapshot.DiscNumber,
            DiscCount = snapshot.DiscCount,
            Genres = [.. snapshot.Genres.Select(value => WithoutNul(value)!)],
            CreatedAt = now,
        };
    }

    private static string? WithoutNul(string? text)
    {
        return text?.Replace("\0", string.Empty, StringComparison.Ordinal);
    }
}

using WaveMP.Core.Library.Covers;
using WaveMP.Core.Scanning.Groupings;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.OnlineMetadata.Covers;

/// <summary>
/// Where a cover from the online search goes, following the cover rules: the cover of an album is a picture named
/// after the first album cover name of the settings (e.g. <c>cover.jpg</c>) in the folder of the album, the cover of a
/// song a picture with the base name of the song next to it (<c>Song.jpg</c>). Exactly one of
/// <paramref name="AlbumId"/> and <paramref name="TrackId"/> is set; it owns the cover if it is kept in the data
/// folder.
/// </summary>
/// <param name="AlbumId">The album of an album cover.</param>
/// <param name="TrackId">The song of a song cover.</param>
/// <param name="SourceId">The media source of the folder.</param>
/// <param name="DirectoryPath">The folder relative to the folder of the source.</param>
/// <param name="BaseName">The name of the picture without extension.</param>
public sealed record CoverTarget(Guid? AlbumId, Guid? TrackId, Guid SourceId, string DirectoryPath, string BaseName)
{
    /// <summary>
    /// The base name of album covers if the settings have no cover names.
    /// </summary>
    public const string DefaultAlbumCoverName = "cover";

    /// <summary>
    /// The cover of a song: the cover of its album if the cover rule that applies to the song shows the album cover
    /// first (and the song has an album), otherwise its own.
    /// </summary>
    public static CoverTarget ForTrack(
        OnlineTrackRow track,
        CoverPriority global,
        IReadOnlyList<OnlineAlbumTrackRow> albumTracks,
        IReadOnlyList<string> coverNames)
    {
        CoverPriority rule = CoverRules.Effective(global, track.AlbumCoverPriority, track.TrackCoverPriority);
        if (rule == CoverPriority.AlbumFirst && track.AlbumId is { } albumId && albumTracks.Count > 0)
        {
            return ForAlbum(albumId, albumTracks, coverNames);
        }

        return new CoverTarget(null, track.TrackId, track.SourceId, track.DirectoryPath, track.BaseName);
    }

    /// <summary>
    /// The cover of an album in the folder of most of its songs; for disc folders such as <c>CD1</c> the folder
    /// above, where the scan looks for album covers as well.
    /// </summary>
    public static CoverTarget ForAlbum(
        Guid albumId,
        IReadOnlyList<OnlineAlbumTrackRow> tracks,
        IReadOnlyList<string> coverNames)
    {
        (Guid sourceId, string directory) = tracks
            .GroupBy(track => (
                track.SourceId,
                Directory: AlbumCoverCandidates.ParentOfDiscFolder(track.DirectoryPath) ?? track.DirectoryPath))
            .OrderByDescending(folder => folder.Count())
            .ThenBy(folder => folder.Key.Directory.Length)
            .ThenBy(folder => folder.Key.Directory, StringComparer.Ordinal)
            .Select(folder => folder.Key)
            .FirstOrDefault();
        string name = coverNames.FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? DefaultAlbumCoverName;
        return new CoverTarget(albumId, null, sourceId, directory ?? string.Empty, name);
    }
}

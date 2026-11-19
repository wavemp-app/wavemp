using WaveMP.Core.Metadata.Texts;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Library.Checks;

/// <summary>
/// Finds the problems of the library in what the database knows of its tracks and files:
/// <list type="bullet">
/// <item>tracks without cover (also no cover of their album and no online cover),</item>
/// <item>tracks whose file lacks the title, artist or album tag,</item>
/// <item>tracks without album ("Unknown album"),</item>
/// <item>possible duplicates: tracks of the same artist with the same title (compared like names: white space and case
/// do not matter) whose durations differ by at most the tolerance; tracks without known artist or duration are not
/// compared,</item>
/// <item>files that cannot be read completely,</item>
/// <item>videos without audio file and songs without video (hints only).</item>
/// </list>
/// </summary>
internal static class LibraryIssueDetector
{
    /// <summary>
    /// The problems of the tracks (in their order) and files. A possible duplicate names the first other track of its
    /// group in the order of <paramref name="tracks"/>.
    /// </summary>
    public static IReadOnlyList<DetectedLibraryIssue> Detect(
        IReadOnlyList<LibraryCheckTrackRow> tracks,
        IReadOnlyList<LibraryCheckFileRow> files,
        TimeSpan duplicateDurationTolerance)
    {
        Dictionary<Guid, Guid> duplicates = FindDuplicates(tracks, duplicateDurationTolerance);
        List<DetectedLibraryIssue> issues = [];
        foreach (LibraryCheckTrackRow track in tracks)
        {
            if (track.MissingTags.Count > 0)
            {
                issues.Add(new DetectedLibraryIssue(
                    LibraryIssueType.MissingTags,
                    track.TrackId,
                    MissingTags: track.MissingTags));
            }

            if (track.AlbumId is null)
            {
                issues.Add(new DetectedLibraryIssue(LibraryIssueType.UnknownAlbum, track.TrackId));
            }

            if (!track.HasCover)
            {
                issues.Add(new DetectedLibraryIssue(LibraryIssueType.MissingCover, track.TrackId));
            }

            if (duplicates.TryGetValue(track.TrackId, out Guid related))
            {
                issues.Add(new DetectedLibraryIssue(
                    LibraryIssueType.PossibleDuplicate,
                    track.TrackId,
                    RelatedTrackId: related));
            }

            if (track.HasVideo && !track.HasAudio)
            {
                issues.Add(new DetectedLibraryIssue(LibraryIssueType.VideoWithoutAudio, track.TrackId));
            }
            else if (track.HasAudio && !track.HasVideo)
            {
                issues.Add(new DetectedLibraryIssue(LibraryIssueType.AudioWithoutVideo, track.TrackId));
            }
        }

        foreach (LibraryCheckFileRow file in files)
        {
            issues.Add(new DetectedLibraryIssue(
                LibraryIssueType.UnreadableFile,
                file.TrackId,
                file.MediaFileId,
                file.Issue));
        }

        return issues;
    }

    /// <summary>
    /// For every track that is a possible duplicate, the first other track of its group. Tracks of the same artist and
    /// title are sorted by duration; a group goes on as long as the next duration differs from the one before by at most
    /// the tolerance.
    /// </summary>
    internal static Dictionary<Guid, Guid> FindDuplicates(
        IReadOnlyList<LibraryCheckTrackRow> tracks,
        TimeSpan tolerance)
    {
        Dictionary<Guid, Guid> duplicates = [];
        IEnumerable<IGrouping<(Guid Artist, string Title), (LibraryCheckTrackRow Track, int Index)>> candidates = tracks
            .Select((track, index) => (Track: track, Index: index, Title: MetadataText.ToKey(track.Title)))
            .Where(item => item.Track.ArtistId is not null && item.Track.Duration is not null && item.Title is not null)
            .GroupBy(
                item => (item.Track.ArtistId!.Value, item.Title!),
                item => (item.Track, item.Index));
        foreach (IGrouping<(Guid Artist, string Title), (LibraryCheckTrackRow Track, int Index)> group in candidates)
        {
            List<(LibraryCheckTrackRow Track, int Index)> sorted =
                [.. group.OrderBy(item => item.Track.Duration).ThenBy(item => item.Index)];
            if (sorted.Count < 2)
            {
                continue;
            }

            List<(LibraryCheckTrackRow Track, int Index)> cluster = [sorted[0]];
            for (int position = 1; position <= sorted.Count; position++)
            {
                if (position < sorted.Count
                    && sorted[position].Track.Duration!.Value - sorted[position - 1].Track.Duration!.Value
                        <= tolerance)
                {
                    cluster.Add(sorted[position]);
                    continue;
                }

                AddCluster(duplicates, cluster);
                if (position < sorted.Count)
                {
                    cluster = [sorted[position]];
                }
            }
        }

        return duplicates;
    }

    private static void AddCluster(
        Dictionary<Guid, Guid> duplicates,
        List<(LibraryCheckTrackRow Track, int Index)> cluster)
    {
        if (cluster.Count < 2)
        {
            return;
        }

        List<Guid> ordered = [.. cluster.OrderBy(item => item.Index).Select(item => item.Track.TrackId)];
        foreach (Guid trackId in ordered)
        {
            duplicates[trackId] = ordered[0] == trackId ? ordered[1] : ordered[0];
        }
    }
}

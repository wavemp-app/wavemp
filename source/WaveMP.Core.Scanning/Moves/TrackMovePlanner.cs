using WaveMP.Core.Scanning.Discoveries;
using WaveMP.Core.Scanning.Groupings;
using WaveMP.Core.Scanning.Plans;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Scanning.Moves;

/// <summary>
/// Finds the songs whose folder was renamed or moved or whose files were renamed, so a scan keeps them (with their id
/// and all their data) instead of removing them and adding new songs. The rule is deterministic and conservative: a
/// new file is paired with a vanished track only when exactly one candidate fits on each side; in every doubt there is
/// no move and the scan works as before.
/// <list type="number">
/// <item>A vanished track is a stored track all of whose stored files are gone from their place, with no file on disk
/// in its folder that has its base name (otherwise it simply goes on by name). The file it played (its primary audio
/// file, otherwise its primary video file) is its evidence.</item>
/// <item>A new file is an audio or video file on disk whose path is not stored.</item>
/// <item>Both are put into buckets by kind, size and extension. A bucket with exactly one file of a vanished track
/// and exactly one new file pairs them; otherwise the files of the bucket pair by their exact file name where exactly
/// one of each side has the name. Only an evidence file pairs.</item>
/// <item>A new file never pairs when its folder has a stored track with its base name (it joins that track), and new
/// files that would end up in one song pair with nothing.</item>
/// </list>
/// The pairs are checked once more with <see cref="TrackMoveEvidence"/> after the new file was read. Moves between
/// folders and media sources of the same scan are found; sources the scan skips contribute nothing.
/// </summary>
public static class TrackMovePlanner
{
    /// <param name="sources">For every source the scan writes: its plan and its stored files of the scanned
    /// folders (the input of <see cref="ScanPlanner"/>).</param>
    public static TrackMoves Plan(
        IEnumerable<(Guid SourceId, ScanPlan Plan, IReadOnlyList<MediaFileSnapshot> Stored)> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        List<OldFile> oldFiles = [];
        List<NewFile> newFiles = [];
        Dictionary<Guid, VanishedTrack> vanished = [];
        foreach ((Guid sourceId, ScanPlan plan, IReadOnlyList<MediaFileSnapshot> stored) in sources)
        {
            Collect(sourceId, plan, stored, oldFiles, newFiles, vanished);
        }

        ILookup<Bucket, OldFile> oldByBucket = oldFiles.ToLookup(file => file.Bucket);
        ILookup<Bucket, NewFile> newByBucket = newFiles.ToLookup(file => file.Bucket);
        List<(NewFile New, OldFile Old)> candidates = [];
        foreach (IGrouping<Bucket, NewFile> bucket in newByBucket)
        {
            OldFile[] olds = [.. oldByBucket[bucket.Key]];
            NewFile[] news = [.. bucket];
            if (olds.Length == 0)
            {
                continue;
            }

            if (olds.Length == 1 && news.Length == 1)
            {
                candidates.Add((news[0], olds[0]));
                continue;
            }

            ILookup<string, OldFile> oldByName = olds.ToLookup(file => file.FileName, StringComparer.Ordinal);
            foreach (IGrouping<string, NewFile> name in news.GroupBy(file => file.FileName, StringComparer.Ordinal))
            {
                if (name.Count() == 1 && oldByName[name.Key].ToArray() is [OldFile old])
                {
                    candidates.Add((name.First(), old));
                }
            }
        }

        // Only the file a vanished track played pairs, and never into a song that is stored already. New files that
        // would form one song together cannot keep two vanished tracks, so they keep none.
        TrackMove[] moves = [.. candidates
            .Where(candidate => candidate.Old.IsEvidence && !candidate.New.JoinsStoredTrack)
            .GroupBy(candidate => (
                candidate.New.SourceId,
                candidate.New.DirectoryPath,
                candidate.New.NormalizedBaseName))
            .Where(song => song.Count() == 1)
            .Select(song => song.Single())
            .Select(pair => new TrackMove(
                pair.New.SourceId,
                pair.New.RelativePath,
                pair.Old.TrackId,
                pair.Old.FileId,
                pair.Old.SourceId,
                pair.Old.DirectoryPath,
                pair.Old.RelativePath))];
        return moves.Length == 0
            ? TrackMoves.None
            : new TrackMoves(moves, moves.Select(move => vanished[move.TrackId]));
    }

    /// <summary>
    /// Adds the audio and video files of the vanished tracks of a source and its new audio and video files.
    /// </summary>
    private static void Collect(
        Guid sourceId,
        ScanPlan plan,
        IReadOnlyList<MediaFileSnapshot> stored,
        List<OldFile> oldFiles,
        List<NewFile> newFiles,
        Dictionary<Guid, VanishedTrack> vanished)
    {
        var changed = plan.ChangedDirectories.ToDictionary(
            directory => directory.RelativePath,
            StringComparer.Ordinal);
        HashSet<string> deleted = new(plan.DeletedDirectories, StringComparer.Ordinal);
        HashSet<string> storedPaths = new(stored.Select(file => file.RelativePath), StringComparer.Ordinal);
        HashSet<(string DirectoryPath, string NormalizedBaseName)> storedTracks = [];

        foreach (IGrouping<Guid, MediaFileSnapshot> track in stored
            .Where(file => file.TrackId is not null)
            .GroupBy(file => file.TrackId!.Value))
        {
            MediaFileSnapshot[] files = [.. track.OrderBy(file => file.RelativePath, StringComparer.Ordinal)];
            (string DirectoryPath, string NormalizedBaseName)[] places = [.. files
                .Select(file => (file.DirectoryPath, NormalizedBaseNameOf(FileNameOf(file.RelativePath))))
                .Distinct()];
            foreach ((string DirectoryPath, string NormalizedBaseName) place in places)
            {
                storedTracks.Add(place);
            }

            // A track whose files do not share one place is left alone.
            if (places is not [(string directory, string normalizedBaseName)])
            {
                continue;
            }

            IReadOnlyList<DiscoveredFile> onDisk;
            if (deleted.Contains(directory))
            {
                onDisk = [];
            }
            else if (changed.TryGetValue(directory, out DirectoryPlan? changedDirectory))
            {
                onDisk = changedDirectory.Files;
            }
            else
            {
                // An unchanged folder still has all its stored files.
                continue;
            }

            HashSet<string> onDiskPaths = new(onDisk.Select(file => file.RelativePath), StringComparer.Ordinal);
            if (files.Any(file => onDiskPaths.Contains(file.RelativePath))
                || onDisk.Any(file => NormalizedBaseNameOf(file.FileName) == normalizedBaseName))
            {
                continue;
            }

            MediaFileSnapshot? evidence =
                files.FirstOrDefault(file => file.IsPrimary && file.Kind == MediaFileKind.Audio)
                ?? files.FirstOrDefault(file => file.IsPrimary && file.Kind == MediaFileKind.Video);
            vanished[track.Key] = new VanishedTrack(
                track.Key,
                sourceId,
                directory,
                [.. files.Select(file => (file.Id, file.RelativePath))]);
            foreach (MediaFileSnapshot file in files.Where(file => IsPlayable(file.Kind)))
            {
                string fileName = FileNameOf(file.RelativePath);
                oldFiles.Add(new OldFile(
                    new Bucket(file.Kind, file.Size, MediaKindClassifier.ExtensionOf(fileName)),
                    fileName,
                    file == evidence,
                    track.Key,
                    file.Id,
                    sourceId,
                    directory,
                    file.RelativePath));
            }
        }

        foreach (DirectoryPlan directory in plan.ChangedDirectories)
        {
            foreach (DiscoveredFile file in directory.Files)
            {
                if (!IsPlayable(file.Kind) || storedPaths.Contains(file.RelativePath))
                {
                    continue;
                }

                string normalizedBaseName = NormalizedBaseNameOf(file.FileName);
                newFiles.Add(new NewFile(
                    new Bucket(file.Kind, file.Size, MediaKindClassifier.ExtensionOf(file.FileName)),
                    file.FileName,
                    storedTracks.Contains((directory.RelativePath, normalizedBaseName)),
                    sourceId,
                    directory.RelativePath,
                    file.RelativePath,
                    normalizedBaseName));
            }
        }
    }

    private static bool IsPlayable(MediaFileKind kind)
    {
        return kind is MediaFileKind.Audio or MediaFileKind.Video;
    }

    private static string FileNameOf(string relativePath)
    {
        return relativePath[(relativePath.LastIndexOf('/') + 1)..];
    }

    private static string NormalizedBaseNameOf(string fileName)
    {
        return FileGrouper.NormalizeBaseName(MediaKindClassifier.BaseNameOf(fileName));
    }

    private readonly record struct Bucket(MediaFileKind Kind, long Size, string Extension);

    private sealed record OldFile(
        Bucket Bucket,
        string FileName,
        bool IsEvidence,
        Guid TrackId,
        Guid FileId,
        Guid SourceId,
        string DirectoryPath,
        string RelativePath);

    private sealed record NewFile(
        Bucket Bucket,
        string FileName,
        bool JoinsStoredTrack,
        Guid SourceId,
        string DirectoryPath,
        string RelativePath,
        string NormalizedBaseName);
}

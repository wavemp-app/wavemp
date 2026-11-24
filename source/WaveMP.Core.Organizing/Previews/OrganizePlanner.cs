using WaveMP.Core.Metadata.Paths;
using WaveMP.Core.Metadata.Resolutions;
using WaveMP.Core.Metadata.Tags;
using WaveMP.Core.Metadata.Texts;
using WaveMP.Core.Organizing.Files;
using WaveMP.Core.Organizing.Templates;
using WaveMP.Core.Scanning.Processors;
using WaveMP.Core.Settings.Templates;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.Organizing.Previews;

/// <summary>
/// Plans the items of an organize operation: where every file of a song goes by the layout template, which tags
/// change, and what keeps an item from running (conflicts) or should be checked (warnings). Reads the disk only
/// through <see cref="IOrganizeDisk"/> and changes nothing. One planner plans one operation, possibly in batches: it
/// remembers the targets and new folders of earlier songs, so two songs never get the same name and new folders are
/// spelled alike.
/// </summary>
/// <param name="operationId">The operation the items belong to.</param>
/// <param name="template">The parsed layout template.</param>
/// <param name="maxPathBytes">Longest relative path of the library in UTF-8 bytes.</param>
/// <param name="disk">The media folders.</param>
/// <param name="usableSources">The media sources that are active and whose folder exists.</param>
/// <param name="titleCleaning">How the scan cleans titles under this template.</param>
public sealed class OrganizePlanner(
    Guid operationId,
    LayoutTemplate template,
    int maxPathBytes,
    IOrganizeDisk disk,
    IReadOnlySet<Guid> usableSources,
    TitleCleaningMode titleCleaning = TitleCleaningMode.None)
{
    /// <summary>
    /// Start of the hidden name a file passes through when only the case or normalization of its name changes.
    /// </summary>
    public const string TemporaryPrefix = ".wavemp-move-";

    /// <summary>
    /// Base name of the cover of an own album in its album folder.
    /// </summary>
    public const string CoverBaseName = "cover";

    private readonly HashSet<string> _targets = new(StringComparer.Ordinal);
    private readonly HashSet<string> _plannedNames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _plannedFolders = new(StringComparer.Ordinal);
    private readonly Dictionary<(Guid SourceId, string DirectoryPath), List<SongPlacement>> _folders = [];
    private readonly Dictionary<Guid, SongPlacement> _songs = [];
    private int _sequence;

    /// <summary>
    /// Plans songs in the given order.
    /// </summary>
    /// <param name="songs">The songs.</param>
    /// <param name="skipUnchanged">Whether songs that neither move nor change tags get no item.</param>
    public IReadOnlyList<OrganizeItem> PlanSongs(IEnumerable<PlannedSong> songs, bool skipUnchanged)
    {
        List<OrganizeItem> items = [];
        foreach (PlannedSong song in songs)
        {
            if (PlanSong(song, skipUnchanged) is { } item)
            {
                items.Add(item);
            }
        }

        return items;
    }

    /// <summary>
    /// Plans the cover of an own album: one item per media source, into the folder of the first of
    /// <paramref name="trackIds"/> of that source. Returns no item for a source whose folder already has this
    /// cover with the same content.
    /// </summary>
    /// <param name="cover">The uploaded cover.</param>
    /// <param name="trackIds">The members of the album in their order; they must have been planned before.</param>
    /// <param name="coverNames">Names of pictures the library takes as album covers (without extension).</param>
    public IReadOnlyList<OrganizeItem> PlanCover(
        PlannedCover cover,
        IReadOnlyList<Guid> trackIds,
        IReadOnlyCollection<string> coverNames)
    {
        HashSet<string> coverKeys = [.. coverNames.Select(FolderNames.Key)];
        string coverName = CoverBaseName + "." + cover.Extension;
        List<OrganizeItem> items = [];
        HashSet<Guid> sources = [];
        foreach (Guid trackId in trackIds)
        {
            if (!_songs.TryGetValue(trackId, out SongPlacement? placement) || !sources.Add(placement.SourceId))
            {
                continue;
            }

            Guid sourceId = placement.SourceId;
            string directory = placement.ToDirectoryPath;
            string target = FolderNames.Combine(directory, coverName);
            IReadOnlyList<DiskEntry> entries = disk.List(sourceId, directory) ?? [];
            OrganizeConflict? conflict = null;
            if (!usableSources.Contains(sourceId))
            {
                conflict = OrganizeConflict.SourceInactive;
            }
            else if (!disk.IsSafe(sourceId, target, isTarget: true))
            {
                conflict = OrganizeConflict.UnsafePath;
            }
            else if (entries.Any(entry => FolderNames.AreSame(entry.Name, coverName)))
            {
                if (disk.HasSameContent(sourceId, target, cover.FullPath))
                {
                    continue;
                }

                conflict = OrganizeConflict.CoverExists;
            }
            else if (_plannedNames.Contains(NameKey(sourceId, target)))
            {
                conflict = OrganizeConflict.CoverExists;
            }

            List<OrganizeWarning> warnings = [];
            if (entries.Any(entry => !entry.IsDirectory
                && !FolderNames.AreSame(entry.Name, coverName)
                && coverKeys.Contains(FolderNames.Key(FolderNames.BaseNameOf(entry.Name)))))
            {
                warnings.Add(OrganizeWarning.OtherCoverExists);
            }

            OrganizeItem item = NewItem(OrganizeItemKind.AlbumCover, sourceId, cover.DisplayTitle, conflict, warnings);
            item.ToDirectoryPath = directory;
            item.ToBaseName = CoverBaseName;
            item.Moves.Add(NewMove(item, 0, null, OrganizeFileKind.AlbumCover, null, target, null, cover.Size, cover.ModifiedAt));
            if (conflict is null)
            {
                _plannedNames.Add(NameKey(sourceId, target));
            }

            items.Add(item);
        }

        return items;
    }

    /// <summary>
    /// The folders songs were planned out of, to load their files without a song for <see cref="PlanFolderFiles"/>.
    /// </summary>
    public IReadOnlyList<(Guid SourceId, string DirectoryPath)> SourceFolders =>
        [.. _folders.Where(folder => folder.Value.Any(song => song.Moves)).Select(folder => folder.Key)];

    /// <summary>
    /// Plans the files without a song (e.g. <c>cover.jpg</c>) of folders whose songs all move into the same new
    /// folder: they follow the songs, so the album keeps its cover and the old folder can be removed.
    /// </summary>
    public IReadOnlyList<OrganizeItem> PlanFolderFiles(IEnumerable<OrganizeFolderRow> folders)
    {
        List<OrganizeItem> items = [];
        foreach (OrganizeFolderRow folder in folders)
        {
            if (folder.LooseFiles.Count == 0 || folder.TrackIds.Count == 0
                || !_folders.TryGetValue((folder.SourceId, folder.DirectoryPath), out List<SongPlacement>? placed))
            {
                continue;
            }

            var byTrack = placed.ToDictionary(song => song.TrackId);
            if (!folder.TrackIds.All(trackId => byTrack.TryGetValue(trackId, out SongPlacement? song) && song.Moves))
            {
                continue;
            }

            string[] targets = [.. placed.Select(song => song.ToDirectoryPath).Distinct(StringComparer.Ordinal)];
            if (targets.Length != 1 || FolderNames.AreSame(targets[0], folder.DirectoryPath))
            {
                continue;
            }

            if (PlanFolder(folder, targets[0]) is { } item)
            {
                items.Add(item);
            }
        }

        return items;
    }

    /// <summary>
    /// Plans the undo of the done items of an operation, last item first: every moved file goes back to its old
    /// path, a cover the operation wrote is removed, and the tags are restored from the backup taken before they were
    /// written, unless they were changed since. A file that is no longer where the operation put it (or changed) is
    /// a conflict.
    /// </summary>
    /// <param name="doneItems">The done items of the operation with their moves.</param>
    /// <param name="currentFiles">The files of the library by id as they are now.</param>
    /// <param name="backups">The tag backups of the items by id.</param>
    public IReadOnlyList<OrganizeItem> PlanUndo(
        IReadOnlyList<OrganizeItem> doneItems,
        IReadOnlyDictionary<Guid, OrganizeFileRow> currentFiles,
        IReadOnlyDictionary<Guid, TagBackup> backups)
    {
        List<OrganizeItem> items = [];
        foreach (OrganizeItem done in doneItems.OrderByDescending(item => item.Sequence))
        {
            if (PlanUndoItem(done, currentFiles, backups) is { } item)
            {
                items.Add(item);
            }
        }

        return items;
    }

    private OrganizeItem? PlanSong(PlannedSong planned, bool skipUnchanged)
    {
        OrganizeSongRow song = planned.Song;
        Guid sourceId = song.SourceId;
        string? longest = song.Files
            .Select(file => FolderNames.ExtensionOf(file.FileName))
            .OfType<string>()
            .MaxBy(PathNameSanitizer.ByteCountOf);
        RenderedPath rendered = LayoutTemplateRenderer.Render(template, planned.Values, maxPathBytes, longest, song.BaseName);
        (string toDirectory, bool folderBlocked) = ResolveFolders(sourceId, rendered.Folders);
        string toBase = rendered.BaseName;

        OrganizeItem item = NewItem(OrganizeItemKind.Track, sourceId, song.Title, null, [.. planned.Warnings ?? []]);
        item.TrackId = song.TrackId;
        item.DisplayArtist = song.ArtistName;
        item.FromDirectoryPath = song.DirectoryPath;
        item.FromBaseName = song.BaseName;
        item.ToDirectoryPath = toDirectory;
        item.ToBaseName = toBase;

        foreach (OrganizeFileRow file in song.Files)
        {
            string? extension = FolderNames.ExtensionOf(file.FileName);
            string target = FolderNames.Combine(toDirectory, extension is null ? toBase : toBase + "." + extension);
            if (string.Equals(target, file.RelativePath, StringComparison.Ordinal))
            {
                continue;
            }

            string? temporary = FolderNames.AreSame(target, file.RelativePath)
                ? FolderNames.Combine(
                    toDirectory,
                    TemporaryPrefix + file.MediaFileId.ToString("N") + (extension is null ? string.Empty : "." + extension))
                : null;
            item.Moves.Add(NewMove(
                item,
                item.Moves.Count,
                file.MediaFileId,
                KindOf(file.Kind),
                file.RelativePath,
                target,
                temporary,
                file.Size,
                file.ModifiedAt));
        }

        OrganizeFileRow? audio = PrimaryOf(song, MediaFileKind.Audio);
        OrganizeTagValues? tags = planned.TagChanges is { IsEmpty: false } changes ? changes : null;
        bool moves = item.Moves.Count > 0;
        if (!moves && tags is null && skipUnchanged)
        {
            Remember(song, toDirectory, false);
            return null;
        }

        item.TagChanges = tags;
        item.TagsBefore = tags is null ? null : OrganizeTags.Snapshot(tags.Fields, audio?.Tags);
        item.Conflict = ConflictOf(planned, rendered, item, audio, folderBlocked);
        if (item.Conflict is null && moves && !_targets.Add(TargetKey(sourceId, toDirectory, toBase)))
        {
            item.Conflict = OrganizeConflict.DuplicateTarget;
        }

        if (item.Conflict is null)
        {
            foreach (OrganizeFileMove move in item.Moves)
            {
                _plannedNames.Add(NameKey(sourceId, move.ToPath!));
            }
        }

        if (moves && MetadataChanges(song, tags, toDirectory, toBase))
        {
            item.Warnings.Add(OrganizeWarning.MetadataChanges);
        }

        item.Status = item.Conflict is null ? OrganizeItemStatus.Pending : OrganizeItemStatus.Conflict;
        Remember(song, toDirectory, moves && item.Conflict is null);
        return item;
    }

    private OrganizeConflict? ConflictOf(
        PlannedSong planned,
        RenderedPath rendered,
        OrganizeItem item,
        OrganizeFileRow? audio,
        bool folderBlocked)
    {
        OrganizeSongRow song = planned.Song;
        Guid sourceId = song.SourceId;
        if (!usableSources.Contains(sourceId))
        {
            return OrganizeConflict.SourceInactive;
        }

        if (planned.RequiresAudio && audio is null)
        {
            return OrganizeConflict.NoAudio;
        }

        if (rendered.IsTooLong)
        {
            return OrganizeConflict.PathTooLong;
        }

        if (song.Files.Any(file => !disk.IsSafe(sourceId, file.RelativePath, isTarget: false))
            || item.Moves.Any(move => !disk.IsSafe(sourceId, move.ToPath!, isTarget: true)
                || (move.TempPath is { } temporary && !disk.IsSafe(sourceId, temporary, isTarget: false))))
        {
            return OrganizeConflict.UnsafePath;
        }

        if (song.Files.Any(file => disk.Fingerprint(sourceId, file.RelativePath) is not { } found
            || !found.Matches(file.Size, file.ModifiedAt)))
        {
            return OrganizeConflict.SourceMissing;
        }

        if (planned.TagChanges is { IsEmpty: false } && audio is not null
            && !disk.IsWritable(sourceId, audio.RelativePath))
        {
            return OrganizeConflict.NotWritable;
        }

        if (item.Moves.Count == 0)
        {
            return null;
        }

        string[] targetKeys = [.. item.Moves.Select(move => FolderNames.Key(move.ToPath!))];
        if (targetKeys.Distinct(StringComparer.Ordinal).Count() != targetKeys.Length)
        {
            return OrganizeConflict.DuplicateTarget;
        }

        IEnumerable<string> own = song.Files.Select(file => file.RelativePath);
        return folderBlocked || IsTaken(sourceId, item.ToDirectoryPath, item.ToBaseName, item.Moves, own)
            ? OrganizeConflict.TargetExists
            : null;
    }

    /// <summary>
    /// Whether the target folder holds anything that is not one of the files of the song and has the new base name
    /// or the name of a moved file, compared without regard to case and normalization. A file with the same base
    /// name would also be grouped with the song by the scan.
    /// </summary>
    private bool IsTaken(
        Guid sourceId,
        string directory,
        string? baseName,
        IEnumerable<OrganizeFileMove> moves,
        IEnumerable<string> ownPaths)
    {
        IReadOnlyList<DiskEntry>? entries = disk.List(sourceId, directory);
        if (entries is null)
        {
            return false;
        }

        HashSet<string> own = [.. ownPaths
            .Where(path => FolderNames.AreSame(FolderNames.DirectoryOf(path), directory))
            .Select(path => FolderNames.Key(FolderNames.FileNameOf(path)))];
        HashSet<string> names = [.. moves.Select(move => FolderNames.Key(FolderNames.FileNameOf(move.ToPath!)))];
        string? baseKey = baseName is null ? null : FolderNames.Key(baseName);
        foreach (DiskEntry entry in entries)
        {
            string key = FolderNames.Key(entry.Name);
            if (own.Contains(key))
            {
                continue;
            }

            if (names.Contains(key)
                || (!entry.IsDirectory && baseKey is not null
                    && FolderNames.Key(FolderNames.BaseNameOf(entry.Name)) == baseKey))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The folders of the target with the spelling of an existing folder (or of a folder planned before) whose name
    /// differs only in case or normalization, so no second folder <c>Abba</c> next to <c>ABBA</c> is created. Also
    /// tells whether a file stands where a folder is needed.
    /// </summary>
    private (string DirectoryPath, bool Blocked) ResolveFolders(Guid sourceId, IReadOnlyList<string> folders)
    {
        string current = string.Empty;
        bool blocked = false;
        foreach (string folder in folders)
        {
            string key = sourceId.ToString("N") + "|" + FolderNames.Key(current) + "|" + FolderNames.Key(folder);
            IReadOnlyList<DiskEntry>? entries = disk.List(sourceId, current);
            DiskEntry? existing = entries?.FirstOrDefault(entry => FolderNames.AreSame(entry.Name, folder));
            string name;
            if (existing is { IsDirectory: true })
            {
                name = existing.Name;
            }
            else
            {
                blocked |= existing is not null;
                name = _plannedFolders.TryGetValue(key, out string? planned) ? planned : folder;
            }

            _plannedFolders.TryAdd(key, name);
            current = FolderNames.Combine(current, name);
        }

        return (current, blocked);
    }

    /// <summary>
    /// Whether the library would read another title, artist, album or album artist at the new place: the metadata is
    /// resolved from the tags (after the changes) and the old and the new path.
    /// </summary>
    private bool MetadataChanges(OrganizeSongRow song, OrganizeTagValues? changes, string toDirectory, string toBase)
    {
        OrganizeFileRow? source = PrimaryOf(song, MediaFileKind.Audio) ?? PrimaryOf(song, MediaFileKind.Video);
        MediaTags tags = OrganizeTags.Apply(
            source?.Tags is { } stored ? FolderProcessor.ToMediaTags(stored) : MediaTags.Empty,
            changes);
        ResolvedTrackMetadata before = TrackMetadataResolver.Resolve(
            tags,
            PathMetadataParser.Parse(SegmentsOf(song.DirectoryPath), song.BaseName),
            titleCleaning);
        ResolvedTrackMetadata after = TrackMetadataResolver.Resolve(
            tags,
            PathMetadataParser.Parse(SegmentsOf(toDirectory), toBase),
            titleCleaning);
        return MetadataText.ToKey(before.Title) != MetadataText.ToKey(after.Title)
            || MetadataText.ToKey(before.Artist) != MetadataText.ToKey(after.Artist)
            || MetadataText.ToKey(before.Album) != MetadataText.ToKey(after.Album)
            || MetadataText.ToKey(before.AlbumArtist) != MetadataText.ToKey(after.AlbumArtist);
    }

    private OrganizeItem? PlanFolder(OrganizeFolderRow folder, string toDirectory)
    {
        Guid sourceId = folder.SourceId;
        OrganizeItem item = NewItem(
            OrganizeItemKind.FolderFiles,
            sourceId,
            FolderNames.FileNameOf(folder.DirectoryPath),
            null,
            []);
        item.FromDirectoryPath = folder.DirectoryPath;
        item.ToDirectoryPath = toDirectory;
        foreach (OrganizeFileRow file in folder.LooseFiles)
        {
            item.Moves.Add(NewMove(
                item,
                item.Moves.Count,
                file.MediaFileId,
                KindOf(file.Kind),
                file.RelativePath,
                FolderNames.Combine(toDirectory, file.FileName),
                null,
                file.Size,
                file.ModifiedAt));
        }

        if (!usableSources.Contains(sourceId))
        {
            item.Conflict = OrganizeConflict.SourceInactive;
        }
        else if (item.Moves.Any(move => !disk.IsSafe(sourceId, move.FromPath!, isTarget: false)
            || !disk.IsSafe(sourceId, move.ToPath!, isTarget: true)))
        {
            item.Conflict = OrganizeConflict.UnsafePath;
        }
        else if (folder.LooseFiles.Any(file => disk.Fingerprint(sourceId, file.RelativePath) is not { } found
            || !found.Matches(file.Size, file.ModifiedAt)))
        {
            item.Conflict = OrganizeConflict.SourceMissing;
        }
        else if (IsTaken(sourceId, toDirectory, null, item.Moves, [])
            || item.Moves.Any(move => _plannedNames.Contains(NameKey(sourceId, move.ToPath!))))
        {
            item.Conflict = OrganizeConflict.TargetExists;
        }

        if (item.Conflict is null)
        {
            foreach (OrganizeFileMove move in item.Moves)
            {
                _plannedNames.Add(NameKey(sourceId, move.ToPath!));
            }
        }

        item.Status = item.Conflict is null ? OrganizeItemStatus.Pending : OrganizeItemStatus.Conflict;
        return item;
    }

    private OrganizeItem? PlanUndoItem(
        OrganizeItem done,
        IReadOnlyDictionary<Guid, OrganizeFileRow> currentFiles,
        IReadOnlyDictionary<Guid, TagBackup> backups)
    {
        Guid sourceId = done.SourceId;
        OrganizeItem item = NewItem(done.Kind, sourceId, done.DisplayTitle, null, []);
        item.TrackId = done.TrackId;
        item.UndoOfItemId = done.Id;
        item.DisplayArtist = done.DisplayArtist;
        item.FromDirectoryPath = done.ToDirectoryPath;
        item.FromBaseName = done.ToBaseName;
        item.ToDirectoryPath = done.FromDirectoryPath;
        item.ToBaseName = done.FromBaseName;

        OrganizeConflict? conflict = usableSources.Contains(sourceId) ? null : OrganizeConflict.SourceInactive;
        foreach (OrganizeFileMove move in done.Moves
            .Where(move => move.Status == OrganizeMoveStatus.Moved)
            .OrderByDescending(move => move.Sequence))
        {
            if (move.ToPath is not { } current)
            {
                // A removal cannot be undone.
                conflict ??= OrganizeConflict.ChangedSince;
                continue;
            }

            bool unchanged = disk.Fingerprint(sourceId, current) is { } found && found.Matches(move.Size, move.ModifiedAt)
                && (move.MediaFileId is not { } fileId || !currentFiles.TryGetValue(fileId, out OrganizeFileRow? file)
                    || string.Equals(file.RelativePath, current, StringComparison.Ordinal));
            if (!unchanged)
            {
                conflict ??= OrganizeConflict.ChangedSince;
            }

            string? temporary = null;
            if (move.FromPath is { } back && FolderNames.AreSame(back, current))
            {
                string? extension = FolderNames.ExtensionOf(back);
                temporary = FolderNames.Combine(
                    FolderNames.DirectoryOf(back),
                    TemporaryPrefix + (move.MediaFileId ?? move.Id).ToString("N")
                        + (extension is null ? string.Empty : "." + extension));
            }

            item.Moves.Add(NewMove(
                item,
                item.Moves.Count,
                move.MediaFileId,
                move.FileKind,
                current,
                move.FromPath,
                temporary,
                move.Size,
                move.ModifiedAt));
        }

        if (conflict is null && item.Moves.Any(move => !disk.IsSafe(sourceId, move.FromPath!, isTarget: false)
            || (move.ToPath is { } back && !disk.IsSafe(sourceId, back, isTarget: false))))
        {
            conflict = OrganizeConflict.UnsafePath;
        }

        if (conflict is null)
        {
            string[] ownPaths = [.. item.Moves.Select(move => move.FromPath!)];
            foreach (IGrouping<string, OrganizeFileMove> folder in item.Moves
                .Where(move => move.ToPath is not null)
                .GroupBy(move => FolderNames.DirectoryOf(move.ToPath!), StringComparer.Ordinal))
            {
                string? baseName = done.Kind == OrganizeItemKind.Track ? done.FromBaseName : null;
                if (IsTaken(sourceId, folder.Key, baseName, folder, ownPaths))
                {
                    conflict = OrganizeConflict.TargetExists;
                }
            }
        }

        if (done.TagChanges is { IsEmpty: false } written && done.TagBackupId is { } backupId
            && backups.TryGetValue(backupId, out TagBackup? backup)
            && backup.MediaFileId is { } audioId && currentFiles.TryGetValue(audioId, out OrganizeFileRow? audio))
        {
            if (OrganizeTags.Matches(written, audio.Tags))
            {
                item.TagChanges = OrganizeTags.FromBackup(written.Fields, backup);
                item.TagsBefore = OrganizeTags.Snapshot(written.Fields, audio.Tags);
                if (conflict is null && !disk.IsWritable(sourceId, audio.RelativePath))
                {
                    conflict = OrganizeConflict.NotWritable;
                }
            }
            else
            {
                item.Warnings.Add(OrganizeWarning.TagsChangedSince);
            }
        }

        if (item.Moves.Count == 0 && item.TagChanges is null)
        {
            return null;
        }

        item.Conflict = conflict;
        item.Status = conflict is null ? OrganizeItemStatus.Pending : OrganizeItemStatus.Conflict;
        return item;
    }

    private void Remember(OrganizeSongRow song, string toDirectory, bool moves)
    {
        SongPlacement placement = new(song.TrackId, song.SourceId, toDirectory, moves);
        _songs[song.TrackId] = placement;
        (Guid, string) folder = (song.SourceId, song.DirectoryPath);
        if (!_folders.TryGetValue(folder, out List<SongPlacement>? placed))
        {
            placed = [];
            _folders[folder] = placed;
        }

        placed.Add(placement);
    }

    private OrganizeItem NewItem(
        OrganizeItemKind kind,
        Guid sourceId,
        string displayTitle,
        OrganizeConflict? conflict,
        List<OrganizeWarning> warnings)
    {
        return new OrganizeItem
        {
            Id = Guid.CreateVersion7(),
            OperationId = operationId,
            Sequence = _sequence++,
            Kind = kind,
            Status = conflict is null ? OrganizeItemStatus.Pending : OrganizeItemStatus.Conflict,
            SourceId = sourceId,
            FromDirectoryPath = string.Empty,
            FromBaseName = string.Empty,
            ToDirectoryPath = string.Empty,
            ToBaseName = string.Empty,
            Conflict = conflict,
            Warnings = warnings,
            DisplayTitle = displayTitle,
        };
    }

    private static OrganizeFileMove NewMove(
        OrganizeItem item,
        int sequence,
        Guid? mediaFileId,
        OrganizeFileKind kind,
        string? from,
        string? to,
        string? temporary,
        long size,
        DateTimeOffset modifiedAt)
    {
        return new OrganizeFileMove
        {
            Id = Guid.CreateVersion7(),
            ItemId = item.Id,
            Sequence = sequence,
            MediaFileId = mediaFileId,
            SourceId = item.SourceId,
            FileKind = kind,
            FromPath = from,
            ToPath = to,
            TempPath = temporary,
            Size = size,
            ModifiedAt = modifiedAt,
            Status = OrganizeMoveStatus.Pending,
        };
    }

    private static OrganizeFileRow? PrimaryOf(OrganizeSongRow song, MediaFileKind kind)
    {
        return song.Files.FirstOrDefault(file => file.Kind == kind && file.IsPrimary)
            ?? song.Files.FirstOrDefault(file => file.Kind == kind);
    }

    private static OrganizeFileKind KindOf(MediaFileKind kind)
    {
        return kind switch
        {
            MediaFileKind.Audio => OrganizeFileKind.Audio,
            MediaFileKind.Video => OrganizeFileKind.Video,
            MediaFileKind.Lyrics => OrganizeFileKind.Lyrics,
            _ => OrganizeFileKind.Image,
        };
    }

    private static string[] SegmentsOf(string directoryPath)
    {
        return directoryPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string TargetKey(Guid sourceId, string directory, string baseName)
    {
        return sourceId.ToString("N") + "|" + FolderNames.Key(directory) + "|" + FolderNames.Key(baseName);
    }

    private static string NameKey(Guid sourceId, string relativePath)
    {
        return sourceId.ToString("N") + "|" + FolderNames.Key(relativePath);
    }

    /// <summary>
    /// Where a planned song ends up.
    /// </summary>
    private sealed record SongPlacement(Guid TrackId, Guid SourceId, string ToDirectoryPath, bool Moves);
}

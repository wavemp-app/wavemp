using WaveMP.Core.Metadata.Readers;
using WaveMP.Core.Metadata.Resolutions;
using WaveMP.Core.Metadata.Tags;
using WaveMP.Core.Metadata.Texts;
using WaveMP.Core.Scanning.Discoveries;
using WaveMP.Core.Scanning.Groupings;
using WaveMP.Core.Scanning.Moves;
using WaveMP.Core.Scanning.Plans;
using WaveMP.Core.Scanning.Processors;
using WaveMP.Core.Scanning.Selections;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;
using WaveMP.Data.Texts;

namespace WaveMP.Core.Scanning.Writers;

/// <summary>
/// Writes the result of the scan of a folder into the library: files, tracks with their primary files and covers,
/// artists and albums. All changes of a folder are saved together. Album covers and the pictures in the folders of
/// artists are written after all folders. The files the admin selected for a track win over the quality rule as long
/// as they exist; whether they do is stored with each selection. A song that moves from one album or artist to another
/// one is recorded with the changes of its folder (see <see cref="ILibraryScanRepository.RecordAlbumMovesAsync"/> and
/// <see cref="ILibraryScanRepository.RecordArtistMovesAsync"/>). A song counts for an artist as its track artist and
/// as the album artist of its album, and leaves an artist only when that artist has neither role on it any more; a
/// song that loses its album or artist or gets its first one records nothing. A song whose folder was renamed or moved
/// or whose files were renamed (see <see cref="TrackMovePlanner"/>) keeps its track, with its id and all its data, at
/// the new place, and its album and artist moves are recorded like those of any other song. Titles are cleaned as the
/// current layout template asks for (see <see cref="TitleCleaningModes"/>).
/// </summary>
public sealed class LibraryWriter(
    ILibraryScanRepository library,
    ISettingsSnapshot<OrganizingSettings> organizing)
{
    private const int GenreMaxLength = 128;

    /// <summary>
    /// Updates the files of a changed folder of a source with the metadata read for its new and changed files, and its
    /// tracks.
    /// </summary>
    /// <remarks>
    /// <paramref name="moves"/> are the songs the scan found at a new place. A new file of the folder that is the new
    /// place of a vanished track and that <see cref="TrackMoveEvidence"/> confirms takes over the stored file the track
    /// played, and its song keeps the track: its id, its data (date added, review state, cover rule, video offset, and
    /// everything that refers to it, such as favorites, ratings, plays, playlist entries and saved queues) and its file
    /// selection, which follows the files to their new names. The other stored files of the track are removed, also
    /// in other folders. A vanished track of a move that this folder does not keep stays untouched with its stored
    /// files, so a later folder of the scan can still keep it; the scan deletes it at its end otherwise.
    /// </remarks>
    public async Task<FolderWriteResult> WriteFolderAsync(
        Guid sourceId,
        DirectoryPlan plan,
        IReadOnlyDictionary<string, MediaFileMetadata> metadata,
        DateTimeOffset now,
        TrackMoves? moves = null,
        CancellationToken cancellationToken = default)
    {
        TrackMoves songMoves = moves ?? TrackMoves.None;
        StoredFolder stored = await library.LoadFolderAsync(sourceId, plan.RelativePath, cancellationToken);
        var storedFiles = stored.Files.ToDictionary(
            file => file.RelativePath,
            StringComparer.Ordinal);
        IReadOnlyDictionary<string, Adoption> adoptions = await AdoptAsync(
            sourceId,
            plan,
            metadata,
            stored,
            songMoves,
            cancellationToken);

        List<MediaFile> files = [];
        foreach (DiscoveredFile discovered in plan.Files)
        {
            storedFiles.Remove(discovered.RelativePath, out MediaFile? file);
            if (metadata.TryGetValue(discovered.RelativePath, out MediaFileMetadata? read))
            {
                if (file is null && adoptions.TryGetValue(discovered.RelativePath, out Adoption? adoption))
                {
                    file = adoption.File;
                    MoveFile(file, sourceId, discovered, plan.RelativePath);
                }
                else if (file is null)
                {
                    file = CreateFile(sourceId, discovered, plan.RelativePath, now);
                    library.Add(file);
                }

                Apply(file, discovered, read, now);
            }

            if (file is not null)
            {
                files.Add(file);
            }
        }

        HashSet<Guid> adopted = [.. adoptions.Values.Select(adoption => adoption.Track.Id)];
        HashSet<MediaFile> kept = [.. files];

        // The stored files an adopted track does not take along are gone, wherever they lie. Those of a vanished track
        // that a later folder may still keep stay as they are.
        foreach (MediaFile removed in storedFiles.Values
            .Concat(adoptions.Values.SelectMany(adoption => adoption.Track.Files))
            .Distinct())
        {
            if (!kept.Contains(removed) && !IsDeferred(removed.TrackId))
            {
                library.Remove(removed);
            }
        }

        foreach (Adoption adoption in adoptions.Values)
        {
            Move(adoption, sourceId, plan.RelativePath, files);
        }

        Track[] tracks = [.. stored.Tracks.Where(track => !adopted.Contains(track.Id)),
            .. adoptions.Values.Select(adoption => adoption.Track)];
        FolderWriteResult result = await WriteTracksAsync(
            sourceId,
            plan.RelativePath,
            FolderProcessor.Process(
                plan.RelativePath,
                files,
                ChoicesOf(tracks, files),
                sourceId,
                TitleCleaningModes.Of(organizing.Current)),
            tracks,
            adopted,
            track => IsDeferred(track.Id),
            now,
            cancellationToken);
        await library.SaveChangesAsync(cancellationToken);
        return result;

        bool IsDeferred(Guid? trackId)
        {
            return trackId is { } id && songMoves.IsDeferred(id) && !adopted.Contains(id);
        }
    }

    /// <summary>
    /// The vanished tracks the new files of the folder keep, by the path of their new file: the moves of the scan
    /// whose new file was read and is not stored yet, whose track still lies at its stored place with the file it
    /// played, whose new file <see cref="TrackMoveEvidence"/> confirms, and whose song has no other stored track of
    /// the folder by name. Loads the tracks in one query, only for a folder with moves.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, Adoption>> AdoptAsync(
        Guid sourceId,
        DirectoryPlan plan,
        IReadOnlyDictionary<string, MediaFileMetadata> metadata,
        StoredFolder stored,
        TrackMoves moves,
        CancellationToken cancellationToken)
    {
        Dictionary<string, Adoption> adoptions = new(StringComparer.Ordinal);
        if (moves.IsEmpty)
        {
            return adoptions;
        }

        HashSet<string> storedPaths = new(stored.Files.Select(file => file.RelativePath), StringComparer.Ordinal);
        TrackMove[] candidates = [.. plan.Files
            .Select(file => moves.Find(sourceId, file.RelativePath))
            .OfType<TrackMove>()
            .Where(move => metadata.ContainsKey(move.RelativePath) && !storedPaths.Contains(move.RelativePath))];
        if (candidates.Length == 0)
        {
            return adoptions;
        }

        HashSet<Guid> candidateIds = [.. candidates.Select(move => move.TrackId)];
        var tracks = (await library.LoadTracksAsync(candidateIds, cancellationToken))
            .ToDictionary(track => track.Id);
        HashSet<string> names = new(
            stored.Tracks.Where(track => !candidateIds.Contains(track.Id)).Select(track => track.NormalizedBaseName),
            StringComparer.Ordinal);
        foreach (TrackMove move in candidates)
        {
            string fileName = move.RelativePath[(move.RelativePath.LastIndexOf('/') + 1)..];
            if (tracks.TryGetValue(move.TrackId, out Track? track)
                && track.SourceId == move.OldSourceId
                && track.DirectoryPath == move.OldDirectoryPath
                && track.Files.FirstOrDefault(file => file.Id == move.FileId) is { } file
                && file.SourceId == move.OldSourceId
                && file.RelativePath == move.OldRelativePath
                && TrackMoveEvidence.Matches(file, metadata[move.RelativePath])
                && !names.Contains(FileGrouper.NormalizeBaseName(MediaKindClassifier.BaseNameOf(fileName))))
            {
                adoptions[move.RelativePath] = new Adoption(track, file);
            }
        }

        return adoptions;
    }

    /// <summary>
    /// Puts an adopted track at the place of its new file (taken over already, see <see cref="MoveFile"/>), and lets
    /// its file selection follow its files: the selection of the file it took along keeps it, any other one moves to
    /// the file of the new song with the same file name or with the old base name replaced by the new one, if exactly
    /// one has it (otherwise the selection is missing and the quality rule chooses).
    /// </summary>
    private static void Move(Adoption adoption, Guid sourceId, string directoryPath, IReadOnlyList<MediaFile> files)
    {
        Track track = adoption.Track;
        MediaFile taken = adoption.File;
        string oldBaseName = track.BaseName;
        MediaFile[] song = [.. files.Where(file => file.NormalizedBaseName == taken.NormalizedBaseName)];
        foreach (TrackFileSelection selection in track.FileSelections)
        {
            if (selection.MediaFileId == taken.Id)
            {
                selection.RelativePath = taken.RelativePath;
                continue;
            }

            string oldName = selection.RelativePath[(selection.RelativePath.LastIndexOf('/') + 1)..];
            string renamed = oldName.StartsWith(oldBaseName, StringComparison.Ordinal)
                ? taken.BaseName + oldName[oldBaseName.Length..]
                : oldName;
            if (song.Where(file => file.FileName == oldName || file.FileName == renamed).ToArray() is [MediaFile match])
            {
                selection.MediaFileId = match.Id;
                selection.RelativePath = match.RelativePath;
            }
        }

        track.SourceId = sourceId;
        track.DirectoryPath = directoryPath;
        track.BaseName = taken.BaseName;
        track.NormalizedBaseName = taken.NormalizedBaseName;
    }

    /// <summary>
    /// Writes the tracks of a folder again from its stored files, without reading any file, e.g. after the admin
    /// changed the file selection of one of its tracks.
    /// </summary>
    public async Task<FolderWriteResult> RewriteFolderAsync(
        Guid sourceId,
        string directoryPath,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        StoredFolder stored = await library.LoadFolderAsync(sourceId, directoryPath, cancellationToken);
        FolderWriteResult result = await WriteTracksAsync(
            sourceId,
            directoryPath,
            FolderProcessor.Process(
                directoryPath,
                stored.Files,
                ChoicesOf(stored.Tracks, stored.Files),
                sourceId,
                TitleCleaningModes.Of(organizing.Current)),
            stored.Tracks,
            new HashSet<Guid>(),
            _ => false,
            now,
            cancellationToken);
        await library.SaveChangesAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// The selections of the tracks by their normalized base name. A selection whose file is gone follows a file that
    /// appeared again under its path (it got a new id).
    /// </summary>
    internal static IReadOnlyDictionary<string, FileChoices> ChoicesOf(
        IReadOnlyList<Track> tracks,
        IReadOnlyList<MediaFile> files)
    {
        HashSet<Guid> ids = [.. files.Select(file => file.Id)];
        var byPath = files.ToDictionary(file => file.RelativePath, StringComparer.Ordinal);
        Dictionary<string, FileChoices> choices = new(StringComparer.Ordinal);
        foreach (Track track in tracks.Where(track => track.FileSelections.Count > 0))
        {
            foreach (TrackFileSelection selection in track.FileSelections)
            {
                if (!ids.Contains(selection.MediaFileId)
                    && byPath.TryGetValue(selection.RelativePath, out MediaFile? reappeared))
                {
                    selection.MediaFileId = reappeared.Id;
                }
            }

            choices[track.NormalizedBaseName] = new FileChoices(
                SelectedId(track, FileSelectionCategory.Audio),
                SelectedId(track, FileSelectionCategory.Video),
                SelectedId(track, FileSelectionCategory.Cover));
        }

        return choices;
    }

    private static Guid? SelectedId(Track track, FileSelectionCategory category)
    {
        return track.FileSelections.FirstOrDefault(selection => selection.Category == category)?.MediaFileId;
    }

    /// <summary>
    /// Replaces the covers of all albums with the pictures of their folders named after the cover names.
    /// </summary>
    public async Task UpdateAlbumCoversAsync(
        IReadOnlyList<string> coverNames,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AlbumFolder> folders = await library.GetAlbumFoldersAsync(cancellationToken);
        IReadOnlyList<AlbumCoverImage> images = await library.GetAlbumCoverImagesAsync(
            [.. coverNames.Select(FileGrouper.NormalizeBaseName)],
            cancellationToken);
        IReadOnlyDictionary<Guid, IReadOnlyList<AlbumCoverImage>> desired =
            AlbumCoverCandidates.Select(folders, images, coverNames);
        ILookup<Guid, Cover> existing = (await library.LoadAlbumCoversAsync(cancellationToken))
            .ToLookup(cover => cover.AlbumId!.Value);

        HashSet<Guid> albums = [.. desired.Keys, .. existing.Select(group => group.Key)];
        foreach (Guid albumId in albums)
        {
            IReadOnlyList<AlbumCoverImage> candidates = desired.GetValueOrDefault(albumId, []);
            SyncCovers(
                [.. existing[albumId]],
                [.. candidates.Select((image, index) => (
                    image.MediaFileId,
                    CoverSource.File,
                    image.Width,
                    image.Height,
                    FolderProcessor.MimeTypeOf(image.Extension),
                    index == 0))],
                cover => cover.AlbumId = albumId);
        }

        await library.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Sets the picture in the folder of every artist by the rules of <see cref="ArtistPictureCandidates"/>, over the
    /// whole library, and clears it where there is none any more. Only artists whose picture changes are written. Runs
    /// after the album covers, also after artists were removed, so a renamed artist finds the picture of its own
    /// folder.
    /// </summary>
    public async Task UpdateArtistPicturesAsync(
        IReadOnlyList<string> pictureNames,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ArtistFolder> folders = await library.GetArtistFoldersAsync(cancellationToken);
        IReadOnlyList<ArtistPictureImage> images = await library.GetArtistPictureImagesAsync(
            [.. pictureNames.Select(FileGrouper.NormalizeBaseName)],
            cancellationToken);
        IReadOnlyDictionary<Guid, IReadOnlyList<ArtistPictureImage>> desired =
            ArtistPictureCandidates.Select(folders, images, pictureNames);
        IReadOnlyDictionary<Guid, Guid> current = await library.GetArtistLocalPicturesAsync(cancellationToken);

        Dictionary<Guid, Guid?> changes = [];
        foreach ((Guid artistId, IReadOnlyList<ArtistPictureImage> candidates) in desired)
        {
            Guid fileId = candidates[0].MediaFileId;
            if (!current.TryGetValue(artistId, out Guid stored) || stored != fileId)
            {
                changes[artistId] = fileId;
            }
        }

        foreach (Guid artistId in current.Keys.Where(artistId => !desired.ContainsKey(artistId)))
        {
            changes[artistId] = null;
        }

        await library.SetArtistLocalPicturesAsync(changes, cancellationToken);
    }

    /// <summary>
    /// Marks the albums as compilations by the rules of <see cref="AlbumCompilations"/>, over the whole library since
    /// an album may span folders. Returns the number of albums whose value changed.
    /// </summary>
    public Task<int> UpdateAlbumCompilationsAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return library.UpdateAlbumCompilationsAsync(AlbumCompilations.VariousArtistsKey, now, cancellationToken);
    }

    /// <summary>
    /// Writes the processed tracks of a folder onto its stored tracks by their normalized base name. Tracks in
    /// <paramref name="adopted"/> came from another place (see <see cref="Move"/>) and count as updated. A stored
    /// track without a song any more is removed unless <paramref name="isDeferred"/> says it is the vanished track of
    /// a move that a later folder may still keep.
    /// </summary>
    private async Task<FolderWriteResult> WriteTracksAsync(
        Guid sourceId,
        string directoryPath,
        ProcessedFolder folder,
        IReadOnlyList<Track> storedTracks,
        IReadOnlySet<Guid> adopted,
        Func<Track, bool> isDeferred,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var tracks = storedTracks.ToDictionary(
            track => track.NormalizedBaseName,
            StringComparer.Ordinal);
        EntityCache cache = new();
        Dictionary<(Guid FromAlbumId, Guid ToAlbumId), int> moves = [];
        List<WrittenSong> written = [];
        int added = 0;
        int updated = 0;

        foreach (ProcessedTrack processed in folder.Tracks)
        {
            Guid? artistId = await GetArtistIdAsync(
                processed.Metadata.Artist,
                processed.Metadata.MusicBrainzArtistId,
                cache,
                now,
                cancellationToken);
            Guid? albumId = await GetAlbumIdAsync(processed.Album, cache, now, cancellationToken);
            Guid? genreId = await GetGenreIdAsync(processed.Metadata.Genre, cache, cancellationToken);

            if (!tracks.Remove(processed.Group.NormalizedBaseName, out Track? track))
            {
                track = new Track
                {
                    Id = Guid.CreateVersion7(now),
                    SourceId = sourceId,
                    DirectoryPath = directoryPath,
                    BaseName = processed.Group.BaseName,
                    NormalizedBaseName = processed.Group.NormalizedBaseName,
                    Title = processed.Metadata.Title,
                    HasAudio = false,
                    HasVideo = false,
                    MetadataSource = processed.Metadata.TitleSource,
                    FirstSeenAt = now,
                    UpdatedAt = now,
                };
                library.Add(track);
                added++;
                Update(track, processed, artistId, albumId, genreId);
            }
            else
            {
                Guid? previousAlbumId = track.AlbumId;
                written.Add(new WrittenSong(track.ArtistId, previousAlbumId, artistId, albumId));
                if (Update(track, processed, artistId, albumId, genreId) | adopted.Contains(track.Id))
                {
                    track.UpdatedAt = now;
                    updated++;
                }

                // A song that leaves one album for another one is recorded, so the data of the old album can follow
                // its songs once it has none left. Songs that lose or get their first album are no move.
                if (previousAlbumId is { } fromAlbumId && albumId is { } toAlbumId && fromAlbumId != toAlbumId)
                {
                    moves[(fromAlbumId, toAlbumId)] = moves.GetValueOrDefault((fromAlbumId, toAlbumId)) + 1;
                }
            }

            foreach (MediaFile file in processed.Group.Files)
            {
                file.TrackId = track.Id;
                file.IsPrimary = file == processed.PrimaryAudio || file == processed.PrimaryVideo;
            }

            UpdateSelections(track, processed);

            Track owner = track;
            SyncCovers(
                [.. track.Covers],
                [.. processed.Covers.Select((cover, index) => (
                    cover.File.Id,
                    cover.Source,
                    cover.Width,
                    cover.Height,
                    cover.MimeType,
                    index == 0))],
                cover => cover.TrackId = owner.Id);
        }

        foreach (MediaFile file in folder.PictureGroups.SelectMany(group => group.Files))
        {
            file.TrackId = null;
            file.IsPrimary = false;
        }

        int removedTracks = 0;
        foreach (Track removed in tracks.Values.Where(track => !isDeferred(track)))
        {
            library.Remove(removed);
            removedTracks++;
        }

        // Saved by the caller together with the tracks, so a move is never lost to an interrupted scan.
        await library.RecordAlbumMovesAsync(moves, now, cancellationToken);
        await library.RecordArtistMovesAsync(
            await ArtistMovesOfAsync(written, cache, cancellationToken),
            now,
            cancellationToken);
        return new FolderWriteResult(added, updated, removedTracks)
        {
            AdoptedTrackIds = [.. adopted.Order()],
        };
    }

    /// <summary>
    /// The artists the stored songs of a folder left, per pair of artists, so the data of an old artist can follow its
    /// songs once it has none left. A song belongs to an artist as its track artist and as the album artist of its
    /// album. Each role that changed from one artist to another counts, but only when the old artist has neither role
    /// on the song any more (it still has the song otherwise), and a pair counts once per song. Songs that lose an
    /// artist or get their first one are no move. The album artists of the albums the songs left are queried once,
    /// and only when some song changed its album: an album never changes its album artist, which is part of its key.
    /// </summary>
    private async Task<Dictionary<(Guid FromArtistId, Guid ToArtistId), int>> ArtistMovesOfAsync(
        IReadOnlyList<WrittenSong> songs,
        EntityCache cache,
        CancellationToken cancellationToken)
    {
        Dictionary<(Guid FromArtistId, Guid ToArtistId), int> moves = [];
        Guid[] leftAlbums = [.. songs
            .Where(song => song.PreviousAlbumId is { } previous && previous != song.AlbumId
                && !cache.AlbumArtists.ContainsKey(previous))
            .Select(song => song.PreviousAlbumId!.Value)
            .Distinct()];
        IReadOnlyDictionary<Guid, Guid?> leftAlbumArtists = leftAlbums.Length == 0
            ? new Dictionary<Guid, Guid?>()
            : await library.GetAlbumArtistIdsAsync(leftAlbums, cancellationToken);

        foreach (WrittenSong song in songs)
        {
            Guid? previousAlbumArtistId = song.PreviousAlbumId is { } previousAlbumId
                ? cache.AlbumArtists.TryGetValue(previousAlbumId, out Guid? cached)
                    ? cached
                    : leftAlbumArtists.GetValueOrDefault(previousAlbumId)
                : null;
            Guid? albumArtistId = song.AlbumId is { } albumId ? cache.AlbumArtists.GetValueOrDefault(albumId) : null;
            HashSet<(Guid, Guid)> pairs = [];
            foreach ((Guid? from, Guid? to) in new[]
            {
                (song.PreviousArtistId, song.ArtistId),
                (previousAlbumArtistId, albumArtistId),
            })
            {
                if (from is { } fromId && to is { } toId && fromId != toId
                    && fromId != song.ArtistId && fromId != albumArtistId && pairs.Add((fromId, toId)))
                {
                    moves[(fromId, toId)] = moves.GetValueOrDefault((fromId, toId)) + 1;
                }
            }
        }

        return moves;
    }

    /// <summary>
    /// Marks the selections of the track whose file the track does not use (gone, or no longer of the category) as
    /// missing, and the others as found.
    /// </summary>
    private static void UpdateSelections(Track track, ProcessedTrack processed)
    {
        foreach (TrackFileSelection selection in track.FileSelections)
        {
            Guid? used = selection.Category switch
            {
                FileSelectionCategory.Audio => processed.PrimaryAudio?.Id,
                FileSelectionCategory.Video => processed.PrimaryVideo?.Id,
                _ => processed.Covers.Count > 0 ? processed.Covers[0].File.Id : null,
            };
            selection.IsMissing = used != selection.MediaFileId;
        }
    }

    /// <summary>
    /// Applies the processed values to the track. Returns whether anything changed.
    /// </summary>
    private static bool Update(
        Track track,
        ProcessedTrack processed,
        Guid? artistId,
        Guid? albumId,
        Guid? genreId)
    {
        ResolvedTrackMetadata metadata = processed.Metadata;
        bool hasAudio = processed.PrimaryAudio is not null;
        bool hasVideo = processed.PrimaryVideo is not null;

        bool changed = track.BaseName != processed.Group.BaseName
            || track.Title != metadata.Title
            || track.MetadataSource != metadata.TitleSource
            || track.ArtistId != artistId
            || track.AlbumId != albumId
            || track.TrackNumber != metadata.TrackNumber
            || track.DiscNumber != metadata.DiscNumber
            || track.Year != metadata.Year
            || track.GenreId != genreId
            || track.Duration != processed.Duration
            || track.HasAudio != hasAudio
            || track.HasVideo != hasVideo;

        track.BaseName = processed.Group.BaseName;
        track.Title = metadata.Title;
        track.MetadataSource = metadata.TitleSource;
        track.ArtistId = artistId;
        track.AlbumId = albumId;
        track.TrackNumber = metadata.TrackNumber;
        track.DiscNumber = metadata.DiscNumber;
        track.Year = metadata.Year;
        track.GenreId = genreId;
        track.Duration = processed.Duration;
        track.HasAudio = hasAudio;
        track.HasVideo = hasVideo;
        return changed;
    }

    /// <summary>
    /// Makes the covers of an album or track match the wanted ones (file and source), keeping the ids of covers that
    /// stay.
    /// </summary>
    private void SyncCovers(
        List<Cover> existing,
        IReadOnlyList<(
            Guid MediaFileId,
            CoverSource Source,
            int? Width,
            int? Height,
            string? MimeType,
            bool IsPrimary)> wanted,
        Action<Cover> assignOwner)
    {
        foreach ((Guid mediaFileId, CoverSource source, int? width, int? height, string? mimeType, bool isPrimary)
            in wanted)
        {
            Cover? cover = existing.Find(item => item.MediaFileId == mediaFileId && item.Source == source);
            if (cover is null)
            {
                cover = new Cover
                {
                    Id = Guid.CreateVersion7(),
                    Source = source,
                    MediaFileId = mediaFileId,
                    IsPrimary = isPrimary,
                };
                assignOwner(cover);
                library.Add(cover);
            }
            else
            {
                existing.Remove(cover);
            }

            cover.Width = width;
            cover.Height = height;
            cover.MimeType = mimeType;
            cover.IsPrimary = isPrimary;
        }

        foreach (Cover removed in existing)
        {
            library.Remove(removed);
        }
    }

    /// <summary>
    /// The artist of a name: the artist with the MusicBrainz artist of the tags if there is one, otherwise the artist
    /// with the same normalized name (see <see cref="NameKeys"/>), otherwise a new one. An artist found by its name
    /// that has no MusicBrainz artist yet takes the one of the tags. The name of an existing artist stays as it is.
    /// </summary>
    private async Task<Guid?> GetArtistIdAsync(
        string? name,
        Guid? musicBrainzArtistId,
        EntityCache cache,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (MetadataText.Normalize(name) is not { } displayName || NameKeys.Of(displayName) is not { } key)
        {
            return null;
        }

        if (musicBrainzArtistId is { } musicBrainzId)
        {
            if (cache.MusicBrainzArtists.TryGetValue(musicBrainzId, out Guid cached))
            {
                return cached;
            }

            if (await library.FindArtistByMusicBrainzIdAsync(musicBrainzId, cancellationToken) is { } known)
            {
                cache.MusicBrainzArtists[musicBrainzId] = known.Id;
                return known.Id;
            }
        }

        if (cache.Artists.TryGetValue(key, out Guid id))
        {
            return id;
        }

        Artist? artist = await library.FindArtistAsync(key, cancellationToken);
        if (artist is null)
        {
            artist = new Artist
            {
                Id = Guid.CreateVersion7(now),
                Name = displayName,
                NormalizedName = key,
                FirstSeenAt = now,
                MusicBrainzArtistId = musicBrainzArtistId,
            };
            library.Add(artist);
        }
        else if (artist.MusicBrainzArtistId is null && musicBrainzArtistId is not null)
        {
            artist.MusicBrainzArtistId = musicBrainzArtistId;
        }

        cache.Artists[key] = artist.Id;
        if (musicBrainzArtistId is { } newId && artist.MusicBrainzArtistId == newId)
        {
            cache.MusicBrainzArtists[newId] = artist.Id;
        }

        return artist.Id;
    }

    /// <summary>
    /// The album of an assignment: the album with the MusicBrainz release of the tags if there is one, otherwise the
    /// album with the key of the assignment, otherwise a new one. An album found by its key that has no release yet
    /// takes the one of the tags.
    /// </summary>
    private async Task<Guid?> GetAlbumIdAsync(
        AlbumAssignment? assignment,
        EntityCache cache,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (assignment is null)
        {
            return null;
        }

        if (cache.Albums.TryGetValue(assignment.Key, out Guid id))
        {
            return id;
        }

        Album? album = null;
        if (assignment.MusicBrainzReleaseId is { } releaseId)
        {
            album = await library.FindAlbumByReleaseIdAsync(releaseId, cancellationToken);
        }

        album ??= await library.FindAlbumAsync(assignment.Key, cancellationToken);
        if (album is null)
        {
            album = new Album
            {
                Id = Guid.CreateVersion7(now),
                Title = assignment.Title,
                NormalizedTitle = NameKeys.Of(assignment.Title)!,
                AlbumKey = assignment.Key,
                AlbumArtistId = await GetArtistIdAsync(
                    assignment.AlbumArtist,
                    assignment.AlbumArtistMusicBrainzId,
                    cache,
                    now,
                    cancellationToken),
                // Set over all its tracks once every folder is written (see UpdateAlbumCompilationsAsync).
                IsCompilation = false,
                Year = assignment.Year,
                MusicBrainzReleaseId = assignment.MusicBrainzReleaseId,
                FirstSeenAt = now,
                UpdatedAt = now,
            };
            library.Add(album);
        }
        else if (UpdateAlbum(album, assignment))
        {
            album.UpdatedAt = now;
        }

        cache.Albums[assignment.Key] = album.Id;
        cache.AlbumArtists[album.Id] = album.AlbumArtistId;
        return album.Id;
    }

    /// <summary>
    /// The genre of a name: the genre with the same normalized name (see <see cref="NameKeys"/>), otherwise a new one.
    /// The name of an existing genre stays as it is.
    /// </summary>
    private async Task<Guid?> GetGenreIdAsync(string? name, EntityCache cache, CancellationToken cancellationToken)
    {
        string? displayName = MetadataText.Normalize(name);
        if (displayName is { Length: > GenreMaxLength })
        {
            displayName = displayName[..GenreMaxLength].TrimEnd();
        }

        if (displayName is null || NameKeys.Of(displayName) is not { } key)
        {
            return null;
        }

        if (cache.Genres.TryGetValue(key, out Guid id))
        {
            return id;
        }

        Genre? genre = await library.FindGenreAsync(key, cancellationToken);
        if (genre is null)
        {
            genre = new Genre
            {
                Id = Guid.CreateVersion7(),
                Name = displayName,
                NormalizedName = key,
            };
            library.Add(genre);
        }

        cache.Genres[key] = genre.Id;
        return genre.Id;
    }

    /// <summary>
    /// An existing album follows the folder written last: the spelling of its title (the key, and so the album, stays
    /// the same) and its year, unless the folder names none. Returns whether anything changed. Artists keep their
    /// first spelling, so their names do not change from folder to folder.
    /// </summary>
    private static bool UpdateAlbum(Album album, AlbumAssignment assignment)
    {
        bool changed = false;
        // An album found by its MusicBrainz release may carry another title in the tags of a song; only a new
        // spelling of the same title is taken over.
        if (album.Title != assignment.Title && NameKeys.AreEqual(album.Title, assignment.Title))
        {
            album.Title = assignment.Title;
            album.NormalizedTitle = NameKeys.Of(assignment.Title)!;
            changed = true;
        }

        if (album.MusicBrainzReleaseId is null && assignment.MusicBrainzReleaseId is not null)
        {
            album.MusicBrainzReleaseId = assignment.MusicBrainzReleaseId;
            changed = true;
        }

        if (assignment.Year is not null && album.Year != assignment.Year)
        {
            album.Year = assignment.Year;
            changed = true;
        }

        return changed;
    }

    private static MediaFile CreateFile(
        Guid sourceId,
        DiscoveredFile discovered,
        string directoryPath,
        DateTimeOffset now)
    {
        string baseName = MediaKindClassifier.BaseNameOf(discovered.FileName);
        return new MediaFile
        {
            Id = Guid.CreateVersion7(now),
            SourceId = sourceId,
            RelativePath = discovered.RelativePath,
            DirectoryPath = directoryPath,
            FileName = discovered.FileName,
            BaseName = baseName,
            NormalizedBaseName = FileGrouper.NormalizeBaseName(baseName),
            Extension = MediaKindClassifier.ExtensionOf(discovered.FileName),
            Kind = discovered.Kind,
            Size = discovered.Size,
            ModifiedAt = discovered.ModifiedAt,
            IsPrimary = false,
            IsLossless = false,
            HasEmbeddedPicture = false,
            IsWritable = false,
            MetadataVersion = ScannerVersions.MetadataOf(discovered.Kind),
            FirstSeenAt = now,
            ScannedAt = now,
        };
    }

    /// <summary>
    /// Puts the stored file an adopted track played at the place of its new file; it keeps its id.
    /// </summary>
    private static void MoveFile(MediaFile file, Guid sourceId, DiscoveredFile discovered, string directoryPath)
    {
        string baseName = MediaKindClassifier.BaseNameOf(discovered.FileName);
        file.SourceId = sourceId;
        file.RelativePath = discovered.RelativePath;
        file.DirectoryPath = directoryPath;
        file.FileName = discovered.FileName;
        file.BaseName = baseName;
        file.NormalizedBaseName = FileGrouper.NormalizeBaseName(baseName);
        file.Extension = MediaKindClassifier.ExtensionOf(discovered.FileName);
    }

    private static void Apply(MediaFile file, DiscoveredFile discovered, MediaFileMetadata metadata, DateTimeOffset now)
    {
        file.Kind = discovered.Kind;
        file.Size = discovered.Size;
        file.ModifiedAt = discovered.ModifiedAt;
        file.Format = metadata.Format;
        file.Codec = metadata.Codec;
        file.AudioCodec = metadata.AudioCodec;
        file.PixelFormat = metadata.PixelFormat;
        file.Bitrate = metadata.Bitrate;
        file.SampleRate = metadata.SampleRate;
        file.BitDepth = metadata.BitDepth;
        file.Channels = metadata.Channels;
        file.IsLossless = metadata.IsLossless;
        file.Width = metadata.Width;
        file.Height = metadata.Height;
        file.Duration = metadata.Duration;
        file.HasEmbeddedPicture = metadata.HasEmbeddedPicture;
        file.EmbeddedPictureWidth = metadata.EmbeddedPictureWidth;
        file.EmbeddedPictureHeight = metadata.EmbeddedPictureHeight;
        file.EmbeddedPictureMimeType = metadata.EmbeddedPictureMimeType;
        file.IsWritable = metadata.IsWritable;
        file.Issue = metadata.Issue;
        file.Tags = ToEntity(metadata.Tags);
        file.MetadataVersion = ScannerVersions.MetadataOf(discovered.Kind);
        file.ScannedAt = now;
    }

    private static MediaFileTags ToEntity(MediaTags tags)
    {
        return new MediaFileTags
        {
            Title = tags.Title,
            TitleSort = tags.TitleSort,
            Artist = tags.Artist,
            Artists = tags.Artists,
            ArtistSort = tags.ArtistSort,
            AlbumArtist = tags.AlbumArtist,
            AlbumArtistSort = tags.AlbumArtistSort,
            Album = tags.Album,
            AlbumSort = tags.AlbumSort,
            TrackNumber = tags.TrackNumber,
            TrackCount = tags.TrackCount,
            DiscNumber = tags.DiscNumber,
            DiscCount = tags.DiscCount,
            Year = tags.Year,
            Genre = tags.Genre is { Length: > GenreMaxLength } genre ? genre[..GenreMaxLength] : tags.Genre,
            Genres = tags.Genres,
            Composer = tags.Composer,
            ReplayGainTrackGain = tags.ReplayGainTrackGain,
            ReplayGainTrackPeak = tags.ReplayGainTrackPeak,
            ReplayGainAlbumGain = tags.ReplayGainAlbumGain,
            ReplayGainAlbumPeak = tags.ReplayGainAlbumPeak,
            MusicBrainzTrackId = Truncate(tags.MusicBrainzTrackId, 64),
            MusicBrainzReleaseId = Truncate(tags.MusicBrainzReleaseId, 64),
            MusicBrainzArtistId = tags.MusicBrainzArtistId,
            MusicBrainzAlbumArtistId = tags.MusicBrainzAlbumArtistId,
            IsCompilation = tags.IsCompilation,
        };
    }

    private static string? Truncate(string? value, int length)
    {
        return value is not null && value.Length > length ? value[..length] : value;
    }

    /// <summary>
    /// A vanished track a new file of the folder keeps, and the stored file it played, which the new file takes over.
    /// </summary>
    private sealed record Adoption(Track Track, MediaFile File);

    /// <summary>
    /// The artists and album of a stored song before and after it was written.
    /// </summary>
    private sealed record WrittenSong(Guid? PreviousArtistId, Guid? PreviousAlbumId, Guid? ArtistId, Guid? AlbumId);

    /// <summary>
    /// Artists and albums found or created while writing one folder, and the album artist of each of these albums.
    /// </summary>
    private sealed class EntityCache
    {
        public Dictionary<string, Guid> Artists { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, Guid> Albums { get; } = new(StringComparer.Ordinal);

        public Dictionary<Guid, Guid?> AlbumArtists { get; } = [];

        public Dictionary<Guid, Guid> MusicBrainzArtists { get; } = [];

        public Dictionary<string, Guid> Genres { get; } = new(StringComparer.Ordinal);
    }
}

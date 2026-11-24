using Microsoft.Extensions.Logging;
using WaveMP.Core.Scanning.Groupings;
using WaveMP.Core.Scanning.Processors;
using WaveMP.Core.Scanning.Qualities;
using WaveMP.Core.Scanning.Writers;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Scanning.Selections;

/// <summary>
/// Stores the selection of the admin and writes the folder of the track again with the logic of the scan, while
/// holding the <see cref="LibraryWriteLock"/>: the primary files, the metadata (from the selected audio file) and the
/// covers follow the selection at once. No file is read.
/// </summary>
internal sealed class FileSelectionService(
    IFileSelectionRepository selections,
    ILibraryScanRepository library,
    LibraryWriter writer,
    LibraryWriteLock writeLock,
    ISettingsSnapshot<LibrarySettings> settings,
    TimeProvider timeProvider,
    ILogger<FileSelectionService> logger) : IFileSelectionService
{
    public Task<Page<TrackFileSummaryRow>> ListAsync(
        FileSelectionListRequest request,
        CancellationToken cancellationToken = default)
    {
        FileSelectionSortField sort = request.Sort ?? FileSelectionSortField.Path;
        SortDirection direction = request.Direction
            ?? (sort == FileSelectionSortField.FileCount ? SortDirection.Desc : SortDirection.Asc);
        string? search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        return selections.ListAsync(
            new FileSelectionListQuery(
                request.Offset,
                request.Limit,
                sort,
                direction,
                request.Filter ?? FileSelectionFilter.All,
                search),
            cancellationToken);
    }

    public async Task<FileSelectionDetails?> GetAsync(Guid trackId, CancellationToken cancellationToken = default)
    {
        TrackFiles? track = await selections.LoadTrackAsync(trackId, cancellationToken);
        return track is null ? null : Describe(track);
    }

    public async Task<FileSelectionResult> SetAsync(
        Guid trackId,
        FileChoices choices,
        CancellationToken cancellationToken = default)
    {
        using (await writeLock.AcquireAsync(cancellationToken))
        {
            TrackFiles? track = await selections.LoadTrackAsync(trackId, cancellationToken);
            if (track is null)
            {
                return FileSelectionResult.TrackNotFound;
            }

            DateTimeOffset now = timeProvider.GetUtcNow();
            var candidates = Candidates.Of(track);
            List<FileSelectionCategory> invalid = [];
            List<TrackFileSelection> wanted = [];
            foreach ((FileSelectionCategory category, Guid? id) in (ReadOnlySpan<(FileSelectionCategory, Guid?)>)
                [
                    (FileSelectionCategory.Audio, choices.Audio),
                    (FileSelectionCategory.Video, choices.Video),
                    (FileSelectionCategory.Cover, choices.Cover),
                ])
            {
                if (id is not { } fileId)
                {
                    continue;
                }

                // Sending the selected file again keeps the selection as it is, also a missing one.
                TrackFileSelection? existing = track.Selections.FirstOrDefault(item => item.Category == category);
                if (existing?.MediaFileId == fileId)
                {
                    wanted.Add(existing);
                    continue;
                }

                MediaFile? file = candidates.Of(category).FirstOrDefault(candidate => candidate.Id == fileId);
                if (file is null)
                {
                    invalid.Add(category);
                    continue;
                }

                wanted.Add(new TrackFileSelection
                {
                    TrackId = trackId,
                    Category = category,
                    MediaFileId = file.Id,
                    RelativePath = file.RelativePath,
                    IsMissing = false,
                    SelectedAt = now,
                });
            }

            if (invalid.Count > 0)
            {
                return FileSelectionResult.Invalid(invalid);
            }

            if (!await selections.ReplaceAsync(trackId, wanted, cancellationToken))
            {
                return FileSelectionResult.TrackNotFound;
            }

            // The selection is stored: the library follows it even if the client is gone by now.
            await writer.RewriteFolderAsync(track.SourceId, track.DirectoryPath, now, CancellationToken.None);
            await library.DeleteOrphansAsync(now, CancellationToken.None);
            await writer.UpdateAlbumCoversAsync(settings.Current.AlbumCoverNames, CancellationToken.None);
            await writer.UpdateArtistPicturesAsync(settings.Current.ArtistPictureNames, CancellationToken.None);
            await writer.UpdateAlbumCompilationsAsync(now, CancellationToken.None);
            logger.LogInformation(
                "File selection of track {TrackId} changed: {Count} categories selected",
                trackId,
                wanted.Count);
        }

        FileSelectionDetails? details = await GetAsync(trackId, CancellationToken.None);
        return details is null ? FileSelectionResult.TrackNotFound : FileSelectionResult.Done(details);
    }

    public async Task<bool> ResetAsync(Guid trackId, CancellationToken cancellationToken = default)
    {
        FileSelectionResult result = await SetAsync(trackId, FileChoices.None, cancellationToken);
        return result.Status != FileSelectionStatus.TrackNotFound;
    }

    /// <summary>
    /// The selection of the track as the scan applies it: the same choice of files and order of covers.
    /// </summary>
    internal static FileSelectionDetails Describe(TrackFiles track)
    {
        var candidates = Candidates.Of(track);
        TrackFileSelection? audio = SelectionOf(track, FileSelectionCategory.Audio);
        TrackFileSelection? video = SelectionOf(track, FileSelectionCategory.Video);
        TrackFileSelection? cover = SelectionOf(track, FileSelectionCategory.Cover);

        MediaFile? activeAudio = FolderProcessor.ChooseAudio(candidates.Group, audio?.MediaFileId);
        MediaFile? activeVideo = FolderProcessor.ChooseVideo(candidates.Group, video?.MediaFileId);
        IReadOnlyList<CoverCandidate> covers = FolderProcessor.OrderCovers(
            candidates.Group,
            activeAudio,
            cover?.MediaFileId);
        MediaFile[] coverFiles = [.. FolderProcessor.OrderCovers(candidates.Group, activeAudio)
            .Select(candidate => candidate.File)];

        return new FileSelectionDetails(
            track.TrackId,
            Describe(audio, candidates.Audio, activeAudio),
            Describe(video, candidates.Video, activeVideo),
            Describe(cover, coverFiles, covers.Count > 0 ? covers[0].File : null));
    }

    private static CategorySelection Describe(
        TrackFileSelection? selection,
        IReadOnlyList<MediaFile> ordered,
        MediaFile? active)
    {
        FileSelectionState state = selection is null
            ? FileSelectionState.Automatic
            : ordered.Any(file => file.Id == selection.MediaFileId)
                ? FileSelectionState.Selected
                : FileSelectionState.Missing;
        return new CategorySelection(
            state,
            selection?.MediaFileId,
            selection?.RelativePath,
            active?.Id,
            ordered.Count > 0 ? ordered[0].Id : null,
            [.. ordered.Select(file => file.Id)]);
    }

    private static TrackFileSelection? SelectionOf(TrackFiles track, FileSelectionCategory category)
    {
        return track.Selections.FirstOrDefault(selection => selection.Category == category);
    }

    /// <summary>
    /// The files of a track that can be selected per category, in the order of the quality rule. Covers are the
    /// readable picture files and the files with an embedded picture.
    /// </summary>
    private sealed record Candidates(
        FileGroup Group,
        IReadOnlyList<MediaFile> Audio,
        IReadOnlyList<MediaFile> Video,
        IReadOnlyList<MediaFile> Covers)
    {
        public static Candidates Of(TrackFiles track)
        {
            FileGroup group = new(string.Empty, string.Empty, track.Files);
            return new Candidates(
                group,
                [.. group.Audio.Order(AudioQualityComparer.Instance)],
                [.. group.Video.Order(VideoQualityComparer.Instance)],
                [.. FolderProcessor.OrderCovers(group, FolderProcessor.ChooseAudio(group))
                    .Select(candidate => candidate.File)]);
        }

        public IReadOnlyList<MediaFile> Of(FileSelectionCategory category)
        {
            return category switch
            {
                FileSelectionCategory.Audio => Audio,
                FileSelectionCategory.Video => Video,
                _ => Covers,
            };
        }
    }
}

using Microsoft.Extensions.Logging;
using WaveMP.Core.Library.Covers;
using WaveMP.Core.Metadata.Tags;
using WaveMP.Core.OnlineMetadata.Clients;
using WaveMP.Core.OnlineMetadata.Covers;
using WaveMP.Core.Scanning.Edits;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.OnlineMetadata.Updates;

internal sealed class OnlineMetadataApplier(
    ISettingsSnapshot<OnlineMetadataSettings> settings,
    ISettingsSnapshot<LibrarySettings> librarySettings,
    IOnlineMetadataRepository repository,
    ITrackMetadataEditor editor,
    ICoverArtArchiveClient coverArtArchive,
    IOnlineCoverWriter covers,
    ILogger<OnlineMetadataApplier> logger) : IOnlineMetadataApplier
{
    public async Task<TrackApplyResult> ApplyToTrackAsync(
        Guid trackId,
        TagChanges changes,
        Guid? coverReleaseId,
        CancellationToken cancellationToken = default)
    {
        if (!settings.Current.Enabled)
        {
            return TrackApplyResult.Failure(OnlineApplyError.Disabled);
        }

        OnlineTrackRow? track = await repository.FindTrackAsync(trackId, cancellationToken);
        if (track is null)
        {
            return TrackApplyResult.Failure(OnlineApplyError.TrackNotFound);
        }

        (byte[]? picture, OnlineApplyError? coverError) = await DownloadCoverAsync(coverReleaseId, cancellationToken);
        if (coverError is { } failed)
        {
            return TrackApplyResult.Failure(failed);
        }

        TrackMetadata? metadata = null;
        if (!changes.IsEmpty)
        {
            TrackMetadataResult written = await editor.UpdateAsync(trackId, changes, cancellationToken);
            if (!written.IsSuccess)
            {
                return TrackApplyResult.Failure(ToError(written.Error));
            }

            metadata = written.Metadata;
        }

        CoverSaveOutcome cover = CoverSaveOutcome.None;
        if (picture is not null && coverReleaseId is { } releaseId)
        {
            // The song may have a new album now; its cover goes where the cover rules expect it today.
            OnlineTrackRow current = await repository.FindTrackAsync(trackId, CancellationToken.None) ?? track;
            var target = CoverTarget.ForTrack(current, librarySettings.Current.TrackCoverPriority);
            cover = await covers.SaveAsync(target, picture, releaseId, CancellationToken.None);
        }

        metadata ??= await editor.GetAsync(trackId, CancellationToken.None);
        return metadata is null
            ? TrackApplyResult.Failure(OnlineApplyError.TrackNotFound)
            : new TrackApplyResult(null, metadata, cover);
    }

    public async Task<AlbumApplyResult> ApplyToAlbumAsync(
        Guid albumId,
        TagChanges changes,
        Guid? coverReleaseId,
        CancellationToken cancellationToken = default)
    {
        if (!settings.Current.Enabled)
        {
            return AlbumApplyResult.Failure(OnlineApplyError.Disabled);
        }

        OnlineAlbumRow? album = await repository.FindAlbumAsync(albumId, cancellationToken);
        if (album is null)
        {
            return AlbumApplyResult.Failure(OnlineApplyError.AlbumNotFound);
        }

        (byte[]? picture, OnlineApplyError? coverError) = await DownloadCoverAsync(coverReleaseId, cancellationToken);
        if (coverError is { } failed)
        {
            return AlbumApplyResult.Failure(failed);
        }

        TagChanges albumChanges = AlbumFieldsOf(changes);
        int written = 0;
        int skipped = 0;
        if (!albumChanges.IsEmpty)
        {
            // Once the first file is written, the album changes; the other songs follow even if the client is gone.
            foreach (OnlineAlbumTrackRow track in album.Tracks)
            {
                TrackMetadataResult result = await editor.UpdateAsync(
                    track.TrackId,
                    albumChanges,
                    written == 0 ? cancellationToken : CancellationToken.None);
                if (result.IsSuccess)
                {
                    written++;
                }
                else
                {
                    skipped++;
                }
            }
        }

        // A new title or album artist makes the songs another album; the cover belongs to that one.
        Guid currentAlbumId = album.Tracks.Count > 0
            ? await repository.FindAlbumIdOfTrackAsync(album.Tracks[0].TrackId, CancellationToken.None) ?? albumId
            : albumId;
        CoverSaveOutcome cover = CoverSaveOutcome.None;
        if (picture is not null && coverReleaseId is { } releaseId)
        {
            cover = await covers.SaveAsync(
                CoverTarget.ForAlbum(currentAlbumId),
                picture,
                releaseId,
                CancellationToken.None);
        }

        logger.LogInformation(
            "Album {AlbumId} updated from the online search: {Written} songs written, {Skipped} skipped",
            albumId,
            written,
            skipped);
        return new AlbumApplyResult(null, currentAlbumId, written, skipped, cover);
    }

    /// <summary>
    /// The fields of the changes that belong to the album: album, album artist, year and genre.
    /// </summary>
    internal static TagChanges AlbumFieldsOf(TagChanges changes)
    {
        return new TagChanges
        {
            Album = changes.Album,
            AlbumArtist = changes.AlbumArtist,
            Year = changes.Year,
            Genre = changes.Genre,
        };
    }

    /// <summary>
    /// The front cover of the release (1200 pixels, or 500 if the release has no larger one), or the error why it
    /// cannot be applied; nothing without a release.
    /// </summary>
    private async Task<(byte[]? Picture, OnlineApplyError? Error)> DownloadCoverAsync(
        Guid? releaseId,
        CancellationToken cancellationToken)
    {
        if (releaseId is not { } release)
        {
            return (null, null);
        }

        OnlineResponse<byte[]> response =
            await coverArtArchive.GetFrontCoverAsync(release, CoverArtSize.ExtraLarge, cancellationToken);
        if (response.Status == OnlineStatus.NotFound)
        {
            response = await coverArtArchive.GetFrontCoverAsync(release, CoverArtSize.Large, cancellationToken);
        }

        return response.Status switch
        {
            OnlineStatus.Found when response.IsFound && ImageFormats.Detect(response.Value) is not null =>
                (response.Value, null),
            OnlineStatus.NotFound => (null, OnlineApplyError.CoverNotFound),
            OnlineStatus.Disabled => (null, OnlineApplyError.Disabled),
            _ => (null, OnlineApplyError.Unavailable),
        };
    }

    private static OnlineApplyError ToError(TrackMetadataError? error)
    {
        return error switch
        {
            TrackMetadataError.NoAudio => OnlineApplyError.NoAudio,
            TrackMetadataError.NotWritable => OnlineApplyError.NotWritable,
            _ => OnlineApplyError.TrackNotFound,
        };
    }
}

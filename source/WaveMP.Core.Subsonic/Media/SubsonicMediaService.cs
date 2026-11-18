using WaveMP.Core.Caching.Covers;
using WaveMP.Core.Library.Covers;
using WaveMP.Core.Playlists.Covers;
using WaveMP.Core.Playlists.Playlists;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Core.Streaming.Sources;
using WaveMP.Core.Subsonic.Authentication;
using WaveMP.Core.Subsonic.Requests;
using WaveMP.Core.Subsonic.Responses;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Subsonic.Media;

internal sealed class SubsonicMediaService(
    IStreamService streams,
    ISubsonicRepository repository,
    ICoverCacheService covers,
    IPlaylistService playlists,
    IPlaylistCoverService playlistCovers,
    ISettingsSnapshot<LibrarySettings> librarySettings,
    ISettingsSnapshot<TranscodingSettings> transcodingSettings) : ISubsonicMediaService
{
    public async Task<SubsonicStreamResult> PrepareStreamAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (!parameters.Has("id"))
        {
            return SubsonicStreamResult.Failed(SubsonicErrorCode.MissingParameter);
        }

        if (parameters.GetId("id") is not { } trackId)
        {
            return SubsonicStreamResult.Failed(SubsonicErrorCode.NotFound);
        }

        StreamSourceResult result = await streams.GetSoundSourceAsync(trackId, cancellationToken);
        if (!result.IsFound)
        {
            return SubsonicStreamResult.Failed(SubsonicErrorCode.NotFound);
        }

        StreamSource source = result.Source;
        IReadOnlyDictionary<Guid, SubsonicTrackDetails> details =
            await repository.ListTrackDetailsAsync(user.Id, [trackId], cancellationToken);
        SubsonicStreamPlan plan = SubsonicStreamPlanner.Plan(
            parameters.Get("format"),
            parameters.GetInt("maxBitRate"),
            parameters.GetInt("timeOffset"),
            Path.GetExtension(source.FullPath).TrimStart('.'),
            details.GetValueOrDefault(trackId)?.File?.Bitrate,
            source.Origin == MediaFileKind.Video,
            transcodingSettings.Current.AudioBitrateKbps);
        return new SubsonicStreamResult(null, source, plan);
    }

    public async Task<SubsonicStreamResult> PrepareDownloadAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (!parameters.Has("id"))
        {
            return SubsonicStreamResult.Failed(SubsonicErrorCode.MissingParameter);
        }

        if (!librarySettings.Current.SubsonicDownloads)
        {
            return SubsonicStreamResult.Failed(SubsonicErrorCode.NotAuthorized);
        }

        if (parameters.GetId("id") is not { } trackId)
        {
            return SubsonicStreamResult.Failed(SubsonicErrorCode.NotFound);
        }

        StreamSourceResult result = await streams.GetSoundSourceAsync(trackId, cancellationToken);
        return result.IsFound
            ? new SubsonicStreamResult(null, result.Source, SubsonicStreamPlan.Direct)
            : SubsonicStreamResult.Failed(SubsonicErrorCode.NotFound);
    }

    public async Task<SubsonicCoverResult> GetCoverAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (!parameters.Has("id"))
        {
            return new SubsonicCoverResult(SubsonicErrorCode.MissingParameter, null);
        }

        if (SubsonicIds.ParseCover(parameters.Get("id")) is not { } cover)
        {
            return new SubsonicCoverResult(SubsonicErrorCode.NotFound, null);
        }

        int? size = parameters.GetInt("size") is { } requested and > 0
            ? Math.Min(requested, CoverSizes.MaxRequested)
            : null;
        CoverDeliveryResult result = CoverDeliveryResult.OwnerNotFound;
        if (cover.Owner is SubsonicCoverOwner.Track or SubsonicCoverOwner.Any)
        {
            result = await covers.GetTrackCoverAsync(cover.Id, size, cancellationToken);
        }

        if (result.Status == CoverStatus.OwnerNotFound
            && cover.Owner is SubsonicCoverOwner.Album or SubsonicCoverOwner.Any)
        {
            result = await covers.GetAlbumCoverAsync(cover.Id, size, cancellationToken);
        }

        if (result.Status == CoverStatus.OwnerNotFound
            && cover.Owner is SubsonicCoverOwner.Playlist or SubsonicCoverOwner.Any)
        {
            result = await GetPlaylistCoverAsync(user.Id, cover.Id, size, cancellationToken);
        }

        return result is { Status: CoverStatus.Found, Delivery: { } delivery }
            ? new SubsonicCoverResult(null, delivery)
            : new SubsonicCoverResult(SubsonicErrorCode.NotFound, null);
    }

    /// <summary>
    /// The own cover of the playlist, otherwise the cover of its first track with a cover.
    /// </summary>
    private async Task<CoverDeliveryResult> GetPlaylistCoverAsync(
        Guid userId,
        Guid playlistId,
        int? size,
        CancellationToken cancellationToken)
    {
        PlaylistItem? playlist = await playlists.GetAsync(userId, playlistId, cancellationToken);
        if (playlist is null)
        {
            return CoverDeliveryResult.OwnerNotFound;
        }

        if (playlist.Cover.Kind == PlaylistCoverKind.Custom)
        {
            return await playlistCovers.GetAsync(userId, playlistId, size, cancellationToken);
        }

        foreach (Guid trackId in playlist.Cover.TrackIds)
        {
            CoverDeliveryResult track = await covers.GetTrackCoverAsync(trackId, size, cancellationToken);
            if (track.Status == CoverStatus.Found)
            {
                return track;
            }
        }

        return CoverDeliveryResult.NotFound;
    }
}

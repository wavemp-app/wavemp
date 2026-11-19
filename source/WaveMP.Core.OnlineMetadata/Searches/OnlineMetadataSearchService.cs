using Microsoft.Extensions.Options;
using WaveMP.Core.Library.Covers;
using WaveMP.Core.OnlineMetadata.Clients;
using WaveMP.Core.OnlineMetadata.Matches;
using WaveMP.Core.OnlineMetadata.Options;
using WaveMP.Core.Scanning.Edits;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.OnlineMetadata.Searches;

internal sealed class OnlineMetadataSearchService(
    ISettingsSnapshot<OnlineMetadataSettings> settings,
    ITrackMetadataEditor editor,
    ITrackRepository tracks,
    IAlbumRepository albums,
    IOnlineMetadataRepository repository,
    IMusicBrainzClient musicBrainz,
    ICoverArtArchiveClient coverArtArchive,
    IOptions<OnlineMetadataOptions> options) : IOnlineMetadataSearchService
{
    public async Task<TrackSearchResult> SearchTrackAsync(Guid trackId, CancellationToken cancellationToken = default)
    {
        if (!settings.Current.Enabled)
        {
            return TrackSearchResult.Failure(OnlineSearchStatus.Disabled);
        }

        TrackRow? track = await tracks.FindAsync(trackId, cancellationToken: cancellationToken);
        TrackMetadata? current = track is null ? null : await editor.GetAsync(trackId, cancellationToken);
        if (track is null || current is null)
        {
            return TrackSearchResult.Failure(OnlineSearchStatus.NotFound);
        }

        TrackLookup lookup = new(current.Title, current.Artist, current.Album, track.Duration);
        OnlineResponse<IReadOnlyList<MusicBrainzRecording>> response = await musicBrainz.SearchRecordingsAsync(
            MusicBrainzQuery.Recording(lookup.Title, lookup.Artist, lookup.Album, lookup.Duration),
            cancellationToken);
        if (!response.IsFound)
        {
            return TrackSearchResult.Failure(ToStatus(response.Status));
        }

        return new TrackSearchResult(
            OnlineSearchStatus.Searched,
            current,
            RankTracks(lookup, response.Value, options.Value));
    }

    public async Task<AlbumSearchResult> SearchAlbumAsync(Guid albumId, CancellationToken cancellationToken = default)
    {
        if (!settings.Current.Enabled)
        {
            return AlbumSearchResult.Failure(OnlineSearchStatus.Disabled);
        }

        AlbumRow? album = await albums.FindAsync(albumId, cancellationToken: cancellationToken);
        OnlineAlbumRow? content = album is null ? null : await repository.FindAlbumAsync(albumId, cancellationToken);
        if (album is null || content is null)
        {
            return AlbumSearchResult.Failure(OnlineSearchStatus.NotFound);
        }

        AlbumLookup lookup = new(album.Title, album.ArtistName, album.TrackCount);
        OnlineResponse<IReadOnlyList<MusicBrainzRelease>> response = await musicBrainz.SearchReleasesAsync(
            MusicBrainzQuery.Release(lookup.Album, lookup.Artist, lookup.TrackCount),
            cancellationToken);
        if (!response.IsFound)
        {
            return AlbumSearchResult.Failure(ToStatus(response.Status));
        }

        int editable = 0;
        foreach (OnlineAlbumTrackRow track in content.Tracks)
        {
            TrackMetadata? metadata = await editor.GetAsync(track.TrackId, cancellationToken);
            if (metadata?.Status == TrackMetadataStatus.Editable)
            {
                editable++;
            }
        }

        AlbumCurrent current = new(
            new OnlineAlbumValues(album.Title, album.ArtistName, album.Year, album.Genre),
            content.Tracks.Count,
            editable);
        return new AlbumSearchResult(
            OnlineSearchStatus.Searched,
            current,
            RankAlbums(lookup, response.Value, options.Value));
    }

    public async Task<OnlineCoverPreview> GetCoverAsync(
        Guid releaseId,
        CoverArtSize size,
        CancellationToken cancellationToken = default)
    {
        if (!settings.Current.Enabled)
        {
            return new OnlineCoverPreview(OnlineStatus.Disabled, null, null);
        }

        OnlineResponse<byte[]> response = await coverArtArchive.GetFrontCoverAsync(releaseId, size, cancellationToken);
        if (!response.IsFound)
        {
            return new OnlineCoverPreview(response.Status, null, null);
        }

        string? contentType = ImageFormats.Detect(response.Value);
        return contentType is null
            ? new OnlineCoverPreview(OnlineStatus.Unavailable, null, null)
            : new OnlineCoverPreview(OnlineStatus.Found, response.Value, contentType);
    }

    /// <summary>
    /// The matches of a song: per recording its releases that agree best (at most
    /// <see cref="OnlineMetadataOptions.MaxReleasesPerRecording"/>), then all of them by agreement, relevance of the
    /// search and date (the earliest release first), at most <see cref="OnlineMetadataOptions.MaxMatches"/>.
    /// </summary>
    internal static IReadOnlyList<TrackMatch> RankTracks(
        TrackLookup lookup,
        IReadOnlyList<MusicBrainzRecording> recordings,
        OnlineMetadataOptions options)
    {
        return [.. recordings
            .SelectMany(recording => MusicBrainzMapper.ForRecording(recording)
                .Select(suggestion => (
                    Recording: recording,
                    Match: new TrackMatch(
                        recording.Id,
                        suggestion.Release,
                        MatchScorer.ForTrack(lookup, suggestion.Values, suggestion.Duration),
                        suggestion.Duration,
                        suggestion.Values)))
                .OrderByDescending(item => item.Match.Agreement)
                .ThenBy(item => item.Match.Release?.Date ?? "9999", StringComparer.Ordinal)
                .Take(Math.Max(1, options.MaxReleasesPerRecording)))
            .OrderByDescending(item => item.Match.Agreement)
            .ThenByDescending(item => item.Recording.Score)
            .ThenBy(item => item.Match.Release?.Date ?? "9999", StringComparer.Ordinal)
            .Select(item => item.Match)
            .Take(Math.Max(1, options.MaxMatches))];
    }

    /// <summary>
    /// The matches of an album by agreement, relevance of the search and date (the earliest release first), at most
    /// <see cref="OnlineMetadataOptions.MaxMatches"/>.
    /// </summary>
    internal static IReadOnlyList<AlbumMatch> RankAlbums(
        AlbumLookup lookup,
        IReadOnlyList<MusicBrainzRelease> releases,
        OnlineMetadataOptions options)
    {
        return [.. releases
            .Select(release =>
            {
                (OnlineRelease online, OnlineAlbumValues values) = MusicBrainzMapper.ForRelease(release);
                return (
                    Score: release.Score ?? 0,
                    Match: new AlbumMatch(online, MatchScorer.ForAlbum(lookup, values, release.TrackCount), values));
            })
            .OrderByDescending(item => item.Match.Agreement)
            .ThenByDescending(item => item.Score)
            .ThenBy(item => item.Match.Release.Date ?? "9999", StringComparer.Ordinal)
            .Select(item => item.Match)
            .Take(Math.Max(1, options.MaxMatches))];
    }

    private static OnlineSearchStatus ToStatus(OnlineStatus status)
    {
        return status == OnlineStatus.Disabled ? OnlineSearchStatus.Disabled : OnlineSearchStatus.Unavailable;
    }
}

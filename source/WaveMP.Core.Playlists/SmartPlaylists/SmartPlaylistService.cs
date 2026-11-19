using Microsoft.Extensions.Options;
using WaveMP.Core.Playlists.Covers;
using WaveMP.Core.Playlists.Names;
using WaveMP.Core.Playlists.Options;
using WaveMP.Core.Playlists.Playlists;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Playlists.SmartPlaylists;

internal sealed class SmartPlaylistService(
    ISmartPlaylistRepository smartPlaylists,
    ITrackRepository tracks,
    IPlaylistService playlists,
    TimeProvider timeProvider,
    IOptions<PlaylistOptions> options) : ISmartPlaylistService
{
    /// <summary>
    /// How often a conversion is tried again when a track of the smart playlist disappeared in the meantime.
    /// </summary>
    private const int ConversionAttempts = 3;

    public async Task<Page<SmartPlaylistItem>> ListAsync(
        PlaylistListQuery query,
        CancellationToken cancellationToken = default)
    {
        Page<SmartPlaylist> page = await smartPlaylists.ListAsync(query, cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        List<SmartPlaylistItem> items = [];
        foreach (SmartPlaylist playlist in page.Items)
        {
            items.Add(await ItemOfAsync(playlist, now, cancellationToken));
        }

        return new Page<SmartPlaylistItem>(items, page.Offset, page.Limit, page.Total);
    }

    public async Task<SmartPlaylistItem?> GetAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        SmartPlaylist? playlist = await smartPlaylists.FindAsync(userId, id, cancellationToken);
        return playlist is null ? null : await ItemOfAsync(playlist, timeProvider.GetUtcNow(), cancellationToken);
    }

    public async Task<SmartPlaylistResult> CreateAsync(
        Guid userId,
        SmartPlaylistDefinition definition,
        CancellationToken cancellationToken = default)
    {
        SmartPlaylistValidation validation = SmartPlaylistRules.Validate(definition);
        if (!validation.IsValid)
        {
            return SmartPlaylistResult.Invalid(validation.Errors);
        }

        DateTimeOffset now = TruncateToMicroseconds(timeProvider.GetUtcNow());
        SmartPlaylist playlist = ToEntity(Guid.CreateVersion7(now), userId, validation, now, now);
        await smartPlaylists.CreateAsync(playlist, cancellationToken);
        return await ResultOfAsync(userId, playlist.Id, cancellationToken);
    }

    public async Task<SmartPlaylistResult> UpdateAsync(
        Guid userId,
        Guid id,
        SmartPlaylistDefinition definition,
        CancellationToken cancellationToken = default)
    {
        SmartPlaylistValidation validation = SmartPlaylistRules.Validate(definition);
        if (!validation.IsValid)
        {
            return SmartPlaylistResult.Invalid(validation.Errors);
        }

        DateTimeOffset now = TruncateToMicroseconds(timeProvider.GetUtcNow());

        // The time of creation is not changed by the update.
        bool updated = await smartPlaylists.UpdateAsync(ToEntity(id, userId, validation, now, now), cancellationToken);
        return updated
            ? await ResultOfAsync(userId, id, cancellationToken)
            : SmartPlaylistResult.Of(SmartPlaylistStatus.NotFound);
    }

    public Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        return smartPlaylists.DeleteAsync(userId, id, cancellationToken);
    }

    public async Task<Page<TrackRow>?> ListTracksAsync(
        Guid userId,
        Guid id,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        SmartPlaylist? playlist = await smartPlaylists.FindAsync(userId, id, cancellationToken);
        if (playlist is null)
        {
            return null;
        }

        Page<Guid> page = await smartPlaylists.ListTrackIdsAsync(
            userId,
            SmartPlaylistCriteria.Of(playlist),
            timeProvider.GetUtcNow(),
            offset,
            limit,
            cancellationToken);
        IReadOnlyList<TrackRow> rows = await tracks.ListByIdsAsync(page.Items, userId, cancellationToken);
        var byId = rows.ToDictionary(row => row.Id);

        // A track removed by a scan between the two queries is left out.
        List<TrackRow> items = [.. page.Items.Where(byId.ContainsKey).Select(trackId => byId[trackId])];
        return new Page<TrackRow>(items, page.Offset, page.Limit, page.Total);
    }

    public async Task<SmartPlaylistConversion> ConvertAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        SmartPlaylist? playlist = await smartPlaylists.FindAsync(userId, id, cancellationToken);
        if (playlist is null)
        {
            return SmartPlaylistConversion.Of(SmartPlaylistStatus.NotFound);
        }

        int maxEntries = options.Value.MaxEntries;
        var criteria = SmartPlaylistCriteria.Of(playlist);
        for (int attempt = 0; attempt < ConversionAttempts; attempt++)
        {
            // One more than allowed, to know whether the playlist would be too long.
            Page<Guid> trackIds = await smartPlaylists.ListTrackIdsAsync(
                userId,
                criteria,
                timeProvider.GetUtcNow(),
                0,
                maxEntries + 1,
                cancellationToken);
            if (trackIds.Total > maxEntries)
            {
                return SmartPlaylistConversion.Of(SmartPlaylistStatus.Full, maxEntries);
            }

            PlaylistResult created = await playlists.CreateAsync(
                userId,
                playlist.Name,
                trackIds.Items,
                cancellationToken);
            if (created.Status == PlaylistStatus.TrackNotFound)
            {
                // A scan removed a track in the meantime; the list is computed again.
                continue;
            }

            if (!created.IsOk)
            {
                return created.Status switch
                {
                    PlaylistStatus.Full => SmartPlaylistConversion.Of(SmartPlaylistStatus.Full, maxEntries),
                    // The profile was deleted in the meantime.
                    PlaylistStatus.NotFound => SmartPlaylistConversion.Of(SmartPlaylistStatus.NotFound),
                    _ => SmartPlaylistConversion.Of(SmartPlaylistStatus.Invalid),
                };
            }

            await smartPlaylists.DeleteAsync(userId, id, cancellationToken);
            return new SmartPlaylistConversion(SmartPlaylistStatus.Ok, created.Item);
        }

        return SmartPlaylistConversion.Of(SmartPlaylistStatus.LibraryChanged);
    }

    private async Task<SmartPlaylistItem> ItemOfAsync(
        SmartPlaylist playlist,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        PlaylistOptions settings = options.Value;
        var criteria = SmartPlaylistCriteria.Of(playlist);
        SmartPlaylistSummary summary = await smartPlaylists.SummarizeAsync(
            playlist.UserId,
            criteria,
            now,
            cancellationToken);
        IReadOnlyList<PlaylistCoverCandidateRow> candidates = summary.TrackCount == 0
            ? []
            : await smartPlaylists.ListCoverCandidatesAsync(
                playlist.Id,
                playlist.UserId,
                criteria,
                now,
                settings.CoverScanEntries,
                cancellationToken);
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> picked = PlaylistCoverPicker.Pick(
            candidates,
            settings.CoverCount);
        PlaylistCover cover = picked.TryGetValue(playlist.Id, out IReadOnlyList<Guid>? trackIds) && trackIds.Count > 0
            ? new PlaylistCover(PlaylistCoverKind.Tracks, null, trackIds)
            : PlaylistCover.None;
        return new SmartPlaylistItem(playlist, summary, cover);
    }

    private async Task<SmartPlaylistResult> ResultOfAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        SmartPlaylistItem? item = await GetAsync(userId, id, cancellationToken);
        return item is null
            ? SmartPlaylistResult.Of(SmartPlaylistStatus.NotFound)
            : new SmartPlaylistResult(SmartPlaylistStatus.Ok, item, []);
    }

    private static SmartPlaylist ToEntity(
        Guid id,
        Guid userId,
        SmartPlaylistValidation validation,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        SmartPlaylistCriteria criteria = validation.Criteria;
        return new SmartPlaylist
        {
            Id = id,
            UserId = userId,
            Name = validation.Name,
            NormalizedName = PlaylistNames.ToNormalized(validation.Name),
            Match = criteria.Match,
            Rules = [.. criteria.Rules],
            Sort = criteria.Sort,
            Direction = criteria.Direction,
            Limit = criteria.Limit,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
        };
    }

    /// <summary>
    /// PostgreSQL keeps microseconds; times are created with the precision they are read with later.
    /// </summary>
    private static DateTimeOffset TruncateToMicroseconds(DateTimeOffset value)
    {
        return new DateTimeOffset(value.Ticks - (value.Ticks % TimeSpan.TicksPerMicrosecond), value.Offset);
    }
}

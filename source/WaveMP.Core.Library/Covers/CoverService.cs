using System.Runtime.CompilerServices;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Library.Covers;

internal sealed class CoverService(
    ICoverRepository covers,
    ISettingsSnapshot<LibrarySettings> settings) : ICoverService
{
    internal const int AlbumBatchSize = 200;
    internal const int TrackBatchSize = 500;

    public async Task<CoverCandidates> GetTrackCandidatesAsync(
        Guid trackId,
        CancellationToken cancellationToken = default)
    {
        return await TrackCandidatesAsync(trackId, preview: false, rule: null, cancellationToken);
    }

    public async Task<CoverCandidates> GetAlbumCandidatesAsync(
        Guid albumId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CoverCandidateRow>? candidates = await covers.GetAlbumCandidatesAsync(albumId, cancellationToken);
        AlbumCoverFallbackRow? fallback = candidates is null
            ? null
            : await covers.GetAlbumCoverFallbackAsync(albumId, cancellationToken);
        if (candidates is null || fallback is null)
        {
            return CoverCandidates.OwnerNotFound;
        }

        bool showsArtist = CoverRules.EffectiveFallback(settings.Current.AlbumCoverFallback, fallback.Fallback)
            == AlbumCoverFallback.ArtistPicture;
        return new CoverCandidates(
            true,
            CoverResolver.OrderForAlbum(candidates),
            showsArtist ? fallback.AlbumArtistId : null);
    }

    public async Task<CoverCandidates> PreviewTrackCandidatesAsync(
        Guid trackId,
        CoverPriority? rule,
        CancellationToken cancellationToken = default)
    {
        return await TrackCandidatesAsync(trackId, preview: true, rule, cancellationToken);
    }

    public async Task<CoverCandidates> PreviewAlbumCandidatesAsync(
        Guid albumId,
        CoverPriority? rule,
        CancellationToken cancellationToken = default)
    {
        // The rule of the songs does not change the cover of the album itself.
        return await GetAlbumCandidatesAsync(albumId, cancellationToken);
    }

    public async IAsyncEnumerable<IReadOnlyList<CoverCandidateRow>> EnumerateShownCoversAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        CoverPriority global = Global();
        HashSet<string> seen = new(StringComparer.Ordinal);

        IReadOnlyList<Guid> albumIds = await covers.ListAlbumIdsWithCoversAsync(cancellationToken);
        foreach (Guid[] batch in albumIds.Chunk(AlbumBatchSize))
        {
            IReadOnlyList<CoverCandidateRow> candidates =
                await covers.GetAlbumBatchCandidatesAsync(batch, cancellationToken);
            foreach (IGrouping<Guid?, CoverCandidateRow> album in candidates
                .GroupBy(candidate => candidate.AlbumId ?? candidate.TrackAlbumId)
                .OrderBy(album => album.Key))
            {
                foreach (IReadOnlyList<CoverCandidateRow> list in CoverChoices.ForAlbum(global, album))
                {
                    if (seen.Add(CoverChoices.KeyOf(list)))
                    {
                        yield return list;
                    }
                }
            }
        }

        IReadOnlyList<Guid> trackIds = await covers.ListLooseTrackIdsWithCoversAsync(cancellationToken);
        foreach (Guid[] batch in trackIds.Chunk(TrackBatchSize))
        {
            IReadOnlyList<CoverCandidateRow> candidates =
                await covers.GetTrackBatchCandidatesAsync(batch, cancellationToken);
            foreach (IGrouping<Guid?, CoverCandidateRow> track in candidates
                .GroupBy(candidate => candidate.TrackId)
                .OrderBy(track => track.Key))
            {
                IReadOnlyList<CoverCandidateRow> list =
                    CoverChoices.ForTrack(track.Key ?? Guid.Empty, global, track);
                if (list.Count > 0 && seen.Add(CoverChoices.KeyOf(list)))
                {
                    yield return list;
                }
            }
        }
    }

    /// <summary>
    /// The covers of a track by the rule that applies to it, or by the rule to try for a preview (<c>null</c> inherits
    /// the rule of its album). The rules are read on their own, since a track without covers of its own carries none.
    /// </summary>
    private async Task<CoverCandidates> TrackCandidatesAsync(
        Guid trackId,
        bool preview,
        CoverPriority? rule,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CoverCandidateRow>? candidates = await covers.GetTrackCandidatesAsync(trackId, cancellationToken);
        CoverRuleRow? rules = candidates is null ? null : await covers.GetTrackRuleAsync(trackId, cancellationToken);
        if (candidates is null || rules is null)
        {
            return CoverCandidates.OwnerNotFound;
        }

        CoverPriority priority = CoverRules.Effective(Global(), rules.AlbumRule, preview ? rule : rules.Rule);
        return new CoverCandidates(true, CoverResolver.OrderForTrack(trackId, priority, candidates));
    }

    /// <summary>
    /// The global rule of the songs; albums and tracks may override it with rules of their own, which travel with their
    /// covers.
    /// </summary>
    private CoverPriority Global()
    {
        return settings.Current.TrackCoverPriority;
    }
}

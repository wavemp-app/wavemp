using Microsoft.Extensions.Logging;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Library.Covers;

internal sealed class CoverRuleService(
    ICoverRepository covers,
    ISettingsSnapshot<LibrarySettings> settings,
    IEnumerable<ICoverRuleChangeHandler> handlers,
    ILogger<CoverRuleService> logger) : ICoverRuleService
{
    public async Task<CoverRuleResult> GetAlbumRuleAsync(Guid albumId, CancellationToken cancellationToken = default)
    {
        CoverRuleRow? row = await covers.GetAlbumRuleAsync(albumId, cancellationToken);
        return row is null ? CoverRuleResult.NotFound : new CoverRuleResult(true, row.Rule, Global());
    }

    public async Task<CoverRuleResult> SetAlbumRuleAsync(
        Guid albumId,
        CoverPriority? rule,
        CancellationToken cancellationToken = default)
    {
        CoverRuleResult current = await GetAlbumRuleAsync(albumId, cancellationToken);
        if (!current.IsFound)
        {
            return current;
        }

        // A scan may have removed the album in the meantime.
        if (!await covers.SetAlbumRuleAsync(albumId, rule, cancellationToken))
        {
            return CoverRuleResult.NotFound;
        }

        await NotifyAsync(new CoverRuleChange(CoverRuleLevel.Album, albumId, current.Rule, rule), cancellationToken);
        return current with { Rule = rule };
    }

    public async Task<CoverRuleResult> GetTrackRuleAsync(Guid trackId, CancellationToken cancellationToken = default)
    {
        CoverRuleRow? row = await covers.GetTrackRuleAsync(trackId, cancellationToken);
        return row is null
            ? CoverRuleResult.NotFound
            : new CoverRuleResult(true, row.Rule, CoverRules.Effective(Global(), row.AlbumRule, track: null));
    }

    public async Task<CoverRuleResult> SetTrackRuleAsync(
        Guid trackId,
        CoverPriority? rule,
        CancellationToken cancellationToken = default)
    {
        CoverRuleResult current = await GetTrackRuleAsync(trackId, cancellationToken);
        if (!current.IsFound)
        {
            return current;
        }

        if (!await covers.SetTrackRuleAsync(trackId, rule, cancellationToken))
        {
            return CoverRuleResult.NotFound;
        }

        await NotifyAsync(new CoverRuleChange(CoverRuleLevel.Track, trackId, current.Rule, rule), cancellationToken);
        return current with { Rule = rule };
    }

    private CoverPriority Global()
    {
        return settings.Current.CoverPriority;
    }

    /// <summary>
    /// Tells the handlers about a change; setting the same rule again changes nothing.
    /// </summary>
    private async Task NotifyAsync(CoverRuleChange change, CancellationToken cancellationToken)
    {
        if (change.Previous == change.Current)
        {
            return;
        }

        foreach (ICoverRuleChangeHandler handler in handlers)
        {
            try
            {
                await handler.OnCoverRuleChangedAsync(change, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(
                    exception,
                    "{Handler} failed to apply the changed cover rule of the {Level} {OwnerId}",
                    handler.GetType().Name,
                    change.Level,
                    change.OwnerId);
            }
        }
    }
}

using WaveMP.Core.Autoplay.Selections;
using WaveMP.Core.Users.Settings;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Autoplay.Services;

internal sealed class AutoplayService(
    IUserSettingsService settings,
    IAutoplaySelector selector,
    ITrackRepository tracks) : IAutoplayService
{
    public async Task<IReadOnlyList<TrackRow>?> SelectAsync(
        Guid userId,
        AutoplayCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.Count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(command.Count, AutoplayLimits.MaxCount);

        UserSettings? userSettings = await settings.GetAsync(userId, cancellationToken);
        if (userSettings is null)
        {
            return null;
        }

        IReadOnlyList<Guid> list = command.ListTrackIds.Count > AutoplayLimits.MaxListTracks
            ? [.. command.ListTrackIds.TakeLast(AutoplayLimits.MaxListTracks)]
            : command.ListTrackIds;
        IReadOnlyList<Guid> chosen = await selector.SelectAsync(
            new AutoplayRequest(
                userId,
                list,
                command.ExcludedTrackIds,
                command.Count,
                userSettings.AutoplayIncludeVideoOnly),
            cancellationToken);
        if (chosen.Count == 0)
        {
            return [];
        }

        // In the order of the choice; tracks removed in the meantime are left out.
        var rows = (await tracks.ListByIdsAsync(chosen, userId, cancellationToken))
            .ToDictionary(row => row.Id);
        return [.. chosen.Where(rows.ContainsKey).Select(id => rows[id])];
    }
}

using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Keeps the references of the rules of smart playlists (see <see cref="SmartPlaylistRule.ReferenceId"/>) valid when
/// artists, albums or genres are joined or removed: a rule that names a removed entry names its successor, with the
/// name of the successor as text, or else keeps its text and loses the reference, so it compares the name again. The
/// change time of the smart playlists stays, since their owners changed nothing.
/// </summary>
internal static class SmartPlaylistReferences
{
    /// <param name="dbContext">The context, in the transaction of the caller.</param>
    /// <param name="field">The field of the rules whose references change.</param>
    /// <param name="removed">The removed artists, albums or genres.</param>
    /// <param name="successors">The successor of each removed entry that has one.</param>
    /// <param name="cancellationToken">Cancels the change.</param>
    public static async Task RelinkAsync(
        WaveMpDbContext dbContext,
        SmartPlaylistField field,
        IReadOnlyCollection<Guid> removed,
        IReadOnlyDictionary<Guid, Guid> successors,
        CancellationToken cancellationToken)
    {
        if (removed.Count == 0)
        {
            return;
        }

        HashSet<Guid> gone = [.. removed];
        List<(Guid Id, List<SmartPlaylistRule> Rules)> affected = [.. (await dbContext.SmartPlaylists.AsNoTracking()
                .Select(playlist => new { playlist.Id, playlist.Rules })
                .ToListAsync(cancellationToken))
            .Where(playlist => playlist.Rules.Any(rule =>
                rule.Field == field && rule.ReferenceId is { } id && gone.Contains(id)))
            .Select(playlist => (playlist.Id, playlist.Rules))];
        if (affected.Count == 0)
        {
            return;
        }

        Guid[] successorIds = [.. successors.Values.Distinct()];
        Dictionary<Guid, string> names = field switch
        {
            SmartPlaylistField.Artist => await dbContext.Artists.AsNoTracking()
                .Where(artist => successorIds.Contains(artist.Id))
                .ToDictionaryAsync(artist => artist.Id, artist => artist.Name, cancellationToken),
            SmartPlaylistField.Album => await dbContext.Albums.AsNoTracking()
                .Where(album => successorIds.Contains(album.Id))
                .ToDictionaryAsync(album => album.Id, album => album.Title, cancellationToken),
            _ => await dbContext.Genres.AsNoTracking()
                .Where(genre => successorIds.Contains(genre.Id))
                .ToDictionaryAsync(genre => genre.Id, genre => genre.Name, cancellationToken),
        };

        foreach ((Guid playlistId, List<SmartPlaylistRule> rules) in affected)
        {
            List<SmartPlaylistRule> relinked = [.. rules.Select(rule =>
            {
                if (rule.Field != field || rule.ReferenceId is not { } id || !gone.Contains(id))
                {
                    return rule;
                }

                return successors.TryGetValue(id, out Guid successor)
                    && names.TryGetValue(successor, out string? name)
                        ? rule with { ReferenceId = successor, Text = name }
                        : rule with { ReferenceId = null };
            })];
            await dbContext.SmartPlaylists
                .Where(playlist => playlist.Id == playlistId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(playlist => playlist.Rules, relinked),
                    cancellationToken);
        }
    }
}

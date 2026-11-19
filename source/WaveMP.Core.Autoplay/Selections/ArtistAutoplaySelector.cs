using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Autoplay.Selections;

/// <summary>
/// Chooses the songs of autoplay by the artists of the list: an album of one artist leads to more songs of that
/// artist, a list of several artists to songs of all of them, weighted by how often each one appears. If their songs
/// are not enough, songs of the same genres and similar decades follow. See <see cref="AutoplayPicker"/> for the
/// weights and the songs that are left out.
/// </summary>
internal sealed class ArtistAutoplaySelector : IAutoplaySelector
{
    private readonly IAutoplayRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly Random _random;

    public ArtistAutoplaySelector(IAutoplayRepository repository, TimeProvider timeProvider)
        : this(repository, timeProvider, Random.Shared)
    {
    }

    internal ArtistAutoplaySelector(IAutoplayRepository repository, TimeProvider timeProvider, Random random)
    {
        _repository = repository;
        _timeProvider = timeProvider;
        _random = random;
    }

    public async Task<IReadOnlyList<Guid>> SelectAsync(
        AutoplayRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Count <= 0 || request.ListTrackIds.Count == 0)
        {
            return [];
        }

        IReadOnlyList<AutoplaySeedRow> seeds = await _repository.ListSeedsAsync(request.ListTrackIds, cancellationToken);
        if (seeds.Count == 0)
        {
            return [];
        }

        // The songs of the list never come again; the recently played ones only when nothing else is left.
        HashSet<Guid> excluded = [.. request.ListTrackIds, .. request.ExcludedTrackIds];
        IReadOnlyDictionary<Guid, DateTimeOffset> recentPlays = await _repository.ListRecentPlaysAsync(
            request.UserId,
            _timeProvider.GetUtcNow() - AutoplayRules.RecentlyPlayedWindow,
            cancellationToken);

        Guid[] artistIds = [.. seeds.Where(seed => seed.ArtistId is not null).Select(seed => seed.ArtistId!.Value)
            .Distinct()];
        IReadOnlyList<AutoplayCandidateRow> artistCandidates = artistIds.Length == 0
            ? []
            : await _repository.ListCandidatesAsync(
                new AutoplayCandidateQuery(
                    request.UserId,
                    AutoplayRules.MaxArtistCandidates,
                    request.IncludeVideoOnly,
                    excluded,
                    ArtistIds: artistIds),
                cancellationToken);

        IReadOnlyList<AutoplayCandidateRow> fallbackCandidates = [];
        int freshArtistSongs = artistCandidates.Count(candidate => !recentPlays.ContainsKey(candidate.TrackId));
        string[] genres = [.. seeds
            .Where(seed => !string.IsNullOrWhiteSpace(seed.Genre))
            .Select(seed => seed.Genre!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)];
        if (freshArtistSongs < request.Count && genres.Length > 0)
        {
            int[] years = [.. seeds.Where(seed => seed.Year is not null).Select(seed => seed.Year!.Value)];
            fallbackCandidates = await _repository.ListCandidatesAsync(
                new AutoplayCandidateQuery(
                    request.UserId,
                    AutoplayRules.MaxFallbackCandidates,
                    request.IncludeVideoOnly,
                    excluded,
                    Genres: genres,
                    Decades: years.Length == 0 ? null : [.. AutoplayRules.SimilarDecades(years)]),
                cancellationToken);
        }

        return AutoplayPicker.Pick(
            seeds,
            artistCandidates,
            fallbackCandidates,
            recentPlays,
            request.Count,
            _random);
    }
}

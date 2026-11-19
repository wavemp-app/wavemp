using WaveMP.Data.Projections;

namespace WaveMP.Core.Autoplay.Selections;

/// <summary>
/// Chooses the songs of autoplay from the candidates, in random order:
/// <list type="number">
/// <item>Songs of the artists of the list. Each song first draws an artist, weighted by how often the artist appears in
/// the list, then one of its songs, weighted by favorite and rating (see <see cref="AutoplayRules.WeightOf"/>).</item>
/// <item>If they are not enough: songs of the same genres and similar decades (the fallback), weighted by how often
/// their genre appears in the list and by favorite and rating.</item>
/// <item>Songs the user played recently are left out. Only if no other song matches, they come back, those played
/// longest ago first.</item>
/// </list>
/// The candidates never contain songs of the list, excluded songs or songs rated with 1 star; songs with the weight 0
/// are left out anyway.
/// </summary>
internal static class AutoplayPicker
{
    public static IReadOnlyList<Guid> Pick(
        IReadOnlyList<AutoplaySeedRow> seeds,
        IReadOnlyList<AutoplayCandidateRow> artistCandidates,
        IReadOnlyList<AutoplayCandidateRow> fallbackCandidates,
        IReadOnlyDictionary<Guid, DateTimeOffset> recentPlays,
        int count,
        Random random)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var artistWeights = seeds
            .Where(seed => seed.ArtistId is not null)
            .GroupBy(seed => seed.ArtistId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());
        var genreWeights = seeds
            .Where(seed => !string.IsNullOrWhiteSpace(seed.Genre))
            .GroupBy(seed => Normalize(seed.Genre!))
            .ToDictionary(group => group.Key, group => group.Count());

        List<AutoplayCandidateRow> artistPool = [.. Distinct(artistCandidates)
            .Where(candidate => candidate.ArtistId is { } artist && artistWeights.ContainsKey(artist))];
        HashSet<Guid> artistIds = [.. artistPool.Select(candidate => candidate.TrackId)];
        List<AutoplayCandidateRow> fallbackPool = [.. Distinct(fallbackCandidates)
            .Where(candidate => !artistIds.Contains(candidate.TrackId))];

        bool IsFresh(AutoplayCandidateRow candidate) => !recentPlays.ContainsKey(candidate.TrackId);

        List<Guid> picked = [];
        PickByArtist([.. artistPool.Where(IsFresh)], artistWeights, count, random, picked);
        PickWeighted(
            [.. fallbackPool.Where(IsFresh)],
            candidate => GenreWeight(candidate, genreWeights),
            count,
            random,
            picked);

        if (picked.Count == 0)
        {
            // Nothing new is left: the songs played longest ago come back, at most half of them (but enough for the
            // request), so the songs of the last hours rest a while longer.
            List<AutoplayCandidateRow> rested = [.. artistPool.Concat(fallbackPool)
                .Where(candidate => !IsFresh(candidate))
                .OrderBy(candidate => recentPlays[candidate.TrackId])
                .ThenBy(candidate => candidate.TrackId)];
            int take = Math.Max(count, (rested.Count + 1) / 2);
            PickWeighted(
                [.. rested.Take(take)],
                candidate => candidate.ArtistId is { } artist && artistWeights.TryGetValue(artist, out int weight)
                    ? weight
                    : GenreWeight(candidate, genreWeights),
                count,
                random,
                picked);
        }

        return picked;
    }

    /// <summary>
    /// Draws an artist by its weight, then one of its songs by the weight of the song, until enough songs are chosen
    /// or none is left.
    /// </summary>
    private static void PickByArtist(
        List<AutoplayCandidateRow> pool,
        IReadOnlyDictionary<Guid, int> artistWeights,
        int count,
        Random random,
        List<Guid> picked)
    {
        var byArtist = pool
            .Where(candidate => SongWeight(candidate) > 0)
            .GroupBy(candidate => candidate.ArtistId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());
        while (picked.Count < count && byArtist.Count > 0)
        {
            List<Guid> artists = [.. byArtist.Keys.Order()];
            Guid artist = Draw(artists, candidate => artistWeights[candidate], random);
            List<AutoplayCandidateRow> songs = byArtist[artist];
            AutoplayCandidateRow song = Draw(songs, SongWeight, random);
            songs.Remove(song);
            picked.Add(song.TrackId);
            if (songs.Count == 0)
            {
                byArtist.Remove(artist);
            }
        }
    }

    /// <summary>
    /// Draws songs by their weight (relevance times the weight of the song) until enough songs are chosen or none is
    /// left.
    /// </summary>
    private static void PickWeighted(
        List<AutoplayCandidateRow> pool,
        Func<AutoplayCandidateRow, double> relevance,
        int count,
        Random random,
        List<Guid> picked)
    {
        HashSet<Guid> chosen = [.. picked];
        List<AutoplayCandidateRow> remaining = [.. pool
            .Where(candidate => !chosen.Contains(candidate.TrackId))
            .Where(candidate => relevance(candidate) * SongWeight(candidate) > 0)];
        while (picked.Count < count && remaining.Count > 0)
        {
            AutoplayCandidateRow song = Draw(remaining, candidate => relevance(candidate) * SongWeight(candidate), random);
            remaining.Remove(song);
            picked.Add(song.TrackId);
        }
    }

    /// <summary>
    /// One of the items, each with the probability of its share of the total weight.
    /// </summary>
    private static T Draw<T>(IReadOnlyList<T> items, Func<T, double> weightOf, Random random)
    {
        double total = items.Sum(weightOf);
        double target = random.NextDouble() * total;
        foreach (T item in items)
        {
            target -= weightOf(item);
            if (target < 0)
            {
                return item;
            }
        }

        // Rounding may leave a tiny rest: the last item.
        return items[^1];
    }

    private static double SongWeight(AutoplayCandidateRow candidate)
    {
        return AutoplayRules.WeightOf(candidate.IsFavorite, candidate.Rating);
    }

    /// <summary>
    /// How often the genre of the song appears in the list; 1 if the list has no genres (the fallback then chose by
    /// decade only), 0 for another genre.
    /// </summary>
    private static double GenreWeight(AutoplayCandidateRow candidate, IReadOnlyDictionary<string, int> genreWeights)
    {
        if (genreWeights.Count == 0)
        {
            return 1;
        }

        return candidate.Genre is { } genre && genreWeights.TryGetValue(Normalize(genre), out int weight) ? weight : 0;
    }

    private static string Normalize(string genre)
    {
        return genre.Trim().ToUpperInvariant();
    }

    private static IEnumerable<AutoplayCandidateRow> Distinct(IEnumerable<AutoplayCandidateRow> candidates)
    {
        return candidates.DistinctBy(candidate => candidate.TrackId);
    }
}

using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Recaps.Statistics;

internal sealed class ListeningStatisticsService(
    IPlayHistoryRepository history,
    ITrackRepository tracks,
    TimeProvider timeProvider) : IListeningStatisticsService
{
    public async Task<ListeningStatistics> GetAsync(
        StatisticsQuery query,
        CancellationToken cancellationToken = default)
    {
        TimeZoneInfo timeZone = query.TimeZone;
        DateOnly today = StatisticsTimeZones.DayOf(timeProvider.GetUtcNow(), timeZone);
        DateOnly to = query.To ?? today;
        DateOnly from = query.From
            ?? (await history.FindFirstPlayAsync(query.UserId, cancellationToken) is { } first
                ? StatisticsTimeZones.DayOf(first, timeZone)
                : to);
        if (from > to)
        {
            from = to;
        }

        IReadOnlyList<PlayStatisticsRow> plays = await history.ListPlaysAsync(
            query.UserId,
            StatisticsTimeZones.StartOfDay(from, timeZone),
            StatisticsTimeZones.StartOfDay(to.AddDays(1), timeZone),
            cancellationToken);
        Guid[] artistIds = [.. plays
            .SelectMany(play => play.AllArtists)
            .Select(artist => artist.Id)
            .Distinct()];
        IReadOnlyDictionary<Guid, DateTimeOffset> firstArtistPlays =
            await history.FindFirstPlaysOfArtistsAsync(query.UserId, artistIds, cancellationToken);

        StatisticsCalculation calculation = ListeningStatisticsCalculator.Calculate(
            plays,
            firstArtistPlays,
            from,
            to,
            timeZone,
            query.Top);

        IEnumerable<CountedGroup> groups = calculation.TopArtists
            .Concat(calculation.TopAlbums)
            .Concat(calculation.TopGenres)
            .Concat(calculation.NewArtists);
        Guid[] trackIds = [.. calculation.TopTracks
            .Select(track => track.TrackId)
            .Concat(groups.SelectMany(group => group.TrackIds))
            .Distinct()];
        var rows = (await tracks.ListByIdsAsync(trackIds, query.ViewerId, cancellationToken))
            .ToDictionary(row => row.Id);

        Guid? CoverOf(CountedGroup group)
        {
            return group.TrackIds
                .Where(id => rows.TryGetValue(id, out TrackRow? row) && row.HasCover)
                .Select(id => (Guid?)id)
                .FirstOrDefault();
        }

        // A track that moved to another album in the meantime does not tell about this one.
        bool IsCompilation(CountedGroup album)
        {
            return album.TrackIds.Any(id =>
                rows.TryGetValue(id, out TrackRow? row) && row.AlbumId == album.Id && row.AlbumIsCompilation);
        }

        return new ListeningStatistics(
            from,
            to,
            timeZone.Id,
            calculation.Totals,
            // A track removed by a scan in the meantime is left out.
            [.. calculation.TopTracks
                .Where(track => rows.ContainsKey(track.TrackId))
                .Select(track => new TopTrack(rows[track.TrackId], track.Plays, track.ListeningTime))],
            [.. calculation.TopArtists.Select(artist => new TopArtist(
                artist.Id,
                artist.Name,
                artist.Plays,
                artist.ListeningTime,
                artist.Tracks,
                CoverOf(artist)))],
            [.. calculation.TopAlbums.Select(album => new TopAlbum(
                album.Id,
                album.Name,
                album.ArtistId,
                album.ArtistName,
                IsCompilation(album),
                album.Plays,
                album.ListeningTime,
                album.Tracks,
                CoverOf(album)))],
            [.. calculation.TopGenres.Select(genre => new TopGenre(
                genre.Name,
                genre.Plays,
                genre.ListeningTime,
                genre.Tracks,
                CoverOf(genre)))],
            [.. calculation.NewArtists.Select(artist => new NewArtist(
                artist.Id,
                artist.Name,
                artist.FirstPlayedAt!.Value,
                artist.Plays,
                artist.ListeningTime,
                CoverOf(artist)))],
            calculation.Timeline,
            calculation.Hours);
    }
}

using WaveMP.Core.Recaps.Statistics;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class RecapMappingExtensions
{
    public static ListeningStatisticsDto ToDto(this ListeningStatistics statistics)
    {
        return new ListeningStatisticsDto
        {
            From = statistics.From,
            To = statistics.To,
            TimeZone = statistics.TimeZone,
            Totals = new StatisticsTotalsDto
            {
                Plays = statistics.Totals.Plays,
                ListeningMilliseconds = ToMilliseconds(statistics.Totals.ListeningTime),
                Tracks = statistics.Totals.Tracks,
                Artists = statistics.Totals.Artists,
                Albums = statistics.Totals.Albums,
                Genres = statistics.Totals.Genres,
                NewArtists = statistics.Totals.NewArtists,
                ActiveDays = statistics.Totals.ActiveDays,
            },
            TopTracks = [.. statistics.TopTracks.Select(track => new TopTrackDto
            {
                Track = track.Track.ToDto(),
                Plays = track.Plays,
                ListeningMilliseconds = ToMilliseconds(track.ListeningTime),
            })],
            TopArtists = [.. statistics.TopArtists.Select(artist => new TopArtistDto
            {
                Artist = new ArtistReferenceDto { Id = artist.Id, Name = artist.Name },
                Plays = artist.Plays,
                ListeningMilliseconds = ToMilliseconds(artist.ListeningTime),
                Tracks = artist.Tracks,
                CoverTrackId = artist.CoverTrackId,
            })],
            TopAlbums = [.. statistics.TopAlbums.Select(album => new TopAlbumDto
            {
                Album = new AlbumReferenceDto
                {
                    Id = album.Id,
                    Title = album.Title,
                    Artist = album.ArtistId is { } artistId && album.ArtistName is { } artistName
                        ? new ArtistReferenceDto { Id = artistId, Name = artistName }
                        : null,
                    Artists = LibraryMappingExtensions.ToCredits(album.ArtistId, album.ArtistName),
                    IsCompilation = album.IsCompilation,
                },
                Plays = album.Plays,
                ListeningMilliseconds = ToMilliseconds(album.ListeningTime),
                Tracks = album.Tracks,
                CoverTrackId = album.CoverTrackId,
            })],
            TopGenres = [.. statistics.TopGenres.Select(genre => new TopGenreDto
            {
                Name = genre.Name,
                Plays = genre.Plays,
                ListeningMilliseconds = ToMilliseconds(genre.ListeningTime),
                Tracks = genre.Tracks,
                CoverTrackId = genre.CoverTrackId,
            })],
            NewArtists = [.. statistics.NewArtists.Select(artist => new NewArtistDto
            {
                Artist = new ArtistReferenceDto { Id = artist.Id, Name = artist.Name },
                FirstPlayedAt = artist.FirstPlayedAt,
                Plays = artist.Plays,
                ListeningMilliseconds = ToMilliseconds(artist.ListeningTime),
                CoverTrackId = artist.CoverTrackId,
            })],
            Timeline = new TimelineDto
            {
                Granularity = statistics.Timeline.Granularity.ToDto(),
                Buckets = [.. statistics.Timeline.Buckets.Select(bucket => new TimelineBucketDto
                {
                    Start = bucket.Start,
                    Plays = bucket.Plays,
                    ListeningMilliseconds = ToMilliseconds(bucket.ListeningTime),
                })],
            },
            Hours = [.. statistics.Hours.Select(hour => new HourStatisticsDto
            {
                Hour = hour.Hour,
                Plays = hour.Plays,
                ListeningMilliseconds = ToMilliseconds(hour.ListeningTime),
            })],
        };
    }

    public static TimelineGranularityDto ToDto(this TimelineGranularity granularity)
    {
        return granularity switch
        {
            TimelineGranularity.Week => TimelineGranularityDto.Week,
            TimelineGranularity.Month => TimelineGranularityDto.Month,
            TimelineGranularity.Year => TimelineGranularityDto.Year,
            _ => TimelineGranularityDto.Day,
        };
    }

    /// <summary>
    /// A shared recap as the profile <paramref name="currentUserId"/> sees it.
    /// </summary>
    public static RecapShareDto ToDto(this RecapShareRow row, Guid currentUserId)
    {
        RecapShare share = row.Share;
        return new RecapShareDto
        {
            Id = share.Id,
            Owner = new ProfileReferenceDto { Id = share.UserId, Name = row.OwnerName },
            IsOwn = share.UserId == currentUserId,
            Period = share.Period.ToDto(),
            From = share.FromDate,
            To = share.ToDate,
            TimeZone = share.TimeZone,
            Design = share.Design,
            CreatedAt = share.CreatedAt,
            UpdatedAt = share.UpdatedAt,
        };
    }

    public static RecapPeriodDto ToDto(this RecapPeriod period)
    {
        return period switch
        {
            RecapPeriod.LastMonth => RecapPeriodDto.LastMonth,
            RecapPeriod.LastThreeMonths => RecapPeriodDto.LastThreeMonths,
            RecapPeriod.LastYear => RecapPeriodDto.LastYear,
            RecapPeriod.CurrentYear => RecapPeriodDto.CurrentYear,
            RecapPeriod.AllTime => RecapPeriodDto.AllTime,
            _ => RecapPeriodDto.Custom,
        };
    }

    public static RecapPeriod ToEntity(this RecapPeriodDto period)
    {
        return period switch
        {
            RecapPeriodDto.LastMonth => RecapPeriod.LastMonth,
            RecapPeriodDto.LastThreeMonths => RecapPeriod.LastThreeMonths,
            RecapPeriodDto.LastYear => RecapPeriod.LastYear,
            RecapPeriodDto.CurrentYear => RecapPeriod.CurrentYear,
            RecapPeriodDto.AllTime => RecapPeriod.AllTime,
            _ => RecapPeriod.Custom,
        };
    }

    private static long ToMilliseconds(TimeSpan time)
    {
        return (long)Math.Round(time.TotalMilliseconds);
    }
}

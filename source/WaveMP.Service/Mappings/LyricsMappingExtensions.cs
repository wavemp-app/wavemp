using WaveMP.Core.Lyrics.Documents;
using WaveMP.Core.Lyrics.Lookups;
using WaveMP.Core.Lyrics.Searches;
using WaveMP.Core.Lyrics.Uploads;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class LyricsMappingExtensions
{
    public static LyricsDto ToDto(this TrackLyrics lyrics)
    {
        return new LyricsDto
        {
            Type = lyrics.Document.Type.ToDto(),
            Source = lyrics.Source.ToDto(),
            FromOtherVersion = lyrics.OtherVersion,
            Lines = [.. lyrics.Document.Lines.Select(line => line.ToDto())],
        };
    }

    public static LyricsLineDto ToDto(this LyricsLine line)
    {
        return new LyricsLineDto
        {
            StartMilliseconds = Milliseconds(line.Start),
            EndMilliseconds = Milliseconds(line.End),
            Text = line.Text,
            Words = [.. line.Words.Select(word => new LyricsWordDto
            {
                StartMilliseconds = (long)word.Start.TotalMilliseconds,
                EndMilliseconds = Milliseconds(word.End),
                Text = word.Text,
            })],
        };
    }

    public static LyricsTypeDto ToDto(this LyricsType type)
    {
        return type switch
        {
            LyricsType.LineSynchronized => LyricsTypeDto.LineSynchronized,
            LyricsType.WordSynchronized => LyricsTypeDto.WordSynchronized,
            LyricsType.Static => LyricsTypeDto.Static,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown lyrics type."),
        };
    }

    public static LyricsSourceDto ToDto(this LyricsSource source)
    {
        return source switch
        {
            LyricsSource.File => LyricsSourceDto.File,
            LyricsSource.Download => LyricsSourceDto.Download,
            LyricsSource.Upload => LyricsSourceDto.Upload,
            LyricsSource.Tags => LyricsSourceDto.Tags,
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown lyrics source."),
        };
    }

    public static LyricsLookupDto ToDto(this LyricsDownloadRow lookup)
    {
        return new LyricsLookupDto
        {
            Status = lookup.Status.ToDto(),
            CheckedAt = lookup.CheckedAt,
            IsForgettable = LyricsLookupResult.IsForgettableStatus(lookup.Status),
        };
    }

    public static LyricsLookupStatusDto ToDto(this LyricsDownloadStatus status)
    {
        return status switch
        {
            LyricsDownloadStatus.SavedToFile => LyricsLookupStatusDto.SavedToFile,
            LyricsDownloadStatus.SavedToDatabase => LyricsLookupStatusDto.SavedToDatabase,
            LyricsDownloadStatus.NotFound => LyricsLookupStatusDto.NotFound,
            LyricsDownloadStatus.Instrumental => LyricsLookupStatusDto.Instrumental,
            LyricsDownloadStatus.NotNeeded => LyricsLookupStatusDto.NotNeeded,
            LyricsDownloadStatus.Uploaded => LyricsLookupStatusDto.Uploaded,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown lyrics lookup status."),
        };
    }

    public static LyricsLookupSummaryDto ToDto(this LyricsLookupSummary summary)
    {
        return new LyricsLookupSummaryDto
        {
            NotFoundCount = summary.NotFound,
            InstrumentalCount = summary.Instrumental,
            LookUpAgainAfterMinutes = summary.LookUpAgainAfter is { } wait
                ? (int)Math.Min(int.MaxValue, Math.Ceiling(wait.TotalMinutes))
                : null,
        };
    }

    public static LyricsOnlineMatchesDto ToDto(this LyricsSearchResult result)
    {
        return new LyricsOnlineMatchesDto
        {
            Title = result.Title,
            Artist = result.Artist,
            Matches = [.. (result.Matches ?? []).Select(match => new LyricsOnlineMatchDto
            {
                Id = match.Id,
                Title = match.Title,
                Artist = match.Artist,
                Album = match.Album,
                DurationSeconds = match.Duration is { } duration ? Math.Round(duration.TotalSeconds, 1) : null,
                Synchronized = match.Synchronized,
                FitsDuration = match.FitsDuration,
            })],
        };
    }

    public static LyricsPublicationDto ToDto(this LyricsPublication publication)
    {
        return publication switch
        {
            LyricsPublication.NotRequested => LyricsPublicationDto.NotRequested,
            LyricsPublication.Published => LyricsPublicationDto.Published,
            LyricsPublication.Disabled => LyricsPublicationDto.Disabled,
            LyricsPublication.MissingMetadata => LyricsPublicationDto.MissingMetadata,
            LyricsPublication.Rejected => LyricsPublicationDto.Rejected,
            LyricsPublication.RateLimited => LyricsPublicationDto.RateLimited,
            LyricsPublication.Unavailable => LyricsPublicationDto.Unavailable,
            _ => throw new ArgumentOutOfRangeException(nameof(publication), publication, "Unknown publication."),
        };
    }

    private static long? Milliseconds(TimeSpan? time)
    {
        return time is { } value ? (long)value.TotalMilliseconds : null;
    }
}

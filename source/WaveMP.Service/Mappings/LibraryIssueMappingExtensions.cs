using WaveMP.Core.Library.Checks;
using WaveMP.Core.Metadata.Artists;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class LibraryIssueMappingExtensions
{
    public static LibraryIssueDto ToDto(this LibraryIssueRow row)
    {
        return new LibraryIssueDto
        {
            Id = row.Id,
            Type = row.Type.ToDto(),
            Severity = LibraryIssueTypes.SeverityOf(row.Type).ToDto(),
            DetectedAt = row.DetectedAt,
            Track = row.Track?.ToDto(),
            File = row.File is { } file
                ? new LibraryIssueFileDto
                {
                    Id = file.Id,
                    SourceId = file.SourceId,
                    SourceName = file.SourceName,
                    RelativePath = file.RelativePath,
                    Kind = file.Kind.ToDto(),
                }
                : null,
            FileIssue = row.FileIssue?.ToDto(),
            MissingTags = [.. row.MissingTags.Select(tag => tag.ToDto())],
            RelatedTrack = row.RelatedTrack?.ToDto(),
            SuggestedArtists = row.Type == LibraryIssueType.PossibleMultipleArtists
                && row.Track is { } track
                && ArtistNames.SuggestSplit(
                    [.. track.AllArtists.Select(artist => new ArtistName(artist.Name, artist.Role))]) is { } suggestion
                    ? [.. suggestion.Select(artist => new ArtistNameDto
                    {
                        Name = artist.Name,
                        Role = artist.Role.ToDto(),
                    })]
                    : null,
        };
    }

    public static LibraryIssueTrackDto ToDto(this LibraryIssueTrackRow track)
    {
        return new LibraryIssueTrackDto
        {
            Id = track.Id,
            Title = track.Title,
            Artist = LibraryMappingExtensions.ToReference(track.ArtistId, track.ArtistName),
            Artists = track.AllArtists.ToDtos(),
            Album = track.AlbumId is { } albumId && track.AlbumTitle is { } albumTitle
                ? new AlbumReferenceDto
                {
                    Id = albumId,
                    Title = albumTitle,
                    Artist = LibraryMappingExtensions.ToReference(track.AlbumArtistId, track.AlbumArtistName),
                    Artists = LibraryMappingExtensions.ToCredits(track.AlbumArtistId, track.AlbumArtistName),
                    IsCompilation = track.AlbumIsCompilation,
                }
                : null,
            SourceId = track.SourceId,
            SourceName = track.SourceName,
            DirectoryPath = track.DirectoryPath,
            BaseName = track.BaseName,
            DurationMilliseconds = LibraryMappingExtensions.ToMilliseconds(track.Duration),
            HasAudio = track.HasAudio,
            HasVideo = track.HasVideo,
        };
    }

    public static LibraryIssueSummaryDto ToDto(this LibraryIssueSummary summary)
    {
        return new LibraryIssueSummaryDto
        {
            Counts =
            [
                .. summary.Counts.Select(count => new LibraryIssueCountDto
                {
                    Type = count.Type.ToDto(),
                    Severity = count.Severity.ToDto(),
                    Count = count.Count,
                }),
            ],
            ProblemCount = summary.Counts
                .Where(count => count.Severity == LibraryIssueSeverity.Problem)
                .Sum(count => count.Count),
            HintCount = summary.Counts
                .Where(count => count.Severity == LibraryIssueSeverity.Hint)
                .Sum(count => count.Count),
            CheckedAt = summary.CheckedAt,
            TrackCount = summary.TrackCount,
        };
    }

    public static LibraryIssueTypeDto ToDto(this LibraryIssueType type)
    {
        return type switch
        {
            LibraryIssueType.UnreadableFile => LibraryIssueTypeDto.UnreadableFile,
            LibraryIssueType.MissingTags => LibraryIssueTypeDto.MissingTags,
            LibraryIssueType.UnknownAlbum => LibraryIssueTypeDto.UnknownAlbum,
            LibraryIssueType.MissingCover => LibraryIssueTypeDto.MissingCover,
            LibraryIssueType.PossibleDuplicate => LibraryIssueTypeDto.PossibleDuplicate,
            LibraryIssueType.VideoWithoutAudio => LibraryIssueTypeDto.VideoWithoutAudio,
            LibraryIssueType.AudioWithoutVideo => LibraryIssueTypeDto.AudioWithoutVideo,
            LibraryIssueType.PossibleMultipleArtists => LibraryIssueTypeDto.PossibleMultipleArtists,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown type."),
        };
    }

    public static LibraryIssueType ToEntity(this LibraryIssueTypeDto type)
    {
        return type switch
        {
            LibraryIssueTypeDto.UnreadableFile => LibraryIssueType.UnreadableFile,
            LibraryIssueTypeDto.MissingTags => LibraryIssueType.MissingTags,
            LibraryIssueTypeDto.UnknownAlbum => LibraryIssueType.UnknownAlbum,
            LibraryIssueTypeDto.MissingCover => LibraryIssueType.MissingCover,
            LibraryIssueTypeDto.PossibleDuplicate => LibraryIssueType.PossibleDuplicate,
            LibraryIssueTypeDto.VideoWithoutAudio => LibraryIssueType.VideoWithoutAudio,
            LibraryIssueTypeDto.AudioWithoutVideo => LibraryIssueType.AudioWithoutVideo,
            LibraryIssueTypeDto.PossibleMultipleArtists => LibraryIssueType.PossibleMultipleArtists,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown type."),
        };
    }

    public static LibraryIssueSeverityDto ToDto(this LibraryIssueSeverity severity)
    {
        return severity switch
        {
            LibraryIssueSeverity.Problem => LibraryIssueSeverityDto.Problem,
            LibraryIssueSeverity.Hint => LibraryIssueSeverityDto.Hint,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown severity."),
        };
    }

    public static LibraryIssueSeverity ToEntity(this LibraryIssueSeverityDto severity)
    {
        return severity switch
        {
            LibraryIssueSeverityDto.Problem => LibraryIssueSeverity.Problem,
            LibraryIssueSeverityDto.Hint => LibraryIssueSeverity.Hint,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown severity."),
        };
    }

    public static LibraryIssueSortField ToEntity(this LibraryIssueSortDto sort)
    {
        return sort switch
        {
            LibraryIssueSortDto.Path => LibraryIssueSortField.Path,
            LibraryIssueSortDto.Artist => LibraryIssueSortField.Artist,
            LibraryIssueSortDto.Title => LibraryIssueSortField.Title,
            LibraryIssueSortDto.DetectedAt => LibraryIssueSortField.DetectedAt,
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Unknown sort key."),
        };
    }
}

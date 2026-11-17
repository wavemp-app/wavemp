using WaveMP.Core.Scanning.Selections;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class FileSelectionMappingExtensions
{
    public static TrackFileSelectionDto ToDto(this FileSelectionDetails details)
    {
        return new TrackFileSelectionDto
        {
            TrackId = details.TrackId,
            Audio = details.Audio.ToDto(),
            Video = details.Video.ToDto(),
            Cover = details.Cover.ToDto(),
        };
    }

    public static FileSelectionDto ToDto(this CategorySelection selection)
    {
        return new FileSelectionDto
        {
            State = selection.State.ToDto(),
            SelectedFileId = selection.SelectedFileId,
            SelectedPath = selection.SelectedPath,
            ActiveFileId = selection.ActiveFileId,
            AutomaticFileId = selection.AutomaticFileId,
            CandidateFileIds = [.. selection.CandidateFileIds],
        };
    }

    public static TrackFileSummaryDto ToDto(this TrackFileSummaryRow row)
    {
        return new TrackFileSummaryDto
        {
            TrackId = row.TrackId,
            Title = row.Title,
            Artist = LibraryMappingExtensions.ToReference(row.ArtistId, row.ArtistName),
            Album = row.AlbumId is { } albumId && row.AlbumTitle is { } albumTitle
                ? new AlbumReferenceDto
                {
                    Id = albumId,
                    Title = albumTitle,
                    Artist = LibraryMappingExtensions.ToReference(row.AlbumArtistId, row.AlbumArtistName),
                    IsCompilation = row.AlbumIsCompilation,
                }
                : null,
            DirectoryPath = row.DirectoryPath,
            BaseName = row.BaseName,
            AudioCount = row.AudioCount,
            VideoCount = row.VideoCount,
            CoverCount = row.CoverCount,
            Audio = row.Audio.ToDto(),
            Video = row.Video.ToDto(),
            Cover = row.Cover.ToDto(),
        };
    }

    public static FileChoices ToChoices(this UpdateTrackFileSelectionDto request)
    {
        return new FileChoices(request.AudioFileId, request.VideoFileId, request.CoverFileId);
    }

    public static FileSelectionStateDto ToDto(this FileSelectionState state)
    {
        return state switch
        {
            FileSelectionState.Automatic => FileSelectionStateDto.Automatic,
            FileSelectionState.Selected => FileSelectionStateDto.Selected,
            FileSelectionState.Missing => FileSelectionStateDto.Missing,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown state."),
        };
    }

    public static FileSelectionFilter ToEntity(this FileSelectionFilterDto filter)
    {
        return filter switch
        {
            FileSelectionFilterDto.All => FileSelectionFilter.All,
            FileSelectionFilterDto.SeveralAudio => FileSelectionFilter.SeveralAudio,
            FileSelectionFilterDto.SeveralVideo => FileSelectionFilter.SeveralVideo,
            FileSelectionFilterDto.SeveralCovers => FileSelectionFilter.SeveralCovers,
            FileSelectionFilterDto.Choices => FileSelectionFilter.Choices,
            FileSelectionFilterDto.Selected => FileSelectionFilter.Selected,
            FileSelectionFilterDto.Missing => FileSelectionFilter.Missing,
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, "Unknown filter."),
        };
    }

    public static FileSelectionSortField ToEntity(this FileSelectionSortDto sort)
    {
        return sort switch
        {
            FileSelectionSortDto.Path => FileSelectionSortField.Path,
            FileSelectionSortDto.Title => FileSelectionSortField.Title,
            FileSelectionSortDto.FileCount => FileSelectionSortField.FileCount,
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Unknown sort key."),
        };
    }
}

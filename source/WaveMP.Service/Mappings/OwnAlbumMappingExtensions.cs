using WaveMP.Core.Organizing.OwnAlbums;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class OwnAlbumMappingExtensions
{
    public static OwnAlbumDto ToDto(this OwnAlbumItem item)
    {
        return new OwnAlbumDto
        {
            Id = item.Album.Id,
            Title = item.Album.Title,
            AlbumArtist = item.Album.AlbumArtist,
            Year = item.Album.Year,
            Genre = item.Album.Genre,
            HasCover = item.Album.CoverFileName is not null,
            CoverUpdatedAt = item.Album.CoverUpdatedAt,
            TrackCount = item.Album.TrackCount,
            RemovedTrackCount = item.Album.RemovedCount,
            Status = item.Status switch
            {
                OwnAlbumStatus.Applied => OwnAlbumStatusDto.Applied,
                OwnAlbumStatus.Changed => OwnAlbumStatusDto.Changed,
                _ => OwnAlbumStatusDto.Draft,
            },
            AppliedAt = item.Album.AppliedAt,
            LibraryAlbumId = item.Album.LibraryAlbumId,
            CreatedAt = item.Album.CreatedAt,
            UpdatedAt = item.Album.UpdatedAt,
        };
    }

    public static OwnAlbumTrackDto ToDto(this OwnAlbumTrackItem item)
    {
        return new OwnAlbumTrackDto
        {
            Position = item.Member.Position,
            State = item.Member.State == OwnAlbumTrackState.Removed
                ? OwnAlbumTrackStateDto.Removed
                : OwnAlbumTrackStateDto.Member,
            AddedAt = item.Member.AddedAt,
            Track = item.Track.ToDto(),
        };
    }

    public static OwnAlbumListQuery ToQuery(
        this OwnAlbumSortDto sort,
        SortDirectionDto direction,
        int offset,
        int limit)
    {
        return new OwnAlbumListQuery(
            offset,
            limit,
            sort == OwnAlbumSortDto.UpdatedAt ? OwnAlbumSortField.UpdatedAt : OwnAlbumSortField.Title,
            direction.ToEntity());
    }

    public static OwnAlbumValues ToValues(this CreateOwnAlbumDto request)
    {
        return new OwnAlbumValues(request.Title, request.AlbumArtist, request.Year, request.Genre);
    }

    public static OwnAlbumChanges ToChanges(this UpdateOwnAlbumDto request)
    {
        return new OwnAlbumChanges(request.Title, request.AlbumArtist, request.Year, request.Genre);
    }
}

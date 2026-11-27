using WaveMP.Core.Library.Groups;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class ListGroupMappingExtensions
{
    public static ListGroupDto ToDto(this ListGroup group)
    {
        return new ListGroupDto
        {
            Kind = group.Kind.ToDto(),
            Position = group.Position,
            Count = group.Count,
            Album = group.Album is { } album
                ? new AlbumReferenceDto
                {
                    Id = album.Id,
                    Title = album.Title,
                    Artist = LibraryMappingExtensions.ToReference(album.ArtistId, album.ArtistName),
                    Artists = album.AllArtists.ToDtos(),
                    IsCompilation = album.IsCompilation,
                }
                : null,
            Artist = group.Artist is { } artist ? new ArtistReferenceDto { Id = artist.Id, Name = artist.Name } : null,
            Letter = group.Letter,
            Year = group.Year,
            HasCover = group.Album?.HasCover ?? false,
        };
    }

    public static ListGroupKindDto ToDto(this ListGroupKind kind)
    {
        return kind switch
        {
            ListGroupKind.Album => ListGroupKindDto.Album,
            ListGroupKind.Artist => ListGroupKindDto.Artist,
            ListGroupKind.Letter => ListGroupKindDto.Letter,
            ListGroupKind.Year => ListGroupKindDto.Year,
            ListGroupKind.Unknown => ListGroupKindDto.Unknown,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown group kind."),
        };
    }
}

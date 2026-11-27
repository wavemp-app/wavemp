using WaveMP.Core.Metadata.Artists;
using WaveMP.Core.Metadata.Tags;
using WaveMP.Core.Scanning.Edits;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class TrackMetadataMappingExtensions
{
    public static TrackMetadataDto ToDto(this TrackMetadata metadata)
    {
        return new TrackMetadataDto
        {
            TrackId = metadata.TrackId,
            Status = metadata.Status.ToDto(),
            AudioFilePath = metadata.AudioFilePath,
            Title = metadata.Title,
            Artist = metadata.Artist,
            Album = metadata.Album,
            AlbumArtist = metadata.AlbumArtist,
            Artists = [.. metadata.Artists.Select(artist => new ArtistNameDto
            {
                Name = artist.Name,
                Role = artist.Role.ToDto(),
            })],
            AlbumArtists = [.. metadata.AlbumArtists],
            HasArtistValues = metadata.HasArtistValues,
            Year = metadata.Year,
            TrackNumber = metadata.TrackNumber,
            DiscNumber = metadata.DiscNumber,
            Genre = metadata.Genre,
        };
    }

    internal static TagChange<IReadOnlyList<string>> ToAlbumArtistChange(IReadOnlyList<AlbumArtistInputDto>? artists)
    {
        return new TagChange<IReadOnlyList<string>>([.. (artists ?? []).Select(artist => artist.Name)]);
    }

    public static TrackMetadataStatusDto ToDto(this TrackMetadataStatus status)
    {
        return status switch
        {
            TrackMetadataStatus.Editable => TrackMetadataStatusDto.Editable,
            TrackMetadataStatus.NoAudio => TrackMetadataStatusDto.NoAudio,
            TrackMetadataStatus.NotWritable => TrackMetadataStatusDto.NotWritable,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown status."),
        };
    }

    /// <summary>
    /// The changes of the request: members it contains, also with null, become changes; missing ones stay.
    /// </summary>
    public static TagChanges ToChanges(this UpdateTrackMetadataDto request)
    {
        return new TagChanges
        {
            Title = request.HasTitle ? new TagChange<string?>(request.Title) : null,
            Artist = request.HasArtist ? new TagChange<string?>(request.Artist) : null,
            Album = request.HasAlbum ? new TagChange<string?>(request.Album) : null,
            AlbumArtist = request.HasAlbumArtist ? new TagChange<string?>(request.AlbumArtist) : null,
            ArtistCredits = request.HasArtists
                ? new TagChange<IReadOnlyList<ArtistName>>([.. (request.Artists ?? [])
                    .Select(artist => new ArtistName(artist.Name, artist.Role.ToEntity()))])
                : null,
            AlbumArtistCredits = request.HasAlbumArtists ? ToAlbumArtistChange(request.AlbumArtists) : null,
            Year = request.HasYear ? new TagChange<int?>(request.Year) : null,
            TrackNumber = request.HasTrackNumber ? new TagChange<int?>(request.TrackNumber) : null,
            DiscNumber = request.HasDiscNumber ? new TagChange<int?>(request.DiscNumber) : null,
            Genre = request.HasGenre ? new TagChange<string?>(request.Genre) : null,
        };
    }
}

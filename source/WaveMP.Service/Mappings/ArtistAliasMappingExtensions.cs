using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class ArtistAliasMappingExtensions
{
    public static ArtistAliasDto ToDto(this ArtistAliasRow alias)
    {
        return new ArtistAliasDto
        {
            Id = alias.Id,
            Name = alias.Name,
            Source = alias.Source.ToDto(),
        };
    }

    public static ArtistAliasSourceDto ToDto(this ArtistAliasSource source)
    {
        return source switch
        {
            ArtistAliasSource.Admin => ArtistAliasSourceDto.Admin,
            ArtistAliasSource.MusicBrainz => ArtistAliasSourceDto.MusicBrainz,
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
        };
    }
}

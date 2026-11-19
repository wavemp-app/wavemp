using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Sort key of the songs of a smart playlist. Songs without a value (e.g. without year or never played) come last in
/// both directions; ties are sorted by album, disc and track number. Rating, PlayCount and LastPlayed are those of the
/// current profile.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<SmartPlaylistTrackSortDto>))]
public enum SmartPlaylistTrackSortDto
{
    Title,
    Artist,
    Album,
    Year,
    Genre,
    Rating,
    PlayCount,
    LastPlayed,
    DateAdded,
    Duration,
}

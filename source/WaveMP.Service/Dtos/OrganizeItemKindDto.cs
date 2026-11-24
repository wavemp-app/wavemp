using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// What an item of an organize operation changes: the files and tags of a song, the cover of an own album in its album
/// folder, or the files of a folder without a song (e.g. its cover) that follow its songs.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<OrganizeItemKindDto>))]
public enum OrganizeItemKindDto
{
    Track,

    AlbumCover,

    FolderFiles,
}

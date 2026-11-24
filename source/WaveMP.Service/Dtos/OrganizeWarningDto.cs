using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Something to check in an item of an organize operation; the item still runs. Clients translate it.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<OrganizeWarningDto>))]
public enum OrganizeWarningDto
{
    /// <summary>
    /// At the new place the library reads another title, artist, album or album artist, e.g. for files without tags.
    /// </summary>
    [JsonStringEnumMemberName("metadata_changes")]
    MetadataChanges,

    /// <summary>
    /// The library already has an album with the title and album artist of the own album; the songs join it.
    /// </summary>
    [JsonStringEnumMemberName("album_merges")]
    AlbumMerges,

    /// <summary>
    /// The template has no album folder, so the cover of the own album is not stored next to the songs.
    /// </summary>
    [JsonStringEnumMemberName("cover_not_written")]
    CoverNotWritten,

    /// <summary>
    /// The album folder already has another cover picture, which may be shown instead.
    /// </summary>
    [JsonStringEnumMemberName("other_cover_exists")]
    OtherCoverExists,

    /// <summary>
    /// The tags were changed since the operation that is undone; they are kept.
    /// </summary>
    [JsonStringEnumMemberName("tags_changed_since")]
    TagsChangedSince,
}

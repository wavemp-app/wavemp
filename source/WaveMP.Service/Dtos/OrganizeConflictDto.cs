using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Why an item of an organize operation cannot run. Clients translate it.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<OrganizeConflictDto>))]
public enum OrganizeConflictDto
{
    /// <summary>
    /// Another file already has the new name in the target folder (compared without regard to case and Unicode
    /// normalization).
    /// </summary>
    [JsonStringEnumMemberName("target_exists")]
    TargetExists,

    /// <summary>
    /// Another item of the operation would get the same name.
    /// </summary>
    [JsonStringEnumMemberName("duplicate_target")]
    DuplicateTarget,

    /// <summary>
    /// The new path is longer than the library allows.
    /// </summary>
    [JsonStringEnumMemberName("path_too_long")]
    PathTooLong,

    /// <summary>
    /// A folder of the old or new path is a symbolic link or junction, or the path leaves the media folder.
    /// </summary>
    [JsonStringEnumMemberName("unsafe_path")]
    UnsafePath,

    /// <summary>
    /// A file of the song is missing or changed since the library read it.
    /// </summary>
    [JsonStringEnumMemberName("source_missing")]
    SourceMissing,

    /// <summary>
    /// The audio file whose tags change cannot be written.
    /// </summary>
    [JsonStringEnumMemberName("not_writable")]
    NotWritable,

    /// <summary>
    /// The song has no audio file, so its tags cannot be written.
    /// </summary>
    [JsonStringEnumMemberName("no_audio")]
    NoAudio,

    /// <summary>
    /// The media source of the song is inactive or its folder is missing.
    /// </summary>
    [JsonStringEnumMemberName("source_inactive")]
    SourceInactive,

    /// <summary>
    /// The album folder already has another file with the name of the cover.
    /// </summary>
    [JsonStringEnumMemberName("cover_exists")]
    CoverExists,

    /// <summary>
    /// A file was changed or moved again since the operation that is undone.
    /// </summary>
    [JsonStringEnumMemberName("changed_since")]
    ChangedSince,
}

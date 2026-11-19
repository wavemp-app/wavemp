using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Problem with a file or a media source found by a scan. Clients translate it.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<ScanIssueCodeDto>))]
public enum ScanIssueCodeDto
{
    /// <summary>
    /// The file cannot be read at all.
    /// </summary>
    [JsonStringEnumMemberName("file.unreadable")]
    FileUnreadable,

    /// <summary>
    /// The tags of the file cannot be read.
    /// </summary>
    [JsonStringEnumMemberName("file.tags_unreadable")]
    FileTagsUnreadable,

    /// <summary>
    /// ffprobe cannot read the file.
    /// </summary>
    [JsonStringEnumMemberName("file.probe_failed")]
    FileProbeFailed,

    /// <summary>
    /// The path of the file is too long; the file was skipped.
    /// </summary>
    [JsonStringEnumMemberName("file.path_too_long")]
    FilePathTooLong,

    /// <summary>
    /// The folder of the media source does not exist; the source was skipped and its library left unchanged.
    /// </summary>
    [JsonStringEnumMemberName("source.not_found")]
    SourceNotFound,

    /// <summary>
    /// The folder of the media source cannot be read; the source was skipped and its library left unchanged.
    /// </summary>
    [JsonStringEnumMemberName("source.unreadable")]
    SourceUnreadable,

    /// <summary>
    /// The folder of the media source has no media files although the library has files of it, e.g. because it is not
    /// mounted; the source was skipped and nothing of it was removed.
    /// </summary>
    [JsonStringEnumMemberName("source.empty")]
    SourceEmpty,
}

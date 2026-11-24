using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Id of a background job.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<JobIdDto>))]
public enum JobIdDto
{
    /// <summary>
    /// Scans the media folder and updates the library.
    /// </summary>
    [JsonStringEnumMemberName("library-scan")]
    LibraryScan,

    /// <summary>
    /// Prepares the scaled covers after scans and removes those of changed or removed pictures.
    /// </summary>
    [JsonStringEnumMemberName("cover-cache")]
    CoverCache,

    /// <summary>
    /// Exports the database into the backup folder, every day and when an admin starts it.
    /// </summary>
    [JsonStringEnumMemberName("database-backup")]
    DatabaseBackup,

    /// <summary>
    /// Downloads the lyrics of new tracks without lyrics from LRCLIB, after scans and when the download is switched
    /// on.
    /// </summary>
    [JsonStringEnumMemberName("lyrics-download")]
    LyricsDownload,

    /// <summary>
    /// Checks the library for problems (missing covers and tags, duplicates, unreadable files, ...) after every scan
    /// and when an admin starts it; see ListLibraryIssues.
    /// </summary>
    [JsonStringEnumMemberName("library-check")]
    LibraryCheck,

    /// <summary>
    /// Organizes the media files: plans the preview of reorganizing the library and runs the organize operations an
    /// admin confirmed; started by the organize endpoints only, see ApplyOrganizeOperation.
    /// </summary>
    [JsonStringEnumMemberName("library-organize")]
    LibraryOrganize,
}

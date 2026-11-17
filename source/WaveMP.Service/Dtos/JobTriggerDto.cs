using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// What started a job run.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<JobTriggerDto>))]
public enum JobTriggerDto
{
    /// <summary>
    /// The periodic schedule of the job.
    /// </summary>
    Schedule,

    /// <summary>
    /// An admin, through the API.
    /// </summary>
    Manual,

    /// <summary>
    /// The file watcher after changes in the media folder.
    /// </summary>
    FileWatcher,

    /// <summary>
    /// A library scan that finished, e.g. to prepare the covers of the changed library.
    /// </summary>
    LibraryScan,

    /// <summary>
    /// A change of a cover rule (global, of an album or of a track), e.g. to prepare the covers shown now.
    /// </summary>
    CoverRule,

    /// <summary>
    /// A change of the settings, e.g. switching on the download of lyrics.
    /// </summary>
    Settings,
}

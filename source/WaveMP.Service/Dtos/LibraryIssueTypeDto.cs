using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Kind of a problem of the library found by the library check. Clients translate it.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<LibraryIssueTypeDto>))]
public enum LibraryIssueTypeDto
{
    /// <summary>
    /// A file cannot be read completely: it is damaged, ffprobe cannot read it or its tags cannot be read (see
    /// fileIssue).
    /// </summary>
    UnreadableFile,

    /// <summary>
    /// The tags of the file of the track lack the title, the artist or the album (see missingTags).
    /// </summary>
    MissingTags,

    /// <summary>
    /// The track has no album, so clients show it under their text for an unknown album.
    /// </summary>
    UnknownAlbum,

    /// <summary>
    /// The track has no cover: neither its own nor one of its album or of another track of its album, also no online
    /// cover.
    /// </summary>
    MissingCover,

    /// <summary>
    /// Another track has the same artist and title and a similar duration (see relatedTrack).
    /// </summary>
    PossibleDuplicate,

    /// <summary>
    /// The track has a video but no audio file. A hint only.
    /// </summary>
    VideoWithoutAudio,

    /// <summary>
    /// The track has an audio file but no video. A hint only.
    /// </summary>
    AudioWithoutVideo,
}

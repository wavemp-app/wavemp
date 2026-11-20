namespace WaveMP.Data.Entities;

/// <summary>
/// The kind of a problem the library check found. Stored as its name.
/// </summary>
public enum LibraryIssueType
{
    /// <summary>
    /// The track has no cover: neither its own nor one of its album, also no online cover.
    /// </summary>
    MissingCover,

    /// <summary>
    /// The tags of the file of the track lack the title, the artist or the album (see
    /// <see cref="LibraryIssue.MissingTags"/>).
    /// </summary>
    MissingTags,

    /// <summary>
    /// The track has no album, so the clients show it under "Unknown album".
    /// </summary>
    UnknownAlbum,

    /// <summary>
    /// Another track has the same artist and title and a similar duration (see
    /// <see cref="LibraryIssue.RelatedTrackId"/>).
    /// </summary>
    PossibleDuplicate,

    /// <summary>
    /// A file cannot be read completely: it is damaged, ffprobe cannot read it or its tags cannot be read (see
    /// <see cref="LibraryIssue.FileIssue"/>).
    /// </summary>
    UnreadableFile,

    /// <summary>
    /// The track has a video but no audio file. A hint only.
    /// </summary>
    VideoWithoutAudio,

    /// <summary>
    /// The track has an audio file but no video. A hint only.
    /// </summary>
    AudioWithoutVideo,
}

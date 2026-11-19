using WaveMP.Data.Entities;

namespace WaveMP.Core.Library.Checks;

/// <summary>
/// The kinds of problems of the library in the order the clients show them, and how much each matters.
/// </summary>
public static class LibraryIssueTypes
{
    /// <summary>
    /// All kinds: the problems first, then the hints.
    /// </summary>
    public static IReadOnlyList<LibraryIssueType> All { get; } =
    [
        LibraryIssueType.UnreadableFile,
        LibraryIssueType.MissingTags,
        LibraryIssueType.UnknownAlbum,
        LibraryIssueType.MissingCover,
        LibraryIssueType.PossibleDuplicate,
        LibraryIssueType.VideoWithoutAudio,
        LibraryIssueType.AudioWithoutVideo,
    ];

    /// <summary>
    /// How much a kind of problem matters: videos without audio and songs without video are hints only.
    /// </summary>
    public static LibraryIssueSeverity SeverityOf(LibraryIssueType type)
    {
        return type is LibraryIssueType.VideoWithoutAudio or LibraryIssueType.AudioWithoutVideo
            ? LibraryIssueSeverity.Hint
            : LibraryIssueSeverity.Problem;
    }

    /// <summary>
    /// The kinds of a severity, in the order of <see cref="All"/>.
    /// </summary>
    public static IReadOnlyList<LibraryIssueType> OfSeverity(LibraryIssueSeverity severity)
    {
        return [.. All.Where(type => SeverityOf(type) == severity)];
    }
}

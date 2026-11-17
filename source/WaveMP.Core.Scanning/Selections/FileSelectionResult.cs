using WaveMP.Data.Entities;

namespace WaveMP.Core.Scanning.Selections;

/// <summary>
/// Outcome of a change of the file selection: the new selection, or why it was not changed.
/// </summary>
/// <param name="Status">Whether the selection was changed.</param>
/// <param name="Details">The selection after the change; only if done.</param>
/// <param name="InvalidCategories">The categories whose selected file the track cannot use; only if invalid.</param>
public sealed record FileSelectionResult(
    FileSelectionStatus Status,
    FileSelectionDetails? Details = null,
    IReadOnlyList<FileSelectionCategory>? InvalidCategories = null)
{
    public static FileSelectionResult TrackNotFound { get; } = new(FileSelectionStatus.TrackNotFound);

    public static FileSelectionResult Done(FileSelectionDetails details)
    {
        return new FileSelectionResult(FileSelectionStatus.Done, details);
    }

    public static FileSelectionResult Invalid(IReadOnlyList<FileSelectionCategory> categories)
    {
        return new FileSelectionResult(FileSelectionStatus.Invalid, InvalidCategories: categories);
    }
}

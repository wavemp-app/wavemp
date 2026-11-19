using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Library.Options;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Library.Checks;

/// <summary>
/// Reads the tracks and the unreadable files from the database, finds their problems with
/// <see cref="LibraryIssueDetector"/> and stores them in place of those of the run before.
/// </summary>
internal sealed class LibraryCheckRunner(
    ILibraryIssueRepository repository,
    IOptions<LibraryCheckOptions> options,
    TimeProvider timeProvider,
    ILogger<LibraryCheckRunner> logger) : ILibraryCheckRunner
{
    public async Task<LibraryCheckJobResult> RunAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<LibraryCheckTrackRow> tracks = await repository.ListTracksAsync(cancellationToken);
        IReadOnlyList<LibraryCheckFileRow> files = await repository.ListUnreadableFilesAsync(cancellationToken);
        IReadOnlyList<DetectedLibraryIssue> issues =
            LibraryIssueDetector.Detect(tracks, files, options.Value.DuplicateDurationTolerance);
        LibraryIssueChanges changes =
            await repository.ReplaceAsync(issues, tracks.Count, timeProvider.GetUtcNow(), cancellationToken);

        int hints = issues.Count(issue => LibraryIssueTypes.SeverityOf(issue.Type) == LibraryIssueSeverity.Hint);
        logger.LogInformation(
            "Library checked: {Tracks} tracks, {Problems} problems and {Hints} hints ({Added} new, {Removed} gone)",
            tracks.Count,
            issues.Count - hints,
            hints,
            changes.Added,
            changes.Removed);
        return new LibraryCheckJobResult(tracks.Count, issues.Count - hints, hints, changes.Added, changes.Removed);
    }
}

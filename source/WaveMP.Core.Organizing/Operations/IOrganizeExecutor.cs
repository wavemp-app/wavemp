using WaveMP.Data.Entities;

namespace WaveMP.Core.Organizing.Operations;

/// <summary>
/// Runs a confirmed organize operation (an own album, a reorganization of the library or an undo): item after item it
/// checks the item against disk and library again, writes its journal, writes the tags into the audio file (after a
/// backup), moves the files without ever replacing one and records the new place, so the songs keep their ids. A
/// failing item is put back and stops the run; a cancel request stops it between two items. The whole run holds the
/// <see cref="Scanning.Writers.LibraryMaintenanceLock"/>, each item the
/// <see cref="Scanning.Writers.LibraryWriteLock"/>.
/// </summary>
public interface IOrganizeExecutor
{
    /// <summary>
    /// Runs the queued operation; does nothing for an operation that is not queued.
    /// </summary>
    /// <param name="operationId">The operation.</param>
    /// <param name="jobRunId">The job run that runs it, for the log.</param>
    /// <param name="cancellationToken">Stops the run between two items (the server shuts down); the operation is then
    /// interrupted.</param>
    Task<OrganizeRunResult> RunAsync(
        Guid operationId,
        Guid? jobRunId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// How the run of an organize operation ended.
/// </summary>
/// <param name="OperationId">The operation.</param>
/// <param name="Status">Its state afterwards; <c>null</c> if it was not run because it was not queued.</param>
/// <param name="DoneItems">Items done.</param>
/// <param name="SkippedItems">Items with a conflict left out.</param>
/// <param name="FailedItems">Items that failed (at most one: a failing item stops the run).</param>
public sealed record OrganizeRunResult(
    Guid OperationId,
    OrganizeOperationStatus? Status,
    int DoneItems,
    int SkippedItems,
    int FailedItems);

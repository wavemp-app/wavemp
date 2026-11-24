using System.Text.Json;
using Microsoft.Extensions.Logging;
using Quartz;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Runs;
using WaveMP.Core.Organizing.Operations;
using WaveMP.Core.Organizing.Previews;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Organizing.Jobs;

/// <summary>
/// The organization of the media files as background job (<see cref="JobIds.LibraryOrganize"/>): plans the preview of
/// a reorganization of the library (an operation that is planning) or runs a confirmed operation (one that is queued),
/// named by the payload <c>{"operationId": "..."}</c>. Without a payload it runs the oldest queued operation, if any.
/// Never runs in parallel to itself and never reorganizes anything on its own: it only works on operations an admin
/// asked for. The result (counts of the operation) becomes part of the run history.
/// </summary>
[DisallowConcurrentExecution]
public sealed class LibraryOrganizeJob(
    IOrganizeRepository repository,
    IOrganizePreviewService previews,
    IOrganizeExecutor executor,
    IJobRunTracker tracker,
    ILogger<LibraryOrganizeJob> logger) : IJob
{
    private static readonly JsonSerializerOptions _payloadOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        Guid? runId = tracker.FindRunId(context.FireInstanceId);
        Guid? operationId = ReadOperationId(context.MergedJobDataMap)
            ?? (await repository.ListOperationIdsAsync(OrganizeOperationStatus.Queued, cancellationToken))
                .Cast<Guid?>()
                .FirstOrDefault();
        if (operationId is not { } id
            || await repository.FindOperationAsync(id, cancellationToken) is not { } operation)
        {
            logger.LogInformation("The organize job found no operation to work on");
            context.Result = new LibraryOrganizeResult(null, null, 0, 0, 0, 0);
            return;
        }

        if (operation.Status == OrganizeOperationStatus.Planning)
        {
            await previews.PlanLibraryAsync(id, cancellationToken);
            OrganizeOperation? planned = await repository.FindOperationAsync(id, CancellationToken.None);
            context.Result = new LibraryOrganizeResult(id, planned?.Status, planned?.TotalItems ?? 0, 0, 0, 0);
            return;
        }

        OrganizeRunResult result = await executor.RunAsync(id, runId, cancellationToken);
        context.Result = new LibraryOrganizeResult(
            id,
            result.Status,
            0,
            result.DoneItems,
            result.SkippedItems,
            result.FailedItems);
    }

    /// <summary>
    /// The payload of a run that works on the operation.
    /// </summary>
    public static string CreatePayload(Guid operationId)
    {
        return JsonSerializer.Serialize(new OrganizeJobPayload(operationId), _payloadOptions);
    }

    internal static Guid? ReadOperationId(JobDataMap data)
    {
        if (!data.TryGetValue(JobDataKeys.Payload, out object? value) || value is not string { Length: > 0 } json)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<OrganizeJobPayload>(json, _payloadOptions)?.OperationId;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record OrganizeJobPayload(Guid? OperationId);
}

/// <summary>
/// The result of a run of the organize job.
/// </summary>
/// <param name="OperationId">The operation it worked on.</param>
/// <param name="Status">The state of the operation afterwards.</param>
/// <param name="PlannedItems">Items of a planned preview.</param>
/// <param name="DoneItems">Items done by the run.</param>
/// <param name="SkippedItems">Items with conflicts left out.</param>
/// <param name="FailedItems">Items that failed.</param>
public sealed record LibraryOrganizeResult(
    Guid? OperationId,
    OrganizeOperationStatus? Status,
    int PlannedItems,
    int DoneItems,
    int SkippedItems,
    int FailedItems);

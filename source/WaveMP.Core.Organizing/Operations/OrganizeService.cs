using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Runs;
using WaveMP.Core.Jobs.Services;
using WaveMP.Core.Metadata.Sources;
using WaveMP.Core.Organizing.Files;
using WaveMP.Core.Organizing.Jobs;
using WaveMP.Core.Organizing.Previews;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Services;
using WaveMP.Data.Entities;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Organizing.Operations;

internal sealed class OrganizeService(
    IOrganizeRepository repository,
    IOwnAlbumRepository ownAlbums,
    IOrganizingSettingsService organizingSettings,
    IMediaSourceCatalog sources,
    IMediaWriteProbe writeProbe,
    IJobService jobs,
    IJobRunTracker tracker,
    OrganizeRunRegistry registry,
    TimeProvider timeProvider) : IOrganizeService
{
    public async Task<OrganizeApplyResult> ApplyAsync(
        Guid id,
        bool skipConflicts,
        CancellationToken cancellationToken = default)
    {
        await ExpireAsync(cancellationToken);
        OrganizeOperation? operation = await repository.FindOperationAsync(id, cancellationToken);
        if (operation is null)
        {
            return OrganizeApplyResult.Failed(OrganizeApplyStatus.NotFound);
        }

        if (operation.Status == OrganizeOperationStatus.Expired)
        {
            return OrganizeApplyResult.Failed(OrganizeApplyStatus.PreviewExpired);
        }

        if (operation.Status != OrganizeOperationStatus.Previewed)
        {
            return OrganizeApplyResult.Failed(OrganizeApplyStatus.NotPreviewed);
        }

        if (await repository.FindActiveOperationIdAsync(cancellationToken) is not null
            || tracker.GetState(JobIds.LibraryOrganize) != JobActivityState.Idle)
        {
            return OrganizeApplyResult.Failed(OrganizeApplyStatus.Busy);
        }

        if (operation.ConflictItems > 0 && !skipConflicts)
        {
            return OrganizeApplyResult.Failed(OrganizeApplyStatus.Conflicts);
        }

        if (await IsOutdatedAsync(operation, cancellationToken))
        {
            return OrganizeApplyResult.Failed(OrganizeApplyStatus.PreviewOutdated);
        }

        foreach (Guid sourceId in await repository.ListItemSourceIdsAsync(id, cancellationToken))
        {
            if (sources.Find(sourceId) is { IsActive: true } source
                && writeProbe.Probe(source.RootPath) == MediaFolderState.ReadOnly)
            {
                return OrganizeApplyResult.Failed(OrganizeApplyStatus.MediaReadOnly);
            }
        }

        if (!await repository.QueueAsync(id, skipConflicts, cancellationToken))
        {
            return OrganizeApplyResult.Failed(OrganizeApplyStatus.NotPreviewed);
        }

        JobRunStartResult started = await jobs.StartRunAsync(
            new JobRunRequest(JobIds.LibraryOrganize, JobRunTrigger.Manual, LibraryOrganizeJob.CreatePayload(id)),
            CancellationToken.None);
        if (!started.IsSuccess)
        {
            await repository.UnqueueAsync(id, CancellationToken.None);
            return OrganizeApplyResult.Failed(OrganizeApplyStatus.Busy);
        }

        return new OrganizeApplyResult(
            OrganizeApplyStatus.Queued,
            await repository.FindOperationAsync(id, CancellationToken.None));
    }

    public async Task<OrganizeCancelResult> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        OrganizeCancelStatus status = await repository.RequestCancelAsync(
            id,
            FileFingerprint.Truncate(timeProvider.GetUtcNow()),
            cancellationToken);
        if (status == OrganizeCancelStatus.Requested)
        {
            registry.RequestCancel(id);
        }

        return new OrganizeCancelResult(status, await repository.FindOperationAsync(id, cancellationToken));
    }

    /// <summary>
    /// Whether what the preview was made from changed since: the layout template (own albums and the library), the own
    /// album (its values, cover or songs), or the operation an undo undoes (undone already).
    /// </summary>
    private async Task<bool> IsOutdatedAsync(OrganizeOperation operation, CancellationToken cancellationToken)
    {
        switch (operation.Kind)
        {
            case OrganizeOperationKind.Undo:
                return operation.UndoOfId is not { } original
                    || await repository.FindOperationAsync(original, cancellationToken) is not { UndoneById: null };
            case OrganizeOperationKind.OwnAlbum:
                if (operation.OwnAlbumId is not { } albumId
                    || await ownAlbums.FindAsync(albumId, cancellationToken) is not { } album
                    || album.UpdatedAt > operation.PreviewedAt)
                {
                    return true;
                }

                break;
        }

        OrganizingSettings settings = await organizingSettings.GetAsync(cancellationToken);
        return !string.Equals(settings.LayoutTemplate, operation.Template, StringComparison.Ordinal);
    }

    public async Task<Page<OrganizeOperation>> ListOperationsAsync(
        OrganizeOperationListQuery query,
        CancellationToken cancellationToken = default)
    {
        await ExpireAsync(cancellationToken);
        return await repository.ListOperationsAsync(query, cancellationToken);
    }

    public async Task<OrganizeOperation?> GetOperationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await ExpireAsync(cancellationToken);
        return await repository.FindOperationAsync(id, cancellationToken);
    }

    public async Task<Page<OrganizeItem>?> ListItemsAsync(
        OrganizeItemListQuery query,
        CancellationToken cancellationToken = default)
    {
        if (await repository.FindOperationAsync(query.OperationId, cancellationToken) is null)
        {
            return null;
        }

        return await repository.ListItemsAsync(query, cancellationToken);
    }

    public Task<OrganizeDiscardStatus> DiscardAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return repository.DeletePreviewAsync(id, cancellationToken);
    }

    public async Task<OrganizeAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        List<SourceAvailability> availability = [];
        foreach (MediaSourceInfo source in sources.Sources)
        {
            SourceAvailabilityStatus status = !source.IsActive
                ? SourceAvailabilityStatus.Inactive
                : writeProbe.Probe(source.RootPath) switch
                {
                    MediaFolderState.Missing => SourceAvailabilityStatus.Missing,
                    MediaFolderState.ReadOnly => SourceAvailabilityStatus.ReadOnly,
                    _ => SourceAvailabilityStatus.Available,
                };
            availability.Add(new SourceAvailability(source.Id, source.Name, status));
        }

        return new OrganizeAvailability(availability, await repository.FindActiveOperationIdAsync(cancellationToken));
    }

    private Task ExpireAsync(CancellationToken cancellationToken)
    {
        return repository.ExpirePreviewsAsync(FileFingerprint.Truncate(timeProvider.GetUtcNow()), cancellationToken);
    }
}

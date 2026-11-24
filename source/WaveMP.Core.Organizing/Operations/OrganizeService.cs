using WaveMP.Core.Metadata.Sources;
using WaveMP.Core.Organizing.Files;
using WaveMP.Core.Organizing.Previews;
using WaveMP.Data.Entities;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Organizing.Operations;

internal sealed class OrganizeService(
    IOrganizeRepository repository,
    IMediaSourceCatalog sources,
    IMediaWriteProbe writeProbe,
    TimeProvider timeProvider) : IOrganizeService
{
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

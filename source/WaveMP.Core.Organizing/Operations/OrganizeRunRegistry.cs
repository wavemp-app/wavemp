using System.Collections.Concurrent;

namespace WaveMP.Core.Organizing.Operations;

/// <summary>
/// Tells a running organize operation (or the planning of a library preview) that the admin asked it to stop. The
/// request is also stored with the operation; this registry only lets the run see it at once, between two items or
/// two parts of the plan. A run never stops in the middle of an item.
/// </summary>
public sealed class OrganizeRunRegistry
{
    private readonly ConcurrentDictionary<Guid, byte> _canceled = new();

    public void RequestCancel(Guid operationId)
    {
        _canceled[operationId] = 0;
    }

    public bool IsCancelRequested(Guid operationId)
    {
        return _canceled.ContainsKey(operationId);
    }

    /// <summary>
    /// Forgets the request once the operation has ended.
    /// </summary>
    public void Clear(Guid operationId)
    {
        _canceled.TryRemove(operationId, out _);
    }
}

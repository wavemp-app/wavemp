using WaveMP.Data.Projections;

namespace WaveMP.Data.Repositories;

/// <summary>
/// The saved share.
/// </summary>
/// <param name="Row">The share with the name of its user.</param>
/// <param name="Created">Whether the share is new; otherwise the user had shared the period before.</param>
public sealed record RecapShareSaveResult(RecapShareRow Row, bool Created);

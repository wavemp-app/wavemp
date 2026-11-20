namespace WaveMP.Data.Repositories;

/// <summary>
/// Paging of shared recaps, the most recently shared first (ties by id).
/// </summary>
/// <param name="UserId">Id of the user who asks.</param>
/// <param name="Own">Whether to list the recaps the user shared; otherwise those the other users shared.</param>
/// <param name="Offset">Number of shares to skip.</param>
/// <param name="Limit">Maximum number of shares of the page.</param>
public sealed record RecapShareListQuery(Guid UserId, bool Own, int Offset, int Limit);

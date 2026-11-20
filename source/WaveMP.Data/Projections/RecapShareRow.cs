using WaveMP.Data.Entities;

namespace WaveMP.Data.Projections;

/// <summary>
/// A shared recap with the name of the profile that shared it.
/// </summary>
/// <param name="Share">The share.</param>
/// <param name="OwnerName">Name of the profile that shared the recap.</param>
public sealed record RecapShareRow(RecapShare Share, string OwnerName);

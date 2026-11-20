using WaveMP.Core.Recaps.Statistics;
using WaveMP.Data.Projections;

namespace WaveMP.Core.Recaps.Shares;

/// <summary>
/// A shared recap with the statistics of its period.
/// </summary>
/// <param name="Share">The share with the name of its profile.</param>
/// <param name="Statistics">The statistics of the profile that shared the recap.</param>
public sealed record SharedRecap(RecapShareRow Share, ListeningStatistics Statistics);

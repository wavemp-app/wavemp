using WaveMP.Data.Entities;

namespace WaveMP.Core.Recaps.Shares;

/// <summary>
/// A recap to share with the other profiles.
/// </summary>
/// <param name="UserId">The user who shares the recap.</param>
/// <param name="Period">How the period was chosen.</param>
/// <param name="From">First day of the period; <c>null</c> from the first play on.</param>
/// <param name="To">Last day of the period.</param>
/// <param name="TimeZone">IANA time zone of the days.</param>
/// <param name="Design">Id of the design the recap is shown in.</param>
public sealed record RecapShareRequest(
    Guid UserId,
    RecapPeriod Period,
    DateOnly? From,
    DateOnly To,
    string TimeZone,
    string Design);

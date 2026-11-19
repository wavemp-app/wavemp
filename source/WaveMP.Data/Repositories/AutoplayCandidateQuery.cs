namespace WaveMP.Data.Repositories;

/// <summary>
/// The tracks autoplay may choose for a user: those of the artists, or (without artists) those of the genres and
/// decades. Tracks the user rated with 1 star and the excluded tracks are never part of it.
/// </summary>
/// <param name="UserId">Id of the user, whose ratings and favorites count.</param>
/// <param name="Limit">Maximum number of tracks; a random selection if there are more.</param>
/// <param name="IncludeVideoOnly">Whether tracks that only have a music video are candidates.</param>
/// <param name="ExcludedTrackIds">Tracks that are never candidates.</param>
/// <param name="ArtistIds">The artists whose tracks are candidates; <c>null</c> to choose by genre and decade.</param>
/// <param name="Genres">With <paramref name="ArtistIds"/> <c>null</c>: the genres (without regard to case) of the
/// candidates; <c>null</c> for any genre.</param>
/// <param name="Decades">With <paramref name="ArtistIds"/> <c>null</c>: the decades of the candidates as their first
/// year, e.g. 1990; <c>null</c> for any year, also none.</param>
public sealed record AutoplayCandidateQuery(
    Guid UserId,
    int Limit,
    bool IncludeVideoOnly,
    IReadOnlyCollection<Guid> ExcludedTrackIds,
    IReadOnlyCollection<Guid>? ArtistIds = null,
    IReadOnlyCollection<string>? Genres = null,
    IReadOnlyCollection<int>? Decades = null);

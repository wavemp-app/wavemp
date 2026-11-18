namespace WaveMP.Data.Repositories;

/// <summary>
/// A page of one of the album lists of the Subsonic API.
/// </summary>
/// <param name="Order">The list.</param>
/// <param name="UserId">The user whose plays and ratings the list follows.</param>
/// <param name="Offset">Number of albums to skip; ignored for <see cref="SubsonicAlbumOrder.Random"/>.</param>
/// <param name="Size">Maximum number of albums.</param>
/// <param name="FromYear">First year of <see cref="SubsonicAlbumOrder.Year"/>.</param>
/// <param name="ToYear">Last year of <see cref="SubsonicAlbumOrder.Year"/>; before <paramref name="FromYear"/>, the
/// newest albums come first.</param>
public sealed record SubsonicAlbumListQuery(
    SubsonicAlbumOrder Order,
    Guid UserId,
    int Offset,
    int Size,
    int FromYear = 0,
    int ToYear = 0);

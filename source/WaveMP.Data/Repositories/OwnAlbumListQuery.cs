using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

/// <summary>
/// One page of the own albums.
/// </summary>
/// <param name="Offset">Number of albums to skip.</param>
/// <param name="Limit">Largest number of albums of the page.</param>
/// <param name="Sort">Sort key; ties by id.</param>
/// <param name="Direction">Sort direction.</param>
public sealed record OwnAlbumListQuery(int Offset, int Limit, OwnAlbumSortField Sort, SortDirection Direction);
